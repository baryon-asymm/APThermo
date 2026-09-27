using APThermo.Thermo;

namespace APThermo.Performance.Tests;

/// <summary>
/// L0: the isentropic-expansion invariant (BOOT.md, Invariants, 2026-09-26): a station whose entropy departs from
/// the chamber's by more than the relative tolerance is <c>NotConverged</c>, even though every fixture's own solve
/// already lands within it (no fixture reaches the path, as the equilibrium node's sp solve targets the chamber's
/// entropy by construction). The throat's own converged state is perturbed directly, so the check itself is driven
/// without needing a solver defect to trigger it.
/// </summary>
[Collection(CpuFixture.CollectionName)]
public sealed class EntropyCheckTests
{
    /// <summary>A station whose entropy drifts from the chamber's by more than the tolerance is reported as NotConverged.</summary>
    [Fact]
    public void AStationWhoseEntropyDriftsFromTheChamberIsNotConverged()
    {
        var inputs = RocketInputs.Of(RocketHost.Load("lox-lh2_of6_pc7MPa_shiftingEquilibrium"));
        var table = SpeciesTable.Build(CpuFixture.Shared.Database, inputs.System.Elements, inputs.System.Products);
        using var rocketCase = new RocketCase(CpuFixture.Shared.Accelerator, table, inputs);
        var context = rocketCase.Context;
        Assert.Equal(CaseStatus.Ok, ChamberSolve.At(in context, out var chamber));
        Assert.Equal(CaseStatus.Ok, ThroatSearch.At(in context, in chamber, out var throat));

        var result = context.Result;
        var state = result.Stations[RocketSolver.Throat];
        state.Entropy *= 1.0 + 10.0 * RocketSolver.EntropyTolerance;
        result.Stations[RocketSolver.Throat] = state;
        result.StationStatus[RocketSolver.Throat] = (int)CaseStatus.Ok;

        var perturbedInputs = new StationFigureInputs(state.Velocity, 1.0, 1.0, throat.CharacteristicVelocity);
        StationFigures.Write(in context, RocketSolver.Throat, in perturbedInputs, chamber.Entropy);

        Assert.Equal(CaseStatus.NotConverged, (CaseStatus)result.StationStatus[RocketSolver.Throat]);
    }

    /// <summary>A station within the tolerance is left as whatever status it already carries.</summary>
    [Fact]
    public void AStationWithinTheEntropyToleranceIsUnaffected()
    {
        var inputs = RocketInputs.Of(RocketHost.Load("lox-lh2_of6_pc7MPa_shiftingEquilibrium"));
        var table = SpeciesTable.Build(CpuFixture.Shared.Database, inputs.System.Elements, inputs.System.Products);
        using var rocketCase = new RocketCase(CpuFixture.Shared.Accelerator, table, inputs);
        var context = rocketCase.Context;
        Assert.Equal(CaseStatus.Ok, ChamberSolve.At(in context, out var chamber));
        Assert.Equal(CaseStatus.Ok, ThroatSearch.At(in context, in chamber, out var throat));

        var result = context.Result;
        var state = result.Stations[RocketSolver.Throat];
        state.Entropy *= 1.0 + 0.1 * RocketSolver.EntropyTolerance;
        result.Stations[RocketSolver.Throat] = state;
        result.StationStatus[RocketSolver.Throat] = (int)CaseStatus.Ok;

        var perturbedInputs = new StationFigureInputs(state.Velocity, 1.0, 1.0, throat.CharacteristicVelocity);
        StationFigures.Write(in context, RocketSolver.Throat, in perturbedInputs, chamber.Entropy);

        Assert.Equal(CaseStatus.Ok, (CaseStatus)result.StationStatus[RocketSolver.Throat]);
    }
}
