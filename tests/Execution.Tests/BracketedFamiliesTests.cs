using APThermo.Equilibrium;
using APThermo.Thermo;
using Xunit.Abstractions;

namespace APThermo.Execution.Tests;

/// <summary>
/// The 0.2.2 families of the gasless verdict and the temperature bracket (<see cref="RecoveryFamilies"/>) on the CPU accelerator alone,
/// the half of <see cref="CudaTests.ABracketedFamilyOnCudaMatchesTheCpuAccelerator"/> that needs no GPU: what each family holds and
/// the check of the launch budget. The exact comparison of two runs, each break shown red once, is <see cref="ExactComparisonTests"/>.
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
}
