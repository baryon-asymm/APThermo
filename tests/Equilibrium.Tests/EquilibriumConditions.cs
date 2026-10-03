using APThermo.Thermo;

namespace APThermo.Equilibrium.Tests;

/// <summary>
/// The independent equilibrium checks this node's tests run over a state that no fixture reference covers (a
/// regression grid, a scan, a probe state): element conservation at the node's own invariant, every retained gas at
/// its own chemical potential (RP-1311's stationarity condition, (2.15)), and no absent condensed candidate in its
/// effective range with a positive inclusion gain, a species of an absent element being no candidate (the mask). One place, shared by <see cref="RegressionStateTests"/> and
/// <see cref="TiedReleaseTests"/>, so the same check is not written twice with two different tolerances by accident.
/// </summary>
internal static class EquilibriumConditions
{
    /// <summary>The element-conservation invariant's tolerance (Equilibrium BOOT.md, matching <see cref="ElementConservationTests"/>).</summary>
    public const double ElementInvariant = 1e-12;

    /// <summary>Candidates this far inside their range are clear of the effective-bound shift at a crossing (matching <see cref="PlateauTests"/>).</summary>
    private const double RangeMargin = 1.5;

    /// <summary>Every violation of the conditions below; empty when the state is honest.</summary>
    public static List<string> Violations(HostSolution solution, double gasChemicalPotentialResidual)
    {
        var table = solution.Case.Table;
        using var buffers = SpeciesTableBuffers.Upload(CpuFixture.Shared.Accelerator, table);
        var view = buffers.View;

        var violations = ElementConservationViolations(solution);
        violations.AddRange(GasChemicalPotentialViolations(solution, view, gasChemicalPotentialResidual));
        violations.AddRange(CondensedInclusionGainViolations(solution, view));
        return violations;
    }

    /// <summary>Every element whose conservation residual exceeds <see cref="ElementInvariant"/>.</summary>
    private static List<string> ElementConservationViolations(HostSolution solution)
    {
        var violations = new List<string>();
        var table = solution.Case.Table;
        var arrays = table.Arrays;
        var speciesCount = table.SpeciesCount;
        for (var i = 0; i < table.ElementCount; i++)
        {
            var sum = 0.0;
            for (var j = 0; j < speciesCount; j++)
            {
                sum += arrays.Stoichiometry[i * speciesCount + j] * solution.Moles[j];
            }

            var residual = Math.Abs(sum - solution.Case.ElementMoles[i]);
            var bound = ElementInvariant * Math.Max(1.0, solution.Case.ElementMoles[i]);
            if (!(residual <= bound))
            {
                violations.Add($"element {table.Elements[i]} residual {residual:E3} above {bound:E3}");
            }
        }

        return violations;
    }

    /// <summary>Every retained gas at or above 1e-6 of the gas whose chemical potential departs from Σ a_ij π_i by more than <paramref name="residualBound"/>.</summary>
    private static List<string> GasChemicalPotentialViolations(HostSolution solution, SpeciesTableView view, double residualBound)
    {
        var violations = new List<string>();
        var table = solution.Case.Table;
        var arrays = table.Arrays;
        var speciesCount = table.SpeciesCount;
        var temperature = solution.State.Temperature;
        var totalGasMoles = solution.Moles.Take(table.GasCount).Sum();
        var logPressureRatio = Math.Log(solution.Case.Pressure / 1e5);
        for (var j = 0; j < table.GasCount; j++)
        {
            if (!(solution.Moles[j] > 0.0))
            {
                continue;
            }

            var moleFraction = solution.Moles[j] / totalGasMoles;
            if (!(moleFraction > 1e-6))
            {
                continue;
            }

            var chemicalPotential = SpeciesFunctions.GOverRT(view, j, temperature) + Math.Log(moleFraction) + logPressureRatio;
            var elementPotential = 0.0;
            for (var i = 0; i < table.ElementCount; i++)
            {
                elementPotential += arrays.Stoichiometry[i * speciesCount + j] * solution.Multipliers[i];
            }

            var residual = Math.Abs(chemicalPotential - elementPotential);
            if (residual > residualBound)
            {
                violations.Add($"gas {table.Species[j]} chemical-potential residual {residual:E3}");
            }
        }

        return violations;
    }

    /// <summary>
    /// Whether species <paramref name="j"/> contains an element whose abundance in the case is zero: the node masks such a
    /// species out of the solve (Equilibrium BOOT.md, an absent element is a mask, not an error), so it is no candidate to
    /// include and its inclusion gain, computed with a multiplier the dropped equation never fixed, says nothing.
    /// </summary>
    private static bool CarriesAbsentElement(HostSolution solution, int j)
    {
        var table = solution.Case.Table;
        for (var i = 0; i < table.ElementCount; i++)
        {
            if (solution.Case.ElementMoles[i] == 0.0 && table.Arrays.Stoichiometry[i * table.SpeciesCount + j] != 0.0)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Every absent condensed candidate, interior to its effective range, still showing a positive inclusion gain above <see cref="Tolerances.ResidualInclusionGain"/>.</summary>
    private static List<string> CondensedInclusionGainViolations(HostSolution solution, SpeciesTableView view)
    {
        var violations = new List<string>();
        var table = solution.Case.Table;
        var arrays = table.Arrays;
        var speciesCount = table.SpeciesCount;
        var temperature = solution.State.Temperature;
        for (var j = table.GasCount; j < speciesCount; j++)
        {
            if (solution.Moles[j] > 0.0 || CarriesAbsentElement(solution, j))
            {
                continue;
            }

            var start = arrays.IntervalStart[j];
            var last = start + arrays.IntervalCount[j] - 1;
            var interior = temperature >= arrays.IntervalBounds[start * 2] + RangeMargin
                           && temperature <= arrays.IntervalBounds[last * 2 + 1] - RangeMargin;
            if (!interior)
            {
                continue;
            }

            var gain = -SpeciesFunctions.GOverRT(view, j, temperature);
            for (var i = 0; i < table.ElementCount; i++)
            {
                gain += arrays.Stoichiometry[i * speciesCount + j] * solution.Multipliers[i];
            }

            if (gain > Tolerances.ResidualInclusionGain)
            {
                violations.Add($"condensed {table.Species[j]} left out with inclusion gain {gain:E3} at {temperature:F3} K");
            }
        }

        return violations;
    }
}
