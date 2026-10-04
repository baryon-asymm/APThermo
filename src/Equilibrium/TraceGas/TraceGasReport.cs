using APThermo.Thermo;

namespace APThermo.Equilibrium.TraceGas;

/// <summary>
/// What a converged trace-gas iteration reports of the gases below the second retention stage (BOOT.md, "An Ok reports its balance
/// carriers"): the ones whose atoms are part of a balance are kept at their converged amounts, the rest are zeroed. The report of the
/// reduced iteration drops every gas below that stage, because there the dropped gases are ones the iteration itself held at zero; a
/// trace-gas iteration converged with every gas, so a gas it drops takes its atoms out of the balance it just closed. Kernel-compatible.
/// </summary>
internal static class TraceGasReport
{
    /// <summary>
    /// The share of an element's abundance below which a gas's atoms of it count for nothing: a thousandth of the node's invariant
    /// (<see cref="ElementBalance.Invariant"/>), so that the gases dropped on this ground, a hundred of them at the most in a table of the
    /// tree, leave the balance a tenth of the invariant open at the most.
    /// </summary>
    private const double NegligibleShare = 1.0e-16;

    /// <summary>
    /// Zeroes the gaseous moles of the result below the second retention stage of <paramref name="logN"/> whose atoms of every active
    /// element stay within <see cref="NegligibleShare"/> of its abundance; the others stay as the iteration converged them. Returns how many
    /// stay: the trace carriers (<see cref="IterationState.TraceCarriers"/>). <c>scratch.LogMoles</c> holds ln n_j of the last evaluation.
    /// </summary>
    public static int KeepBalanceCarriers(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch,
                                          in EquilibriumResult result, double logN)
    {
        var kept = 0;
        for (var j = 0; j < table.GasCount; j++)
        {
            if (result.Moles[j] == 0.0 || scratch.LogMoles[j] - logN > -EquilibriumSolver.SecondStageTraceThreshold)
            {
                continue;
            }

            if (CarriesABalance(table, problem, scratch, result, j))
            {
                kept++;
            }
            else
            {
                result.Moles[j] = 0.0;
            }
        }

        return kept;
    }

    /// <summary>Whether gas <paramref name="j"/> holds more than <see cref="NegligibleShare"/> of some active element's abundance.</summary>
    private static bool CarriesABalance(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch,
                                        in EquilibriumResult result, int j)
    {
        for (var i = 0; i < table.ElementCount; i++)
        {
            if (scratch.ElementActive[i] != 0
                && table.Stoichiometry[i * table.SpeciesCount + j] * result.Moles[j] > NegligibleShare * problem.ElementMoles[i])
            {
                return true;
            }
        }

        return false;
    }
}
