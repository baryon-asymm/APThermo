using APThermo.Thermo;

namespace APThermo.Performance.Tests;

/// <summary>The real area iteration must invoke its final residual guard.</summary>
[Collection(CpuFixture.CollectionName)]
public sealed class AreaRatioDispatchTests
{
    /// <summary>Nineteen subsonic passes leave one final, supersonic but insufficiently accurate pass.</summary>
    [Fact]
    public void AFinalReportTolerancePassWithExcessAreaResidualIsRejected()
    {
        var inputs = RocketInputs.Of(RocketHost.Load("lox-lh2_of6_pc7MPa_shiftingEquilibrium")) with { Flow = FlowModel.FrozenAtThroat };
        var table = SpeciesTable.Build(CpuFixture.Shared.Database, inputs.System.Elements, inputs.System.Products);
        using var rocketCase = new RocketCase(CpuFixture.Shared.Accelerator, table, inputs);
        var context = rocketCase.Context;
        Assert.Equal(CaseStatus.Ok, ChamberSolve.At(in context, out var chamber));
        Assert.Equal(CaseStatus.Ok, ThroatSearch.At(in context, in chamber, out var physicalThroat));
        var throat = new ThroatReference(physicalThroat.Pressure, 20.0 * physicalThroat.MassFlux,
            physicalThroat.CharacteristicVelocity, physicalThroat.LogPressureRatio, physicalThroat.GammaS);
        const int exit = RocketLayout.FixedStations;
        var throatTemperature = context.Result.Stations[RocketSolver.Throat].Temperature;
        var finalLogPressureRatio = throat.LogPressureRatio + 0.095;
        StationSolve.CopyComposition(in context, RocketSolver.Throat, exit);
        var request = new StationRequest(exit, chamber.Pressure * Math.Exp(-finalLogPressureRatio), throatTemperature,
            chamber.Entropy, StationFlow.Frozen);
        Assert.True(StationSolve.At(in context, in request));
        var probe = context.Result.Stations[exit];
        var velocitySquared = StationFigures.VelocitySquared(chamber.Enthalpy, in probe);
        var soundSquared = probe.SoundSpeed * probe.SoundSpeed;
        Assert.True(velocitySquared > soundSquared);
        var derivative = (velocitySquared - soundSquared) / (probe.GammaS * velocitySquared);
        var actual = throat.MassFlux / (probe.Density * Math.Sqrt(velocitySquared));
        var requested = actual * Math.Exp(derivative * 3.9e-5);
        Assert.True(requested > RocketSolver.ExtrapolationAreaRatio);
        Assert.True(Math.Abs(actual - requested) > RocketSolver.AreaRatioResidualTolerance * requested);
        var estimate = new ExitEstimate
        {
            Temperature = throatTemperature,
            Extrapolable = true,
            LogPressureRatio = finalLogPressureRatio - 1.9,
            LogAreaRatio = Math.Log(requested),
            Derivative = 1.0,
        };
        StationSolve.CopyComposition(in context, RocketSolver.Throat, exit);
        var outcome = AreaRatioIteration.At(in context, in chamber, in throat, requested, exit, ref estimate);
        Assert.Equal(ExitOutcome.NotMet, outcome);
        Assert.Equal(CaseStatus.NotConverged, (CaseStatus)context.Result.StationStatus[exit]);
    }
}
