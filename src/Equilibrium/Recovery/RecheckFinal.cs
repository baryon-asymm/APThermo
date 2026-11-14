using APThermo.Thermo;

namespace APThermo.Equilibrium.Recovery;

/// <summary>Final-attempt routing, separated from the floor cursor and candidate bank.</summary>
internal static class RecheckFinal
{
    /// <summary>Record a successful final then continue; a failed uncertified branch makes the completed search unresolved.</summary>
    public static bool End(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch,
                                in EquilibriumResult result, ref AttemptPlan plan)
    {
        if (plan.Recheck.Stage is Recheck.Pending or Recheck.Held)
        {
            if (plan.Status == CaseStatus.Ok)
            {
                DeadEndCandidates.Consider(table, problem, scratch, result, ref plan);
                return DeadEndRecheck.AdvanceOrComplete(table, problem, scratch, result, unresolved: false, ref plan);
            }

            if (BracketDriver.RetryFinalWithTraceGas(table, problem, scratch, result, ref plan))
            {
                return true;
            }

            var gap = plan.Bracket.NarrowEnds && DeadEnds.BoundNear(table, scratch, plan.Current.Temperature);
            return DeadEndRecheck.AdvanceOrComplete(table, problem, scratch, result, unresolved: !gap, ref plan);
        }

        if (plan.Status == CaseStatus.Ok)
        {
            return false;
        }

        if (BracketDriver.RetryFinalWithTraceGas(table, problem, scratch, result, ref plan)
            || BracketDriver.ScanBelowGap(table, problem, scratch, result, ref plan))
        {
            return true;
        }

        PassOutcome.ClearState(result);
        return false;
    }

}
