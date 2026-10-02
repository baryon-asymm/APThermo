namespace APThermo.Performance.Tests;

/// <summary>
/// L0: the throat's tight stop tolerance is its own constant and is tighter than the area-ratio iteration's (BOOT.md,
/// Constraints, the owner's decision of 2026-10-02).
/// </summary>
public sealed class ThroatToleranceTests
{
    /// <summary>
    /// The throat's tight tolerance is at least a decade below the area-ratio iteration's, which keeps
    /// <c>RocketSolver.TightTolerance</c>. Both are internal to the performance node and reached through its
    /// <c>InternalsVisibleTo</c> grant. Red when the throat's tolerance is set back to the area-ratio iteration's.
    /// </summary>
    [Fact]
    public void TheThroatStopsAtLeastADecadeTighterThanTheAreaRatioIteration()
    {
        var throat = RocketSolver.ThroatTightTolerance;
        var areaRatio = RocketSolver.TightTolerance;

        Assert.True(throat > 0.0, $"throat tolerance {throat:R} is not positive");
        Assert.True(throat <= areaRatio / 10.0,
                    $"throat tolerance {throat:R} is not a decade below the area-ratio iteration's {areaRatio:R}");
    }
}
