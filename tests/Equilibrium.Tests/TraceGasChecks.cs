using APThermo.Thermo;

namespace APThermo.Equilibrium.Tests;

/// <summary>The assertions the trace-gas facts share: a state is <c>Ok</c> and clear of every condition, every reported gas on its stationarity included.</summary>
internal static class TraceGasChecks
{
    /// <summary>
    /// The equilibrium heat capacity of <paramref name="solution"/>, an <c>Ok</c> state of <paramref name="state"/> or of its mixture, equals
    /// the central difference of the solver's own tp enthalpies at its temperature ± 0.01 K within <see cref="Tolerances.FiniteDifference"/>.
    /// </summary>
    public static void AssertHeatCapacityMatchesTheCentralDifference(TraceGasCase state, HostSolution solution, string label)
    {
        const double step = 0.01;
        var temperature = solution.State.Temperature;
        var above = state.At(temperature + step).Solve();
        var below = state.At(temperature - step).Solve();
        Assert.True(above.Status == CaseStatus.Ok && below.Status == CaseStatus.Ok, $"{label}: the neighbours of {temperature:R} K end {above.Status} and {below.Status}");
        var difference = (above.State.Enthalpy - below.State.Enthalpy) / (2.0 * step);
        Assert.True(
            Math.Abs(solution.State.CpEquilibrium / difference - 1.0) <= Tolerances.FiniteDifference,
            $"{label}: Cp_eq {solution.State.CpEquilibrium:R} against the central difference {difference:R}");
    }

    /// <summary>
    /// The solution is <c>Ok</c>, clear of <see cref="EquilibriumConditions.Violations"/> at <see cref="Tolerances.EveryGasChemicalPotential"/>
    /// and of <see cref="EquilibriumConditions.EveryGasViolations"/> (the gases of any share).
    /// </summary>
    public static void AssertOkAndClear(HostSolution solution, string label)
    {
        Assert.True(solution.Status == CaseStatus.Ok, $"{label}: status {solution.Status} after {solution.Iterations} iterations");
        var violations = EquilibriumConditions.Violations(solution, Tolerances.EveryGasChemicalPotential);
        violations.AddRange(EquilibriumConditions.EveryGasViolations(solution, Tolerances.EveryGasChemicalPotential));
        Assert.True(violations.Count == 0, $"{label}: {string.Join("; ", violations)}");
    }
}
