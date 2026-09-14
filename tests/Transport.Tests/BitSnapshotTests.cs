using System.Buffers.Binary;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using AerospacePropellantThermodynamics.Fixtures;
using AerospacePropellantThermodynamics.Thermo;

namespace AerospacePropellantThermodynamics.Transport.Tests;

/// <summary>
/// Bits level: the host evaluation of every station of every rocket fixture run with transport hashes to the line recorded in
/// Bits.approved.txt. A tripwire, not a contract (BOOT.md): a decomposition, a renaming or a reordering of code moves no line,
/// and a line that does move is legitimate only with the numerical change that moved it named in the same commit.
/// </summary>
[Collection(CpuCollection.Name)]
public sealed class BitSnapshotTests(CpuFixture fixture)
{
    /// <summary>The fields of the figures in declaration order; read by reflection, so that a new field cannot be forgotten.</summary>
    private static readonly IReadOnlyList<FieldInfo> FiguresFields =
        [.. typeof(TransportFigures).GetFields(BindingFlags.Public | BindingFlags.Instance).OrderBy(f => f.MetadataToken)];

    /// <summary>The committed snapshot: one line per fixture, its path relative to the repository root, a space, the hash.</summary>
    public static string ApprovedPath => Path.Combine(RepositoryPaths.Root, "tests", "Transport.Tests", "Bits.approved.txt");

    /// <summary>What the run produced, written beside the approved file on a failure; ignored by git.</summary>
    public static string ActualPath => Path.Combine(RepositoryPaths.Root, "tests", "Transport.Tests", "Bits.actual.txt");

    [Fact]
    public void Every_fixture_with_transport_gives_the_recorded_bits()
    {
        var actual = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var row in TransportHost.RocketCasesWithTransport())
        {
            var c = TransportHost.LoadRocket((string)row[0]);
            actual[RelativePath(c)] = HashOfStations(c);
        }

        Assert.NotEmpty(actual);
        if (!File.Exists(ApprovedPath))
        {
            File.WriteAllText(ActualPath, Render(actual));
            Assert.Fail($"No approved bit snapshot existed at {ApprovedPath}; what this run produced was written to {ActualPath}. " +
                        "Satisfy yourself that the figures are the ones the node's acceptance criteria prove, rename it to Bits.approved.txt " +
                        "and commit it in the same commit as this test.");
        }

        var differences = Compare(ReadApproved(), actual);
        if (differences.Count == 0)
        {
            File.Delete(ActualPath);
            return;
        }

        File.WriteAllText(ActualPath, Render(actual));
        Assert.Fail($"The station bits no longer match Bits.approved.txt:\n{string.Join("\n", differences)}\n" +
                    $"What this run produced was written to {ActualPath}. A decomposition or a rename moves no line: if a line moved, " +
                    "find the expression whose form or order changed. If the change is an intended numerical one, name it in the commit " +
                    "that replaces the approved file with the actual one.");
    }

    /// <summary>The SHA-256, lower-case hex, of the raw bits of every station of the case in station order: the figures, then the status.</summary>
    private string HashOfStations(CeaCase c)
    {
        var (table, transport) = TransportHost.TablesOf(fixture, c);
        using var speciesBuffers = SpeciesTableBuffers.Upload(fixture.Accelerator, table);
        using var transportBuffers = TransportTableBuffers.Upload(fixture.Accelerator, transport);
        var stations = TransportHost.StationsWithTransport(c);
        Assert.NotEmpty(stations);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var station in stations)
        {
            var evaluation = TransportHost.Evaluate(fixture.Accelerator, speciesBuffers, transportBuffers,
                                                    station.GetProperty("temperature").GetDouble(), TransportHost.MolesOf(table, station));
            Append(hash, evaluation);
        }

        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    /// <summary>One station: every field of the figures in declaration order, doubles by their bits and ints as they are, then the status.</summary>
    private static void Append(IncrementalHash hash, TransportEvaluation evaluation)
    {
        Span<byte> buffer = stackalloc byte[8];
        foreach (var field in FiguresFields)
        {
            var value = field.GetValue(evaluation.Figures)!;
            if (value is double x)
            {
                BinaryPrimitives.WriteInt64LittleEndian(buffer, BitConverter.DoubleToInt64Bits(x));
                hash.AppendData(buffer);
                continue;
            }

            BinaryPrimitives.WriteInt32LittleEndian(buffer, (int)value);
            hash.AppendData(buffer[..4]);
        }

        BinaryPrimitives.WriteInt32LittleEndian(buffer, (int)evaluation.Status);
        hash.AppendData(buffer[..4]);
    }

    private static string RelativePath(CeaCase c) => Path.GetRelativePath(RepositoryPaths.Root, c.Path).Replace('\\', '/');

    private static IReadOnlyDictionary<string, string> ReadApproved()
    {
        var approved = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var line in File.ReadAllLines(ApprovedPath))
        {
            var separator = line.IndexOf(' ', StringComparison.Ordinal);
            if (separator > 0)
            {
                approved[line[..separator]] = line[(separator + 1)..].Trim();
            }
        }

        return approved;
    }

    private static IReadOnlyList<string> Compare(IReadOnlyDictionary<string, string> approved, SortedDictionary<string, string> actual)
    {
        var differences = new List<string>();
        foreach (var (path, hash) in actual)
        {
            if (!approved.TryGetValue(path, out var recorded))
            {
                differences.Add($"{path}: no line in the approved snapshot; this fixture was never approved");
            }
            else if (recorded != hash)
            {
                differences.Add($"{path}: approved {recorded}, this run {hash}");
            }
        }

        foreach (var path in approved.Keys.Where(path => !actual.ContainsKey(path)).Order(StringComparer.Ordinal))
        {
            differences.Add($"{path}: recorded in the approved snapshot, but no such fixture is run with transport");
        }

        return differences;
    }

    private static string Render(SortedDictionary<string, string> hashes)
    {
        var builder = new StringBuilder();
        foreach (var (path, hash) in hashes)
        {
            builder.Append(path).Append(' ').Append(hash).Append('\n');
        }

        return builder.ToString();
    }
}
