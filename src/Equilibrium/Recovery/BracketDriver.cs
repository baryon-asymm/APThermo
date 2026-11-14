using APThermo.Thermo;

namespace APThermo.Equilibrium.Recovery;

/// <summary>
/// Drives the temperature bracket of an <see cref="AttemptPlan"/> (BOOT.md, "The bracket"): after each tp pass it records the
/// probe as an end, asks the <see cref="TemperatureBracket"/> for its move and carries the move out, as the next probe, a final
/// attempt, a gasless final or the end of the case. The arithmetic is the bracket's; the compositions are
/// <see cref="BracketSeeds"/>'. Kernel-compatible.
/// </summary>
internal static class BracketDriver
{
    /// <summary>The bracket's first failure: the case's own attempts have failed, so tp probes begin at its temperature estimate.</summary>
    public static bool Begin(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch,
                             in EquilibriumResult result, ref AttemptPlan plan)
    {
        plan.BeginBracket(problem.Temperature);
        return LaunchProbe(table, problem, scratch, result, ref plan);
    }

    /// <summary>
    /// A tp pass the verdict did not prove: a verdict-only pass goes on to its attempts from its seed (the final of the
    /// bracket, to the hp or sp case itself); a failed probe retreats; a failed case ends with its status.
    /// </summary>
    public static bool Unproven(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch,
                                in EquilibriumResult result, ref AttemptPlan plan)
    {
        if (plan.Phase != AttemptPhase.VerdictOnly)
        {
            return plan.Bracket.Active && BracketLaunch.Move(table, problem, scratch, result, plan.Bracket.Retreat(), ref plan);
        }

        if (plan.Bracket.Finishing)
        {
            var both = plan.Bracket.HaveLow && plan.Bracket.HaveHigh;
            return BracketLaunch.Move(table, problem, scratch, result, both ? BracketMove.AttemptFromLever : BracketMove.AttemptFromProbe, ref plan);
        }

        plan.Phase = AttemptPhase.Warm;
        return true;
    }

    /// <summary>
    /// A probe that found a gas or a gasless minimum becomes an end of the bracket, with its moles; the bracket then asks for
    /// the next probe, a final, or gives up. The verdict-only final that held ends the case instead.
    /// </summary>
    public static bool Probed(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch,
                              in EquilibriumResult result, ref AttemptPlan plan)
    {
        if (plan.Bracket.Finishing)
        {
            return GaslessFinal.End(table, problem, scratch, result, ref plan);
        }

        var hp = PassOutcome.IsEnthalpy(problem);
        var value = PassOutcome.Value(result, plan, hp);
        var rejected = DeadEndRecheck.Rejects(table, problem, scratch, result, value, ref plan);
        if (rejected || DeadEndRecheck.Completed(plan))
        {
            return rejected;
        }

        if (plan.Bracket.Scanning && !(value > problem.Target))
        {
            var below = DeadEnds.FloorBelow(table, scratch, plan.Bracket.ProbeAsTemperature);
            return BracketLaunch.Move(table, problem, scratch, result, plan.Bracket.Scan(below), ref plan);
        }

        var step = plan.Bracket.Record(problem.Target, value, PassOutcome.Slope(result, plan, hp), plan.Found);
        BracketLaunch.SaveEnd(table, scratch, result, plan.Bracket.LastBelow);
        var floor = plan.Bracket.NeedsFloor ? DeadEnds.FloorBelow(table, scratch, plan.Bracket.ProbeAsTemperature) : 0.0;
        return BracketLaunch.Move(table, problem, scratch, result, plan.Bracket.Advance(step, floor), ref plan);
    }

    /// <summary>
    /// The final attempt of two narrow ends failed at a dead-end bound (BOOT.md, "Dead-end gaps"): the target lies in a gap
    /// where no state exists, so the search goes on below the nearest dead-end floor, scanning the far side of each floor for
    /// the first probe above the target; with no floor left the case ends <c>TemperatureOutOfRange</c>. False when the failure is not that.
    /// </summary>
    public static bool ScanBelowGap(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch,
                                    in EquilibriumResult result, ref AttemptPlan plan)
    {
        if (!(plan.Bracket.HaveLow && plan.Bracket.HaveHigh) || !DeadEnds.BoundNear(table, scratch, plan.Current.Temperature))
        {
            return false;
        }

        var floor = DeadEnds.FloorBelow(table, scratch, plan.Current.Temperature);
        plan.Bracket.BeginScan();
        return BracketLaunch.Move(table, problem, scratch, result, plan.Bracket.Scan(floor), ref plan);
    }

    /// <summary>Carry out the next probe requested by this node's floor cursor or bracket.</summary>
    public static bool LaunchProbe(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch,
                                   in EquilibriumResult result, ref AttemptPlan plan) =>
        BracketLaunch.Probe(table, problem, scratch, result, ref plan);
    /// <summary>
    /// Seam (b′): the ordinary final of the bracket ended <c>NotConverged</c> or <c>SingularMatrix</c>, and the case has not had a
    /// trace-gas final yet, so the same final runs again from the same seed, rebuilt from the ends, by the trace-gas pass. False
    /// when the case is not owed one.
    /// </summary>
    public static bool RetryFinalWithTraceGas(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch,
                                              in EquilibriumResult result, ref AttemptPlan plan)
    {
        if (!plan.OwesTraceGasFinal)
        {
            return false;
        }

        var move = plan.Bracket.Final == BracketMove.AttemptFromLever ? BracketMove.TraceGasFromLever : BracketMove.TraceGasFromProbe;
        return BracketLaunch.Final(table, problem, scratch, result, move, ref plan);
    }

}
