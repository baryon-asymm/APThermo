using APThermo.Thermo;

namespace APThermo.Equilibrium.Recovery;

/// <summary>Carry bracket moves out through seeded probes, finals and gasless attempts.</summary>
internal static class BracketLaunch
{
    /// <summary>The just-recorded composition becomes one bracket end before its next move is carried out.</summary>
    public static void SaveEnd(in SpeciesTableView table, in EquilibriumScratch scratch, in EquilibriumResult result, bool below) =>
        BracketSeeds.Save(table, scratch, result, below);
    /// <summary>
    /// The tp probe at the bracket's temperature: cold when no end is known; else seeded from the nearer end, and the verdict
    /// alone first when that end holds no gas.
    /// </summary>
    public static bool Probe(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch,
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

    /// <summary>Carry out the next probe, final, gasless attempt or completed search.</summary>
    public static bool Move(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch,
                            in EquilibriumResult result, BracketMove move, ref AttemptPlan plan)
    {
        return move switch
        {
            BracketMove.Probe => Probe(table, problem, scratch, result, ref plan),
            BracketMove.GiveUp => DeadEndRecheck.GiveUp(table, problem, scratch, result, ref plan),
            BracketMove.GaslessAtProbe => Gasless(problem, move, ref plan),
            BracketMove.GaslessFromLever => Gasless(problem, move, ref plan),
            BracketMove.AttemptFromLever => Final(table, problem, scratch, result, move, ref plan),
            BracketMove.AttemptFromProbe => Final(table, problem, scratch, result, move, ref plan),
            BracketMove.TraceGasFromLever => Final(table, problem, scratch, result, move, ref plan),
            BracketMove.TraceGasFromProbe => Final(table, problem, scratch, result, move, ref plan),
            _ => Final(table, problem, scratch, result, move, ref plan),
        };
    }
    /// <summary>
    /// The final attempt, the case itself, warm, from the lever rule between the two ends or from the last probe; it never falls back.
    /// A trace-gas final (seams (b) and (b′)) takes the multipliers the result holds as its anchor, and counts once against the case.
    /// </summary>
    public static bool Final(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch,
                                    in EquilibriumResult result, BracketMove move, ref AttemptPlan plan)
    {
        var temperature = plan.Bracket.ProbeAsTemperature;
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
    private static bool Gasless(in EquilibriumProblem problem, BracketMove move, ref AttemptPlan plan)
    {
        var temperature = move == BracketMove.GaslessAtProbe ? plan.Bracket.FinalAsTemperature : plan.Bracket.LeverTemperature(problem.Target);
        plan.Bracket.Finishing = true;
        plan.Bracket.Final = move;
        plan.Current = PassOutcome.TpAt(problem, temperature);
        plan.Source = EstimateSource.PreviousSolution;
        plan.Phase = AttemptPhase.VerdictOnly;
        return true;
    }
}
