using APThermo.Thermo;

namespace APThermo.Performance.Tests;

/// <summary>Pressure-assigned exits never accept nonfinite performance figures.</summary>
[Collection(CpuFixture.CollectionName)]
public sealed class FinitePressureRatioTests
{
    /// <summary>A zero-velocity exit cannot publish an infinite area or vacuum impulse as Ok.</summary>
    [Fact]
    public void AZeroVelocityExitIsNotAccepted()
    {
        var inputs = RocketInputs.Of(RocketHost.Load("lox-lh2_of6_pc7MPa_shiftingEquilibrium"));
        var table = SpeciesTable.Build(CpuFixture.Shared.Database, inputs.System.Elements, inputs.System.Products);
        using var rocketCase = new RocketCase(CpuFixture.Shared.Accelerator, table, inputs);
        var context = rocketCase.Context;
        const int station = RocketLayout.FixedStations;
        context.Result.Stations[station] = new MixtureState { Pressure = 10000.0, Density = 0.005, SoundSpeed = 1600.0, Entropy = 100.0 };
        context.Result.StationStatus[station] = (int)CaseStatus.Ok;
        var figures = new StationFigureInputs(0.0, double.PositiveInfinity, 1.0000000000001, 2000.0);
        StationFigures.Write(in context, station, in figures, 100.0);
        Assert.Equal(CaseStatus.NotConverged, (CaseStatus)context.Result.StationStatus[station]);
    }

    /// <summary>A near-chamber exit is either refused or has finite figures for each flow model.</summary>
    [Theory]
    [InlineData(FlowModel.ShiftingEquilibrium)]
    [InlineData(FlowModel.FrozenAtChamber)]
    [InlineData(FlowModel.FrozenAtThroat)]
    public void APressureRatioJustAboveOneCannotReturnInfiniteFigures(FlowModel flow)
    {
        var inputs = RocketInputs.Of(RocketHost.Load("lox-lh2_of6_pc7MPa_shiftingEquilibrium")) with
        {
            ChamberPressure = 10000.0,
            Mixture = new Mixture([0.4960612734885013, 0.031251171918946962], -2438056.578507193),
            Flow = flow,
            Exits = new ExitPlan([1.0000000000001], [ExitSpecification.PressureRatio]),
        };
        var result = RocketHost.Solve(CpuFixture.Shared, inputs);
        var station = RocketLayout.FixedStations;
        if (result.Outcome.StationStatus[station] == CaseStatus.Ok)
        {
            var figures = result.Outcome.Figures[station];
            Assert.True(double.IsFinite(figures.AreaRatio), $"area={figures.AreaRatio:R}");
            Assert.True(double.IsFinite(figures.VacuumSpecificImpulse), $"Ivac={figures.VacuumSpecificImpulse:R}");
        }
    }

    /// <summary>A conventional pressure-assigned exit remains accepted and finite.</summary>
    [Theory]
    [InlineData(FlowModel.ShiftingEquilibrium)]
    [InlineData(FlowModel.FrozenAtChamber)]
    [InlineData(FlowModel.FrozenAtThroat)]
    public void AnOrdinaryPressureRatioRemainsAccepted(FlowModel flow)
    {
        var inputs = RocketInputs.Of(RocketHost.Load("lox-lh2_of6_pc7MPa_shiftingEquilibrium")) with
        {
            Flow = flow,
            Exits = new ExitPlan([2.5], [ExitSpecification.PressureRatio]),
        };
        var result = RocketHost.Solve(CpuFixture.Shared, inputs);
        var station = RocketLayout.FixedStations;
        Assert.Equal(CaseStatus.Ok, result.Outcome.StationStatus[station]);
        Assert.True(double.IsFinite(result.Outcome.Figures[station].AreaRatio));
        Assert.True(double.IsFinite(result.Outcome.Figures[station].VacuumSpecificImpulse));
    }
}
