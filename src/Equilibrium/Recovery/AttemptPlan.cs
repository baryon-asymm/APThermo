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

    /// <summary>Which attempt of the case the one asked for is.</summary>
    public AttemptPhase Phase;

    /// <summary>The status the case ends with, once <see cref="Next"/> has answered false.</summary>
    public CaseStatus Status;

    /// <summary>Newton steps of every attempt so far, the number the case reports.</summary>
    public int Iterations;

    /// <summary>The plan of a case's first attempt: warm from the result's moles when <paramref name="useMolesAsEstimate"/>, else cold.</summary>
    public static AttemptPlan Start(in EquilibriumProblem problem, bool useMolesAsEstimate) =>
        new()
        {
            Current = problem,
            Source = useMolesAsEstimate ? EstimateSource.PreviousSolution : EstimateSource.Defaults,
            Phase = useMolesAsEstimate ? AttemptPhase.Warm : AttemptPhase.Cold,
        };

    /// <summary>
    /// Decides the attempt after one ended with <paramref name="status"/>: true with <see cref="Current"/>, <see cref="Source"/>
    /// and <see cref="Phase"/> set when another attempt is to run, false when the case is finished and <see cref="Status"/>
    /// holds its status. Reached through this one method so that ILGPU compiles it once (root BOOT.md, compile size).
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool Next(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch,
                            in EquilibriumResult result, CaseStatus status, ref AttemptPlan plan)
    {
        plan.Status = status;
        if (status == CaseStatus.Ok)
        {
            return false;
        }

        if (plan.Phase == AttemptPhase.Warm)
        {
            plan.Phase = AttemptPhase.Cold;
            plan.Source = EstimateSource.Defaults;
            plan.Current = ColdRetryProblem(plan.Current);
            return true;
        }

        if (problem.Kind == ProblemKind.AssignedTemperaturePressure && IsGasless(table, scratch, result, plan.Current))
        {
            plan.Status = CaseStatus.NoGasPhase;
        }

        return false;
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

    /// <summary>
    /// Whether the verdict of the gas phase proves the tp attempt that just failed to hold no gas; if so the result is the
    /// condensed minimum, its multipliers, and a state of the temperature and the pressure only (the one exception to "no
    /// status but Ok carries a state", root BOOT.md, 2026-10-03). The window of the mixture bounds the temperature, as
    /// for an Ok state.
    /// </summary>
    private static bool IsGasless(in SpeciesTableView table, in EquilibriumScratch scratch, in EquilibriumResult result,
                                  in EquilibriumProblem attempt)
    {
        if (attempt.Temperature is < EquilibriumSolver.MinMixtureTemperature or > EquilibriumSolver.MaxMixtureTemperature
            || GasPhaseVerdict.Decide(table, attempt, scratch, result, out _) != GasVerdict.Gasless)
        {
            return false;
        }

        result.State[0] = new MixtureState { Temperature = attempt.Temperature, Pressure = attempt.Pressure };
        return true;
    }
}
