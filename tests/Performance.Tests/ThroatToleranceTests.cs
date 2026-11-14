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
    /// <c>InternalsVisibleTo</c> grant. This fact checks policy constants, not execution of the tail. Red for a threshold of 1e-11 or a zero tail-length constant.
    /// </summary>
    [Fact]
    public void TheThroatPolicyConstantsSpecifyTheDecisionThresholdAndTailLength()
    {
        var decision = RocketSolver.ThroatDecisionTolerance;
        var tail = RocketSolver.ThroatTailSteps;

        Assert.True(decision >= 1.0e-8, $"throat decision threshold {decision:R} is below 1e-8, near the noise of u²/a²");
        Assert.True(decision > RocketSolver.TightTolerance,
                    $"throat decision threshold {decision:R} is not looser than the area-ratio iteration's {RocketSolver.TightTolerance:R}");
        Assert.Equal(2, tail);
    }
    /// <summary>A later higher peak does not erase the earlier smaller local maximum.</summary>
    [Fact]
    public void ALaterHigherMaximumDoesNotSatisfyTheFirstMaximumOracle()
    {
        double[] ratios = [0.3, 0.4, 0.5, 0.6, 0.7, 0.8, 0.9];
        double[] fluxes = [0.0, 10.0, 0.0, 0.0, 5.0, 0.0, 0.0];
        var peaks = MassFluxOracle.LocalMaxima(fluxes).ToArray();
        Assert.Equal(2, peaks.Length);
        Assert.True(fluxes[peaks[0]] > fluxes[peaks[1]]);
        Assert.Contains(peaks, peak => MassFluxOracle.IsDistinctEarlierMaximum(ratios, peak, selectedRatio: ratios[peaks[0]]));
        Assert.DoesNotContain(peaks, peak => MassFluxOracle.IsDistinctEarlierMaximum(ratios, peak, selectedRatio: ratios[peaks[1]]));
    }
}
