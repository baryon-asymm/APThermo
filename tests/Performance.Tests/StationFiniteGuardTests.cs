using APThermo.Thermo;

namespace APThermo.Performance.Tests;

/// <summary>Synthetic station inputs isolate independent finite-output guards without a physical solve.</summary>
[Collection(CpuFixture.CollectionName)]
public sealed class StationFiniteGuardTests
{
    /// <summary>A lone infinite Mach or vacuum impulse is refused while every other guarded output stays finite.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AnIndividualNonfiniteFigureIsRefused(bool mach)
    {
        var inputs = RocketInputs.Of(RocketHost.Load("lox-lh2_of6_pc7MPa_shiftingEquilibrium"));
        var table = SpeciesTable.Build(CpuFixture.Shared.Database, inputs.System.Elements, inputs.System.Products);
        using var rocketCase = new RocketCase(CpuFixture.Shared.Accelerator, table, inputs);
        var context = rocketCase.Context;
        const int station = RocketLayout.FixedStations;
        context.Result.Stations[station] = new MixtureState { Pressure = 1.0, Density = mach ? 1.0 : 1e-310, SoundSpeed = mach ? 1e-310 : 1.0, Entropy = 100.0 };
        context.Result.StationStatus[station] = (int)CaseStatus.Ok;
        var arguments = new StationFigureInputs(1.0, 1.0, 1.0, 1.0);
        StationFigures.Write(in context, station, in arguments, 100.0);
        var state = context.Result.Stations[station];
        var figures = context.Result.Figures[station];
        Assert.False(double.IsFinite(mach ? state.Mach : figures.VacuumSpecificImpulse));
        Assert.True(double.IsFinite(mach ? figures.VacuumSpecificImpulse : state.Mach));
        Assert.True(double.IsFinite(state.Velocity) && double.IsFinite(figures.AreaRatio) && double.IsFinite(figures.PressureRatio));
        Assert.True(double.IsFinite(figures.CharacteristicVelocity) && double.IsFinite(figures.ThrustCoefficient) && double.IsFinite(figures.SpecificImpulse));
        Assert.Equal(CaseStatus.NotConverged, (CaseStatus)context.Result.StationStatus[station]);
    }
}
