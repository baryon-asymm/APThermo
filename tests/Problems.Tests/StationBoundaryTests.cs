using APThermo.Equilibrium;
using APThermo.Performance;
using APThermo.Thermo;

namespace APThermo.Problems.Tests;

/// <summary>The public station assembly owns the failure postcondition.</summary>
[Collection(SolverFixture.CollectionName)]
public sealed class StationBoundaryTests
{
    /// <summary>Every failed status discards stale state fields, retaining only the NoGasPhase diagnostic pair.</summary>
    [Fact]
    public void FailedSlicesCannotPublishStaleStateOrFigures()
    {
        var table = SpeciesTable.Build(SolverFixture.Shared.Database, ["H"], ["H", "H2"]);
        var stale = new MixtureState { Temperature = 600.0, Pressure = 100000.0, Enthalpy = 123.0, Mach = 2.0 };
        foreach (var status in Enum.GetValues<CaseStatus>().Where(status => status != CaseStatus.Ok))
        {
            var station = StationFactory.Create("state", new StationSlice
            {
                Table = table,
                State = stale,
                Performance = new PerformanceFigures { SpecificImpulse = 42.0 },
                Moles = [1.0, 2.0],
                Offset = 0,
                Status = status,
            });
            var expected = status == CaseStatus.NoGasPhase
                ? new MixtureState { Temperature = stale.Temperature, Pressure = stale.Pressure } : default;
            Assert.Equal(expected, station.State);
            Assert.Equal(default(PerformanceFigures), station.Performance);
            Assert.True(station.MoleFractions["H2"] > 0.0);
        }
    }

    /// <summary>A public frozen failure keeps earlier stations and returns a zero failed exit.</summary>
    [Fact]
    public void FailedFrozenExitKeepsEarlierSuccessfulStations()
    {
        var propellant = Propellant.From(SolverFixture.Shared.Database).Oxidizer("O2(L)", temperature: 90.17)
            .Fuel("H2(L)", temperature: 20.27).OxidizerToFuelRatio(6.0).Build();
        var result = SolverFixture.Shared.Solver.Solve(propellant, new RocketProblem
        {
            ChamberPressure = 1000000.0,
            Flow = FlowModel.FrozenAtChamber,
            AreaRatios = [300.0, 1800.0, 6000.0, 10518.632521348223],
        });
        Assert.All(result.Stations.Take(result.Stations.Count - 1), station => Assert.Equal(CaseStatus.Ok, station.Status));
        var failed = result.Stations[^1];
        Assert.Equal(CaseStatus.TemperatureOutOfRange, failed.Status);
        Assert.Equal(default, failed.State);
        Assert.Equal(default(PerformanceFigures), failed.Performance);
    }

    /// <summary>A condensed-only TP result preserves its useful diagnostic temperature and pressure.</summary>
    [Fact]
    public void PublicNoGasPhaseKeepsTemperatureAndPressureOnly()
    {
        var database = SolverFixture.Shared.Database;
        var mixture = ElementalMixture.Create(new Dictionary<string, double> { ["C"] = 1000.0 / database.AtomicWeight("C") },
            only: ["C", "C2", "C3", "C(gr)"]);
        var result = SolverFixture.Shared.Solver.Solve(mixture, new EquilibriumProblem
        {
            Kind = ProblemKind.AssignedTemperaturePressure,
            Temperature = 400.0,
            Pressure = 100000.0,
        });
        Assert.Equal(CaseStatus.NoGasPhase, result.Status);
        Assert.Equal(new MixtureState { Temperature = 400.0, Pressure = 100000.0 }, result.State.State);
        Assert.True(result.State.CondensedMassFractions["C(gr)"] > 0.0);
    }
}
