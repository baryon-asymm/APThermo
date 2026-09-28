using APThermo.Thermo;
using ILGPU.Runtime;

namespace APThermo.Equilibrium.Tests;

/// <summary>L0: the status codes on invalid input, and that nothing but the status is written.</summary>
[Collection(CpuFixture.CollectionName)]
public sealed class InvalidInputTests
{
    private static readonly string[] Elements = ["H", "O"];
    private static readonly string[] Species = ["H2", "O2", "H2O", "H", "O", "OH", "H2O(L)"];
    private static readonly double[] OneElement = [1.0];

    /// <summary>An empty table is invalid input and writes nothing else.</summary>
    [Fact]
    public void AnEmptyTableIsInvalidInputAndWritesNothingElse()
    {
        var accelerator = CpuFixture.Shared.Accelerator;
        using var doubles = accelerator.Allocate1D<double>(ScratchLayout.DoublesPerCase(0, 0));
        using var ints = accelerator.Allocate1D<int>(ScratchLayout.IntsPerCase(0, 0));
        using var moles = accelerator.Allocate1D([double.NaN]);
        using var multipliers = accelerator.Allocate1D([double.NaN]);
        using var state = accelerator.Allocate1D([new MixtureState { Temperature = double.NaN }]);
        using var status = accelerator.Allocate1D([-1]);
        using var iterations = accelerator.Allocate1D([-1]);
        using var elements = accelerator.Allocate1D(OneElement);
        var table = new SpeciesTableView(
            speciesCount: 0, gasCount: 0, elementCount: 0,
            molarMass: default, formationEnthalpy: default, stoichiometry: default,
            intervalStart: default, intervalCount: default,
            intervalBounds: default, exponents: default, coefficients: default);
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

    /// <summary>Invalid inputs are reported as such.</summary>
    [Theory]
    [InlineData(new[] { 0.0, 0.0 }, 1e5, 3000.0)]      // every abundance zero
    [InlineData(new[] { 0.1, -0.05 }, 1e5, 3000.0)]    // a negative abundance
    [InlineData(new[] { 0.1, 0.05 }, 0.0, 3000.0)]     // no pressure
    [InlineData(new[] { 0.1, 0.05 }, -1e5, 3000.0)]    // negative pressure
    [InlineData(new[] { 0.1, 0.05 }, 1e5, 0.0)]        // tp without a temperature
    public void InvalidInputsAreReportedAsSuch(double[] elementMoles, double pressure, double temperature)
    {
        var table = SpeciesTable.Build(CpuFixture.Shared.Database, Elements, Species);
        var problem = new EquilibriumCase(table, ProblemKind.AssignedTemperaturePressure, pressure, temperature, 0.0, elementMoles);
        var solution = HostSolver.Solve(CpuFixture.Shared.Accelerator, problem);
        Assert.Equal(CaseStatus.InvalidInput, solution.Status);
        Assert.Equal(0, solution.Iterations);
    }

    /// <summary>A valid small case converges.</summary>
    [Fact]
    public void AValidSmallCaseConverges()
    {
        var table = SpeciesTable.Build(CpuFixture.Shared.Database, Elements, Species);
        var problem = new EquilibriumCase(table, ProblemKind.AssignedTemperaturePressure, 1e5, 3000.0, 0.0, [0.1, 0.05]);
        var solution = HostSolver.Solve(CpuFixture.Shared.Accelerator, problem);
        Assert.Equal(CaseStatus.Ok, solution.Status);
        Assert.True(solution.Iterations > 0);
        Assert.True(solution.Moles[table.IndexOf("H2O")] > solution.Moles[table.IndexOf("O2")], "stoichiometric hydrogen and oxygen burn mostly to water");
    }

    /// <summary>Frozen mode without a composition is invalid input.</summary>
    [Fact]
    public void FrozenModeWithoutACompositionIsInvalidInput()
    {
        var table = SpeciesTable.Build(CpuFixture.Shared.Database, Elements, Species);
        var zeroes = new double[table.SpeciesCount];
        var problem = new EquilibriumCase(table, ProblemKind.AssignedEnthalpyPressure, 1e5, 0.0, -1e6, [0.1, 0.05]);
        var solution = HostSolver.SolveFrozen(CpuFixture.Shared.Accelerator, problem, zeroes);
        Assert.Equal(CaseStatus.InvalidInput, solution.Status);
    }

    /// <summary>
    /// A composition with a non-finite or negative mole number, gaseous or condensed, is invalid input for every
    /// problem kind (BOOT.md, the audit's finding 5, 2026-09-26): the prior check only rejected a gas sum that was
    /// not positive, so a NaN, infinite or negative mole number elsewhere in the composition passed through to the
    /// state and performance formulas below. The validation runs before any kind-specific branch, so tp, hp and sp
    /// all take the same path; the theory names all three rather than assuming it.
    /// </summary>
    [Theory]
    [InlineData(ProblemKind.AssignedTemperaturePressure, "H2", -1.0)]
    [InlineData(ProblemKind.AssignedTemperaturePressure, "H2", double.NaN)]
    [InlineData(ProblemKind.AssignedTemperaturePressure, "H2", double.PositiveInfinity)]
    [InlineData(ProblemKind.AssignedTemperaturePressure, "H2O(L)", -1.0)]
    [InlineData(ProblemKind.AssignedTemperaturePressure, "H2O(L)", double.NaN)]
    [InlineData(ProblemKind.AssignedTemperaturePressure, "H2O(L)", double.PositiveInfinity)]
    [InlineData(ProblemKind.AssignedEnthalpyPressure, "H2", -1.0)]
    [InlineData(ProblemKind.AssignedEnthalpyPressure, "H2", double.NaN)]
    [InlineData(ProblemKind.AssignedEnthalpyPressure, "H2", double.PositiveInfinity)]
    [InlineData(ProblemKind.AssignedEnthalpyPressure, "H2O(L)", -1.0)]
    [InlineData(ProblemKind.AssignedEnthalpyPressure, "H2O(L)", double.NaN)]
    [InlineData(ProblemKind.AssignedEnthalpyPressure, "H2O(L)", double.PositiveInfinity)]
    [InlineData(ProblemKind.AssignedEntropyPressure, "H2", -1.0)]
    [InlineData(ProblemKind.AssignedEntropyPressure, "H2", double.NaN)]
    [InlineData(ProblemKind.AssignedEntropyPressure, "H2", double.PositiveInfinity)]
    [InlineData(ProblemKind.AssignedEntropyPressure, "H2O(L)", -1.0)]
    [InlineData(ProblemKind.AssignedEntropyPressure, "H2O(L)", double.NaN)]
    [InlineData(ProblemKind.AssignedEntropyPressure, "H2O(L)", double.PositiveInfinity)]
    public void FrozenModeRejectsAnInvalidMoleNumberGaseousOrCondensed(ProblemKind kind, string name, double invalid)
    {
        var table = SpeciesTable.Build(CpuFixture.Shared.Database, Elements, Species);
        var composition = new double[table.SpeciesCount];
        composition[table.IndexOf("H2")] = 0.1;
        composition[table.IndexOf("O2")] = 0.05;
        composition[table.IndexOf(name)] = invalid;
        var temperature = kind == ProblemKind.AssignedTemperaturePressure ? 3000.0 : 0.0;
        var target = kind switch
        {
            ProblemKind.AssignedEnthalpyPressure => -1e6,
            ProblemKind.AssignedEntropyPressure => 1e4,
            ProblemKind.AssignedTemperaturePressure => 0.0,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "unhandled problem kind"),
        };
        var problem = new EquilibriumCase(table, kind, 1e5, temperature, target, [0.1, 0.05]);
        var solution = HostSolver.SolveFrozen(CpuFixture.Shared.Accelerator, problem, composition);
        Assert.Equal(CaseStatus.InvalidInput, solution.Status);
    }

    /// <summary>
    /// A frozen tp at a non-finite temperature is InvalidInput; at a finite but absurdly low one, below the frozen
    /// floor (BOOT.md, 0.8 of the lowest lower bound of the gases present), it is TemperatureOutOfRange (the second
    /// hidden-defect audit's finding F4, 2026-09-28). Before the fix all three returned `Ok`: +∞ and 1e-300 K with
    /// every state property NaN, and (by a separate probe, not repeated here) 1e6 K with an unphysical finite Cp.
    /// </summary>
    [Theory]
    [InlineData(double.PositiveInfinity, CaseStatus.InvalidInput)]
    [InlineData(double.NaN, CaseStatus.InvalidInput)]
    [InlineData(1e-300, CaseStatus.TemperatureOutOfRange)]
    public void FrozenTpAtANonFiniteOrAbsurdTemperatureIsRefused(double temperature, CaseStatus expected)
    {
        var table = SpeciesTable.Build(CpuFixture.Shared.Database, Elements, Species);
        var composition = new double[table.SpeciesCount];
        composition[table.IndexOf("H2")] = 0.1;
        composition[table.IndexOf("O2")] = 0.05;
        var problem = new EquilibriumCase(table, ProblemKind.AssignedTemperaturePressure, 1e5, temperature, 0.0, [0.1, 0.05]);
        var solution = HostSolver.SolveFrozen(CpuFixture.Shared.Accelerator, problem, composition);
        Assert.Equal(expected, solution.Status);
    }

    /// <summary>
    /// A tp Solve at +∞ is InvalidInput (BOOT.md, finding F4): the prior check refused only a non-positive
    /// temperature, so +∞ passed it and reached a singular Newton system instead of being refused as bad input.
    /// </summary>
    [Fact]
    public void SolveTpAtPositiveInfinityIsInvalidInput()
    {
        var table = SpeciesTable.Build(CpuFixture.Shared.Database, Elements, Species);
        var problem = new EquilibriumCase(table, ProblemKind.AssignedTemperaturePressure, 1e5, double.PositiveInfinity, 0.0, [0.1, 0.05]);
        var solution = HostSolver.Solve(CpuFixture.Shared.Accelerator, problem);
        Assert.Equal(CaseStatus.InvalidInput, solution.Status);
    }
}
