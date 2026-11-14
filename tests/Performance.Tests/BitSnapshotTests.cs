using System.Reflection;
using APThermo.Fixtures;
using APThermo.Harness;
using APThermo.Thermo;

namespace APThermo.Performance.Tests;

/// <summary>
/// The raw bits of one host solve, hashed in the recorded traversal order. Diagnostic labels share that traversal;
/// names and text values do not enter the hash. Reflected properties retain their metadata-token declaration order.
/// </summary>
internal static class RocketBits
{
    private static readonly PropertyInfo[] StateFields = Declared(typeof(MixtureState));

    private static readonly PropertyInfo[] FigureFields = Declared(typeof(PerformanceFigures));

    public static string Hash(RocketSolution solution, out IReadOnlyList<string> fields)
    {
        var hash = new BitHash();
        var named = new List<string>();
        for (var station = 0; station < solution.StationCount; station++)
        {
            var state = solution.Outcome.Stations[station];
            foreach (var field in StateFields)
            {
                Append(hash, named, $"station[{station}].State.{field.Name}", (double)field.GetValue(state)!);
            }
        }

        for (var station = 0; station < solution.StationCount; station++)
        {
            AppendStation(hash, named, solution, station);
        }

        for (var station = 0; station < solution.Outcome.StationStatus.Length; station++)
        {
            var status = solution.Outcome.StationStatus[station];
            Append(hash, named, $"station[{station}].Status ({status})", (int)status);
        }

        for (var station = 0; station < solution.Outcome.Iterations.Length; station++)
        {
            Append(hash, named, $"station[{station}].Iterations", solution.Outcome.Iterations[station]);
        }

        Append(hash, named, $"CaseStatus ({solution.Status})", (int)solution.Status);
        fields = named;
        return hash.ToHex();
    }

    private static void AppendStation(BitHash hash, List<string> named, RocketSolution solution, int station)
    {
        var speciesCount = solution.Table.SpeciesCount;
        var elementCount = solution.Table.ElementCount;
        for (var species = 0; species < speciesCount; species++)
        {
            Append(hash, named, $"station[{station}].Moles[{species}:{solution.Table.Records[species].Name}]",
                solution.Outcome.Moles[station * speciesCount + species]);
        }

        for (var element = 0; element < elementCount; element++)
        {
            Append(hash, named, $"station[{station}].Multipliers[{element}:{solution.Table.Elements[element]}]",
                solution.Outcome.Multipliers[station * elementCount + element]);
        }

        var figures = solution.Outcome.Figures[station];
        foreach (var field in FigureFields)
        {
            Append(hash, named, $"station[{station}].Figures.{field.Name}", (double)field.GetValue(figures)!);
        }
    }

    private static void Append(BitHash hash, List<string> named, string name, double value)
    {
        _ = hash.Add(value);
        named.Add($"{name}: {hash.Fields[^1]}");
    }

    private static void Append(BitHash hash, List<string> named, string name, int value)
    {
        _ = hash.Add(value);
        named.Add($"{name}: {hash.Fields[^1]}");
    }

    /// <summary>The struct's properties in declaration order; GetProperties promises no order, the metadata token carries it.</summary>
    private static PropertyInfo[] Declared(Type type) => [.. type.GetProperties().OrderBy(field => field.MetadataToken)];
}

/// <summary>
/// Bits level: the host solve of every rocket fixture gives the bits recorded in Bits.approved.txt. A tripwire,
/// not a contract: a refactoring moves no line. A mismatch includes the actual named fields beside the actual digest.
/// </summary>
[Collection(CpuFixture.CollectionName)]
public sealed class BitSnapshotTests
{
    private static readonly ApprovedSnapshot Snapshot = ApprovedSnapshot.Load(ApprovedPath);

    /// <summary>The path of this node's approved bit snapshot.</summary>
    public static string ApprovedPath => ApprovedSnapshot.RecordPathFor(RepositoryPaths.Resolve("tests", "Performance.Tests"), "Bits");

    /// <summary>The rocket fixture files as theory data, delegating to RocketHost.Cases.</summary>
    public static TheoryData<string> Cases() => RocketHost.Cases();

    /// <summary>The host solve of every rocket fixture gives the recorded bits.</summary>
    [Theory]
    [MemberData(nameof(Cases))]
    [Trait("Category", "BitSnapshot")]
    public void EveryRocketFixtureGivesTheRecordedBits(string name)
    {
        var c = RocketHost.Load(name);
        var bits = RocketBits.Hash(RocketHost.Solve(CpuFixture.Shared, c), out var fields);
        var problem = Snapshot.Problem(PathOf(c.Path), bits, fields);
        Assert.True(problem is null, problem);
    }

    /// <summary>Every approved line still names a rocket fixture; deleted or renamed fixtures cannot leave stale records.</summary>
    [Fact]
    [Trait("Category", "BitSnapshot")]
    public void EveryApprovedLineNamesARocketFixture()
    {
        var keys = FixtureFiles.Enumerate("rocket").Select(PathOf).ToList();
        var stale = Snapshot.StaleKeys(keys);
        Assert.True(stale.Count == 0,
                    string.Join("\n", stale.Select(key => $"{key}: recorded in the approved snapshot, but no longer a rocket fixture")));
    }

    /// <summary>The fixture's path from the repository root, with forward slashes, as the snapshot spells it.</summary>
    private static string PathOf(string path) => Path.GetRelativePath(RepositoryPaths.Root, path).Replace('\\', '/');
}
