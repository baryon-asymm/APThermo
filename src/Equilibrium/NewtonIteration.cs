using AerospacePropellantThermodynamics.Thermo;

namespace AerospacePropellantThermodynamics.Equilibrium;

/// <summary>
/// The damped Newton–Raphson iteration of RP-1311 chapter 3 on the reduced system, run until the report's tests pass and a
/// few polish steps have brought the corrections to rounding level. Kernel-compatible.
/// </summary>
/// <remarks>
/// One step: assemble and solve the system, apply the damped step of <see cref="DampedStep"/>, and read the verdict of
/// <see cref="ConvergenceTests"/>. A singular system is met with the remedies of <see cref="SingularRemedies"/> before the
/// case is given up. This class holds no formula of its own (BOOT.md, ## Structure, "The Newton loop holds no formula"):
/// it keeps only the step and polish counts, the order of the calls, and the status.
/// </remarks>
internal static class NewtonIteration
{
    /// <summary>The steps after the report's tests run until the corrections are this small: the rounding floor of the linear solves.</summary>
    private const int MaxPolishSteps = 6;

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
                if (SingularRemedies.Recover(scratch, result, table.GasCount, ref singularResets, ref state))
                {
                    continue;
                }

                return CaseStatus.SingularMatrix;
            }

            steps++;
            state.Iterations++;
            var lambda = DampedStep.ControlFactor(table, scratch, result, layout, sums);
            if (!DampedStep.Apply(table, scratch, result, layout, lambda, ref state))
            {
                return CaseStatus.TemperatureOutOfRange;
            }

            var verdict = ConvergenceTests.Evaluate(table, problem, scratch, result, layout, sums);
            if (verdict == ConvergenceVerdict.NotConverged)
            {
                if (steps >= EquilibriumSolver.MaxNewtonSteps)
                {
                    break;
                }

                continue;
            }

            // The report's tests passed; a few more steps bring the corrections to rounding level.
            converged = true;
            if (verdict == ConvergenceVerdict.Polished || polishSteps >= MaxPolishSteps)
            {
                break;
            }

            polishSteps++;
        }

        return converged ? CaseStatus.Ok : CaseStatus.NotConverged;
    }
}
