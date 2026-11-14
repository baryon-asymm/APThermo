using APThermo.Equilibrium.GasPhase;
using APThermo.Thermo;

namespace APThermo.Equilibrium.Recovery;

/// <summary>Finalize the condensed lever composition and offer its actual thermodynamic figures to the candidate bank.</summary>
internal static class GaslessFinal
{
    /// <summary>The gasless final held: the case ends <c>NoGasPhase</c> at its temperature, with the lever mix of the ends in place of the verdict's moles when it was taken between two ends.</summary>
    public static bool End(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch,
                                 in EquilibriumResult result, ref AttemptPlan plan)
    {
        if (plan.Bracket.Final == BracketMove.GaslessFromLever)
        {
            BracketSeeds.Lever(table, scratch, result, plan.Bracket.LeverFraction(problem.Target));
        }

        PassOutcome.EndGasless(result, ref plan);
        if (DeadEndRecheck.InProgress(plan))
        {
            plan.Figures = GasPhaseVerdict.Figures(table, scratch, result, plan.Current.Temperature);
            DeadEndCandidates.Consider(table, problem, scratch, result, ref plan);
            return DeadEndRecheck.AdvanceOrComplete(table, problem, scratch, result, unresolved: false, ref plan);
        }
        return false;
    }

}
