using APThermo.Thermo;

namespace APThermo.Equilibrium.Recovery;

/// <summary>Exact best-candidate bank; selection never discards paid Newton iterations.</summary>
internal static class DeadEndCandidates
{
    /// <summary>The original feasible result is the first candidate and wins an exact tie.</summary>
    public static void Begin(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch,
                             in EquilibriumResult result, ref AttemptPlan plan)
    {
        plan.Recheck.BestObjective = Objective(problem, result, plan);
        plan.Recheck.BestStatus = CaseStatus.Ok;
        plan.Recheck.Unresolved = false;
        Save(table, scratch, result, ref plan.Recheck);
    }

    /// <summary>Strictly better objective replaces the bank; equality retains the earlier feasible state.</summary>
    public static void Consider(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch,
                                in EquilibriumResult result, ref AttemptPlan plan)
    {
        var objective = Objective(problem, result, plan);
        if (objective is not (> double.NegativeInfinity and < double.PositiveInfinity))
        {
            plan.Recheck.Unresolved = true;
            return;
        }

        var improves = PassOutcome.IsEnthalpy(problem) ? objective > plan.Recheck.BestObjective : objective < plan.Recheck.BestObjective;
        if (improves)
        {
            plan.Recheck.BestObjective = objective;
            plan.Recheck.BestStatus = plan.Status;
            Save(table, scratch, result, ref plan.Recheck);
        }
    }

    /// <summary>Restore the bank exactly; an unresolved branch gives an honest refusal with diagnostic composition.</summary>
    public static void Complete(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch,
                                in EquilibriumResult result, ref AttemptPlan plan)
    {
        for (var j = 0; j < table.SpeciesCount; j++)
        {
            result.Moles[j] = scratch.Recovery.BestMoles[j];
        }

        for (var i = 0; i < table.ElementCount; i++)
        {
            result.Multipliers[i] = scratch.Recovery.BestMultipliers[i];
        }

        result.State[0] = plan.Recheck.Unresolved ? default : plan.Recheck.BestState;
        plan.Status = plan.Recheck.Unresolved ? CaseStatus.NotConverged : plan.Recheck.BestStatus;
        plan.Current = PassOutcome.CaseAt(problem, plan.Recheck.BestState.Temperature);
        plan.Bracket = default;
        plan.Recheck.Stage = Recheck.Complete;
    }

    private static double Objective(in EquilibriumProblem problem, in EquilibriumResult result, in AttemptPlan plan) =>
        plan.Status == CaseStatus.NoGasPhase ? PassOutcome.Value(result, plan, !PassOutcome.IsEnthalpy(problem))
            : PassOutcome.IsEnthalpy(problem) ? result.State[0].Entropy : result.State[0].Enthalpy;

    private static void Save(in SpeciesTableView table, in EquilibriumScratch scratch, in EquilibriumResult result, ref RecheckState state)
    {
        for (var j = 0; j < table.SpeciesCount; j++)
        {
            scratch.Recovery.BestMoles[j] = result.Moles[j];
        }

        for (var i = 0; i < table.ElementCount; i++)
        {
            scratch.Recovery.BestMultipliers[i] = result.Multipliers[i];
        }

        state.BestState = result.State[0];
    }
}
