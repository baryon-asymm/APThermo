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

    /// <summary>What the recheck of an Ok state below a dead-end floor keeps of the attempt it may replace.</summary>
    public RecheckState Recheck;

    /// <summary>Whether <see cref="EquilibriumSolver.Solve"/> runs the iteration for the pass asked for; false for a pass that is the verdict alone.</summary>
    public readonly bool RunsAttempt => Phase != AttemptPhase.VerdictOnly;

    /// <summary>Starts the bracket at <paramref name="estimate"/> K, remembering the status of the attempt that sent the case there.</summary>
    public void BeginBracket(double estimate) => Bracket.Start(Status, estimate);

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
            return DeadEndRecheck.EndFinal(result, ref plan);
        }

        if (status != CaseStatus.Ok && plan.Phase == AttemptPhase.Warm)
        {
            plan.Phase = AttemptPhase.Cold;
            plan.Source = EstimateSource.Defaults;
            plan.Current = PassOutcome.ColdRetry(plan.Current);
            return true;
        }

        if (!PassOutcome.IsTp(plan.Current))
        {
            return status == CaseStatus.Ok
                ? DeadEndRecheck.AfterOk(table, problem, scratch, result, ref plan)
                : BracketDriver.Begin(table, problem, scratch, result, ref plan);
        }

        plan.Found = EndKind.Gas;
        var needsVerdict = status != CaseStatus.Ok || plan.Phase == AttemptPhase.VerdictOnly;
        var proven = !needsVerdict || PassOutcome.Finds(table, scratch, result, ref plan);
        return !proven
            ? BracketDriver.Unproven(table, problem, scratch, result, ref plan)
            : plan.Bracket.Active ? BracketDriver.Probed(table, problem, scratch, result, ref plan) : EndTp(result, ref plan);
    }

    /// <summary>A tp case ends: Ok, or gasless when the verdict proved it, or the failure that stands.</summary>
    private static bool EndTp(in EquilibriumResult result, ref AttemptPlan plan)
    {
        if (plan.Found == EndKind.Gasless)
        {
            PassOutcome.EndGasless(result, ref plan);
        }

        return false;
    }
}
