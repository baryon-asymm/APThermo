using APThermo.Thermo;

namespace APThermo.Equilibrium.Tests;

/// <summary>
/// The residue (TraceGas BOOT.md, <c>## Acceptance criteria</c>; Recovery BOOT.md, "The ladder"): a mixture of exact
/// stoichiometry, or within 1e-12 of it, that the condensed species hold entirely. The only equation that fixes the amount of a
/// residue gas below 1e-12 kmol/kg is the balance whose tolerance equals it, so both a gasless answer and a residue-gas answer
/// satisfy the invariant, and the proven one is reported: the verdict runs before the trace-gas pass, and such a state ends
/// <c>NoGasPhase</c>, never <c>Ok</c> with a gas below 1e-12 kmol/kg.
/// </summary>
[Collection(CpuFixture.CollectionName)]
public sealed class ResidueVerdictTests
{
    /// <summary>kmol/kg: the amount of gas below which the gas of an <c>Ok</c> state is residue.</summary>
    private const double ResidueGas = 1.0e-12;

    /// <summary>The excesses of the last element that stand within the invariant of exact stoichiometry.</summary>
    private static readonly double[] Excesses = [-1.0e-12, 0.0, 1.0e-12];

    /// <summary>
    /// Every state of the 17 systems and of the five binary systems at the three excesses ends <c>Ok</c> with at least 1e-12 kmol/kg of gas, clear of the equilibrium
    /// conditions, or <c>NoGasPhase</c> clear of the gasless conditions; none ends <c>NotConverged</c> but the declared leftovers
    /// (<see cref="TraceGasLeftovers"/>). Red when the pass runs before the verdict: the residue ends <c>Ok</c> with a gas below
    /// 1e-12 kmol/kg.
    /// </summary>
    [Fact]
    public void AMixtureOfExactStoichiometryEndsGaslessOrWithMoreThanAResidueOfGas()
    {
        var problems = new List<string>();
        var states = 0;
        foreach (var state in TraceGasCases.Scan(Excesses).Concat(TraceGasCases.Binary(Excesses)))
        {
            states++;
            var solution = state.Solve();
            var problem = Problem(state, solution);
            if (problem is not null)
            {
                problems.Add(problem);
            }
        }

        Assert.True(states > 0);
        Assert.True(problems.Count == 0, $"{problems.Count} of {states} states:\n" + string.Join("\n", problems.Take(20)));
    }

    private static string? Problem(TraceGasCase state, HostSolution solution)
    {
        if (solution.Status == CaseStatus.NoGasPhase)
        {
            var gasless = EquilibriumConditions.GaslessViolations(solution);
            return gasless.Count == 0 ? null : $"{state.Name}: gasless but {string.Join("; ", gasless)}";
        }

        if (solution.Status != CaseStatus.Ok)
        {
            return TraceGasLeftovers.Declared(state.Name) ? null : $"{state.Name}: {solution.Status}";
        }

        var gas = solution.Moles.Take(solution.Case.Table.GasCount).Sum();
        if (!(gas >= ResidueGas) && !TraceGasLeftovers.Residue.Contains(state.Name))
        {
            return $"{state.Name}: Ok with {gas:E2} kmol/kg of gas";
        }

        var violations = EquilibriumConditions.Violations(solution, Tolerances.EveryGasChemicalPotential);
        violations.AddRange(EquilibriumConditions.EveryGasViolations(solution, Tolerances.EveryGasChemicalPotential));
        return violations.Count == 0 ? null : $"{state.Name}: {string.Join("; ", violations)}";
    }
}
