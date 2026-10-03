using APThermo.Equilibrium;
using APThermo.Thermo;

namespace APThermo.Execution.Tests;

/// <summary>
/// The appear-or-vanish rule of the balance-remnant correction (<see cref="BalanceSensitivities"/>, Execution.Tests BOOT.md) shown at
/// a phase onset, on the CPU accelerator alone: no fixture family triggers it, so a case is built at the boundary where a condensed
/// species appears, found by bisection on one element's moles through the CPU accelerator itself.
/// </summary>
[Collection(EngineFixture.CollectionName)]
public sealed class PhaseOnsetTests
{
    /// <summary>The family whose first temperature-pressure case the onset is built from: the three-element table of RP-1311 example 12, on which scaling the moles of carbon makes C(gr) appear next to H2O(L).</summary>
    private const string Family = "three-element-example12";

    /// <summary>The relative range of one element's moles searched for an onset, either side of the fixture's own amount.</summary>
    private const double SearchRange = 0.2;

    /// <summary>The bisection stops when the bracket is this relative width, a tenth of the step <see cref="GpuCpuTolerances.SensitivityStep"/> of the central differences, so that b(1 − h) and b(1 + h) straddle the boundary whichever side the midpoint lies on.</summary>
    private const double BracketWidth = GpuCpuTolerances.SensitivityStep / 10.0;

    /// <summary>A case at a phase onset has no derivative, no species of it is corrected, and the comparison over it still passes uncorrected.</summary>
    [Fact]
    public void ACaseAtAPhaseOnsetGetsNoCorrectionAndTheComparisonStillPasses()
    {
        var (batch, table, element) = OnsetCase();
        var step = GpuCpuTolerances.SensitivityStep;
        using var tables = EngineFixture.Shared.Cpu.Upload(table);

        var centre = EngineFixture.Shared.Cpu.Run(tables, batch);
        var minus = EngineFixture.Shared.Cpu.Run(tables, Scaled(batch, element, 1.0 - step));
        var plus = EngineFixture.Shared.Cpu.Run(tables, Scaled(batch, element, 1.0 + step));
        EquilibriumBatchResult[] results = [centre, minus, plus];
        Assert.All(results, result => Assert.Equal(CaseStatus.Ok, result.Status[0]));
        var presence = results.Select(result => CondensedSet(result, table)).ToArray();
        Assert.Contains(presence, set => set != presence[0]);
        Assert.NotEqual(string.Empty, string.Concat(presence));

        var sensitivities = BalanceSensitivities.Measure(EngineFixture.Shared.Cpu, tables, batch, table);
        Assert.Equal(1, sensitivities.StationsWithoutDerivative);
        Assert.All(sensitivities.Correction(0, new double[table.ElementCount]), correction => Assert.True(double.IsNaN(correction)));

        var run = BalanceRemnantTests.TwoRuns((batch, table, []));
        Assert.Equal(CaseStatus.Ok, run.Cpu.Status[0]);
        Assert.Equal(CaseStatus.Ok, run.Other.Status[0]);
        Assert.Equal(CondensedSet(run.Cpu, table), CondensedSet(run.Other, table));
        var cpuResiduals = run.Balance.Residuals(0, run.Cpu.Moles, 0);
        var otherResiduals = run.Balance.Residuals(0, run.Other.Moles, 0);
        var comparison = new GpuCpuComparison(EngineFixture.Shared.Tolerances);
        var station = new MoleStation(run.Cpu.Moles, run.Other.Moles, 0, run.Cpu.Iterations[0] == run.Other.Iterations[0], "onset");
        var mismatches = comparison.Balance(run.Balance, cpuResiduals, otherResiduals, "onset")
            .Concat(comparison.Moles(station, table, run.Sensitivities.Correction(0, cpuResiduals), run.Sensitivities.Correction(0, otherResiduals)))
            .ToList();
        Assert.True(mismatches.Count == 0, string.Join("\n", mismatches) + "\nworst: " + comparison.Worst());
        Assert.Equal(0, comparison.CorrectedSpecies);
        Assert.True(comparison.UncorrectedSpecies > 0);
    }

    /// <summary>
    /// The first temperature-pressure case of <see cref="Family"/> as a batch of one, with the moles of the first element whose scaling
    /// within <see cref="SearchRange"/> changes the condensed set bisected to a bracket of <see cref="BracketWidth"/>, and the batch
    /// set at the middle of the bracket. Nothing is typed: the boundary is what the solver says it is.
    /// </summary>
    private static (EquilibriumBatch Batch, SpeciesTable Table, int Element) OnsetCase()
    {
        var (family, table, cases) = FixtureBatches.NamedEquilibriumFamily(EngineFixture.Shared.Database, Family);
        var first = Enumerable.Range(0, cases.Count).First(k => family.Kind[k] == ProblemKind.AssignedTemperaturePressure);
        var single = new EquilibriumBatch(1, table.ElementCount);
        single.Kind[0] = family.Kind[first];
        single.Pressure[0] = family.Pressure[first];
        single.Temperature[0] = family.Temperature[first];
        single.Target[0] = family.Target[first];
        Array.Copy(family.ElementMoles, first * table.ElementCount, single.ElementMoles, 0, table.ElementCount);

        using var tables = EngineFixture.Shared.Cpu.Upload(table);
        for (var element = 0; element < table.ElementCount; element++)
        {
            var low = 1.0 - SearchRange;
            var high = 1.0 + SearchRange;
            var lowSet = CondensedSetAt(tables, single, table, element, low);
            if (lowSet == CondensedSetAt(tables, single, table, element, high))
            {
                continue;
            }

            while (high - low > BracketWidth)
            {
                var middle = 0.5 * (low + high);
                if (CondensedSetAt(tables, single, table, element, middle) == lowSet)
                {
                    low = middle;
                }
                else
                {
                    high = middle;
                }
            }

            return (Scaled(single, element, 0.5 * (low + high)), table, element);
        }

        throw new InvalidOperationException($"no element of the {Family} case changes the condensed set within ±{SearchRange}");
    }

    private static string CondensedSetAt(UploadedTables tables, EquilibriumBatch batch, SpeciesTable table, int element, double factor) =>
        CondensedSet(EngineFixture.Shared.Cpu.Run(tables, Scaled(batch, element, factor)), table);

    /// <summary>The condensed species present in case 0 of a result, as one string; a failed case is reported as such, so that a failure is never mistaken for a change of the set.</summary>
    private static string CondensedSet(EquilibriumBatchResult result, SpeciesTable table)
    {
        Assert.Equal(CaseStatus.Ok, result.Status[0]);
        return string.Join(",", Enumerable.Range(table.GasCount, table.SpeciesCount - table.GasCount).Where(j => result.Moles[j] > 0.0).Select(j => table.Species[j]));
    }

    /// <summary>The batch with the moles of one element of its case multiplied by a factor.</summary>
    private static EquilibriumBatch Scaled(EquilibriumBatch batch, int element, double factor)
    {
        var scaled = FixtureBatches.CopyOf(batch);
        scaled.ElementMoles[element] *= factor;
        return scaled;
    }
}
