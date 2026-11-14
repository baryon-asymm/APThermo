using APThermo.Thermo;

namespace APThermo.Equilibrium.Recovery;

/// <summary>Bounded rechecks of distinct dead-end floors above an otherwise converged cold HP/SP state.</summary>
internal static class DeadEndRecheck
{
    /// <summary>A cold HP/SP Ok below a positive-gain dead-end floor starts at the first distinct floor above it.</summary>
    public static bool AfterOk(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch,
                               in EquilibriumResult result, ref AttemptPlan plan)
    {
        if (plan.Recheck.Stage == Recheck.Replay)
        {
            plan.Iterations = plan.Recheck.Banked;
            return false;
        }

        var temperature = PassOutcome.Temperature(result);
        var hasFloor = plan.Phase == AttemptPhase.Cold && DeadEnds.FloorAbove(table, scratch, result, temperature) > 0.0;
        var floor = hasFloor ? DeadEnds.NextFloorAbove(table, scratch, temperature) : 0.0;
        if (!(floor > 0.0))
        {
            return false;
        }

        plan.Recheck.Original = plan.Current;
        plan.Recheck.Banked = plan.Iterations;
        plan.Recheck.OriginalObjective = PassOutcome.IsEnthalpy(problem) ? result.State[0].Entropy : result.State[0].Enthalpy;
        return ProbeAt(table, problem, scratch, result, floor, ref plan);
    }

    /// <summary>A floor probe above the target rejects that segment only; another distinct floor is still checked.</summary>
    public static bool Rejects(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch,
                               in EquilibriumResult result, double value, ref AttemptPlan plan)
    {
        if (plan.Recheck.Stage != Recheck.Pending)
        {
            return false;
        }

        plan.Recheck.Stage = Recheck.Held;
        return value > problem.Target && AdvanceOrReplay(table, problem, scratch, result, ref plan);
    }

    /// <summary>Finish a bracket, or continue the recheck after a failed or inferior final before replaying the original.</summary>
    public static bool EndFinal(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch,
                                in EquilibriumResult result, ref AttemptPlan plan)
    {
        if (plan.Status == CaseStatus.Ok)
        {
            return plan.Recheck.Stage == Recheck.Held && !Improves(problem, result, plan.Recheck.OriginalObjective)
                && AdvanceOrReplay(table, problem, scratch, result, ref plan);
        }

        if (BracketDriver.RetryFinalWithTraceGas(table, problem, scratch, result, ref plan)
            || AdvanceOrReplay(table, problem, scratch, result, ref plan)
            || BracketDriver.ScanBelowGap(table, problem, scratch, result, ref plan))
        {
            return true;
        }

        PassOutcome.ClearState(result);
        return false;
    }

    /// <summary>The next floor is independent of the probe's multipliers; exhaustion permits deterministic replay.</summary>
    public static bool AdvanceOrReplay(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch,
                                       in EquilibriumResult result, ref AttemptPlan plan)
    {
        if (plan.Recheck.Stage is not (Recheck.Pending or Recheck.Held))
        {
            return false;
        }

        var floor = DeadEnds.NextFloorAbove(table, scratch, plan.Recheck.Floor);
        return floor > 0.0 ? ProbeAt(table, problem, scratch, result, floor, ref plan) : Rerun(ref plan);
    }

    /// <summary>Rerun the original cold attempt only after the recheck exhausts its distinct floors.</summary>
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

    /// <summary>Each candidate branch must improve the original state in the problem's thermodynamic objective.</summary>
    private static bool Improves(in EquilibriumProblem problem, in EquilibriumResult result, double original) =>
        PassOutcome.IsEnthalpy(problem) ? result.State[0].Entropy >= original : result.State[0].Enthalpy <= original;

    /// <summary>Drop stale bracket ends and launch the cold TP probe at the next strictly greater floor.</summary>
    private static bool ProbeAt(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch,
                                in EquilibriumResult result, double floor, ref AttemptPlan plan)
    {
        plan.Recheck.Floor = floor;
        plan.Recheck.Stage = Recheck.Pending;
        plan.Bracket = default;
        plan.BeginBracket(floor);
        return BracketDriver.LaunchProbe(table, problem, scratch, result, ref plan);
    }
}
