using AerospacePropellantThermodynamics.Thermo;

namespace AerospacePropellantThermodynamics.Equilibrium;

/// <summary>
/// Newton on the temperature at a fixed composition: the temperature at which the frozen mixture carries the assigned
/// enthalpy (hp) or entropy (sp). Kernel-compatible.
/// </summary>
/// <remarks>
/// The step is limited to a fraction of the current temperature, as the reference's frozen loop limits it, so that a poor
/// first estimate cannot throw the iterate out of the fitted range in one move. The cap and the test are this loop's own:
/// they happen to equal the Gibbs iteration's today, and tuning one must not retune the other.
/// </remarks>
internal static class FrozenTemperature
{
    /// <summary>Relative test on the Newton step: the temperature is settled when the step falls below this fraction of it.</summary>
    private const double Test = 1.0e-10;

    /// <summary>The largest step, as a fraction of the current temperature.</summary>
    private const double StepLimit = 0.4;

    /// <summary>Steps allowed; equal to the Gibbs iteration's cap today, but this loop's own number.</summary>
    private const int MaxSteps = 50;

    /// <summary>
    /// Moves <c>state.Temperature</c> onto the assigned target and counts the steps in <c>state.Iterations</c>. The species
    /// functions in the scratch are left at the last temperature tried, which is not the converged one; the caller
    /// evaluates them again before reading the state.
    /// </summary>
    public static CaseStatus Solve(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch,
                                   in EquilibriumResult result, double logPressure, ref IterationState state)
    {
        // h0/R in K·kmol/kg for hp and s0/R in kmol/kg for sp: the units differ with the problem kind, the scaling does not.
        var target = problem.Target / PhysicalConstants.R;
        for (var step = 0; step < MaxSteps; step++)
        {
            Composition.EvaluateFunctions(table, scratch, state.Temperature);
            var value = Value(table, problem, scratch, result, state, logPressure);
            var slope = Slope(table, problem, scratch, result, state.Temperature);
            state.Iterations++;
            var deltaT = -(value - target) / slope;
            if (Math.Abs(deltaT) > StepLimit * state.Temperature)
            {
                deltaT = StepLimit * state.Temperature * (deltaT > 0.0 ? 1.0 : -1.0);
            }

            state.Temperature += deltaT;
            if (!(state.Temperature >= EquilibriumSolver.MinTemperature) || !(state.Temperature <= EquilibriumSolver.MaxTemperature))
            {
                return CaseStatus.TemperatureOutOfRange;
            }

            if (Math.Abs(deltaT) <= Test * state.Temperature)
            {
                return CaseStatus.Ok;
            }
        }

        return CaseStatus.NotConverged;
    }

    /// <summary>The mixture's enthalpy over R (in K·kmol/kg) or its entropy over R (in kmol/kg), whichever is assigned.</summary>
    private static double Value(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch,
                                in EquilibriumResult result, in IterationState state, double logPressure)
    {
        var gasCount = table.GasCount;
        var enthalpy = problem.Kind == ProblemKind.AssignedEnthalpyPressure;
        var value = 0.0;
        for (var j = 0; j < table.SpeciesCount; j++)
        {
            var nj = result.Moles[j];
            if (nj == 0.0)
            {
                continue;
            }

            if (enthalpy)
            {
                value += nj * scratch.HOverRT[j] * state.Temperature;
            }
            else
            {
                value += j < gasCount
                    ? nj * (scratch.SOverR[j] - Math.Log(nj) + state.LogN - logPressure)
                    : nj * scratch.SOverR[j];
            }
        }

        return value;
    }

    /// <summary>d(value)/dT: the frozen heat capacity over R, divided by the temperature for the entropy.</summary>
    private static double Slope(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch,
                                in EquilibriumResult result, double temperature)
    {
        var enthalpy = problem.Kind == ProblemKind.AssignedEnthalpyPressure;
        var slope = 0.0;
        for (var j = 0; j < table.SpeciesCount; j++)
        {
            var nj = result.Moles[j];
            if (nj == 0.0)
            {
                continue;
            }

            slope += enthalpy ? nj * scratch.CpOverR[j] : nj * scratch.CpOverR[j] / temperature;
        }

        return slope;
    }
}
