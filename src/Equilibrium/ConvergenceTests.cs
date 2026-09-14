using AerospacePropellantThermodynamics.Thermo;

namespace AerospacePropellantThermodynamics.Equilibrium;

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
        if (!(worst <= CorrectionTest && balanced && (layout.IsTp || Math.Abs(deltaLogT) <= TemperatureTest)))
        {
            return ConvergenceVerdict.NotConverged;
        }

        return worst <= PolishTest && (layout.IsTp || Math.Abs(deltaLogT) <= PolishTest)
            ? ConvergenceVerdict.Polished
            : ConvergenceVerdict.ReportTestsMet;
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
                worst = Math.Max(worst, result.Moles[j] * Math.Abs(scratch.Corrections[j]) / total);
            }
        }

        for (var c = 0; c < layout.CondensedCount; c++)
        {
            worst = Math.Max(worst, Math.Abs(scratch.RightHandSide[layout.ElementCount + c]) / total);
        }

        return worst;
    }
}
