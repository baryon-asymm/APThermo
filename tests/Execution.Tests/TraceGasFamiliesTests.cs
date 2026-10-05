using APThermo.Equilibrium;
using APThermo.Harness;
using APThermo.Thermo;
using Xunit.Abstractions;

namespace APThermo.Execution.Tests;

/// <summary>
/// The 0.2.2 families of the trace-gas pass (<see cref="TraceGasFamilies"/>) on the CPU accelerator alone, the half of
/// <see cref="CudaTests.ATraceGasFamilyOnCudaMatchesTheCpuAccelerator"/> that needs no GPU: what each family holds, that every case of
/// it reaches the pass, and the check of the launch budget.
/// </summary>
[Collection(EngineFixture.CollectionName)]
public sealed class TraceGasFamiliesTests(ITestOutputHelper output)
{
    /// <summary>The cases of the CPU launch of the budget check: the family's own hp and sp cases repeated, enough to time a launch and few enough for the fast set.</summary>
    private const int LaunchCases = 64;

    /// <summary>The fewest cases a family keeps: a grid whose states all end elsewhere is a family that stands for nothing.</summary>
    private const int FewestCases = 16;

    /// <summary>The temperature of the KCl state that no trace gas decides: the liquid under its own vapour, above the junction.</summary>
    private const double OrdinaryTemperature = 2000.0;

    /// <summary>The names of the families as theory data, delegating to <see cref="TraceGasFamilies.Names"/>.</summary>
    public static TheoryData<string> FamilyNames() => TraceGasFamilies.Names();

    /// <summary>
    /// A family keeps the cases it stands for, on the CPU accelerator: every case ends <c>Ok</c> or <c>NoGasPhase</c>, as the host solver
    /// ended it (status, iterations, temperature), every <c>Ok</c> tp case was settled by the pass and every hp and sp case by the bracket,
    /// and every kind of problem the family's grids ask for is among its cases.
    /// </summary>
    [Theory]
    [MemberData(nameof(FamilyNames))]
    public void AFamilyHoldsTheCasesItStandsFor(string name)
    {
        var family = TraceGasFamilies.Family(EngineFixture.Shared.Database, name);
        output.WriteLine($"{name}: {family.Batch.Count} cases kept, {family.Gasless} gasless, {family.Dropped} dropped");
        Assert.True(family.Batch.Count >= FewestCases, $"{name} keeps {family.Batch.Count} cases, fewer than {FewestCases}");
        foreach (var kind in TraceGasFamilies.KindsAsked(name))
        {
            Assert.Contains(kind, family.Batch.Kind);
        }

        using var tables = EngineFixture.Shared.Cpu.Upload(family.Table);
        var run = EngineFixture.Shared.Cpu.Run(tables, family.Batch);
        Assert.Equal(family.Gasless, run.Status.Count(status => status == CaseStatus.NoGasPhase));
        for (var k = 0; k < family.Batch.Count; k++)
        {
            var solved = HostSolves.EquilibriumProbed(EngineFixture.Shared.Cpu.IlgpuAccelerator, tables.SpeciesBuffers, family.Batch, k);
            Assert.True(run.Status[k] is CaseStatus.Ok or CaseStatus.NoGasPhase, $"{family.Labels[k]} ends {run.Status[k]}");
            Assert.True(solved.Status == run.Status[k] && solved.Iterations == run.Iterations[k], $"{family.Labels[k]}: the host ends {solved.Status} in {solved.Iterations} steps, the accelerator {run.Status[k]} in {run.Iterations[k]}");
            var reaches = family.Batch.Kind[k] == ProblemKind.AssignedTemperaturePressure
                ? solved.Status == CaseStatus.NoGasPhase || TraceGasFamilies.ReachesThePass(solved)
                : TraceGasFamilies.BracketsToThePass(solved);
            Assert.True(reaches, $"{family.Labels[k]} does not reach the pass: {solved.Status} in {solved.Iterations} steps, verdict anchor {solved.Anchored}, bracket {solved.Bracketed}");
        }
    }

    /// <summary>
    /// The observable of the pass is not degenerate: a KCl state with a trace excess of chlorine whose ordinary attempt fails is anchored
    /// and takes more than one attempt's steps, the same mixture at 2000 K ends in one ordinary attempt with neither anchor nor bracket,
    /// an hp state of it brackets only where the ordinary iteration fails, and filling the scratch with a value no solve writes moves no
    /// bit of the result.
    /// </summary>
    [Fact]
    public void ThePassObservableTellsAStateTheOrdinaryAttemptSettlesFromOneThePassSettles()
    {
        var database = EngineFixture.Shared.Database;
        string[] elements = ["K", "CL"];
        var table = GasPlateauFamilies.TableOver(database, elements);
        var moles = GasPlateauFamilies.ElementMolesOf(database, elements, [1.0, 1.0 + 1.0e-10]);
        using var tables = EngineFixture.Shared.Cpu.Upload(table);
        var accelerator = EngineFixture.Shared.Cpu.IlgpuAccelerator;
        var cold = Case(table, moles, ProblemKind.AssignedTemperaturePressure, 900.0, 0.0);
        var ordinary = Case(table, moles, ProblemKind.AssignedTemperaturePressure, OrdinaryTemperature, 0.0);
        var coldSolved = HostSolves.EquilibriumProbed(accelerator, tables.SpeciesBuffers, cold, 0);
        var ordinarySolved = HostSolves.EquilibriumProbed(accelerator, tables.SpeciesBuffers, ordinary, 0);
        Assert.True(TraceGasFamilies.ReachesThePass(coldSolved), $"{coldSolved.Status}, {coldSolved.Iterations} steps, anchor {coldSolved.Anchored}");
        Assert.False(TraceGasFamilies.ReachesThePass(ordinarySolved), $"{ordinarySolved.Status}, {ordinarySolved.Iterations} steps, anchor {ordinarySolved.Anchored}");
        Assert.False(ordinarySolved.Anchored || ordinarySolved.Bracketed);
        var easy = Case(table, moles, ProblemKind.AssignedEnthalpyPressure, 0.0, ordinarySolved.State.Enthalpy);
        Assert.False(TraceGasFamilies.BracketsToThePass(HostSolves.EquilibriumProbed(accelerator, tables.SpeciesBuffers, easy, 0)));
        foreach (var batch in new[] { cold, ordinary, easy })
        {
            var plain = HostSolves.Equilibrium(accelerator, tables.SpeciesBuffers, batch, 0);
            var probed = HostSolves.EquilibriumProbed(accelerator, tables.SpeciesBuffers, batch, 0);
            Assert.Equal(plain.Status, probed.Status);
            Assert.Equal(plain.Iterations, probed.Iterations);
            Assert.Empty(Bits.Differences(plain.State, probed.State, "state"));
            for (var j = 0; j < plain.Moles.Length; j++)
            {
                Assert.True(Bits.Same(plain.Moles[j], probed.Moles[j]), $"moles of {table.Species[j]}");
            }
        }
    }

    /// <summary>
    /// Neither condition of <see cref="TraceGasFamilies.ReachesThePass"/> suffices alone: a KCl state at the data junction takes more than
    /// one attempt's steps in an ordinary attempt (no verdict ran, no anchor), and a lithium oxide state at 2000 K carries an anchor
    /// although it ends within one attempt's steps.
    /// </summary>
    [Fact]
    public void TheTwoConditionsOfThePassObservableAreEachNecessary()
    {
        var junction = Solve(["K", "CL"], [1.0, 1.0 + 1.0e-6], 1.0e5, 1000.0);
        Assert.True(junction.Iterations > EquilibriumSolver.MaxNewtonSteps && !junction.Anchored, $"{junction.Status}, {junction.Iterations} steps, anchor {junction.Anchored}");
        Assert.False(TraceGasFamilies.ReachesThePass(junction));
        var anchored = Solve(["LI", "O"], [2.0, 1.0 + 1.0e-10], 1.0e3, OrdinaryTemperature);
        Assert.True(anchored.Iterations <= EquilibriumSolver.MaxNewtonSteps && anchored.Anchored, $"{anchored.Status}, {anchored.Iterations} steps, anchor {anchored.Anchored}");
        Assert.False(TraceGasFamilies.ReachesThePass(anchored));
    }

    /// <summary>
    /// The check of the launch-budget fact on the CPU accelerator (<see cref="CudaTests.AFamilyOfTraceGasStatesStaysWithinTheLaunchBudget"/>
    /// needs the device): a launch of the tiled hp and sp cases of the family ends every case <c>Ok</c> or <c>NoGasPhase</c> and passes a
    /// generous limit, and a limit of zero and a case that ended otherwise are refused.
    /// </summary>
    [Fact]
    public void TheLaunchBudgetCheckRefusesALaunchOverTheLimitAndACaseThatFailed()
    {
        var family = TraceGasFamilies.Family(EngineFixture.Shared.Database, TraceGasFamilies.LaunchFamily);
        var batch = TraceGasFamilies.Tiled(TraceGasFamilies.WithoutTp(family.Batch), LaunchCases);
        Assert.DoesNotContain(ProblemKind.AssignedTemperaturePressure, batch.Kind);
        using var tables = EngineFixture.Shared.Cpu.Upload(family.Table);
        var run = EngineFixture.Shared.Cpu.Run(tables, batch);
        Assert.Null(TraceGasFamilies.LaunchViolation(run, TimeSpan.FromMinutes(1)));
        Assert.Contains("above the budget", TraceGasFamilies.LaunchViolation(run, TimeSpan.Zero), StringComparison.Ordinal);
        run.Status[0] = CaseStatus.NotConverged;
        Assert.Contains("neither Ok nor NoGasPhase", TraceGasFamilies.LaunchViolation(run, TimeSpan.FromMinutes(1)), StringComparison.Ordinal);
    }

    private static HostEquilibriumCase Solve(string[] elements, double[] ratio, double pressure, double temperature)
    {
        var table = GasPlateauFamilies.TableOver(EngineFixture.Shared.Database, elements);
        var moles = GasPlateauFamilies.ElementMolesOf(EngineFixture.Shared.Database, elements, ratio);
        using var tables = EngineFixture.Shared.Cpu.Upload(table);
        return HostSolves.EquilibriumProbed(EngineFixture.Shared.Cpu.IlgpuAccelerator, tables.SpeciesBuffers,
                                            Case(table, moles, ProblemKind.AssignedTemperaturePressure, temperature, 0.0, pressure), 0);
    }

    private static EquilibriumBatch Case(SpeciesTable table, double[] moles, ProblemKind kind, double temperature, double target, double pressure = 1.0e5)
    {
        var batch = new EquilibriumBatch(1, table.ElementCount);
        batch.Kind[0] = kind;
        batch.Pressure[0] = pressure;
        batch.Temperature[0] = temperature;
        batch.Target[0] = target;
        Array.Copy(moles, batch.ElementMoles, moles.Length);
        return batch;
    }
}
