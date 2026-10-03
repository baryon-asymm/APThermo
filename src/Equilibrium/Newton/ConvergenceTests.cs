using APThermo.Thermo;

namespace APThermo.Equilibrium.Newton;

/// <summary>
/// Equations (3.5) and (3.6) of RP-1311 chapter 3 on the corrections just applied, with the element balance, and the polish
/// test that follows once they pass: one verdict the Newton loop reads. Kernel-compatible.
/// </summary>
internal static class ConvergenceTests
{
    /// <summary>Equation (3.5), on the mole-number corrections weighted by their share of the mixture.</summary>
    private const double CorrectionTest = 0.5e-5;

    /// <summary>Equation (3.6b), on Δln T.</summary>
    private const double TemperatureTest = 1.0e-4;

    /// <summary>The steps after the report's tests run until the corrections are this small: the rounding floor of the linear solves.</summary>
    private const double PolishTest = 1.0e-11;

    /// <summary>
    /// NotConverged when equations (3.5) or (3.6) or the element balance are not met by the step just applied; otherwise
    /// ReportTestsMet, or Polished once the same corrections also fall below the rounding floor. The polish-step cap that
    /// forces a stop regardless is the loop's own, not this verdict's.
    /// </summary>
    public static ConvergenceVerdict Evaluate(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch,
                                              in EquilibriumResult result, in SystemLayout layout, in MixtureSums sums)
    {
        var worst = Worst(table, scratch, result, layout, sums);
        var deltaLogT = layout.IsTp ? 0.0 : scratch.RightHandSide[layout.TRow];
        var balanced = ElementBalance.WithinReportTest(table, problem, scratch, result);
        return !(worst <= CorrectionTest && balanced && (layout.IsTp || Math.Abs(deltaLogT) <= TemperatureTest))
            ? ConvergenceVerdict.NotConverged
            : worst <= PolishTest && (layout.IsTp || Math.Abs(deltaLogT) <= PolishTest)
                ? ConvergenceVerdict.Polished
                : ConvergenceVerdict.ReportTestsMet;
    }

    /// <summary>
    /// The verdict after the retention rule (BOOT.md, the loop's bookkeeping and the threshold flip, 2026-10-03). The
    /// tests above are taken over the gases <c>result.Moles</c> already retains from the step's own linearization point,
    /// so a passed verdict of a step that carried a gas across the threshold covered a set the final refresh would not
    /// report, and a crossing refuses it, but under the second stage only: the first stage's convergence only triggers
    /// the switch, as the reference's <c>tsize</c> does. A step that passed the report's tests and was refused because
    /// one gas entered the retained set while another left it is a flip, and the second flip in a row sets
    /// <see cref="IterationState.RetainedSetHeld"/> for the rest of the attempt.
    /// </summary>
    public static ConvergenceVerdict RetentionVerdict(in SpeciesTableView table, in EquilibriumScratch scratch, in EquilibriumResult result,
                                                      ConvergenceVerdict verdict, ref NewtonLoopState loop, ref IterationState state)
    {
        var crossing = verdict != ConvergenceVerdict.NotConverged && state.RetentionSecondStage
            ? Crossing(table, scratch, result, state.LogN, EquilibriumSolver.RetentionThreshold(state), state.RetainedSetHeld)
            : RetentionCrossing.None;
        if (loop.RecordFlip(crossing == RetentionCrossing.Both))
        {
            state.RetainedSetHeld = true;
        }

        return crossing == RetentionCrossing.None ? verdict : ConvergenceVerdict.NotConverged;
    }

    /// <summary>
    /// Which way the step just applied moved gaseous species across the retention rule of
    /// <see cref="Composition.IsRetained"/>: none, some entered, some left, or both at once. <paramref name="logN"/> is
    /// the iterate's <c>ln n</c> after the step, <see cref="IterationState.LogN"/>; <paramref name="traceThreshold"/> is
    /// the case's active stage (BOOT.md, the two-stage retention threshold, 2026-09-28), the same one
    /// <see cref="Composition.Retain"/> used for this step's sums; <paramref name="held"/> is
    /// <see cref="IterationState.RetainedSetHeld"/>.
    /// </summary>
    public static RetentionCrossing Crossing(in SpeciesTableView table, in EquilibriumScratch scratch, in EquilibriumResult result, double logN,
                                             double traceThreshold, bool held)
    {
        var crossing = RetentionCrossing.None;
        for (var j = 0; j < table.GasCount; j++)
        {
            var wasRetained = result.Moles[j] > 0.0;
            var isRetained = Composition.IsRetained(scratch, result, j, logN, traceThreshold, held);
            if (isRetained && !wasRetained)
            {
                crossing |= RetentionCrossing.Entered;
            }
            else if (wasRetained && !isRetained)
            {
                crossing |= RetentionCrossing.Left;
            }
        }

        return crossing;
    }

    /// <summary>Equation (3.5) on the undamped corrections: the largest mole-number correction as a share of the whole mixture.</summary>
    private static double Worst(in SpeciesTableView table, in EquilibriumScratch scratch, in EquilibriumResult result,
                                in SystemLayout layout, in MixtureSums sums)
    {
        var total = sums.SumGas;
        for (var c = 0; c < layout.CondensedCount; c++)
        {
            total += result.Moles[scratch.CondensedInSolution[c]];
        }

        var worst = sums.N * Math.Abs(scratch.RightHandSide[layout.NRow]) / total;
        for (var j = 0; j < table.GasCount; j++)
        {
            if (result.Moles[j] > 0.0)
            {
                worst = KernelMath.Max(worst, result.Moles[j] * Math.Abs(scratch.Corrections[j]) / total);
            }
        }

        for (var c = 0; c < layout.CondensedCount; c++)
        {
            worst = KernelMath.Max(worst, Math.Abs(scratch.RightHandSide[layout.ElementCount + c]) / total);
        }

        return worst;
    }
}
