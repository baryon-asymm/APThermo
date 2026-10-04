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
            return plan.Bracket.Active && Launch(table, problem, scratch, result, plan.Bracket.Retreat(), ref plan);
        }

        if (plan.Bracket.Finishing)
        {
            var both = plan.Bracket.HaveLow && plan.Bracket.HaveHigh;
            return Launch(table, problem, scratch, result, both ? BracketMove.AttemptFromLever : BracketMove.AttemptFromProbe, ref plan);
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
            return Finished(table, problem, scratch, result, ref plan);
        }

        var hp = PassOutcome.IsEnthalpy(problem);
        var value = PassOutcome.Value(result, plan, hp);
        if (DeadEndRecheck.Rejects(ref plan, value, problem.Target))
        {
            return true;
        }

        if (plan.Bracket.Scanning && !(value > problem.Target))
        {
            var below = DeadEnds.FloorBelow(table, scratch, Math.Exp(plan.Bracket.ProbeX));
            return Launch(table, problem, scratch, result, plan.Bracket.Scan(below), ref plan);
        }

        var step = plan.Bracket.Record(problem.Target, value, PassOutcome.Slope(result, plan, hp), plan.Found);
        BracketSeeds.Save(table, scratch, result, plan.Bracket.LastBelow);
        var floor = plan.Bracket.NeedsFloor ? DeadEnds.FloorBelow(table, scratch, Math.Exp(plan.Bracket.ProbeX)) : 0.0;
        return Launch(table, problem, scratch, result, plan.Bracket.Advance(step, floor), ref plan);
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
        return Launch(table, problem, scratch, result, plan.Bracket.Scan(floor), ref plan);
    }

    /// <summary>
    /// The tp probe at the bracket's temperature: cold when no end is known; else seeded from the nearer end, and the verdict
    /// alone first when that end holds no gas.
    /// </summary>
    public static bool LaunchProbe(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch,
                                   in EquilibriumResult result, ref AttemptPlan plan)
    {
        plan.Current = PassOutcome.TpAt(problem, plan.Bracket.ProbeTemperature);
        if (!(plan.Bracket.HaveLow || plan.Bracket.HaveHigh))
        {
            plan.Source = EstimateSource.Defaults;
            plan.Phase = AttemptPhase.Cold;
            return true;
        }

        BracketSeeds.Seed(table, scratch, result, plan.Bracket.NearerIsLow(plan.Bracket.ProbeX));
        plan.Source = EstimateSource.PreviousSolution;
        plan.Phase = plan.Bracket.NearerKind == EndKind.Gasless ? AttemptPhase.VerdictOnly : AttemptPhase.Warm;
        return true;
    }

    /// <summary>The gasless final held: the case ends <c>NoGasPhase</c> at its temperature, with the lever mix of the ends in place of the verdict's moles when it was taken between two ends.</summary>
    private static bool Finished(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch,
                                 in EquilibriumResult result, ref AttemptPlan plan)
    {
        if (plan.Bracket.Final == BracketMove.GaslessFromLever)
        {
            BracketSeeds.Lever(table, scratch, result, plan.Bracket.LeverFraction(problem.Target));
        }

        PassOutcome.EndGasless(result, ref plan);
        return false;
    }

    /// <summary>Carries out the bracket's move: the next probe, a final, or the end of the case.</summary>
    private static bool Launch(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch,
                               in EquilibriumResult result, BracketMove move, ref AttemptPlan plan)
    {
        if (move == BracketMove.Probe)
        {
            return LaunchProbe(table, problem, scratch, result, ref plan);
        }

        if (move == BracketMove.GiveUp)
        {
            if (DeadEndRecheck.Rerun(ref plan))
            {
                return true;
            }

            PassOutcome.EndGiveUp(result, ref plan);
            return false;
        }

        return move is BracketMove.GaslessAtProbe or BracketMove.GaslessFromLever
            ? LaunchGasless(problem, move, ref plan)
            : LaunchFinal(table, problem, scratch, result, move, ref plan);
    }

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
        return LaunchFinal(table, problem, scratch, result, move, ref plan);
    }

    /// <summary>
    /// The final attempt, the case itself, warm, from the lever rule between the two ends or from the last probe; it never falls back.
    /// A trace-gas final (seams (b) and (b′)) takes the multipliers the result holds as its anchor, and counts once against the case.
    /// </summary>
    private static bool LaunchFinal(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch,
                                    in EquilibriumResult result, BracketMove move, ref AttemptPlan plan)
    {
        var temperature = Math.Exp(plan.Bracket.ProbeX);
        if (move is BracketMove.AttemptFromLever or BracketMove.TraceGasFromLever)
        {
            temperature = plan.Bracket.LeverTemperature(problem.Target);
            BracketSeeds.Lever(table, scratch, result, plan.Bracket.LeverFraction(problem.Target));
        }
        else
        {
            BracketSeeds.Seed(table, scratch, result, plan.Bracket.LastBelow);
        }

        var traceGas = move is BracketMove.TraceGasFromLever or BracketMove.TraceGasFromProbe;
        if (traceGas)
        {
            BracketSeeds.Anchor(table, scratch, result);
            plan.TraceGasFinals++;
        }

        plan.Bracket.Final = move;
        plan.Current = PassOutcome.CaseAt(problem, temperature);
        plan.Source = EstimateSource.PreviousSolution;
        plan.Phase = traceGas ? AttemptPhase.TraceGasFinal : AttemptPhase.Final;
        return true;
    }

    /// <summary>The gasless final: the verdict alone at the temperature Newton's step converged on, or at the lever temperature between two gasless ends.</summary>
    private static bool LaunchGasless(in EquilibriumProblem problem, BracketMove move, ref AttemptPlan plan)
    {
        var temperature = move == BracketMove.GaslessAtProbe ? Math.Exp(plan.Bracket.FinalX) : plan.Bracket.LeverTemperature(problem.Target);
        plan.Bracket.Finishing = true;
        plan.Bracket.Final = move;
        plan.Current = PassOutcome.TpAt(problem, temperature);
        plan.Source = EstimateSource.PreviousSolution;
        plan.Phase = AttemptPhase.VerdictOnly;
        return true;
    }
}
