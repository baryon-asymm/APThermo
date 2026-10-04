using APThermo.Thermo;

namespace APThermo.Equilibrium.Tests;

/// <summary>The assertions the trace-gas facts share: a state is <c>Ok</c> and clear of every condition, every reported gas on its stationarity included.</summary>
internal static class TraceGasChecks
{
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
