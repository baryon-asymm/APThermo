using APThermo.Thermo;

namespace APThermo.Equilibrium.Recovery;

/// <summary>Walk every distinct relevant floor and restore the best feasible candidate, never the first improvement alone.</summary>
internal static class DeadEndRecheck
{
    /// <summary>An initial cold or warm HP/SP Ok below a positive-gain floor starts the bounded best-candidate search.</summary>
    public static bool AfterOk(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch,
                               in EquilibriumResult result, ref AttemptPlan plan)
    {
        var temperature = PassOutcome.Temperature(result);
        var initial = plan.Phase is AttemptPhase.Cold or AttemptPhase.Warm;
        var hasFloor = initial && DeadEnds.FloorAbove(table, scratch, result, temperature) > 0.0;
        var floor = hasFloor ? DeadEnds.NextFloorAbove(table, scratch, temperature) : 0.0;
        if (!(floor > 0.0))
        {
            return false;
        }

        DeadEndCandidates.Begin(table, problem, scratch, result, ref plan);
        return ProbeAt(table, problem, scratch, result, floor, ref plan);
    }

    /// <summary>A floor above the target excludes its segment only; continue to the next distinct floor.</summary>
    public static bool Rejects(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch,
                               in EquilibriumResult result, double value, ref AttemptPlan plan)
    {
        if (plan.Recheck.Stage != Recheck.Pending)
        {
            return false;
        }

        plan.Recheck.Stage = Recheck.Held;
        return value > problem.Target && AdvanceOrComplete(table, problem, scratch, result, unresolved: false, ref plan);
    }

    /// <summary>Advance strictly; exhaustion restores the bank and keeps every attempted iteration counted.</summary>
    public static bool AdvanceOrComplete(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch,
                                         in EquilibriumResult result, bool unresolved, ref AttemptPlan plan)
    {
        if (plan.Recheck.Stage is not (Recheck.Pending or Recheck.Held))
        {
            return false;
        }

        plan.Recheck.Unresolved |= unresolved;
        var floor = DeadEnds.NextFloorAbove(table, scratch, plan.Recheck.Floor);
        if (floor > 0.0)
        {
            return ProbeAt(table, problem, scratch, result, floor, ref plan);
        }

        DeadEndCandidates.Complete(table, problem, scratch, result, ref plan);
        return false;
    }

    /// <summary>Whether a bounded floor recheck is active.</summary>
    public static bool InProgress(in AttemptPlan plan) => plan.Recheck.Stage is Recheck.Pending or Recheck.Held;

    /// <summary>Whether the bank has been restored and no bracket action remains.</summary>
    public static bool Completed(in AttemptPlan plan) => plan.Recheck.Stage == Recheck.Complete;

    /// <summary>The final-attempt policy is private behind this node's recheck facade.</summary>
    public static bool EndFinal(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch,
                                in EquilibriumResult result, ref AttemptPlan plan) =>
        RecheckFinal.End(table, problem, scratch, result, ref plan);

    /// <summary>A failed numerical bracket is unresolved; reaching the declared domain edge excludes that branch.</summary>
    public static bool GiveUp(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch,
                              in EquilibriumResult result, ref AttemptPlan plan)
    {
        if (InProgress(plan))
        {
            return AdvanceOrComplete(table, problem, scratch, result, !plan.Bracket.GaveUpAtDomainEdge, ref plan);
        }

        PassOutcome.EndGiveUp(result, ref plan);
        return false;
    }
    private static bool ProbeAt(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch,
                                in EquilibriumResult result, double floor, ref AttemptPlan plan)
    {
        plan.Recheck.Floor = floor;
        plan.Recheck.Stage = Recheck.Pending;
        plan.Bracket = default;
        plan.TraceGasFinals = 0;
        plan.BeginBracket(floor);
        return BracketDriver.LaunchProbe(table, problem, scratch, result, ref plan);
    }
}
