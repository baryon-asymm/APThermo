using APThermo.Thermo;

namespace APThermo.Performance.Tests;

/// <summary>L1: every rocket fixture case is solved and compared with the reference station by station.</summary>
[Collection(CpuFixture.CollectionName)]
public sealed class RocketFixtureTests
{
    /// <summary>The rocket fixture files as theory data, delegating to <see cref="RocketHost.Cases"/>.</summary>
    public static TheoryData<string> Cases() => RocketHost.Cases();

    /// <summary>The rocket case reproduces the reference.</summary>
    [Theory]
    [MemberData(nameof(Cases))]
    public void TheRocketCaseReproducesTheReference(string name)
    {
        var c = RocketHost.Load(name);
        var solution = RocketHost.Solve(CpuFixture.Shared, c);
        Assert.True(solution.Status == CaseStatus.Ok,
                    $"status {solution.Status}; stations [{string.Join(", ", solution.Outcome.StationStatus)}], iterations [{string.Join(", ", solution.Outcome.Iterations)}]");
        var mismatches = StationComparison.Compare(c, solution, CpuFixture.Shared.Tolerances);
        Assert.True(mismatches.Count == 0, $"{mismatches.Count} mismatches: " + string.Join("; ", mismatches));
    }
}
