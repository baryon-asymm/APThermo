using APThermo.Thermo;

namespace APThermo.Performance.Tests;

/// <summary>L1: every rocket fixture case is solved and compared with the reference station by station.</summary>
[Collection(CpuCollection.Name)]
public sealed class RocketFixtureTests(CpuFixture fixture)
{
    public static IEnumerable<object[]> Cases() => RocketHost.Cases();

    [Theory]
    [MemberData(nameof(Cases))]
    public void The_rocket_case_reproduces_the_reference(string name)
    {
        var c = RocketHost.Load(name);
        var solution = RocketHost.Solve(fixture, c);
        Assert.True(solution.Status == CaseStatus.Ok,
                    $"status {solution.Status}; stations [{string.Join(", ", solution.Outcome.StationStatus)}], iterations [{string.Join(", ", solution.Outcome.Iterations)}]");
        var mismatches = StationComparison.Compare(c, solution, fixture.Tolerances);
        Assert.True(mismatches.Count == 0, $"{mismatches.Count} mismatches: " + string.Join("; ", mismatches));
    }
}
