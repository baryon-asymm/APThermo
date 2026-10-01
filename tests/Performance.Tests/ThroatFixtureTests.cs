using APThermo.Thermo;

namespace APThermo.Performance.Tests;

/// <summary>
/// L1: every fixture of the throat family reproduces the reference (`tests/Fixtures/generate/BOOT.md`, the `throat`
/// entry of the case matrix; Performance BOOT.md, Invariants, "The throat is the first maximum of the mass flux met from the
/// chamber"). The fixture carries the chamber and the throat only, so the
/// same station-by-station comparison the rocket kind uses applies unchanged.
/// </summary>
[Collection(CpuFixture.CollectionName)]
public sealed class ThroatFixtureTests
{
    /// <summary>The throat fixture files as theory data, delegating to <see cref="RocketHost.Cases(string)"/>.</summary>
    public static TheoryData<string> Cases() => RocketHost.Cases("throat");

    /// <summary>The throat case reproduces the reference chamber and throat.</summary>
    [Theory]
    [MemberData(nameof(Cases))]
    public void TheThroatCaseReproducesTheReference(string name)
    {
        var c = RocketHost.Load("throat", name);
        var solution = RocketHost.Solve(CpuFixture.Shared, RocketInputs.Of(c));
        Assert.True(solution.Status == CaseStatus.Ok,
                    $"status {solution.Status}; stations [{string.Join(", ", solution.Outcome.StationStatus)}], iterations [{string.Join(", ", solution.Outcome.Iterations)}]");
        var mismatches = StationComparison.Compare(c, solution, CpuFixture.Shared.Tolerances);
        Assert.True(mismatches.Count == 0, $"{mismatches.Count} mismatches: " + string.Join("; ", mismatches));
    }
}
