using AerospacePropellantThermodynamics.Thermo;
using ILGPU;

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

    /// <summary>−ln(1e-4), the bound of equation (3.2) on the growth of a small species in one step.</summary>
    private const double SmallSpeciesBound = 9.2103404;

    /// <summary>Newton steps allowed after the last change of the condensed set.</summary>
    public const int MaxNewtonSteps = 50;

    /// <summary>Changes of the condensed set allowed per case.</summary>
    public const int MaxCondensedSetChanges = 3 * ScratchLayout.MaxCondensedInSolution;

    private const double CorrectionTest = 0.5e-5;      // equation (3.5)
    private const double TemperatureTest = 1.0e-4;     // equation (3.6b)
    private const double PolishTest = 1.0e-11;         // the steps after the report's tests, until the corrections are this small
    private const int MaxPolishSteps = 6;
    private const double DefaultTemperatureEstimate = 3800.0;
    /// <summary>The temperature window of the node: an hp or sp iterate outside it is TemperatureOutOfRange. Shared with the frozen loop.</summary>
    internal const double MinTemperature = 100.0;

    internal const double MaxTemperature = 20000.0;
    private const double StandardPressure = 1.0e5;    // Pa; the thermodynamic data are for 1 bar
    private const double ResetMoles = 1.0e-6;          // section 3.6: the reset of vanished species on a singular matrix
    private const int MaxSingularResets = 2;
    private const double FrozenTemperatureTest = 1.0e-10; // relative, on the Newton step of the frozen temperature

    /// <summary>Solves the tp, hp or sp problem. With <paramref name="useMolesAsEstimate"/> the result's moles (and the problem's temperature) are the initial estimate.</summary>
    public static void Solve(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch,
                             in EquilibriumResult result, bool useMolesAsEstimate)
    {
        var speciesCount = table.SpeciesCount;
        var gasCount = table.GasCount;
        var elementCount = table.ElementCount;
        result.Iterations[0] = 0;
        result.Status[0] = (int)CaseStatus.InvalidInput;

        if (speciesCount <= 0 || elementCount <= 0 || !(problem.Pressure > 0.0) || elementCount > TableLimits.MaxElements)
        {
            return;
        }

        if (problem.Kind == ProblemKind.AssignedTemperaturePressure && !(problem.Temperature > 0.0))
        {
            return;
        }

        // Element and species masks: an absent element removes every species that contains it.
        var anyElement = false;
        for (var i = 0; i < elementCount; i++)
        {
            var b = problem.ElementMoles[i];
            if (!(b >= 0.0))
            {
                return;
            }

            scratch.ElementActive[i] = b > 0.0 ? 1 : 0;
            anyElement |= b > 0.0;
        }

        if (!anyElement)
        {
            return;
        }

        var activeGases = 0;
        for (var j = 0; j < speciesCount; j++)
        {
            var present = true;
            for (var i = 0; i < elementCount; i++)
            {
                if (table.Stoichiometry[i * speciesCount + j] != 0.0 && scratch.ElementActive[i] == 0)
                {
                    present = false;
                }
            }

            CaseSetup.Mark(scratch, j, present ? SpeciesMark.Active : SpeciesMark.Absent);
            if (present && j < gasCount)
            {
                activeGases++;
            }
        }

        if (activeGases == 0)
        {
            return;
        }

        var isTp = problem.Kind == ProblemKind.AssignedTemperaturePressure;
        var isHp = problem.Kind == ProblemKind.AssignedEnthalpyPressure;
        var logPressure = Math.Log(problem.Pressure / StandardPressure);

        // Initial estimates (section 3.1) or the caller's.
        var state = new IterationState
        {
            Temperature = isTp ? problem.Temperature : (problem.Temperature > 0.0 ? problem.Temperature : DefaultTemperatureEstimate),
            FunctionsAt = -1.0,
            LastSwitchedOut = -1,
            LastRemovedForRange = -1,
        };
        for (var c = 0; c < ScratchLayout.MaxCondensedInSolution; c++)
        {
            scratch.CondensedInSolution[c] = -1;
        }

        if (useMolesAsEstimate)
        {
            var estimate = 0.0;
            for (var j = 0; j < gasCount; j++)
            {
                if (scratch.SpeciesActive[j] == 1 && result.Moles[j] > 0.0)
                {
                    estimate += result.Moles[j];
                }
            }

            if (!(estimate > 0.0))
            {
                estimate = 0.1;
            }

            state.LogN = Math.Log(estimate);
            for (var j = 0; j < gasCount; j++)
            {
                scratch.LogMoles[j] = scratch.SpeciesActive[j] == 1 && result.Moles[j] > 0.0
                    ? Math.Log(result.Moles[j])
                    : state.LogN - TraceThreshold - 1.0;
            }

            for (var j = gasCount; j < speciesCount; j++)
            {
                if (scratch.SpeciesActive[j] == 1 && result.Moles[j] > 0.0 && state.CondensedCount < ScratchLayout.MaxCondensedInSolution)
                {
                    scratch.CondensedInSolution[state.CondensedCount++] = j;
                }
                else
                {
                    result.Moles[j] = 0.0;
                }
            }
        }
        else
        {
            state.LogN = Math.Log(0.1);
            var each = Math.Log(0.1 / activeGases);
            for (var j = 0; j < gasCount; j++)
            {
                scratch.LogMoles[j] = each;
            }

            for (var j = gasCount; j < speciesCount; j++)
            {
                result.Moles[j] = 0.0;
            }
        }

        for (var i = 0; i < elementCount; i++)
        {
            result.Multipliers[i] = 0.0;
        }

        var unknownStride = ScratchLayout.MaxUnknowns(elementCount);
        var status = CaseStatus.NotConverged;

        while (true)
        {
            var converged = false;
            var polishSteps = 0;
            var singularResets = 0;
            var steps = 0;
            while (steps < MaxNewtonSteps + MaxPolishSteps)
            {
                if (state.FunctionsAt != state.Temperature)
                {
                    Composition.EvaluateFunctions(table, scratch, state.Temperature);
                    state.FunctionsAt = state.Temperature;
                }

                // Gaseous moles retained in the sums (section 3.2).
                var sumGas = 0.0;
                for (var j = 0; j < gasCount; j++)
                {
                    var retained = scratch.SpeciesActive[j] == 1 && scratch.LogMoles[j] - state.LogN > -TraceThreshold;
                    result.Moles[j] = retained ? Math.Exp(scratch.LogMoles[j]) : 0.0;
                    sumGas += result.Moles[j];
                }

                var sums = new MixtureSums { LogN = state.LogN, LogPressure = logPressure, Temperature = state.Temperature, N = Math.Exp(state.LogN), SumGas = sumGas };
                for (var j = 0; j < speciesCount; j++)
                {
                    var nj = result.Moles[j];
                    if (nj == 0.0)
                    {
                        continue;
                    }

                    sums.HOverRT += nj * scratch.HOverRT[j];
                    sums.SOverR += j < gasCount
                        ? nj * (scratch.SOverR[j] - scratch.LogMoles[j] + state.LogN - logPressure)
                        : nj * scratch.SOverR[j];
                    sums.CpOverR += nj * scratch.CpOverR[j];
                    if (j >= gasCount)
                    {
                        sums.CondensedMoles += nj;
                    }
                }

                var layout = new SystemLayout(problem.Kind, elementCount, state.CondensedCount, unknownStride);
                IterationMatrix.Assemble(table, problem, scratch, result, layout, sums);

                var solved = DenseSolver.Solve(scratch.Matrix, scratch.RightHandSide, scratch.RowScale, layout.Unknowns, layout.Stride);
                if (!solved)
                {
                    // Section 3.6: reset the vanished gaseous species, then drop the last condensed species, then give up.
                    if (singularResets < MaxSingularResets)
                    {
                        singularResets++;
                        for (var j = 0; j < gasCount; j++)
                        {
                            if (scratch.SpeciesActive[j] == 1 && result.Moles[j] == 0.0)
                            {
                                scratch.LogMoles[j] = Math.Log(ResetMoles);
                            }
                        }

                        continue;
                    }

                    if (state.CondensedCount > 0)
                    {
                        state.CondensedCount--;
                        result.Moles[scratch.CondensedInSolution[state.CondensedCount]] = 0.0;
                        scratch.CondensedInSolution[state.CondensedCount] = -1;
                        state.SetChanges++;
                        singularResets = 0;
                        continue;
                    }

                    status = CaseStatus.SingularMatrix;
                    goto Finish;
                }

                steps++;
                state.Iterations++;
                for (var i = 0; i < elementCount; i++)
                {
                    result.Multipliers[i] = scratch.RightHandSide[i];
                }

                var deltaLogN = scratch.RightHandSide[layout.NRow];
                var deltaLogT = isTp ? 0.0 : scratch.RightHandSide[layout.TRow];

                // Corrections of the gaseous species, equation (2.18), and the control factor λ, equations (3.1)–(3.3).
                var largest = Math.Max(5.0 * Math.Abs(deltaLogT), 5.0 * Math.Abs(deltaLogN));
                var lambda2 = double.MaxValue;
                for (var j = 0; j < gasCount; j++)
                {
                    if (scratch.SpeciesActive[j] == 0)
                    {
                        scratch.Corrections[j] = 0.0;
                        continue;
                    }

                    var sum = deltaLogN + scratch.HOverRT[j] * deltaLogT;
                    for (var i = 0; i < elementCount; i++)
                    {
                        sum += table.Stoichiometry[i * speciesCount + j] * result.Multipliers[i];
                    }

                    var mu = scratch.GOverRT[j] + scratch.LogMoles[j] - state.LogN + logPressure;
                    var delta = sum - mu;
                    scratch.Corrections[j] = delta;
                    // Only growth is limited, as in CEA: a species on its way out may shrink by any factor in one step.
                    var logFraction = scratch.LogMoles[j] - state.LogN;
                    if (delta <= 0.0)
                    {
                        continue;
                    }

                    if (logFraction > -TraceThreshold)
                    {
                        largest = Math.Max(largest, delta);
                    }
                    else if (delta - deltaLogN > 0.0)
                    {
                        lambda2 = Math.Min(lambda2, (-logFraction - SmallSpeciesBound) / (delta - deltaLogN));
                    }
                }

                var lambda = 1.0;
                if (largest > 0.0)
                {
                    lambda = Math.Min(lambda, 2.0 / largest);
                }

                lambda = Math.Min(lambda, lambda2);

                // Apply the corrections, equation (3.4).
                for (var j = 0; j < gasCount; j++)
                {
                    if (scratch.SpeciesActive[j] == 1)
                    {
                        scratch.LogMoles[j] += lambda * scratch.Corrections[j];
                    }
                }

                for (var c = 0; c < state.CondensedCount; c++)
                {
                    result.Moles[scratch.CondensedInSolution[c]] += lambda * scratch.RightHandSide[elementCount + c];
                }

                state.LogN += lambda * deltaLogN;
                if (!isTp)
                {
                    state.Temperature = Math.Exp(Math.Log(state.Temperature) + lambda * deltaLogT);
                    if (!(state.Temperature >= MinTemperature) || !(state.Temperature <= MaxTemperature))
                    {
                        status = CaseStatus.TemperatureOutOfRange;
                        goto Finish;
                    }
                }

                // Convergence tests, equations (3.5) and (3.6), on the undamped corrections.
                var total = sumGas;
                for (var c = 0; c < state.CondensedCount; c++)
                {
                    total += result.Moles[scratch.CondensedInSolution[c]];
                }

                var worst = sums.N * Math.Abs(deltaLogN) / total;
                for (var j = 0; j < gasCount; j++)
                {
                    if (result.Moles[j] > 0.0)
                    {
                        worst = Math.Max(worst, result.Moles[j] * Math.Abs(scratch.Corrections[j]) / total);
                    }
                }

                for (var c = 0; c < state.CondensedCount; c++)
                {
                    worst = Math.Max(worst, Math.Abs(scratch.RightHandSide[elementCount + c]) / total);
                }

                var balanced = ElementBalance.WithinReportTest(table, problem, scratch, result);
                var reportConverged = worst <= CorrectionTest && balanced && (isTp || Math.Abs(deltaLogT) <= TemperatureTest);
                if (reportConverged)
                {
                    // The report's tests passed; a few more steps bring the corrections to rounding level.
                    converged = true;
                    var polished = worst <= PolishTest && (isTp || Math.Abs(deltaLogT) <= PolishTest);
                    if (polished || polishSteps >= MaxPolishSteps)
                    {
                        break;
                    }

                    polishSteps++;
                }
                else if (steps >= MaxNewtonSteps)
                {
                    break;
                }
            }

            if (!converged)
            {
                status = CaseStatus.NotConverged;
                goto Finish;
            }

            // The final iterate: the functions at the final state.Temperature and the retained mole numbers.
            if (state.FunctionsAt != state.Temperature)
            {
                Composition.EvaluateFunctions(table, scratch, state.Temperature);
                state.FunctionsAt = state.Temperature;
            }

            for (var j = 0; j < gasCount; j++)
            {
                var retained = scratch.SpeciesActive[j] == 1 && scratch.LogMoles[j] - state.LogN > -TraceThreshold;
                result.Moles[j] = retained ? Math.Exp(scratch.LogMoles[j]) : 0.0;
            }

            // Condensed species: at most one change of the set per convergence (sections 3.4 and 3.5, and the
            // condensed-species rule of BOOT.md), after which the case is converged again.
            var changed = CondensedSet.Update(table, problem, scratch, result, ref state);
            if (!changed)
            {
                status = CaseStatus.Ok;
                break;
            }

            state.SetChanges++;
            if (state.SetChanges > MaxCondensedSetChanges)
            {
                status = CaseStatus.NotConverged;
                break;
            }
        }

        if (status == CaseStatus.Ok && !ElementBalance.WithinInvariant(table, problem, scratch, result))
        {
            status = CaseStatus.NotConverged;
        }

        if (status == CaseStatus.Ok && CondensedSet.StoodDownCandidateRemains(table, scratch, result, state))
        {
            status = CaseStatus.NotConverged;
        }

        if (status == CaseStatus.Ok)
        {
            status = FinishState(table, problem, scratch, result, state.CondensedCount, state.LogN, logPressure, state.Temperature, unknownStride);
        }

    Finish:
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

        var isTp = problem.Kind == ProblemKind.AssignedTemperaturePressure;
        var temperature = isTp ? problem.Temperature : (problem.Temperature > 0.0 ? problem.Temperature : DefaultTemperatureEstimate);
        if (!(temperature > 0.0))
        {
            return;
        }

        var state = new IterationState { Temperature = temperature, LogN = Math.Log(sumGas) };
        var logPressure = Math.Log(problem.Pressure / StandardPressure);
        var status = isTp ? CaseStatus.Ok : FrozenTemperature.Solve(table, problem, scratch, result, logPressure, ref state);
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

    /// <summary>The mixture properties and the equilibrium derivatives (RP-1311 sections 2.5 and 2.6) at the converged composition.</summary>
    private static CaseStatus FinishState(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch,
                                          in EquilibriumResult result, int condensedCount, double logN, double logPressure,
                                          double temperature, int stride)
    {
        var gasCount = table.GasCount;
        var sums = new MixtureSums { LogN = logN, LogPressure = logPressure, Temperature = temperature };
        for (var j = 0; j < table.SpeciesCount; j++)
        {
            var nj = result.Moles[j];
            if (nj == 0.0)
            {
                continue;
            }

            sums.HOverRT += nj * scratch.HOverRT[j];
            sums.CpOverR += nj * scratch.CpOverR[j];
            if (j < gasCount)
            {
                sums.SumGas += nj;
                sums.SOverR += nj * (scratch.SOverR[j] - scratch.LogMoles[j] + logN - logPressure);
            }
            else
            {
                sums.SOverR += nj * scratch.SOverR[j];
                sums.CondensedMoles += nj;
            }
        }

        var derivatives = DerivativeSystem.Solve(table, scratch, result, condensedCount, stride);
        if (!derivatives.Solved)
        {
            return CaseStatus.SingularMatrix;
        }

        MixtureProperties.WriteEquilibrium(problem, result, sums, derivatives);
        return CaseStatus.Ok;
    }
}
