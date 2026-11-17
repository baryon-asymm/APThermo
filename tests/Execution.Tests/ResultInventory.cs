using System.Reflection;
using APThermo.Performance;
using APThermo.Thermo;
using APThermo.Transport;

namespace APThermo.Execution.Tests;

/// <summary>Ensures every public result member has an explicit numerical or metadata policy.</summary>
internal static class ResultInventory
{
    private static readonly HashSet<Type> NumericalTypes =
    [typeof(int), typeof(int[]), typeof(bool[]), typeof(double[]), typeof(CaseStatus[]),
     typeof(MixtureState[]), typeof(PerformanceFigures[]), typeof(TransportFigures[])];

    internal static void Verify(Type type, params string[] numerical)
    {
        VerifyEngine(typeof(Engine), typeof(ExactComparison));
        var remaining = numerical.ToHashSet(StringComparer.Ordinal);
        if (remaining.Count != numerical.Length)
        {
            throw new InvalidOperationException($"{type} has duplicate numerical policies");
        }

        var metadata = new HashSet<string>(StringComparer.Ordinal);
        var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance).Select(field => (field.Name, Type: field.FieldType));
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(property => (property.Name, Type: property.PropertyType));
        foreach (var (name, memberType) in fields.Concat(properties))
        {
            if ((name == "Accelerator" && memberType == typeof(AcceleratorInfo)) || (name == "Timings" && memberType == typeof(RunTimings)))
            {
                if (!metadata.Add(name)) { throw new InvalidOperationException($"{type}.{name} is duplicated"); }
            }
            else if (!NumericalTypes.Contains(memberType) || !remaining.Remove(name))
            {
                throw new InvalidOperationException($"{type}.{name}: no comparison policy for {memberType}");
            }
        }

        if (remaining.Count != 0 || !metadata.SetEquals(["Accelerator", "Timings"]))
        {
            throw new InvalidOperationException($"{type}: stale numerical policy or missing metadata: {string.Join(", ", remaining)}");
        }
    }

    internal static void VerifyEngine(Type engine, Type comparison)
    {
        var results = engine.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(method => method.Name == "Run").Select(method => method.ReturnType).ToHashSet();
        var comparisons = comparison.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(method => method.ReturnType == typeof(List<string>))
            .Select(method => method.GetParameters())
            .Where(parameters => parameters.Length >= 2 && parameters[0].ParameterType == parameters[1].ParameterType)
            .Select(parameters => parameters[0].ParameterType).ToHashSet();
        if (results.Count == 0 || !results.SetEquals(comparisons))
        {
            throw new InvalidOperationException($"{engine}: Engine.Run result types and exact comparisons differ");
        }
    }

    internal static void VerifyMetadata(AcceleratorInfo accelerator, RunTimings timings)
    {
        ArgumentNullException.ThrowIfNull(accelerator);
        ArgumentNullException.ThrowIfNull(timings);
        VerifyMetadataShape(typeof(AcceleratorInfo),
            (nameof(AcceleratorInfo.Kind), typeof(AcceleratorKind)), (nameof(AcceleratorInfo.DeviceName), typeof(string)),
            (nameof(AcceleratorInfo.IlgpuVersion), typeof(string)), (nameof(AcceleratorInfo.ThreadsOrMultiprocessors), typeof(int)),
            (nameof(AcceleratorInfo.CudaSkippedBecause), typeof(string)));
        VerifyMetadataShape(typeof(RunTimings),
            (nameof(RunTimings.WarmUp), typeof(TimeSpan)), (nameof(RunTimings.Upload), typeof(TimeSpan)),
            (nameof(RunTimings.Kernel), typeof(TimeSpan)), (nameof(RunTimings.Download), typeof(TimeSpan)),
            (nameof(RunTimings.LaunchCount), typeof(int)), (nameof(RunTimings.FirstLaunchCases), typeof(int)),
            (nameof(RunTimings.ReusedPrefix), typeof(bool)), (nameof(RunTimings.CoveredCases), typeof(int)),
            (nameof(RunTimings.MaxLaunchDuration), typeof(TimeSpan)), (nameof(RunTimings.MaxLaunchCases), typeof(int)),
            (nameof(RunTimings.MaxLaunchOrdinal), typeof(int)));
        if (accelerator.Kind is not (AcceleratorKind.Cpu or AcceleratorKind.Cuda) ||
            string.IsNullOrWhiteSpace(accelerator.DeviceName) || string.IsNullOrWhiteSpace(accelerator.IlgpuVersion) ||
            accelerator.ThreadsOrMultiprocessors <= 0 || timings.WarmUp < TimeSpan.Zero || timings.Upload < TimeSpan.Zero ||
            timings.Kernel < TimeSpan.Zero || timings.Download < TimeSpan.Zero || timings.MaxLaunchDuration < TimeSpan.Zero ||
            timings.LaunchCount < 0 || timings.FirstLaunchCases < 0 || timings.CoveredCases < 0 || timings.MaxLaunchCases < 0 || timings.MaxLaunchOrdinal < 0)
        {
            throw new InvalidOperationException("Invalid accelerator or timing metadata in batch result");
        }
    }

    internal static void VerifyMetadataShape(Type type, params (string Name, Type Type)[] members)
    {
        var expected = members.ToDictionary(member => member.Name, member => member.Type, StringComparer.Ordinal);
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(property => property.GetMethod is { } getter && (getter.IsPublic || getter.IsAssembly));
        var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(field => (field.Name, Type: field.FieldType));
        foreach (var (name, memberType) in fields.Concat(properties.Select(property => (property.Name, Type: property.PropertyType))))
        {
            if (!expected.Remove(name, out var declared) || declared != memberType)
            {
                throw new InvalidOperationException($"{type}.{name}: no metadata policy for {memberType}");
            }
        }

        if (expected.Count != 0) { throw new InvalidOperationException($"{type}: stale metadata policy"); }
    }
}

/// <summary>Records the numerical members whose comparison callbacks actually ran.</summary>
internal sealed class ResultCoverage(Type type, params string[] dimensions)
{
    private readonly HashSet<string> _checked = new(dimensions, StringComparer.Ordinal);

    internal void Compare(string name, Action comparison)
    {
        comparison();
        if (!_checked.Add(name)) { throw new InvalidOperationException($"{type}.{name} compared twice"); }
    }

    internal void Verify() => ResultInventory.Verify(type, [.. _checked]);
}
