using AerospacePropellantThermodynamics.Fixtures;
using AerospacePropellantThermodynamics.Harness;
using AerospacePropellantThermodynamics.Thermo;

namespace AerospacePropellantThermodynamics.Equilibrium.Tests;

/// <summary>
/// Bits: the host solve of every tp, hp and sp fixture case gives the recorded bits. A tripwire, not a contract (BOOT.md):
/// it cannot say that a number is right — the fixtures node's tolerance table says that — only that no number moved without
/// the commit saying so. The same role <c>PublicSurface.approved.txt</c> plays for the contract, and the same shape:
/// one approved file, an actual file written beside it on a difference, and instructions in the failure.
/// </summary>
[Collection(CpuCollection.Name)]
public sealed class BitSnapshotTests(CpuFixture fixture)
{
    /// <summary>The problem kinds whose fixture directories the snapshot covers.</summary>
    private static readonly string[] Kinds = ["tp", "hp", "sp"];

    public static string ApprovedPath => RepositoryPaths.Resolve("tests", "Equilibrium.Tests", "Bits.approved.txt");

    [Fact]
    public void Every_fixture_case_gives_the_recorded_bits()
    {
        var snapshot = ApprovedSnapshot.Load(ApprovedPath);
        var keys = new List<string>();
        var problems = new List<string>();
        foreach (var kind in Kinds)
        {
            foreach (var path in FixtureFiles.Enumerate(kind))
            {
                var name = Path.GetFileNameWithoutExtension(path);
                var key = kind + "/" + name + ".json";
                keys.Add(key);
                var problem = snapshot.Problem(key, Hash(HostSolver.Solve(fixture, HostSolver.Load(kind, name))));
                if (problem is not null)
                {
                    problems.Add(problem);
                }
            }
        }

        foreach (var stale in snapshot.StaleKeys(keys))
        {
            problems.Add($"{stale}: recorded in the approved snapshot, but no longer a fixture case");
        }

        Assert.True(problems.Count == 0,
            $"{problems.Count} fixture case(s) no longer give the recorded bits:\n" + string.Join("\n", problems.Take(20)) +
            (problems.Count > 20 ? $"\n… and {problems.Count - 20} more." : string.Empty));
    }

    /// <summary>
    /// SHA-256, lower-case hex, over the raw bits of the whole result in one order: every mole, every multiplier, every field
    /// of <see cref="MixtureState"/> in declaration order (by reflection, so that a field added there cannot be forgotten),
    /// the status and the iteration count.
    /// </summary>
    private static string Hash(HostSolution solution)
    {
        var hash = new BitHash().Add(solution.Moles).Add(solution.Multipliers);
        foreach (var field in typeof(MixtureState).GetFields())
        {
            hash.Add((double)field.GetValue(solution.State)!);
        }

        return hash.Add((int)solution.Status).Add(solution.Iterations).ToHex();
    }
}
