using AerospacePropellantThermodynamics.Thermo;
using ILGPU;
using ILGPU.Runtime;

namespace AerospacePropellantThermodynamics.Equilibrium.Tests;

/// <summary>L0: the status codes on invalid input, and that nothing but the status is written.</summary>
[Collection(CpuCollection.Name)]
public sealed class InvalidInputTests(CpuFixture fixture)
{
    private static readonly string[] Elements = ["H", "O"];
    private static readonly string[] Species = ["H2", "O2", "H2O", "H", "O", "OH", "H2O(L)"];

    [Fact]
    public void An_empty_table_is_invalid_input_and_writes_nothing_else()
    {
        var accelerator = fixture.Accelerator;
        using var doubles = accelerator.Allocate1D<double>(ScratchLayout.DoublesPerCase(0, 0));
        using var ints = accelerator.Allocate1D<int>(ScratchLayout.IntsPerCase(0, 0));
        using var moles = accelerator.Allocate1D(new[] { double.NaN });
        using var multipliers = accelerator.Allocate1D(new[] { double.NaN });
        using var state = accelerator.Allocate1D(new[] { new MixtureState { Temperature = double.NaN } });
        using var status = accelerator.Allocate1D(new[] { -1 });
        using var iterations = accelerator.Allocate1D(new[] { -1 });
        using var elements = accelerator.Allocate1D(new[] { 1.0 });
        var table = new SpeciesTableView(0, 0, 0, default, default, default, default, default, default, default, default);
        var problem = new EquilibriumProblem(ProblemKind.AssignedTemperaturePressure, 1e5, 3000.0, 0.0, elements.View);
        var scratch = EquilibriumScratch.Slice(doubles.View, ints.View, 0, 0);
        var result = new EquilibriumResult(moles.View, multipliers.View, state.View, status.View, iterations.View);

        EquilibriumSolver.Solve(in table, in problem, in scratch, in result, false);

        Assert.Equal(CaseStatus.InvalidInput, (CaseStatus)status.GetAsArray1D()[0]);
        Assert.Equal(0, iterations.GetAsArray1D()[0]);
        Assert.True(double.IsNaN(moles.GetAsArray1D()[0]), "moles were written");
        Assert.True(double.IsNaN(multipliers.GetAsArray1D()[0]), "multipliers were written");
        Assert.True(double.IsNaN(state.GetAsArray1D()[0].Temperature), "the state was written");
    }

    [Theory]
    [InlineData(new[] { 0.0, 0.0 }, 1e5, 3000.0)]      // every abundance zero
    [InlineData(new[] { 0.1, -0.05 }, 1e5, 3000.0)]    // a negative abundance
    [InlineData(new[] { 0.1, 0.05 }, 0.0, 3000.0)]     // no pressure
    [InlineData(new[] { 0.1, 0.05 }, -1e5, 3000.0)]    // negative pressure
    [InlineData(new[] { 0.1, 0.05 }, 1e5, 0.0)]        // tp without a temperature
    public void Invalid_inputs_are_reported_as_such(double[] elementMoles, double pressure, double temperature)
    {
        var table = SpeciesTable.Build(fixture.Database, Elements, Species);
        var problem = new EquilibriumCase(table, ProblemKind.AssignedTemperaturePressure, pressure, temperature, 0.0, elementMoles);
        var solution = HostSolver.Solve(fixture.Accelerator, problem);
        Assert.Equal(CaseStatus.InvalidInput, solution.Status);
        Assert.Equal(0, solution.Iterations);
    }

    [Fact]
    public void A_valid_small_case_converges()
    {
        var table = SpeciesTable.Build(fixture.Database, Elements, Species);
        var problem = new EquilibriumCase(table, ProblemKind.AssignedTemperaturePressure, 1e5, 3000.0, 0.0, [0.1, 0.05]);
        var solution = HostSolver.Solve(fixture.Accelerator, problem);
        Assert.Equal(CaseStatus.Ok, solution.Status);
        Assert.True(solution.Iterations > 0);
        Assert.True(solution.Moles[table.IndexOf("H2O")] > solution.Moles[table.IndexOf("O2")], "stoichiometric hydrogen and oxygen burn mostly to water");
    }

    [Fact]
    public void Frozen_mode_without_a_composition_is_invalid_input()
    {
        var table = SpeciesTable.Build(fixture.Database, Elements, Species);
        var zeroes = new double[table.SpeciesCount];
        var problem = new EquilibriumCase(table, ProblemKind.AssignedEnthalpyPressure, 1e5, 0.0, -1e6, [0.1, 0.05]);
        var solution = HostSolver.SolveFrozen(fixture.Accelerator, problem, zeroes);
        Assert.Equal(CaseStatus.InvalidInput, solution.Status);
    }
}
