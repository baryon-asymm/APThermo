using APThermo.Thermo;

namespace APThermo.Equilibrium;

/// <summary>
/// The equilibrium composition of one case by minimization of the Gibbs energy: the reduced Newton–Raphson iteration of
/// NASA RP-1311 Part I (Gordon and McBride, 1994), chapters 2 and 3, with the derivatives of section 2.5. Kernel-compatible:
/// static, no allocation, no exceptions; every input, output and scratch area is a view given by the caller.
/// </summary>
/// <remarks>
/// Unknowns of the reduced system: the Lagrange multipliers π_i of the active elements, the mole-number corrections Δn_j of the
/// condensed species in the solution, Δln n (n = gaseous kmol per kg), and Δln T for hp and sp. Gaseous corrections follow
/// from equation (2.18). The control factor λ of equations (3.1)–(3.3) limits every step; the tests of (3.5) and (3.6) decide
/// convergence, after which a few further Newton steps polish the solution to machine precision. Condensed species are
/// added one at a time by the test of equation (3.7) and removed when their mole number turns negative; a record beyond
/// its effective range pairs with, or is switched for, the record of its formula on the other side of their crossing
/// (sections 3.4 and 3.5, completed by the condensed-species rule of BOOT.md: pinned pairs, the crossing T*, the switch
/// and range memories, and the anti-cycling skip of the inclusion test).
/// </remarks>
internal static class EquilibriumSolver
{
    /// <summary>
    /// −ln(1e-8): the first-stage retention threshold, in force until a case's first convergence (BOOT.md, the
    /// two-stage retention threshold, 2026-09-28; cea 3.3.4's <c>tsize</c>). The report holds whatever
    /// <see cref="RetentionThreshold"/> was active at the case's last <c>Composition.Refresh</c>, which for any
    /// <c>Ok</c> exit is always the second stage below, not this one (BOOT.md, the ⚠ 2026-09-28 correction).
    /// </summary>
    public const double TraceThreshold = 18.420681;

    /// <summary>
    /// −ln(1e-11): the second-stage retention threshold, in force for the rest of the solve after a case's first
    /// convergence (2026-09-28; cea 3.3.4's <c>xsize</c>). The threshold every <c>Ok</c> report is taken at, since
    /// the switch to this stage is itself a change of the retained set that the case must converge again under
    /// before it may exit (BOOT.md).
    /// </summary>
    public const double SecondStageTraceThreshold = 25.328436;

    /// <summary>The case's active retention threshold: the first stage until its first convergence, the second for the rest of the solve (BOOT.md, 2026-09-28).</summary>
    public static double RetentionThreshold(in IterationState state) => state.RetentionSecondStage ? SecondStageTraceThreshold : TraceThreshold;

    /// <summary>Newton steps allowed after the last change of the condensed set.</summary>
    public const int MaxNewtonSteps = 50;

    /// <summary>Changes of the condensed set allowed per case.</summary>
    public const int MaxCondensedSetChanges = 3 * ScratchLayout.MaxCondensedInSolution;

    /// <summary>The temperature window of the node: an hp or sp iterate outside it is TemperatureOutOfRange. Shared with the frozen loop.</summary>
    internal const double MinTemperature = 100.0;

    internal const double MaxTemperature = 20000.0;

    /// <summary>
    /// K: the reference's minimum gas temperature defined in thermo data (cea 3.3.4 <c>equilibrium.f90:1845</c>,
    /// applied at 1913-1914); also the first standard range bound of the committed <c>thermo.inp</c> header. A
    /// condensed record whose data begin here has no lower bound in the condensed-species rules, unless a record of
    /// its formula adjoins it below (BOOT.md, open below, 2026-09-26): in the committed file this is <c>H2O(cr)</c> alone.
    /// </summary>
    internal const double GasDataFloor = 200.0;

    /// <summary>K: an Ok state, tp included, is valid only within this window at convergence (BOOT.md, the mixture window, 2026-09-28; cea 3.3.4's `T_min`/`T_max` of the solver, `equilibrium.f90:78-80`).</summary>
    internal const double MinMixtureTemperature = 160.0;

    internal const double MaxMixtureTemperature = 22000.0;

    /// <summary>Solves the tp, hp or sp problem. With <paramref name="useMolesAsEstimate"/> the result's moles (and the problem's temperature) are the initial estimate.</summary>
    public static void Solve(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch,
                             in EquilibriumResult result, bool useMolesAsEstimate)
    {
        result.Iterations[0] = 0;
        result.Status[0] = (int)CaseStatus.InvalidInput;
        var source = useMolesAsEstimate ? EstimateSource.PreviousSolution : EstimateSource.Defaults;
        var canFallBack = useMolesAsEstimate;
        var priorIterations = 0;
        var current = problem;
        while (true)
        {
            var state = new IterationState();
            if (CaseSetup.Begin(table, current, scratch, result, source, ref state) != CaseStatus.Ok)
            {
                return;
            }

            var logPressure = CaseSetup.LogPressure(current);
            var status = RunToConvergence(table, current, scratch, result, logPressure, ref state);

            if (canFallBack && FallsBackToColdStart(status))
            {
                canFallBack = false;
                source = EstimateSource.Defaults;
                current = ColdRetryProblem(current);
                priorIterations += state.Iterations;
                continue;
            }

            state.Iterations += priorIterations;
            if (status == CaseStatus.Ok)
            {
                status = Close(table, current, scratch, result, logPressure, state);
            }

            result.Iterations[0] = state.Iterations;
            result.Status[0] = (int)status;
            return;
        }
    }

    /// <summary>
    /// The cold retry takes no part of the warm attempt's own seed (BOOT.md, 2026-09-28): for hp and sp it starts at
    /// section 3.1's 3800 K, never at the failed warm attempt's temperature estimate, which is part of the seed the
    /// retry is discarding, since <see cref="CaseSetup.InitialTemperature"/> reads a positive <see cref="EquilibriumProblem.Temperature"/>
    /// as an estimate regardless of source. A tp's temperature is assigned, not an estimate, and is kept.
    /// </summary>
    private static EquilibriumProblem ColdRetryProblem(in EquilibriumProblem problem) =>
        problem.Kind == ProblemKind.AssignedTemperaturePressure
            ? problem
            : new EquilibriumProblem(problem.Kind, problem.Pressure, 0.0, problem.Target, problem.ElementMoles);

    /// <summary>One convergence sequence: the Newton loop, then one change of the condensed set per convergence until the report's tests hold with no further change.</summary>
    private static CaseStatus RunToConvergence(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch,
                                               in EquilibriumResult result, double logPressure, ref IterationState state)
    {
        CaseStatus status;
        while (true)
        {
            status = NewtonIteration.Converge(table, problem, scratch, result, logPressure, ref state);
            if (status != CaseStatus.Ok)
            {
                break;
            }

            // One change of the condensed set per convergence; a change means converging again.
            Composition.Refresh(table, scratch, result, ref state);
            if (!CondensedSet.Update(table, problem, scratch, result, ref state))
            {
                // Rule A's release (BOOT.md, 2026-09-28): once the settled set finds no further change and some species
                // of the sums tells the tied pair apart, the tie is released, at most once per solve, and the settled
                // set converges again on the element's own row.
                if (state.Tie.Active && !state.TieReleased
                    && !ElementCoupling.Coupled(table, scratch, result, state.CondensedCount, state.Tie))
                {
                    state.Tie = default;
                    state.TieReleased = true;
                    continue;
                }

                break;
            }

            state.SetChanges++;
            if (state.SetChanges > MaxCondensedSetChanges)
            {
                status = CaseStatus.NotConverged;
                break;
            }
        }

        return status;
    }

    /// <summary>
    /// The warm-start fallback (BOOT.md, 2026-09-27, widened 2026-09-28): a warm start whose convergence fails, in any
    /// way other than <see cref="CaseStatus.InvalidInput"/> — which never reaches this point, since
    /// <see cref="CaseSetup.Begin"/> already returned by then — retries once from the cold start of section 3.1. A
    /// sign test on a seeded condensed mole cannot tell a diverged seed from a sound one (the second hidden-defect
    /// audit's finding F3): the seeded liquid can also diverge positive, or go negative and recover before the
    /// remedies or the step cap ever see it, so every failure is retried rather than only the ones a sign test catches.
    /// </summary>
    private static bool FallsBackToColdStart(CaseStatus status) => status != CaseStatus.Ok;

    /// <summary>0.8: the frozen floor factor of the reference's stop of a frozen expansion (cea 3.3.4 <c>rocket.f90:331-341</c>, BOOT.md, 2026-09-28).</summary>
    private const double FrozenFloorFactor = 0.8;

    /// <summary>
    /// With the composition fixed to the result's moles, solves for the temperature (hp, sp) or evaluates at the assigned one
    /// (tp), and writes the frozen properties: the derivatives of an ideal gas of fixed composition.
    /// </summary>
    public static void SolveFrozen(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch,
                                   in EquilibriumResult result)
    {
        result.Iterations[0] = 0;
        result.Status[0] = (int)CaseStatus.InvalidInput;
        if (table.SpeciesCount <= 0 || !(problem.Pressure > 0.0))
        {
            return;
        }

        var sumGas = 0.0;
        for (var j = 0; j < table.SpeciesCount; j++)
        {
            var moles = result.Moles[j];
            if (moles is not (>= 0.0 and not double.PositiveInfinity))
            {
                return;
            }

            if (j < table.GasCount)
            {
                sumGas += moles;
            }
        }

        if (!(sumGas > 0.0))
        {
            return;
        }

        var initialTemperature = CaseSetup.InitialTemperature(problem);
        if (initialTemperature is not (> 0.0 and < double.PositiveInfinity))
        {
            return;
        }

        var state = new IterationState { Temperature = initialTemperature, LogN = Math.Log(sumGas) };
        var logPressure = CaseSetup.LogPressure(problem);
        var status = problem.Kind == ProblemKind.AssignedTemperaturePressure
            ? CaseStatus.Ok
            : FrozenTemperature.Solve(table, problem, scratch, result, logPressure, ref state);
        if (status != CaseStatus.Ok)
        {
            Exit(result, state, status);
            return;
        }

        // The floor is 0.8 of the lowest lower bound of the fits of the gases present (cea 3.3.4 rocket.f90:331-341);
        // the mixture window's own ceiling bounds it above (BOOT.md, 2026-09-28).
        var floor = FrozenFloorFactor * LowestGasBound(table, result);
        if (state.Temperature < floor || state.Temperature > MaxMixtureTemperature)
        {
            Exit(result, state, CaseStatus.TemperatureOutOfRange);
            return;
        }

        // The species functions are wanted at the settled temperature, not at the last one the Newton step tried.
        Composition.EvaluateFunctions(table, scratch, state.Temperature);
        for (var i = 0; i < table.ElementCount; i++)
        {
            result.Multipliers[i] = 0.0;
        }

        MixtureProperties.WriteFrozen(problem, result, Composition.FrozenSums(table, scratch, result, state, logPressure));
        Exit(result, state, MixtureProperties.IsPhysical(result.State[0], pinned: false) ? CaseStatus.Ok : CaseStatus.TemperatureOutOfRange);
    }

    /// <summary>Writes the case's iteration count and final status, the last thing every exit of <see cref="SolveFrozen"/> does.</summary>
    private static void Exit(in EquilibriumResult result, in IterationState state, CaseStatus status)
    {
        result.Iterations[0] = state.Iterations;
        result.Status[0] = (int)status;
    }

    /// <summary>The lowest lower bound over the gaseous species present (a positive mole number); +∞ when none are.</summary>
    private static double LowestGasBound(in SpeciesTableView table, in EquilibriumResult result)
    {
        var low = double.PositiveInfinity;
        for (var j = 0; j < table.GasCount; j++)
        {
            if (result.Moles[j] > 0.0)
            {
                low = KernelMath.Min(low, SpeciesFunctions.RecordLow(table, j));
            }
        }

        return low;
    }

    /// <summary>
    /// The exit guards of an Ok status and the state record: the mixture's temperature window, element conservation at
    /// the node's invariant, no condensed candidate hidden by the anti-cycling rule, then the derivatives of section
    /// 2.5, the mixture properties of section 2.6 and the state guard (BOOT.md, 2026-09-28). The reported moles are
    /// exactly the composition every one of these checks was taken over: <c>Composition.Refresh</c>'s last call, at
    /// the case's own active threshold (BOOT.md, "The report's own zeroing", corrected 2026-09-28).
    /// </summary>
    private static CaseStatus Close(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch,
                                    in EquilibriumResult result, double logPressure, in IterationState state)
    {
        if (state.Temperature is < MinMixtureTemperature or > MaxMixtureTemperature)
        {
            return CaseStatus.TemperatureOutOfRange;
        }

        if (!ElementBalance.WithinInvariant(table, problem, scratch, result)
            || CondensedSet.ExitGuardFindsAPositiveCandidate(table, scratch, result, state))
        {
            return CaseStatus.NotConverged;
        }

        var sums = Composition.Sums(table, scratch, result, state, logPressure, RetentionThreshold(state));
        var derivatives = DerivativeSystem.Solve(table, scratch, result, state, ScratchLayout.MaxUnknowns(table.ElementCount));
        if (!derivatives.Solved)
        {
            return CaseStatus.SingularMatrix;
        }

        MixtureProperties.WriteEquilibrium(problem, result, sums, derivatives);
        return MixtureProperties.IsPhysical(result.State[0], derivatives.Pinned) ? CaseStatus.Ok : CaseStatus.TemperatureOutOfRange;
    }
}
