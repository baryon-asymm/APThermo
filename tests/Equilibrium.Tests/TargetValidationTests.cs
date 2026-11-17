using APThermo.Thermo;
using ILGPU.Runtime;

namespace APThermo.Equilibrium.Tests;

/// <summary>Input validation happens before an attempt mutates any output.</summary>
[Collection(CpuFixture.CollectionName)]
public sealed class TargetValidationTests
{
    /// <summary>Nonfinite assigned h/s is invalid before cold, warm or frozen work.</summary>
    [Theory]
    [InlineData(ProblemKind.AssignedEnthalpyPressure, double.NaN)]
    [InlineData(ProblemKind.AssignedEnthalpyPressure, double.PositiveInfinity)]
    [InlineData(ProblemKind.AssignedEnthalpyPressure, double.NegativeInfinity)]
    [InlineData(ProblemKind.AssignedEntropyPressure, double.NaN)]
    [InlineData(ProblemKind.AssignedEntropyPressure, double.PositiveInfinity)]
    [InlineData(ProblemKind.AssignedEntropyPressure, double.NegativeInfinity)]
    public void NonfiniteTargetIsRejectedBeforeAnyAttempt(ProblemKind kind, double target)
    {
        var table = SpeciesTable.Build(CpuFixture.Shared.Database, ["H", "O"], ["H2", "O2", "H2O", "H", "O", "OH"]);
        var input = new EquilibriumCase(table, kind, 1e5, 3000.0, target, [0.1, 0.05]);
        var seed = new double[table.SpeciesCount];
        seed[table.IndexOf("H2")] = 0.1;
        seed[table.IndexOf("O2")] = 0.05;
        var cold = HostSolver.Solve(CpuFixture.Shared.Accelerator, input);
        var warm = HostSolver.Solve(CpuFixture.Shared.Accelerator, input, seed);
        var frozen = HostSolver.SolveFrozen(CpuFixture.Shared.Accelerator, input, seed);
        AssertInvalid(cold);
        AssertInvalid(warm);
        AssertInvalid(frozen);
        Assert.Equal(seed, warm.Moles);
        Assert.Equal(seed, frozen.Moles);
    }

    /// <summary>A frozen given invalid estimate is not silently replaced by the default.</summary>
    [Theory]
    [InlineData(ProblemKind.AssignedEnthalpyPressure, double.NaN)]
    [InlineData(ProblemKind.AssignedEnthalpyPressure, double.NegativeInfinity)]
    [InlineData(ProblemKind.AssignedEnthalpyPressure, -1.0)]
    [InlineData(ProblemKind.AssignedEntropyPressure, double.NaN)]
    [InlineData(ProblemKind.AssignedEntropyPressure, double.NegativeInfinity)]
    [InlineData(ProblemKind.AssignedEntropyPressure, -1.0)]
    public void FrozenGivenInvalidEstimateIsRejected(ProblemKind kind, double estimate)
    {
        var table = SpeciesTable.Build(CpuFixture.Shared.Database, ["H", "O"], ["H2", "O2", "H2O", "H", "O", "OH"]);
        var composition = new double[table.SpeciesCount];
        composition[table.IndexOf("H2")] = 0.1;
        composition[table.IndexOf("O2")] = 0.05;
        var target = kind == ProblemKind.AssignedEnthalpyPressure ? -1e6 : 1e4;
        var input = new EquilibriumCase(table, kind, 1e5, estimate, target, [0.1, 0.05]);
        AssertInvalid(HostSolver.SolveFrozen(CpuFixture.Shared.Accelerator, input, composition));
    }

    /// <summary>An invalid target leaves a previously solved state and both output arrays untouched.</summary>
    [Theory]
    [InlineData(ProblemKind.AssignedEnthalpyPressure)]
    [InlineData(ProblemKind.AssignedEntropyPressure)]
    public void InvalidTargetLeavesPreviousTpResultUntouched(ProblemKind kind)
    {
        var accelerator = CpuFixture.Shared.Accelerator;
        var table = SpeciesTable.Build(CpuFixture.Shared.Database, ["H", "O"], ["H2", "O2", "H2O", "H", "O", "OH"]);
        using var buffers = SpeciesTableBuffers.Upload(accelerator, table);
        using var elements = accelerator.Allocate1D([0.1, 0.05]);
        using var doubles = accelerator.Allocate1D<double>(ScratchLayout.DoublesPerCase(table.SpeciesCount, table.ElementCount));
        using var ints = accelerator.Allocate1D<int>(ScratchLayout.IntsPerCase(table.SpeciesCount, table.ElementCount));
        using var moles = accelerator.Allocate1D<double>(table.SpeciesCount);
        using var multipliers = accelerator.Allocate1D<double>(table.ElementCount);
        using var state = accelerator.Allocate1D<MixtureState>(1);
        using var status = accelerator.Allocate1D<int>(1);
        using var iterations = accelerator.Allocate1D<int>(1);
        using var recoveryStates = accelerator.Allocate1D<MixtureState>(1);
        var scratch = EquilibriumScratch.Slice(doubles.View, ints.View, table.SpeciesCount, table.ElementCount, recoveryStates.View);
        var result = new EquilibriumResult(moles.View, multipliers.View, state.View, status.View, iterations.View);
        var tableView = buffers.View;
        var tp = new EquilibriumProblem(ProblemKind.AssignedTemperaturePressure, 1e5, 3000.0, 0.0, elements.View);
        EquilibriumSolver.Solve(in tableView, in tp, in scratch, in result, false);
        Assert.Equal(CaseStatus.Ok, (CaseStatus)result.Status[0]);
        var previousState = result.State[0];
        var previousMoles = moles.GetAsArray1D();
        var previousMultipliers = multipliers.GetAsArray1D();
        var invalid = new EquilibriumProblem(kind, 1e5, 3000.0, double.NaN, elements.View);
        EquilibriumSolver.Solve(in tableView, in invalid, in scratch, in result, true);
        Assert.Equal(CaseStatus.InvalidInput, (CaseStatus)result.Status[0]);
        Assert.Equal(0, result.Iterations[0]);
        Assert.Equal(previousState, result.State[0]);
        Assert.Equal(previousMoles, moles.GetAsArray1D());
        Assert.Equal(previousMultipliers, multipliers.GetAsArray1D());
    }

    private static void AssertInvalid(HostSolution solution)
    {
        Assert.Equal(CaseStatus.InvalidInput, solution.Status);
        Assert.Equal(0, solution.Iterations);
        Assert.Equal(default, solution.State);
    }
}
