using AerospacePropellantThermodynamics.Thermo;

namespace AerospacePropellantThermodynamics.Equilibrium;

/// <summary>
/// Element conservation: the sum Σ a_ij n_j over the whole composition, written once here and used both by the iteration
/// matrix (as the right-hand side b_i° − b_i of the element rows) and by the two tolerance tests below. Kernel-compatible.
/// </summary>
/// <remarks>
/// The two tests are two different questions and therefore two methods rather than one with a flag: equation (3.6a) of
/// RP-1311 asks whether the iterate is converged, relative to the largest abundance of the case; the node's own invariant
/// (BOOT.md) asks whether a converged result may be reported as Ok at all, per element and absolute.
/// </remarks>
internal static class ElementBalance
{
    /// <summary>Equation (3.6a): the convergence test on the element residuals, relative to the largest abundance.</summary>
    private const double ReportTest = 1.0e-6;

    /// <summary>The node's element-conservation invariant (BOOT.md), on max(1, b_i).</summary>
    private const double Invariant = 1.0e-12;

    /// <summary>The element's abundance in the composition: Σ a_ij n_j in kmol per kilogram.</summary>
    public static double Residual(in SpeciesTableView table, in EquilibriumResult result, int element)
    {
        var speciesCount = table.SpeciesCount;
        var b = 0.0;
        for (var j = 0; j < speciesCount; j++)
        {
            b += table.Stoichiometry[element * speciesCount + j] * result.Moles[j];
        }

        return b;
    }

    /// <summary>Equation (3.6a): every active element's residual within ReportTest times the largest abundance of the case.</summary>
    public static bool WithinReportTest(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch,
                                        in EquilibriumResult result)
    {
        var largest = 0.0;
        for (var i = 0; i < table.ElementCount; i++)
        {
            largest = Math.Max(largest, problem.ElementMoles[i]);
        }

        for (var i = 0; i < table.ElementCount; i++)
        {
            if (scratch.ElementActive[i] == 0)
            {
                continue;
            }

            if (Math.Abs(problem.ElementMoles[i] - Residual(table, result, i)) > ReportTest * largest)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>The node's invariant: every active element's residual within Invariant times max(1, b_i°). An Ok status requires it.</summary>
    public static bool WithinInvariant(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch,
                                       in EquilibriumResult result)
    {
        for (var i = 0; i < table.ElementCount; i++)
        {
            if (scratch.ElementActive[i] == 0)
            {
                continue;
            }

            if (Math.Abs(problem.ElementMoles[i] - Residual(table, result, i)) > Invariant * Math.Max(1.0, problem.ElementMoles[i]))
            {
                return false;
            }
        }

        return true;
    }
}
