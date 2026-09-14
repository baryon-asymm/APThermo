using AerospacePropellantThermodynamics.Thermo;

namespace AerospacePropellantThermodynamics.Performance.Tests;

/// <summary>
/// L0: an exit station whose iteration never leaves the subsonic side of the sonic point is NotConverged, and the stations
/// around it are Ok. No fixture reaches the path — a physical case crosses the sonic point in a pass or two — so the station
/// is driven through <see cref="AreaRatioIteration"/> from an estimate placed deep on the subsonic side.
/// </summary>
[Collection(CpuCollection.Name)]
public sealed class SubsonicStationTests(CpuFixture fixture)
{
    /// <summary>
    /// The ln(p_c/p_e) the iteration starts from. The throat of this case lies near 0.55, and the twenty subsonic steps of
    /// 0.1 the iteration takes end at −0.1, so no pass of the station can reach the sonic point.
    /// </summary>
    private const double SubsonicLogPressureRatio = -2.0;

    private const int FirstExit = RocketLayout.FixedStations;

    [Fact]
    public void A_station_that_never_leaves_the_subsonic_side_is_not_converged()
    {
        var inputs = RocketInputs.Of(RocketHost.Load("lox-lh2_of6_pc7MPa_shiftingEquilibrium"));
        var areaRatio = inputs.ExitValues[0];
        var table = SpeciesTable.Build(fixture.Database, inputs.Elements, inputs.Products);
        using var rocketCase = new RocketCase(fixture.Accelerator, table, inputs);
        var context = rocketCase.Context;
        Assert.Equal(CaseStatus.Ok, ChamberSolve.At(in context, out var chamber));
        Assert.Equal(CaseStatus.Ok, ThroatSearch.At(in context, in chamber, out var throat));
        var throatTemperature = rocketCase.Read().Stations[RocketSolver.Throat].Temperature;

        // The extrapolation of (6.23) from a previous station that lay deep on the subsonic side: the estimate is taken as it is.
        var subsonic = new ExitEstimate
        {
            Temperature = throatTemperature,
            Extrapolable = true,
            LogPressureRatio = SubsonicLogPressureRatio,
            LogAreaRatio = Math.Log(areaRatio),
            Derivative = 1.0,
        };
        StationSolve.CopyComposition(in context, RocketSolver.Throat, FirstExit);
        var outcome = AreaRatioIteration.At(in context, in chamber, in throat, areaRatio, FirstExit, ref subsonic);

        // Its neighbour, from the throat, as the exit loop starts every station that follows a failed one.
        var next = new ExitEstimate { Temperature = throatTemperature, Derivative = 1.0 };
        StationSolve.CopyComposition(in context, RocketSolver.Throat, FirstExit + 1);
        var nextOutcome = AreaRatioIteration.At(in context, in chamber, in throat, inputs.ExitValues[1], FirstExit + 1, ref next);

        var solution = rocketCase.Read();
        Assert.Equal(ExitOutcome.NeverSupersonic, outcome);
        Assert.Equal(CaseStatus.NotConverged, solution.StationStatus[FirstExit]);
        Assert.Equal(CaseStatus.Ok, solution.StationStatus[RocketSolver.Chamber]);
        Assert.Equal(CaseStatus.Ok, solution.StationStatus[RocketSolver.Throat]);
        Assert.Equal(CaseStatus.Ok, solution.StationStatus[FirstExit + 1]);
        Assert.True(nextOutcome is ExitOutcome.Converged or ExitOutcome.WithinReportTolerance, $"the neighbour ended as {nextOutcome}");
    }
}
