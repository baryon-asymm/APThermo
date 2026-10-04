using System.Runtime.CompilerServices;
using APThermo.Equilibrium.GasPhase;
using APThermo.Thermo;

namespace APThermo.Equilibrium.Recovery;

/// <summary>
/// What a case does after each attempt and when it ends (BOOT.md, "The ladder"): the per-case state carried by
/// <see cref="EquilibriumSolver.Solve"/> across its attempts, and the attempt it asks for next. Kernel-compatible.
/// </summary>
internal struct AttemptPlan
{
    /// <summary>The problem of the attempt asked for.</summary>
    public EquilibriumProblem Current;

    /// <summary>Where the estimate of the attempt asked for comes from.</summary>
    public EstimateSource Source;

    /// <summary>Which pass of the case the one asked for is.</summary>
    public AttemptPhase Phase;

    /// <summary>The status the case ends with, once <see cref="Next"/> has answered false.</summary>
    public CaseStatus Status;

    /// <summary>Newton steps of every attempt so far, the number the case reports; a verdict counts none.</summary>
    public int Iterations;

    /// <summary>The temperature bracket of an hp or sp case whose own attempts failed.</summary>
    public TemperatureBracket Bracket;

    /// <summary>What the pass just ended found, for the bracket: a converged state or a gasless verdict.</summary>
    public EndKind Found;

    /// <summary>The figures of the gasless verdict of the pass just ended.</summary>
    public CondensedFigures Figures;

    /// <summary>Whether <see cref="EquilibriumSolver.Solve"/> runs the iteration for the pass asked for; false for a pass that is the verdict alone.</summary>
    public readonly bool RunsAttempt => Phase != AttemptPhase.VerdictOnly;

    /// <summary>The plan of a case's first attempt: warm from the result's moles when <paramref name="useMolesAsEstimate"/>, else cold.</summary>
    public static AttemptPlan Start(in EquilibriumProblem problem, bool useMolesAsEstimate) =>
        new()
        {
            Current = problem,
            Source = useMolesAsEstimate ? EstimateSource.PreviousSolution : EstimateSource.Defaults,
            Phase = useMolesAsEstimate ? AttemptPhase.Warm : AttemptPhase.Cold,
        };

    /// <summary>
    /// Decides the pass after one ended with <paramref name="status"/> (the placeholder of a verdict-only pass): true with
    /// <see cref="Current"/>, <see cref="Source"/> and <see cref="Phase"/> set when another pass is to run, false when the case
    /// is finished and <see cref="Status"/> holds its status. Reached through this one method so that ILGPU compiles it once
    /// (root BOOT.md, compile size).
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool Next(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch,
                            in EquilibriumResult result, CaseStatus status, ref AttemptPlan plan)
    {
        plan.Status = status;
        if (plan.Phase == AttemptPhase.Final)
        {
            return EndFinal(result, ref plan);
        }

        if (status != CaseStatus.Ok && plan.Phase == AttemptPhase.Warm)
        {
            plan.Phase = AttemptPhase.Cold;
            plan.Source = EstimateSource.Defaults;
            plan.Current = ColdRetryProblem(plan.Current);
            return true;
        }

        if (plan.Current.Kind != ProblemKind.AssignedTemperaturePressure)
        {
            return status != CaseStatus.Ok && StartBracket(table, problem, scratch, result, ref plan);
        }

        plan.Found = EndKind.Gas;
        var needsVerdict = status != CaseStatus.Ok || plan.Phase == AttemptPhase.VerdictOnly;
        var proven = !needsVerdict || PassOutcome.Finds(table, scratch, result, ref plan);
        return !proven
            ? Unproven(table, problem, scratch, result, ref plan)
            : plan.Bracket.Active ? Probed(table, problem, scratch, result, ref plan) : EndTp(result, ref plan);
    }

    /// <summary>
    /// The cold retry takes no part of the warm attempt's own seed (Equilibrium BOOT.md, 2026-09-28): for hp and sp it starts at
    /// section 3.1's 3800 K, never at the failed warm attempt's temperature estimate, which is part of the seed the
    /// retry is discarding, since <see cref="CaseSetup.InitialTemperature"/> reads a positive <see cref="EquilibriumProblem.Temperature"/>
    /// as an estimate regardless of source. A tp's temperature is assigned, not an estimate, and is kept.
    /// </summary>
    private static EquilibriumProblem ColdRetryProblem(in EquilibriumProblem problem) =>
        problem.Kind == ProblemKind.AssignedTemperaturePressure
            ? problem
            : new EquilibriumProblem(problem.Kind, problem.Pressure, 0.0, problem.Target, problem.ElementMoles);

    /// <summary>A tp case ends: Ok, or gasless when the verdict proved it, or the failure that stands.</summary>
    private static bool EndTp(in EquilibriumResult result, ref AttemptPlan plan)
    {
        if (plan.Found == EndKind.Gasless)
        {
            plan.Status = CaseStatus.NoGasPhase;
            PassOutcome.WriteState(result, plan);
        }

        return false;
    }

    /// <summary>The final attempt's status is the case's; a state a probe wrote is cleared when it is not Ok.</summary>
    private static bool EndFinal(in EquilibriumResult result, ref AttemptPlan plan)
    {
        if (plan.Status != CaseStatus.Ok)
        {
            PassOutcome.ClearState(result);
        }

        return false;
    }

    /// <summary>
    /// A tp pass the verdict did not prove: a verdict-only pass goes on to its attempts from its seed (the final of the
    /// bracket, to the hp or sp case itself); a failed probe retreats; a failed case ends with its status.
    /// </summary>
    private static bool Unproven(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch,
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

    /// <summary>The bracket's first failure: the case's own attempts have failed, so tp probes begin at its temperature estimate.</summary>
    private static bool StartBracket(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch,
                                     in EquilibriumResult result, ref AttemptPlan plan)
    {
        plan.Bracket.Start(plan.Status, problem.Temperature);
        return LaunchProbe(table, problem, scratch, result, ref plan);
    }

    /// <summary>
    /// A probe that found a gas or a gasless minimum becomes an end of the bracket, with its moles; the bracket then asks for
    /// the next probe, a final, or gives up. The verdict-only final that held ends the case instead.
    /// </summary>
    private static bool Probed(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch,
                               in EquilibriumResult result, ref AttemptPlan plan)
    {
        if (plan.Bracket.Finishing)
        {
            return Finished(table, problem, scratch, result, ref plan);
        }

        var hp = problem.Kind == ProblemKind.AssignedEnthalpyPressure;
        var value = PassOutcome.Value(result, plan, hp);
        var slope = PassOutcome.Slope(result, plan, hp);
        var step = plan.Bracket.Record(problem.Target, value, slope, plan.Found);
        BracketSeeds.Save(table, scratch, result, plan.Bracket.LastBelow);
        var floor = plan.Bracket.NeedsFloor ? BracketSeeds.HighestFloorBelow(table, result, Math.Exp(plan.Bracket.ProbeX)) : 0.0;
        return Launch(table, problem, scratch, result, plan.Bracket.Advance(step, floor), ref plan);
    }

    /// <summary>The gasless final held: the case ends <c>NoGasPhase</c> at its temperature, with the lever mix of the ends in place of the verdict's moles when it was taken between two ends.</summary>
    private static bool Finished(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch,
                                 in EquilibriumResult result, ref AttemptPlan plan)
    {
        if (plan.Bracket.Final == BracketMove.GaslessFromLever)
        {
            BracketSeeds.Lever(table, scratch, result, plan.Bracket.LeverFraction(problem.Target));
        }

        plan.Status = CaseStatus.NoGasPhase;
        PassOutcome.WriteState(result, plan);
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
            plan.Status = plan.Bracket.GiveUpStatus;
            PassOutcome.ClearState(result);
            return false;
        }

        return move is BracketMove.AttemptFromLever or BracketMove.AttemptFromProbe
            ? LaunchFinal(table, problem, scratch, result, move, ref plan)
            : LaunchGasless(problem, move, ref plan);
    }

    /// <summary>
    /// The tp probe at the bracket's temperature: cold when no end is known; else seeded from the nearer end, and the verdict
    /// alone first when that end holds no gas.
    /// </summary>
    private static bool LaunchProbe(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch,
                                    in EquilibriumResult result, ref AttemptPlan plan)
    {
        plan.Current = new EquilibriumProblem(ProblemKind.AssignedTemperaturePressure, problem.Pressure, plan.Bracket.ProbeTemperature, 0.0, problem.ElementMoles);
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

    /// <summary>The final attempt, the case itself, warm, from the lever rule between the two ends or from the last probe; it never falls back.</summary>
    private static bool LaunchFinal(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch,
                                    in EquilibriumResult result, BracketMove move, ref AttemptPlan plan)
    {
        var temperature = Math.Exp(plan.Bracket.ProbeX);
        if (move == BracketMove.AttemptFromLever)
        {
            temperature = plan.Bracket.LeverTemperature(problem.Target);
            BracketSeeds.Lever(table, scratch, result, plan.Bracket.LeverFraction(problem.Target));
        }
        else
        {
            BracketSeeds.Seed(table, scratch, result, plan.Bracket.LastBelow);
        }

        plan.Current = new EquilibriumProblem(problem.Kind, problem.Pressure, temperature, problem.Target, problem.ElementMoles);
        plan.Source = EstimateSource.PreviousSolution;
        plan.Phase = AttemptPhase.Final;
        return true;
    }

    /// <summary>The gasless final: the verdict alone at the temperature Newton's step converged on, or at the lever temperature between two gasless ends.</summary>
    private static bool LaunchGasless(in EquilibriumProblem problem, BracketMove move, ref AttemptPlan plan)
    {
        var temperature = move == BracketMove.GaslessAtProbe ? Math.Exp(plan.Bracket.FinalX) : plan.Bracket.LeverTemperature(problem.Target);
        plan.Bracket.Finishing = true;
        plan.Bracket.Final = move;
        plan.Current = new EquilibriumProblem(ProblemKind.AssignedTemperaturePressure, problem.Pressure, temperature, 0.0, problem.ElementMoles);
        plan.Source = EstimateSource.PreviousSolution;
        plan.Phase = AttemptPhase.VerdictOnly;
        return true;
    }
}
