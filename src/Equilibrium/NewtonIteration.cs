using AerospacePropellantThermodynamics.Thermo;

namespace AerospacePropellantThermodynamics.Equilibrium;

/// <summary>
/// The damped Newton–Raphson iteration of RP-1311 chapter 3 on the reduced system, run until the report's tests pass and a
/// few polish steps have brought the corrections to rounding level. Kernel-compatible.
/// </summary>
/// <remarks>
/// One step: assemble and solve the system, read the multipliers, form the gaseous corrections of equation (2.18), damp
/// every correction by the control factor λ of equations (3.1)–(3.3), apply them by equation (3.4) and test (3.5) and (3.6).
/// A singular system is met with the remedies of section 3.6 before the case is given up.
/// </remarks>
internal static class NewtonIteration
{
    /// <summary>Equation (3.1): the weight of Δln n and Δln T in the largest correction, and the numerator of the factor.</summary>
    private const double ControlFactorWeight = 5.0;

    private const double ControlFactorLimit = 2.0;

    /// <summary>−ln(1e-4), the bound of equation (3.2) on the growth of a small species in one step.</summary>
    private const double SmallSpeciesBound = 9.2103404;

    /// <summary>Equation (3.5), on the mole-number corrections weighted by their share of the mixture.</summary>
    private const double CorrectionTest = 0.5e-5;

    /// <summary>Equation (3.6b), on Δln T.</summary>
    private const double TemperatureTest = 1.0e-4;

    /// <summary>The steps after the report's tests run until the corrections are this small: the rounding floor of the linear solves.</summary>
    private const double PolishTest = 1.0e-11;

    private const int MaxPolishSteps = 6;

    /// <summary>Section 3.6: the mole number a vanished gaseous species is reset to when the matrix comes out singular.</summary>
    private const double ResetMoles = 1.0e-6;

    private const int MaxSingularResets = 2;

    /// <summary>
    /// Converges the current condensed set. Ok when the report's tests passed, NotConverged when the step cap ran out, and
    /// SingularMatrix or TemperatureOutOfRange where the iterate left what the node can solve.
    /// </summary>
    public static CaseStatus Converge(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch,
                                      in EquilibriumResult result, double logPressure, ref IterationState state)
    {
        var stride = ScratchLayout.MaxUnknowns(table.ElementCount);
        var converged = false;
        var polishSteps = 0;
        var singularResets = 0;
        var steps = 0;
        while (steps < EquilibriumSolver.MaxNewtonSteps + MaxPolishSteps)
        {
            Composition.Evaluate(table, scratch, ref state);
            var sums = Composition.Sums(table, scratch, result, state.LogN, logPressure, state.Temperature);
            var layout = new SystemLayout(problem.Kind, table.ElementCount, state.CondensedCount, stride);
            IterationMatrix.Assemble(table, problem, scratch, result, layout, sums);
            if (!DenseSolver.Solve(scratch.Matrix, scratch.RightHandSide, scratch.RowScale, layout.Unknowns, layout.Stride))
            {
                if (Recover(scratch, result, table.GasCount, ref singularResets, ref state))
                {
                    continue;
                }

                return CaseStatus.SingularMatrix;
            }

            steps++;
            state.Iterations++;
            if (!Apply(table, scratch, result, layout, ControlFactor(table, scratch, result, layout, sums), ref state))
            {
                return CaseStatus.TemperatureOutOfRange;
            }

            var worst = Worst(table, scratch, result, layout, sums);
            var deltaLogT = layout.IsTp ? 0.0 : scratch.RightHandSide[layout.TRow];
            var balanced = ElementBalance.WithinReportTest(table, problem, scratch, result);
            if (!(worst <= CorrectionTest && balanced && (layout.IsTp || Math.Abs(deltaLogT) <= TemperatureTest)))
            {
                if (steps >= EquilibriumSolver.MaxNewtonSteps)
                {
                    break;
                }

                continue;
            }

            // The report's tests passed; a few more steps bring the corrections to rounding level.
            converged = true;
            if ((worst <= PolishTest && (layout.IsTp || Math.Abs(deltaLogT) <= PolishTest)) || polishSteps >= MaxPolishSteps)
            {
                break;
            }

            polishSteps++;
        }

        return converged ? CaseStatus.Ok : CaseStatus.NotConverged;
    }

    /// <summary>
    /// The multipliers of this step, the gaseous corrections of equation (2.18) into the scratch, and the control factor λ
    /// of equations (3.1)–(3.3) that damps them all. Only growth is limited, as in the reference's code: a species on its
    /// way out may shrink by any factor in one step (BOOT.md).
    /// </summary>
    private static double ControlFactor(in SpeciesTableView table, in EquilibriumScratch scratch, in EquilibriumResult result,
                                        in SystemLayout layout, in MixtureSums sums)
    {
        var speciesCount = table.SpeciesCount;
        var elementCount = layout.ElementCount;
        for (var i = 0; i < elementCount; i++)
        {
            result.Multipliers[i] = scratch.RightHandSide[i];
        }

        var deltaLogN = scratch.RightHandSide[layout.NRow];
        var deltaLogT = layout.IsTp ? 0.0 : scratch.RightHandSide[layout.TRow];
        var largest = Math.Max(ControlFactorWeight * Math.Abs(deltaLogT), ControlFactorWeight * Math.Abs(deltaLogN));
        var lambda2 = double.MaxValue;
        for (var j = 0; j < table.GasCount; j++)
        {
            if (!CaseSetup.InPlay(scratch, j))
            {
                scratch.Corrections[j] = 0.0;
                continue;
            }

            var sum = deltaLogN + scratch.HOverRT[j] * deltaLogT;
            for (var i = 0; i < elementCount; i++)
            {
                sum += table.Stoichiometry[i * speciesCount + j] * result.Multipliers[i];
            }

            var mu = scratch.GOverRT[j] + scratch.LogMoles[j] - sums.LogN + sums.LogPressure;
            var delta = sum - mu;
            scratch.Corrections[j] = delta;
            var logFraction = scratch.LogMoles[j] - sums.LogN;
            if (delta <= 0.0)
            {
                continue;
            }

            if (logFraction > -EquilibriumSolver.TraceThreshold)
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
            lambda = Math.Min(lambda, ControlFactorLimit / largest);
        }

        return Math.Min(lambda, lambda2);
    }

    /// <summary>Equation (3.4): the damped corrections onto the iterate. False when the new temperature left the node's window.</summary>
    private static bool Apply(in SpeciesTableView table, in EquilibriumScratch scratch, in EquilibriumResult result,
                              in SystemLayout layout, double lambda, ref IterationState state)
    {
        for (var j = 0; j < table.GasCount; j++)
        {
            if (CaseSetup.InPlay(scratch, j))
            {
                scratch.LogMoles[j] += lambda * scratch.Corrections[j];
            }
        }

        for (var c = 0; c < state.CondensedCount; c++)
        {
            result.Moles[scratch.CondensedInSolution[c]] += lambda * scratch.RightHandSide[layout.ElementCount + c];
        }

        state.LogN += lambda * scratch.RightHandSide[layout.NRow];
        if (layout.IsTp)
        {
            return true;
        }

        state.Temperature = Math.Exp(Math.Log(state.Temperature) + lambda * scratch.RightHandSide[layout.TRow]);
        return state.Temperature >= EquilibriumSolver.MinTemperature && state.Temperature <= EquilibriumSolver.MaxTemperature;
    }

    /// <summary>Equation (3.5) on the undamped corrections: the largest mole-number correction as a share of the whole mixture.</summary>
    private static double Worst(in SpeciesTableView table, in EquilibriumScratch scratch, in EquilibriumResult result,
                                in SystemLayout layout, in MixtureSums sums)
    {
        var total = sums.SumGas;
        for (var c = 0; c < layout.CondensedCount; c++)
        {
            total += result.Moles[scratch.CondensedInSolution[c]];
        }

        var worst = sums.N * Math.Abs(scratch.RightHandSide[layout.NRow]) / total;
        for (var j = 0; j < table.GasCount; j++)
        {
            if (result.Moles[j] > 0.0)
            {
                worst = Math.Max(worst, result.Moles[j] * Math.Abs(scratch.Corrections[j]) / total);
            }
        }

        for (var c = 0; c < layout.CondensedCount; c++)
        {
            worst = Math.Max(worst, Math.Abs(scratch.RightHandSide[layout.ElementCount + c]) / total);
        }

        return worst;
    }

    /// <summary>
    /// The remedies of section 3.6, in order: reset the gaseous species that vanished, twice; then drop the last condensed
    /// species. False when neither is left and the case is singular.
    /// </summary>
    private static bool Recover(in EquilibriumScratch scratch, in EquilibriumResult result, int gasCount,
                                ref int singularResets, ref IterationState state)
    {
        if (singularResets < MaxSingularResets)
        {
            singularResets++;
            for (var j = 0; j < gasCount; j++)
            {
                if (CaseSetup.InPlay(scratch, j) && result.Moles[j] == 0.0)
                {
                    scratch.LogMoles[j] = Math.Log(ResetMoles);
                }
            }

            return true;
        }

        if (state.CondensedCount > 0)
        {
            state.CondensedCount = CondensedSet.Remove(scratch, result, state.CondensedCount, state.CondensedCount - 1);
            state.SetChanges++;
            singularResets = 0;
            return true;
        }

        return false;
    }
}
