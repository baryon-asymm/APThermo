using System.Reflection;
using APThermo.Fixtures;
using APThermo.Harness;
using APThermo.Thermo;

namespace APThermo.Transport.Tests;

/// <summary>
/// Bits level: the host evaluation of every station of every rocket fixture run with transport hashes to the
/// recorded line. A mismatch includes the actual station/property names and values without changing hash input.
/// </summary>
[Collection(CpuFixture.CollectionName)]
public sealed class BitSnapshotTests
{
    /// <summary>The properties of the figures in declaration order, including future properties.</summary>
    private static readonly IReadOnlyList<PropertyInfo> FiguresFields =
        [.. typeof(TransportFigures).GetProperties(BindingFlags.Public | BindingFlags.Instance).OrderBy(f => f.MetadataToken)];

    /// <summary>The committed snapshot: one line per fixture, its path relative to the repository root and hash.</summary>
    public static string ApprovedPath => ApprovedSnapshot.RecordPathFor(RepositoryPaths.Resolve("tests", "Transport.Tests"), "Bits");

    /// <summary>Every fixture with transport gives the recorded bits.</summary>
    [Fact]
    [Trait("Category", "BitSnapshot")]
    public void EveryFixtureWithTransportGivesTheRecordedBits()
    {
        var snapshot = ApprovedSnapshot.Load(ApprovedPath);
        var keys = new List<string>();
        var problems = new List<string>();
        foreach (var name in TransportHost.RocketCaseNamesWithTransport())
        {
            var c = TransportHost.LoadRocket(name);
            var key = RelativePath(c);
            keys.Add(key);
            var bits = HashOfStations(c, out var fields);
            var problem = snapshot.Problem(key, bits, fields);
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

    /// <summary>The raw bits of every station: figures in declaration order, then status, retaining recorded station order.</summary>
    private static string HashOfStations(CeaCase c, out IReadOnlyList<string> fields)
    {
        var (table, transport) = TransportHost.TablesOf(CpuFixture.Shared, c);
        using var speciesBuffers = SpeciesTableBuffers.Upload(CpuFixture.Shared.Accelerator, table);
        using var transportBuffers = TransportTableBuffers.Upload(CpuFixture.Shared.Accelerator, transport);
        var stations = TransportHost.StationsWithTransport(c);
        Assert.NotEmpty(stations);
        var hash = new BitHash();
        var named = new List<string>();
        for (var index = 0; index < stations.Count; index++)
        {
            var station = stations[index];
            var evaluation = TransportHost.Evaluate(CpuFixture.Shared.Accelerator, speciesBuffers, transportBuffers,
                                                    station.GetProperty("temperature").GetDouble(), TransportHost.MolesOf(table, station));
            Append(hash, named, $"station[{index}:{station.GetProperty("station").GetString()}]", evaluation);
        }

        fields = named;
        return hash.ToHex();
    }

    /// <summary>Named diagnostics use the same actual values and order as the hash; labels do not enter it.</summary>
    private static void Append(BitHash hash, List<string> named, string station, TransportEvaluation evaluation)
    {
        foreach (var field in FiguresFields)
        {
            var value = field.GetValue(evaluation.Figures)!;
            _ = value is double x ? hash.Add(x) : hash.Add((int)value);
            named.Add($"{station}.Figures.{field.Name}: {hash.Fields[^1]}");
        }

        _ = hash.Add((int)evaluation.Status);
        named.Add($"{station}.Status ({evaluation.Status}): {hash.Fields[^1]}");
    }

    private static string RelativePath(CeaCase c) => Path.GetRelativePath(RepositoryPaths.Root, c.Path).Replace('\\', '/');
}
