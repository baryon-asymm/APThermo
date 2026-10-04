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

    /// <summary>The status of the failed attempt the verdict judged <c>GasRequired</c>, which a failed trace-gas pass leaves as the pass's own status (BOOT.md, seam (a)).</summary>
    public CaseStatus Judged;

    /// <summary>Whether <see cref="EquilibriumSolver.Solve"/> runs the iteration for the pass asked for; false for a pass that is the verdict alone.</summary>
    public readonly bool RunsAttempt => Phase != AttemptPhase.VerdictOnly;

    /// <summary>Whether <see cref="EquilibriumSolver.Solve"/> runs the trace-gas pass, not the reduced iteration, for the pass asked for.</summary>
    public readonly bool RunsTraceGas => Phase is AttemptPhase.TraceGas or AttemptPhase.TraceGasFinal;

    /// <summary>Starts the bracket at <paramref name="estimate"/> K, remembering the status of the attempt that sent the case there.</summary>
    public void BeginBracket(double estimate) => Bracket.Start(Status, estimate);

    /// <summary>
    /// Sets the plan of a case's first attempt on a plan that is <c>default</c>: warm from the result's moles when
    /// <paramref name="useMolesAsEstimate"/>, else cold. A method on the plan and not a function that returns one: the whole-struct
    /// copy of a return value is a load of every field, and ILGPU 1.5.3 emits the pair of adjacent <c>bool</c> fields of
    /// <see cref="TemperatureBracket"/> as a vector load into predicate registers, which ptxas refuses (BOOT.md, "No whole-struct copies").
    /// </summary>
    public void Begin(in EquilibriumProblem problem, bool useMolesAsEstimate)
    {
        Current = problem;
        Source = useMolesAsEstimate ? EstimateSource.PreviousSolution : EstimateSource.Defaults;
        Phase = useMolesAsEstimate ? AttemptPhase.Warm : AttemptPhase.Cold;
    }

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
            return DeadEndRecheck.EndFinal(table, problem, scratch, result, ref plan);
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
        if (plan.Phase == AttemptPhase.TraceGas)
        {
            return AfterTraceGas(table, problem, scratch, result, ref plan);
        }

        var needsVerdict = status != CaseStatus.Ok || plan.Phase == AttemptPhase.VerdictOnly;
        var proven = !needsVerdict || PassOutcome.Finds(table, scratch, result, ref plan);
        return !proven
            ? plan.Phase == AttemptPhase.TraceGas || BracketDriver.Unproven(table, problem, scratch, result, ref plan)
            : plan.Bracket.Active ? BracketDriver.Probed(table, problem, scratch, result, ref plan) : EndTp(result, ref plan);
    }

    /// <summary>
    /// Seam (a): the verdict judged the failed pass <c>GasRequired</c>. A pass that ended <c>NotConverged</c> or <c>SingularMatrix</c> is
    /// followed by one trace-gas pass at the same temperature, warm from the iterate the verdict restored, its status kept in
    /// <see cref="Judged"/>. A verdict-only pass has no failed attempt to judge, and another status (the state guard's
    /// <c>TemperatureOutOfRange</c>) stands.
    /// </summary>
    public void SeekTraceGas()
    {
        if (Phase != AttemptPhase.VerdictOnly && Status is CaseStatus.NotConverged or CaseStatus.SingularMatrix)
        {
            Judged = Status;
            Phase = AttemptPhase.TraceGas;
            Source = EstimateSource.PreviousSolution;
        }
    }

    /// <summary>
    /// A trace-gas pass ended (seam (a)): its <c>Ok</c> is the pass's outcome, of kind <see cref="EndKind.TraceGas"/> for a probe; any
    /// other status leaves the status of the attempt the verdict judged, and the case goes on as after a failed pass the verdict
    /// did not prove.
    /// </summary>
    private static bool AfterTraceGas(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch,
                                      in EquilibriumResult result, ref AttemptPlan plan)
    {
        if (plan.Status != CaseStatus.Ok)
        {
            plan.Status = plan.Judged;
            return BracketDriver.Unproven(table, problem, scratch, result, ref plan);
        }

        plan.Found = EndKind.TraceGas;
        return plan.Bracket.Active ? BracketDriver.Probed(table, problem, scratch, result, ref plan) : EndTp(result, ref plan);
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
