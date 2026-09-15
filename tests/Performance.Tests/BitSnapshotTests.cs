using System.Reflection;
using AerospacePropellantThermodynamics.Fixtures;
using AerospacePropellantThermodynamics.Harness;
using AerospacePropellantThermodynamics.Thermo;

namespace AerospacePropellantThermodynamics.Performance.Tests;

/// <summary>
/// The raw bits of one host solve, hashed: every station's <see cref="MixtureState"/> fields in declaration order, then every
/// station's moles, multipliers and <see cref="PerformanceFigures"/> fields, the station statuses, the iteration counts and the
/// case status. The fields are enumerated by reflection, so a new field enters the hash without a change here.
/// </summary>
internal static class RocketBits
{
    private static readonly FieldInfo[] StateFields = Declared(typeof(MixtureState));

    private static readonly FieldInfo[] FigureFields = Declared(typeof(PerformanceFigures));

    public static string Hash(RocketSolution solution)
    {
        var hash = new BitHash();
        var speciesCount = solution.Table.SpeciesCount;
        var elementCount = solution.Table.ElementCount;
        for (var station = 0; station < solution.StationCount; station++)
        {
            var state = solution.Stations[station];
            foreach (var field in StateFields)
            {
                hash.Add((double)field.GetValue(state)!);
            }
        }

        for (var station = 0; station < solution.StationCount; station++)
        {
            hash.Add(solution.Moles.AsSpan(station * speciesCount, speciesCount));
            hash.Add(solution.Multipliers.AsSpan(station * elementCount, elementCount));
            var figures = solution.Figures[station];
            foreach (var field in FigureFields)
            {
                hash.Add((double)field.GetValue(figures)!);
            }
        }

        foreach (var status in solution.StationStatus)
        {
            hash.Add((int)status);
        }

        return hash.Add(solution.Iterations).Add((int)solution.Status).ToHex();
    }

    /// <summary>The struct's fields in declaration order; <see cref="Type.GetFields()"/> promises no order, the metadata token carries it.</summary>
    private static FieldInfo[] Declared(Type type) => type.GetFields().OrderBy(field => field.MetadataToken).ToArray();
}

/// <summary>
/// Bits level: the host solve of every rocket fixture gives the bits recorded in <c>Bits.approved.txt</c>. A tripwire, not a
/// contract (BOOT.md): a decomposition, a renaming or a reordering of code moves no line, so a moved line is a numerical change
/// and must be named in the commit that moves it.
/// </summary>
[Collection(CpuCollection.Name)]
public sealed class BitSnapshotTests(CpuFixture fixture)
{
    private static readonly ApprovedSnapshot Snapshot = ApprovedSnapshot.Load(ApprovedPath);

    public static string ApprovedPath => RepositoryPaths.Resolve("tests", "Performance.Tests", "Bits.approved.txt");

    public static IEnumerable<object[]> Cases() => RocketHost.Cases();

    [Theory]
    [MemberData(nameof(Cases))]
    public void Every_rocket_fixture_gives_the_recorded_bits(string name)
    {
        var c = RocketHost.Load(name);
        var bits = RocketBits.Hash(RocketHost.Solve(fixture, c));
        var problem = Snapshot.Problem(PathOf(c.Path), bits);
        Assert.True(problem is null, problem);
    }

    /// <summary>
    /// The Bits invariant's other direction (BOOT.md): every approved line still names a rocket fixture. Without this, a
    /// deleted or renamed fixture would leave its line in place, read by no case and never failing.
    /// </summary>
    [Fact]
    public void Every_approved_line_names_a_rocket_fixture()
    {
        var keys = FixtureFiles.Enumerate("rocket").Select(PathOf).ToList();
        var stale = Snapshot.StaleKeys(keys);
        Assert.True(stale.Count == 0,
                    string.Join("\n", stale.Select(key => $"{key}: recorded in the approved snapshot, but no longer a rocket fixture")));
    }

    /// <summary>The fixture's path from the repository root, with forward slashes, as the snapshot spells it.</summary>
    private static string PathOf(string path) => Path.GetRelativePath(RepositoryPaths.Root, path).Replace('\\', '/');
}
