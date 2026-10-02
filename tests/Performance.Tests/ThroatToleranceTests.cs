namespace APThermo.Performance.Tests;

/// <summary>
/// L0: the throat's stop is a decision followed by a fixed tail (BOOT.md, Constraints, the owner's decision of
/// 2026-10-02), and the area-ratio iteration keeps its own tight tolerance.
/// </summary>
public sealed class ThroatToleranceTests
{
    /// <summary>
    /// The decision threshold is the owner's 1e-8, far from the noise of u²/a² that a threshold of 1e-11 sat in, and
    /// the tail is two momentum steps. Both are internal to the performance node and reached through its
    /// <c>InternalsVisibleTo</c> grant. Red for a threshold of 1e-11 and for no tail.
    /// </summary>
    [Fact]
    public void TheThroatDecidesAtOneEMinusEightAndTakesTwoMomentumSteps()
    {
        var decision = RocketSolver.ThroatDecisionTolerance;
        var tail = RocketSolver.ThroatTailSteps;

        Assert.True(decision >= 1.0e-8, $"throat decision threshold {decision:R} is below 1e-8, near the noise of u²/a²");
        Assert.True(decision > RocketSolver.TightTolerance,
                    $"throat decision threshold {decision:R} is not looser than the area-ratio iteration's {RocketSolver.TightTolerance:R}");
        Assert.Equal(2, tail);
    }
}
