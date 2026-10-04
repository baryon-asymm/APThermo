using APThermo.Equilibrium;
using APThermo.Thermo;
using Xunit.Abstractions;

namespace APThermo.Execution.Tests;

/// <summary>
/// The 0.2.2 families of the gasless verdict and the temperature bracket (<see cref="RecoveryFamilies"/>) on the CPU accelerator alone,
/// the half of <see cref="CudaTests.ABracketedFamilyOnCudaMatchesTheCpuAccelerator"/> that needs no GPU: what each family holds, that the
/// comparison accepts a second CPU run over moved element moles (the stand-in for the other accelerator, as in
/// <see cref="BalanceRemnantTests"/>), and that it refuses what it should, each break shown red once on <c>NoGasPhase</c> and on <c>Ok</c> cases.
/// </summary>
[Collection(EngineFixture.CollectionName)]
public sealed class BracketedFamiliesTests(ITestOutputHelper output)
{
    /// <summary>The cases of the CPU launch of the budget check: the family's own hp and sp cases repeated, enough to time a launch and few enough for the fast set.</summary>
    private const int LaunchCases = 64;

    /// <summary>The names of the families as theory data, delegating to <see cref="RecoveryFamilies.Names"/>.</summary>
    public static TheoryData<string> FamilyNames() => RecoveryFamilies.Names();

    /// <summary>Every case of a gasless family ends <c>NoGasPhase</c> and of the others <c>Ok</c>, on the CPU accelerator, and the gasless families hold tp, hp and sp cases.</summary>
    [Theory]
    [MemberData(nameof(FamilyNames))]
    public void AFamilyHoldsTheCasesItStandsFor(string name)
    {
        var family = RecoveryFamilies.Family(EngineFixture.Shared.Database, name);
        output.WriteLine($"{name}: {family.Batch.Count} cases kept, {family.Gasless} gasless, {family.Dropped} dropped");
        Assert.NotEmpty(family.Labels);
        using var tables = EngineFixture.Shared.Cpu.Upload(family.Table);
        var result = EngineFixture.Shared.Cpu.Run(tables, family.Batch);
        var expected = family.Gasless > 0 ? CaseStatus.NoGasPhase : CaseStatus.Ok;
        Assert.All(result.Status, status => Assert.Equal(expected, status));
        if (family.Gasless > 0)
        {
            Assert.Equal(family.Batch.Count, family.Gasless);
            Assert.Contains(ProblemKind.AssignedTemperaturePressure, family.Batch.Kind);
            Assert.Contains(ProblemKind.AssignedEnthalpyPressure, family.Batch.Kind);
            Assert.Contains(ProblemKind.AssignedEntropyPressure, family.Batch.Kind);
            Assert.All(Enumerable.Range(0, family.Batch.Count), k => Assert.Equal(family.Batch.Pressure[k], result.State[k].Pressure));
        }
    }

    /// <summary>The gasless KO2 family holds a melting plateau: its hp and sp states inside the plateau carry both phases and one temperature.</summary>
    [Fact]
    public void TheGaslessMeltingPlateauHoldsBothPhasesAtOneTemperature()
    {
        var family = RecoveryFamilies.Family(EngineFixture.Shared.Database, "gasless-ko2");
        using var tables = EngineFixture.Shared.Cpu.Upload(family.Table);
        var result = EngineFixture.Shared.Cpu.Run(tables, family.Batch);
        var solid = family.Table.Species.ToList().IndexOf("KO2(a)");
        var liquid = family.Table.Species.ToList().IndexOf("KO2(L)");
        Assert.True(solid >= family.Table.GasCount && liquid >= family.Table.GasCount);
        var plateau = Enumerable.Range(0, family.Batch.Count).Where(k => family.Labels[k].Contains("melting plateau", StringComparison.Ordinal)).ToList();
        Assert.NotEmpty(plateau);
        foreach (var k in plateau)
        {
            Assert.True(result.Moles[k * family.Table.SpeciesCount + solid] > 0.0 && result.Moles[k * family.Table.SpeciesCount + liquid] > 0.0, family.Labels[k]);
            Assert.True(EngineFixture.Shared.Tolerances.Matches("temperature", result.State[plateau[0]].Temperature, result.State[k].Temperature), family.Labels[k]);
        }
    }

    /// <summary>The comparison accepts a second CPU run over moved element moles on every family: the CPU half of the CUDA comparison, <c>NoGasPhase</c> cases included.</summary>
    [Theory]
    [MemberData(nameof(FamilyNames))]
    public void TheComparisonAcceptsASecondRunOverMovedElementMoles(string name)
    {
        var family = RecoveryFamilies.Family(EngineFixture.Shared.Database, name);
        var run = BalanceRemnantTests.TwoRuns((family.Batch, family.Table, []));
        var comparison = new GpuCpuComparison(EngineFixture.Shared.Tolerances);
        var mismatches = comparison.Equilibrium(run.Cpu, run.Other, family.Batch, family.Table, family.Labels, run.Sensitivities);
        output.WriteLine($"{name}: worst {comparison.Worst()}");
        Assert.True(mismatches.Count == 0, string.Join("\n", mismatches.Take(30)) + "\nworst: " + comparison.Worst());
    }

    /// <summary>Two identical CPU runs over the gasless KO2 family compare clean, and each deliberate break of the second is refused with its own message: red once per rule.</summary>
    [Fact]
    public void TheGaslessComparisonRefusesWhatItShould()
    {
        var family = RecoveryFamilies.Family(EngineFixture.Shared.Database, "gasless-ko2");
        using var tables = EngineFixture.Shared.Cpu.Upload(family.Table);
        var sensitivities = BalanceSensitivities.Measure(EngineFixture.Shared.Cpu, tables, family.Batch, family.Table);
        var tp = family.Batch.Kind.ToList().IndexOf(ProblemKind.AssignedTemperaturePressure);
        var hp = family.Batch.Kind.ToList().IndexOf(ProblemKind.AssignedEnthalpyPressure);
        var species = family.Table.SpeciesCount;
        var baseline = EngineFixture.Shared.Cpu.Run(tables, family.Batch);
        var condensed = Enumerable.Range(family.Table.GasCount, species - family.Table.GasCount).First(j => baseline.Moles[tp * species + j] > 0.0);
        Assert.Empty(Compare(tables, family, sensitivities, _ => { }));

        Assert.Contains("zero for a NoGasPhase state", Single(Compare(tables, family, sensitivities, r => r.State[tp] = r.State[tp] with { Density = 1.0 })), StringComparison.Ordinal);
        Assert.Contains("zero for a NoGasPhase state", Single(Compare(tables, family, sensitivities, r => r.State[hp] = r.State[hp] with { SoundSpeed = 1e-300 })), StringComparison.Ordinal);
        Assert.Contains("pressure on cuda", Single(Compare(tables, family, sensitivities, r => r.State[tp] = r.State[tp] with { Pressure = Math.BitIncrement(r.State[tp].Pressure) })), StringComparison.Ordinal);
        Assert.Contains("temperature on cuda", string.Join('\n', Compare(tables, family, sensitivities, r => r.State[tp] = r.State[tp] with { Temperature = Math.BitIncrement(r.State[tp].Temperature) })), StringComparison.Ordinal);
        Assert.Empty(Compare(tables, family, sensitivities, r => r.State[hp] = r.State[hp] with { Temperature = r.State[hp].Temperature * (1.0 + 1e-12) }));
        Assert.Contains("temperature cpu", string.Join('\n', Compare(tables, family, sensitivities, r => r.State[hp] = r.State[hp] with { Temperature = r.State[hp].Temperature * (1.0 + 1e-9) })), StringComparison.Ordinal);
        Assert.Contains($"moles of {family.Table.Species[0]}", string.Join('\n', Compare(tables, family, sensitivities, r => r.Moles[tp * species] = 1e-3)), StringComparison.Ordinal);
        Assert.Contains($"moles of {family.Table.Species[condensed]}", string.Join('\n', Compare(tables, family, sensitivities, r => r.Moles[tp * species + condensed] *= 1.0 + 1e-8)), StringComparison.Ordinal);
        Assert.Contains("status cpu NoGasPhase, cuda NotConverged", Single(Compare(tables, family, sensitivities, r => r.Status[tp] = CaseStatus.NotConverged)), StringComparison.Ordinal);
    }

    /// <summary>The comparison still refuses a perturbed <c>Ok</c> case of a bracketed family: the state, and a mole fraction.</summary>
    [Fact]
    public void TheOkComparisonOfABracketedFamilyRefusesWhatItShould()
    {
        var family = RecoveryFamilies.Family(EngineFixture.Shared.Database, "bracket-calcite-1e5");
        using var tables = EngineFixture.Shared.Cpu.Upload(family.Table);
        var sensitivities = BalanceSensitivities.Measure(EngineFixture.Shared.Cpu, tables, family.Batch, family.Table);
        Assert.Empty(Compare(tables, family, sensitivities, _ => { }));
        Assert.Contains("Enthalpy", string.Join('\n', Compare(tables, family, sensitivities, r => r.State[0] = r.State[0] with { Enthalpy = r.State[0].Enthalpy * (1.0 + 1e-6) })), StringComparison.Ordinal);
        Assert.Contains("status cpu Ok, cuda NoGasPhase", Single(Compare(tables, family, sensitivities, r => r.Status[0] = CaseStatus.NoGasPhase)), StringComparison.Ordinal);
    }

    /// <summary>
    /// The check of the launch-budget fact on the CPU accelerator (<see cref="CudaTests.AFamilyOfCasesThatAllBracketStaysWithinTheLaunchBudget"/>
    /// needs the device): a launch of the tiled hp and sp cases ends every case <c>NoGasPhase</c> and passes a generous limit, and a limit of
    /// zero and a case that ended otherwise are refused.
    /// </summary>
    [Fact]
    public void TheLaunchBudgetCheckRefusesALaunchOverTheLimitAndACaseThatDidNotBracket()
    {
        var family = RecoveryFamilies.Family(EngineFixture.Shared.Database, "gasless-ko2");
        var batch = RecoveryFamilies.Tiled(RecoveryFamilies.WithoutTp(family.Batch), LaunchCases);
        Assert.DoesNotContain(ProblemKind.AssignedTemperaturePressure, batch.Kind);
        using var tables = EngineFixture.Shared.Cpu.Upload(family.Table);
        var run = EngineFixture.Shared.Cpu.Run(tables, batch);
        Assert.Null(RecoveryFamilies.LaunchViolation(run, TimeSpan.FromMinutes(1)));
        Assert.Contains("above the budget", RecoveryFamilies.LaunchViolation(run, TimeSpan.Zero), StringComparison.Ordinal);
        run.Status[0] = CaseStatus.Ok;
        Assert.Contains("did not end NoGasPhase", RecoveryFamilies.LaunchViolation(run, TimeSpan.FromMinutes(1)), StringComparison.Ordinal);
    }

    private static string Single(List<string> mismatches) => Assert.Single(mismatches);

    /// <summary>The family run twice on the CPU accelerator, the second result broken by <paramref name="breakIt"/>, and the comparison of the two.</summary>
    private static List<string> Compare(UploadedTables tables, BracketedFamily family, BalanceSensitivities sensitivities, Action<EquilibriumBatchResult> breakIt)
    {
        var cpu = EngineFixture.Shared.Cpu.Run(tables, family.Batch);
        var other = EngineFixture.Shared.Cpu.Run(tables, family.Batch);
        breakIt(other);
        return new GpuCpuComparison(EngineFixture.Shared.Tolerances).Equilibrium(cpu, other, family.Batch, family.Table, family.Labels, sensitivities);
    }
}
