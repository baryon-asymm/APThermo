using APThermo.Equilibrium;
using APThermo.Thermo;
using Xunit.Abstractions;

namespace APThermo.Execution.Tests;

/// <summary>
/// The rules of <see cref="ComparisonSupport"/> and the balance-remnant guard, proven on the CPU accelerator alone (Execution.Tests BOOT.md,
/// "What a difference between the accelerators is not", 2026-10-04): a second CPU run stands for the other accelerator, over a batch moved by a
/// few ULP, over a table whose entropy constant b2 of one species moved, or with a result edited by the fact. Each rule is shown to accept what it
/// is derived for and to refuse a difference that is not that.
/// </summary>
[Collection(EngineFixture.CollectionName)]
public sealed class ComparisonRuleTests(ITestOutputHelper output)
{
    /// <summary>The table of the water fixtures, whose tp cases at 300 K to 304.3 K sit on the liquid-gas boundary of water.</summary>
    private const string WaterTable = "lox-lh2_of6_pc7MPa_shiftingEquilibrium_chamber";

    /// <summary>The ULP the "other accelerator" of the noise fact moves the element moles by: not one of the replicates' counts.</summary>
    private const int OtherUlps = 5;

    /// <summary>How far the entropy constant of liquid water moves in the species-data fact: 6e-11, above the unit in the last place of b2, above the mole-fraction tier once multiplied by the sensitivity of the case at 300 K (4e-11 stays inside it), and below the state tier in every field.</summary>
    private const double WaterMove = 6e-11;

    /// <summary>
    /// The enthalpy of a state that is zero up to cancellation (silicon and argon at their reference temperature, 3e-6 J/kg) is accepted within the
    /// rounding bound of its sum and refused beyond it; for a state whose enthalpy does not cancel the bound is a millionth of the tier, so the
    /// rule decides nothing there. Red once, 2026-10-04: with the bound fixed at zero the first assertion fails.
    /// </summary>
    [Fact]
    public void TheEnthalpyRoundingBoundAcceptsACancellingEnthalpyAndNothingElse()
    {
        var (batch, table, cases) = FixtureBatches.EquilibriumTableFamily(EngineFixture.Shared.Database, "si-in-argon_T298.15");
        var labels = cases.Select(c => c.Name).ToList();
        using var tables = EngineFixture.Shared.Cpu.Upload(table);
        var cpu = EngineFixture.Shared.Cpu.Run(tables, batch);
        var sensitivities = BalanceSensitivities.Measure(EngineFixture.Shared.Cpu, tables, batch, table);
        var support = SupportOver(tables, table, batch, cpu, tables);
        var tier = GpuCpuTolerances.RelativeFor(typeof(MixtureState), nameof(MixtureState.Enthalpy));
        var k = 0;
        var cancelling = Math.Abs(cpu.State[k].Enthalpy);
        Assert.True(cancelling < 1e-3, $"the enthalpy of {labels[k]} does not cancel: {cancelling:E2} J/kg");
        var bound = support.EnthalpyBound(k);
        Assert.True(bound > tier * cancelling, $"the bound {bound:E2} is not above the tier's {tier * cancelling:E2}: the rule decides nothing here");

        Assert.Empty(Compare(cpu, (batch, table, labels), sensitivities, support, other => other.State[k] = other.State[k] with { Enthalpy = other.State[k].Enthalpy + 0.5 * bound }));
        Assert.Contains("Enthalpy", string.Join('\n', Compare(cpu, (batch, table, labels), sensitivities, null, other => other.State[k] = other.State[k] with { Enthalpy = other.State[k].Enthalpy + 0.5 * bound })), StringComparison.Ordinal);
        Assert.Contains("Enthalpy", string.Join('\n', Compare(cpu, (batch, table, labels), sensitivities, support, other => other.State[k] = other.State[k] with { Enthalpy = other.State[k].Enthalpy + 2.0 * bound })), StringComparison.Ordinal);

        var (rocket, rocketTable, rocketCases) = FixtureBatches.EquilibriumTableFamily(EngineFixture.Shared.Database, WaterTable);
        using var rocketTables = EngineFixture.Shared.Cpu.Upload(rocketTable);
        var rocketRun = EngineFixture.Shared.Cpu.Run(rocketTables, rocket);
        var rocketSupport = SupportOver(rocketTables, rocketTable, rocket, rocketRun, rocketTables);
        for (var i = 0; i < rocket.Count; i++)
        {
            Assert.True(rocketSupport.EnthalpyBound(i) <= 1e-3 * tier * Math.Abs(rocketRun.State[i].Enthalpy),
                        $"{rocketCases[i].Name}: the rounding bound {rocketSupport.EnthalpyBound(i):E2} J/kg reaches a thousandth of the tier on {rocketRun.State[i].Enthalpy:E2} J/kg");
        }
    }

    /// <summary>
    /// A tp case whose other-accelerator composition differs because the other accelerator's G/RT of liquid water differs is accepted once that
    /// difference is measured and carried to first order, and a composition that differs by more than that is not. The other accelerator here is the
    /// CPU over a table whose b2 of H2O(L) moved by 6e-11, which moves x(H2O) of the one case at 300 K by 3.8e-10 relative (tier 1e-10) and no state field above its tier; the plain comparison refuses it.
    /// Red once, 2026-10-04: with <c>DataEffect</c> answering null the second assertion fails.
    /// </summary>
    [Fact]
    public void TheOtherAcceleratorsOwnSpeciesDataAreCarriedToFirstOrderForATpCase()
    {
        var (family, table, cases) = FixtureBatches.EquilibriumTableFamily(EngineFixture.Shared.Database, WaterTable);
        var k = cases.Select(c => c.Name).ToList().IndexOf("rp1311-example14_T300");
        var batch = FixtureBatches.CaseOf(family, k);
        var labels = new List<string> { cases[k].Name };
        k = 0;
        using var tables = EngineFixture.Shared.Cpu.Upload(table);
        var cpu = EngineFixture.Shared.Cpu.Run(tables, batch);
        var sensitivities = BalanceSensitivities.Measure(EngineFixture.Shared.Cpu, tables, batch, table);
        var water = table.IndexOf("H2O(L)");
        var arrays = table.Arrays;
        for (var i = 0; i < arrays.IntervalCount[water]; i++)
        {
            arrays.Coefficients[(arrays.IntervalStart[water] + i) * 9 + 8] -= WaterMove;
        }

        try
        {
            using var movedTables = EngineFixture.Shared.Cpu.Upload(table);
            var other = EngineFixture.Shared.Cpu.Run(movedTables, batch);
            var support = SupportOver(tables, table, batch, cpu, movedTables);
            var plain = new GpuCpuComparison(EngineFixture.Shared.Tolerances).Equilibrium(cpu, other, batch, table, labels, sensitivities);
            Assert.Contains(plain, mismatch => mismatch.Contains("rp1311-example14_T300 x(H2O)", StringComparison.Ordinal));

            var comparison = new GpuCpuComparison(EngineFixture.Shared.Tolerances) { Support = support };
            var rescued = comparison.Equilibrium(cpu, other, batch, table, labels, sensitivities);
            output.WriteLine(string.Join('\n', support.Decisions));
            Assert.True(rescued.Count == 0, string.Join('\n', rescued.Take(10)));
            Assert.Contains(support.Decisions, decision => decision.StartsWith("rp1311-example14_T300: the accelerators' own species data", StringComparison.Ordinal));

            var gas = Enumerable.Range(0, table.GasCount).First(j => other.Moles[k * table.SpeciesCount + j] / Enumerable.Range(0, table.SpeciesCount).Sum(i => other.Moles[k * table.SpeciesCount + i]) > 1e-3);
            other.Moles[k * table.SpeciesCount + gas] *= 1.0 + 1e-8;
            var wrong = new GpuCpuComparison(EngineFixture.Shared.Tolerances) { Support = support }.Equilibrium(cpu, other, batch, table, labels, sensitivities);
            Assert.Contains(wrong, mismatch => mismatch.Contains($"x({table.Species[gas]})", StringComparison.Ordinal));
        }
        finally
        {
            for (var i = 0; i < arrays.IntervalCount[water]; i++)
            {
                arrays.Coefficients[(arrays.IntervalStart[water] + i) * 9 + 8] += WaterMove;
            }
        }

        Assert.Null(SupportOver(tables, table, family, EngineFixture.Shared.Cpu.Run(tables, family), tables).DataEffect(family.Kind.ToList().FindIndex(kind => kind != ProblemKind.AssignedTemperaturePressure)));
    }

    /// <summary>
    /// The states on the AP/HTPB/Al reaction plateau, whose mole fractions the CPU accelerator itself moves by up to 7e-9 under 16 ULP of the element
    /// moles, are compared with that response as the floor: a second CPU run over element moles moved by another 5 ULP is accepted (it differs above the
    /// tier in at least one case), a deviation of 1e-6 in one species of one case is refused, and a count of Newton steps outside the range the
    /// replicates take is counted in the share while one inside it is not. Red once, 2026-10-04: with <c>NoiseFactor</c> at 0 the second assertion fails.
    /// </summary>
    [Fact]
    public void ThePlateauStatesAreComparedAgainstTheCpuAcceleratorsOwnResponseToRoundingNoise()
    {
        var family = SeededFamilies.Family(EngineFixture.Shared.Database, SeededFamilies.Fixtures);
        var (batch, table, labels) = (family.Batch, family.Table, family.Labels);
        var moved = FixtureBatches.CopyOf(batch);
        for (var n = 0; n < moved.ElementMoles.Length; n++)
        {
            for (var u = 0; u < OtherUlps; u++)
            {
                moved.ElementMoles[n] = n % 2 == 0 ? Math.BitIncrement(moved.ElementMoles[n]) : Math.BitDecrement(moved.ElementMoles[n]);
            }
        }

        using var tables = EngineFixture.Shared.Cpu.Upload(table);
        var cpu = EngineFixture.Shared.Cpu.Run(tables, batch);
        var other = EngineFixture.Shared.Cpu.Run(tables, moved);
        var sensitivities = BalanceSensitivities.Measure(EngineFixture.Shared.Cpu, tables, batch, table);
        var plain = new GpuCpuComparison(EngineFixture.Shared.Tolerances).Equilibrium(cpu, other, batch, table, labels, sensitivities);
        Assert.NotEmpty(plain);

        var support = SupportOver(tables, table, batch, cpu, tables);
        var comparison = new GpuCpuComparison(EngineFixture.Shared.Tolerances) { Support = support };
        var rescued = comparison.Equilibrium(cpu, other, batch, table, labels, sensitivities);
        output.WriteLine(string.Join('\n', support.Decisions));
        Assert.True(rescued.Count == 0, string.Join('\n', rescued.Take(10)));
        Assert.Contains(support.Decisions, decision => decision.Contains("rounding noise", StringComparison.Ordinal));

        var differing = Enumerable.Range(0, batch.Count).Where(k => cpu.Iterations[k] != other.Iterations[k]).ToList();
        Assert.Equal(differing.Count, comparison.StepShareExcluded + comparison.DifferentSteps);

        var s = table.SpeciesCount;
        var gas = Enumerable.Range(0, table.GasCount).First(j => cpu.Moles[s + j] / Enumerable.Range(0, s).Sum(i => cpu.Moles[s + i]) > 1e-3);
        other.Moles[s + gas] *= 1.0 + 1e-6;
        var refused = new GpuCpuComparison(EngineFixture.Shared.Tolerances) { Support = support }.Equilibrium(cpu, other, batch, table, labels, sensitivities);
        Assert.Contains(refused, mismatch => mismatch.Contains($"{labels[1]} x({table.Species[gas]})", StringComparison.Ordinal));

        other.Iterations[2] = 1000;
        var outside = new GpuCpuComparison(EngineFixture.Shared.Tolerances) { Support = support };
        _ = outside.Equilibrium(cpu, other, batch, table, labels, sensitivities);
        Assert.True(outside.DifferentSteps >= 1);
    }

    /// <summary>
    /// A remnant whose κ is large enough that its one-sided differences disagree by their curvature share is corrected, not dropped, and the guard
    /// stays between the entry and its cap: the calcite states seeded 20 K above the plateau at 1e4 Pa (κ of CO 4.3e6, disagreement 2.15e-2 against
    /// the entry's 2e-2) compare clean over a second CPU run, where the entry alone leaves 1e-8 on x(CO). Red once, 2026-10-04: with the guard at the
    /// entry the first assertion fails on the fractions 0.1 of the hp and the sp state.
    /// </summary>
    [Fact]
    public void TheGuardAdmitsACurvedRemnantOfLargeKappaAndKeepsItsEntryAndCap()
    {
        Assert.Equal(GpuCpuTolerances.Entries["sensitivityDisagreement"].Relative, GpuCpuTolerances.SensitivityGuard(100.0));
        Assert.True(GpuCpuTolerances.SensitivityGuard(4.3e6) > 2.15e-2);
        Assert.Equal(GpuCpuTolerances.SensitivityGuardCap, GpuCpuTolerances.SensitivityGuard(1e12));

        var family = SeededFamilies.Family(EngineFixture.Shared.Database, "seeded-bracket-calcite-p1e4");
        var run = BalanceRemnantTests.TwoRuns((family.Batch, family.Table, []));
        var comparison = new GpuCpuComparison(EngineFixture.Shared.Tolerances) { IterationsSumAttempts = true };
        var mismatches = comparison.Equilibrium(run.Cpu, run.Other, family.Batch, family.Table, family.Labels, run.Sensitivities);
        Assert.True(mismatches.Count == 0, string.Join('\n', mismatches.Take(10)) + "\nworst: " + comparison.Worst());
        Assert.True(comparison.CorrectedSpecies > 0);
    }

    private static ComparisonSupport SupportOver(UploadedTables cpuTables, SpeciesTable table, EquilibriumBatch batch, EquilibriumBatchResult cpu, UploadedTables otherTables) =>
        new(EngineFixture.Shared.Cpu, cpuTables, table, batch, cpu,
            new SpeciesFunctionSources(functions => EngineFixture.Shared.Cpu.Run(cpuTables, functions), functions => EngineFixture.Shared.Cpu.Run(otherTables, functions)));

    /// <summary>The family's CPU result against a copy of it that <paramref name="edit"/> changed, by the comparison with or without support.</summary>
    private static List<string> Compare(EquilibriumBatchResult cpu, (EquilibriumBatch Batch, SpeciesTable Table, IReadOnlyList<string> Labels) family,
                                        BalanceSensitivities sensitivities, ComparisonSupport? support, Action<EquilibriumBatchResult> edit)
    {
        var other = new EquilibriumBatchResult(speciesCount: cpu.SpeciesCount, state: [.. cpu.State], moles: [.. cpu.Moles], status: [.. cpu.Status],
                                               iterations: [.. cpu.Iterations], timings: cpu.Timings, accelerator: cpu.Accelerator);
        edit(other);
        return new GpuCpuComparison(EngineFixture.Shared.Tolerances) { Support = support }.Equilibrium(cpu, other, family.Batch, family.Table, family.Labels, sensitivities);
    }
}
