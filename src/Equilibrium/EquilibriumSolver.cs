using AerospacePropellantThermodynamics.Thermo;

namespace AerospacePropellantThermodynamics.Equilibrium;

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
public static class EquilibriumSolver
{
    /// <summary>−ln(1e-8): gaseous species below this mole fraction are held at zero in the sums but keep their logarithms.</summary>
    public const double TraceThreshold = 18.420681;

    /// <summary>Newton steps allowed after the last change of the condensed set.</summary>
    public const int MaxNewtonSteps = 50;

    /// <summary>Changes of the condensed set allowed per case.</summary>
    public const int MaxCondensedSetChanges = 3 * ScratchLayout.MaxCondensedInSolution;

    /// <summary>The temperature window of the node: an hp or sp iterate outside it is TemperatureOutOfRange. Shared with the frozen loop.</summary>
    internal const double MinTemperature = 100.0;

    internal const double MaxTemperature = 20000.0;

    /// <summary>Solves the tp, hp or sp problem. With <paramref name="useMolesAsEstimate"/> the result's moles (and the problem's temperature) are the initial estimate.</summary>
    public static void Solve(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch,
                             in EquilibriumResult result, bool useMolesAsEstimate)
    {
        result.Iterations[0] = 0;
        result.Status[0] = (int)CaseStatus.InvalidInput;
        var state = new IterationState();
        var source = useMolesAsEstimate ? EstimateSource.PreviousSolution : EstimateSource.Defaults;
        if (CaseSetup.Begin(table, problem, scratch, result, source, ref state) != CaseStatus.Ok)
        {
            return;
        }

        var logPressure = CaseSetup.LogPressure(problem);
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
                break;
            }

            state.SetChanges++;
            if (state.SetChanges > MaxCondensedSetChanges)
            {
                status = CaseStatus.NotConverged;
                break;
            }
        }

        if (status == CaseStatus.Ok)
        {
            status = Close(table, problem, scratch, result, logPressure, state);
        }

        result.Iterations[0] = state.Iterations;
        result.Status[0] = (int)status;
    }

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
        for (var j = 0; j < table.GasCount; j++)
        {
            if (!(result.Moles[j] >= 0.0))
            {
                return;
            }

            sumGas += result.Moles[j];
        }

        if (!(sumGas > 0.0))
        {
            return;
        }

        var state = new IterationState { Temperature = CaseSetup.InitialTemperature(problem), LogN = Math.Log(sumGas) };
        if (!(state.Temperature > 0.0))
        {
            return;
        }

        var logPressure = CaseSetup.LogPressure(problem);
        var status = problem.Kind == ProblemKind.AssignedTemperaturePressure
            ? CaseStatus.Ok
            : FrozenTemperature.Solve(table, problem, scratch, result, logPressure, ref state);
        if (status != CaseStatus.Ok)
        {
            result.Iterations[0] = state.Iterations;
            result.Status[0] = (int)status;
            return;
        }

        // The species functions are wanted at the settled temperature, not at the last one the Newton step tried.
        Composition.EvaluateFunctions(table, scratch, state.Temperature);
        for (var i = 0; i < table.ElementCount; i++)
        {
            result.Multipliers[i] = 0.0;
        }

        MixtureProperties.WriteFrozen(problem, result, Composition.FrozenSums(table, scratch, result, state, logPressure));
        result.Iterations[0] = state.Iterations;
        result.Status[0] = (int)CaseStatus.Ok;
    }

    /// <summary>
    /// The exit guards of an Ok status and the state record: element conservation at the node's invariant, no condensed
    /// candidate hidden by the anti-cycling rule, then the derivatives of section 2.6 and the mixture properties.
    /// </summary>
    private static CaseStatus Close(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch,
                                    in EquilibriumResult result, double logPressure, in IterationState state)
    {
        if (!ElementBalance.WithinInvariant(table, problem, scratch, result)
            || CondensedSet.StoodDownCandidateRemains(table, scratch, result, state))
        {
            return CaseStatus.NotConverged;
        }

        var sums = Composition.Sums(table, scratch, result, state.LogN, logPressure, state.Temperature);
        var derivatives = DerivativeSystem.Solve(table, scratch, result, state.CondensedCount,
                                                 ScratchLayout.MaxUnknowns(table.ElementCount));
        if (!derivatives.Solved)
        {
            return CaseStatus.SingularMatrix;
        }

        MixtureProperties.WriteEquilibrium(problem, result, sums, derivatives);
        return CaseStatus.Ok;
    }
}
