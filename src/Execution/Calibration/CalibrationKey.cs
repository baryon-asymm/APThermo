using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using APThermo.Execution.Chunks;
using ILGPU;

namespace APThermo.Execution.Calibration;

/// <summary>Compact identity without owning any workload, launcher or table.</summary>
internal sealed class CalibrationKey
{
    private readonly WeakReference<Delegate> _launcher;
    private readonly WeakReference<UploadedTables> _tables;
    private readonly string _identity;

    private CalibrationKey(Delegate launcher, UploadedTables tables, string identity)
    {
        _launcher = new WeakReference<Delegate>(launcher);
        _tables = new WeakReference<UploadedTables>(tables);
        _identity = identity;
    }

    internal static CalibrationKey? Create<TViews>(Delegate launcher, UploadedTables tables,
        ChunkPlan plan, string? inputIdentity, in TViews views) where TViews : struct
    {
        if (inputIdentity is null || !plan.Budget.IsBounded || plan.Count == 0)
        {
            return null;
        }

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Append(hash, typeof(TViews).AssemblyQualifiedName!);
        Append(hash, inputIdentity);
        Append(hash, plan.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Append(hash, plan.Size.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Append(hash, plan.Budget.TargetDuration!.Value.Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture));
        foreach (var field in typeof(TViews).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                     .OrderBy(field => field.Name, StringComparer.Ordinal))
        {
            if (field.FieldType.IsGenericType && field.FieldType.GetGenericTypeDefinition() == typeof(ArrayView<>))
            {
                continue;
            }

            if (field.FieldType != typeof(int))
            {
                return null;
            }

            Append(hash, field.Name);
            Append(hash, field.FieldType.AssemblyQualifiedName!);
            Append(hash, ((int)field.GetValue(views)!).ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        return new CalibrationKey(launcher, tables, Convert.ToHexString(hash.GetHashAndReset()));
    }

    internal bool Matches(CalibrationKey other) =>
        _identity == other._identity &&
        _launcher.TryGetTarget(out var launcher) && other._launcher.TryGetTarget(out var otherLauncher) &&
        ReferenceEquals(launcher, otherLauncher) &&
        _tables.TryGetTarget(out var tables) && other._tables.TryGetTarget(out var otherTables) &&
        ReferenceEquals(tables, otherTables);

    private static void Append(IncrementalHash hash, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        Span<byte> length = stackalloc byte[sizeof(int)];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(length, bytes.Length);
        hash.AppendData(length);
        hash.AppendData(bytes);
    }
}