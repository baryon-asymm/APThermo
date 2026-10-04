using APThermo.Thermo;

namespace APThermo.Equilibrium.Recovery;

/// <summary>
/// The recheck of an Ok hp or sp state below a dead-end floor (BOOT.md, "Dead-end floors"): when the state is a
/// supercooled vapour with a positive inclusion gain for a phase whose data stop short of it, one tp probe at the floor decides
/// whether a state of the same h or s holding the phase lies above it. If so the bracket takes over from that probe, and
/// its final replaces the Ok; if not, or if the bracket fails, the original cold attempt runs again, deterministic, to the same
/// bits and the iterations it reported. The single exception to "an Ok attempt ends the case untouched". Kernel-compatible.
/// </summary>
internal static class DeadEndRecheck
{
    /// <summary>
    /// An hp or sp attempt ended Ok. A cold one below a dead-end floor starts the recheck with the probe at that floor; the rerun
    /// that ends a recheck reports the iterations of the original. False when the case ends as it stands.
    /// </summary>
    public static bool AfterOk(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch,
                               in EquilibriumResult result, ref AttemptPlan plan)
    {
        if (plan.Recheck.Stage == Recheck.Replay)
        {
            plan.Iterations = plan.Recheck.Banked;
            return false;
        }

        var floor = plan.Phase == AttemptPhase.Cold ? DeadEnds.FloorAbove(table, scratch, result, PassOutcome.Temperature(result)) : 0.0;
        if (!(floor > 0.0))
        {
            return false;
        }

        plan.Recheck.Original = plan.Current;
        plan.Recheck.Banked = plan.Iterations;
        plan.Recheck.Stage = Recheck.Pending;
        plan.BeginBracket(floor);
        return BracketDriver.LaunchProbe(table, problem, scratch, result, ref plan);
    }

    /// <summary>
    /// Whether the probe at the floor, whose assigned property is <paramref name="value"/>, shows that no state above the floor
    /// has the <paramref name="target"/>: its property is above it, so the original attempt is rerun. The first probe only.
    /// </summary>
    public static bool Rejects(ref AttemptPlan plan, double value, double target)
    {
        if (plan.Recheck.Stage != Recheck.Pending)
        {
            return false;
        }

        plan.Recheck.Stage = Recheck.Held;
        return value > target && Rerun(ref plan);
    }

    /// <summary>The final attempt's status is the case's; a state a probe wrote is cleared when it is not Ok, an ordinary final that ends <c>NotConverged</c> or <c>SingularMatrix</c> is owed one trace-gas final (seam (b′)), a recheck that fails here reruns the original attempt instead, and a final that fails on a gap at a dead-end bound scans below it.</summary>
    public static bool EndFinal(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch,
                                in EquilibriumResult result, ref AttemptPlan plan)
    {
        if (plan.Status == CaseStatus.Ok)
        {
            return false;
        }

        if (BracketDriver.RetryFinalWithTraceGas(table, problem, scratch, result, ref plan)
            || Rerun(ref plan)
            || BracketDriver.ScanBelowGap(table, problem, scratch, result, ref plan))
        {
            return true;
        }

        PassOutcome.ClearState(result);
        return false;
    }

    /// <summary>The recheck did not replace the Ok state: its original cold attempt runs again; false when no recheck is under way.</summary>
    public static bool Rerun(ref AttemptPlan plan)
    {
        if (plan.Recheck.Stage is not (Recheck.Pending or Recheck.Held))
        {
            return false;
        }

        plan.Bracket = default;
        plan.Current = plan.Recheck.Original;
        plan.Source = EstimateSource.Defaults;
        plan.Phase = AttemptPhase.Cold;
        plan.Recheck.Stage = Recheck.Replay;
        return true;
    }
}
