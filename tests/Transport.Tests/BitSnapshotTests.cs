using System.Reflection;
using APThermo.Fixtures;
using APThermo.Harness;
using APThermo.Thermo;

namespace APThermo.Transport.Tests;

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
    public static string ApprovedPath => ApprovedSnapshot.ApprovedPathFor(RepositoryPaths.Resolve("tests", "Transport.Tests"), "Bits");

    [Fact]
    [Trait("Category", "BitSnapshot")]
    public void Every_fixture_with_transport_gives_the_recorded_bits()
    {
        var snapshot = ApprovedSnapshot.Load(ApprovedPath);
        var keys = new List<string>();
        var problems = new List<string>();
        foreach (var row in TransportHost.RocketCasesWithTransport())
        {
            var c = TransportHost.LoadRocket((string)row[0]);
            var key = RelativePath(c);
            keys.Add(key);
            var problem = snapshot.Problem(key, HashOfStations(c));
            if (problem is not null)
            {
                problems.Add(problem);
            }
        }

        Assert.NotEmpty(keys);
        foreach (var stale in snapshot.StaleKeys(keys))
        {
            problems.Add($"{stale}: recorded in the approved snapshot, but no such fixture is run with transport");
        }

        Assert.True(problems.Count == 0,
            $"{problems.Count} fixture(s) no longer give the recorded bits:\n{string.Join("\n", problems)}");
    }

    /// <summary>The SHA-256, lower-case hex, of the raw bits of every station of the case in station order: the figures, then the status.</summary>
    private string HashOfStations(CeaCase c)
    {
        var (table, transport) = TransportHost.TablesOf(fixture, c);
        using var speciesBuffers = SpeciesTableBuffers.Upload(fixture.Accelerator, table);
        using var transportBuffers = TransportTableBuffers.Upload(fixture.Accelerator, transport);
        var stations = TransportHost.StationsWithTransport(c);
        Assert.NotEmpty(stations);
        var hash = new BitHash();
        foreach (var station in stations)
        {
            var evaluation = TransportHost.Evaluate(fixture.Accelerator, speciesBuffers, transportBuffers,
                                                    station.GetProperty("temperature").GetDouble(), TransportHost.MolesOf(table, station));
            Append(hash, evaluation);
        }

        return hash.ToHex();
    }

    /// <summary>One station: every field of the figures in declaration order, then the status.</summary>
    private static void Append(BitHash hash, TransportEvaluation evaluation)
    {
        foreach (var field in FiguresFields)
        {
            var value = field.GetValue(evaluation.Figures)!;
            if (value is double x)
            {
                hash.Add(x);
                continue;
            }

            hash.Add((int)value);
        }

        hash.Add((int)evaluation.Status);
    }

    private static string RelativePath(CeaCase c) => Path.GetRelativePath(RepositoryPaths.Root, c.Path).Replace('\\', '/');
}
