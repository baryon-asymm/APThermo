using System.Reflection;
using APThermo.Fixtures;
using APThermo.Harness;
using APThermo.Thermo;

namespace APThermo.Performance.Tests;

/// <summary>
/// The raw bits of one host solve, hashed: every station's <see cref="MixtureState"/> fields in declaration order, then every
/// station's moles, multipliers and <see cref="PerformanceFigures"/> fields, the station statuses, the iteration counts and the
/// case status. The fields are enumerated by reflection, so a new field enters the hash without a change here.
/// </summary>
internal static class RocketBits
{
    private static readonly PropertyInfo[] StateFields = Declared(typeof(MixtureState));

    private static readonly PropertyInfo[] FigureFields = Declared(typeof(PerformanceFigures));

    public static string Hash(RocketSolution solution)
    {
        var hash = new BitHash();
        var speciesCount = solution.Table.SpeciesCount;
        var elementCount = solution.Table.ElementCount;
        for (var station = 0; station < solution.StationCount; station++)
        {
            var state = solution.Outcome.Stations[station];
            foreach (var field in StateFields)
            {
                _ = hash.Add((double)field.GetValue(state)!);
            }
        }

        for (var station = 0; station < solution.StationCount; station++)
        {
            _ = hash.Add(solution.Outcome.Moles.AsSpan(station * speciesCount, speciesCount));
            _ = hash.Add(solution.Outcome.Multipliers.AsSpan(station * elementCount, elementCount));
            var figures = solution.Outcome.Figures[station];
            foreach (var field in FigureFields)
            {
                _ = hash.Add((double)field.GetValue(figures)!);
            }
        }

        foreach (var status in solution.Outcome.StationStatus)
        {
            _ = hash.Add((int)status);
        }

        return hash.Add(solution.Outcome.Iterations).Add((int)solution.Status).ToHex();
    }

    /// <summary>The struct's properties in declaration order; <see cref="Type.GetProperties()"/> promises no order, the metadata token carries it.</summary>
    private static PropertyInfo[] Declared(Type type) => [.. type.GetProperties().OrderBy(field => field.MetadataToken)];
}

/// <summary>
/// Bits level: the host solve of every rocket fixture gives the bits recorded in <c>Bits.approved.txt</c>. A tripwire, not a
/// contract (BOOT.md): a decomposition, a renaming or a reordering of code moves no line, so a moved line is a numerical change
/// and must be named in the commit that moves it.
/// </summary>
public sealed class BitSnapshotTests
{
    private static readonly ApprovedSnapshot Snapshot = ApprovedSnapshot.Load(ApprovedPath);

    /// <summary>The path of this node's approved bit snapshot.</summary>
    public static string ApprovedPath => ApprovedSnapshot.ApprovedPathFor(RepositoryPaths.Resolve("tests", "Performance.Tests"), "Bits");

    /// <summary>The rocket fixture files as theory data, delegating to <see cref="RocketHost.Cases"/>.</summary>
    public static TheoryData<string> Cases() => RocketHost.Cases();

    /// <summary>The host solve of every rocket fixture gives the recorded bits.</summary>
    [Theory]
    [MemberData(nameof(Cases))]
    [Trait("Category", "BitSnapshot")]
    public void EveryRocketFixtureGivesTheRecordedBits(string name)
    {
        var c = RocketHost.Load(name);
        var bits = RocketBits.Hash(RocketHost.Solve(CpuFixture.Shared, c));
        var problem = Snapshot.Problem(PathOf(c.Path), bits);
        Assert.True(problem is null, problem);
    }

    /// <summary>
    /// The Bits invariant's other direction (BOOT.md): every approved line still names a rocket fixture. Without this, a
    /// deleted or renamed fixture would leave its line in place, read by no case and never failing.
    /// </summary>
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
