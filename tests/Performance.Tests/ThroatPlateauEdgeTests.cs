using APThermo.Fixtures;
using APThermo.Thermo;

namespace APThermo.Performance.Tests;

/// <summary>
/// L0/L1: the three properties Performance BOOT.md asks of a plateau-edge throat (2026-09-26, finding F1), checked
/// independently of the throat fixture family's reference values: the throat's mass flux is not below that of the
/// package's own sp solves at the neighbouring pressures, the accepted state is single-phase with Mach below 1, and
/// (via <see cref="ThroatFixtureTests"/>) its c* and pressure ratio match the reference within tolerance. Also the
/// audit's sweep: example 13 over a chamber-pressure and enthalpy grid never ends `ThroatNotFound` and every case is
/// `Ok` (the second hidden-defect audit's guards part, observation O3: the count is asserted, not only the absence
/// of `ThroatNotFound`).
/// </summary>
[Collection(CpuFixture.CollectionName)]
public sealed class ThroatPlateauEdgeTests
{
    private const double NeighbourFraction = 1.0e-4;

    /// <summary>A throat fixture's own reference is at the plateau edge when its throat station's Mach is below
    /// this: the momentum-only (sonic) fixtures converge to 1 within rounding, the plateau-edge ones stay well
    /// under it (`tests/Fixtures/generate/BOOT.md`, the `throat` entry of the case matrix).</summary>
    private const double PlateauEdgeMachThreshold = 0.999;

    /// <summary>The plateau-edge throat fixtures, generated from the throat family by the reference's own throat
    /// Mach (the second hidden-defect audit's guards part, observation O4: not a typed list).</summary>
    public static TheoryData<string> PlateauEdgeCases()
    {
        var data = new TheoryData<string>();
        foreach (var path in FixtureFiles.Enumerate("throat"))
        {
            var name = Path.GetFileNameWithoutExtension(path);
            var throatMach = ThroatMachOf(RocketHost.Load("throat", name));
            if (throatMach < PlateauEdgeMachThreshold)
            {
                data.Add(name);
            }
        }

        return data;
    }

    private static double ThroatMachOf(CeaCase c) =>
        c.Outputs.GetProperty("stations").EnumerateArray()
            .First(s => s.GetProperty("station").GetString() == "throat")
            .GetProperty("mach").GetDouble();

    /// <summary>
    /// The plateau edge is single-phase and subsonic: the two properties the fixture's own reference does not carry.
    /// Single-phase is read from the throat's own isentropic exponent and heat capacity, the way the plateau-edge
    /// reseed fix was itself diagnosed (BOOT.md): a pinned melting pair collapses `GammaS` to 1 and `Cp` to 0, values
    /// no single-phase gas state of this tree reaches.
    /// </summary>
    [Theory]
    [MemberData(nameof(PlateauEdgeCases))]
    public void ThePlateauEdgeIsSinglePhaseAndSubsonic(string name)
    {
        using var rocketCase = SolveThroat(name, out _, out var throat);
        var context = rocketCase.Context;
        var throatState = context.Result.Stations[RocketSolver.Throat];
        Assert.True(throatState.Mach < 1.0, $"{name}: throat Mach {throatState.Mach:R}");
        Assert.True(throat.GammaS > 1.05, $"{name}: throat GammaS {throat.GammaS:R} is not a single-phase exponent");
        Assert.True(throatState.CpEquilibrium > 0.0, $"{name}: throat CpEquilibrium {throatState.CpEquilibrium:R} is not a single-phase heat capacity");
    }

    /// <summary>The plateau edge's mass flux is not below that of the sp solves at p(1 ± 1e-4): it is the maximum.</summary>
    [Theory]
    [MemberData(nameof(PlateauEdgeCases))]
    public void ThePlateauEdgeHasTheGreatestMassFluxNearby(string name)
    {
        // Two dummy pressure-ratio exits reserve two extra station rows for the neighbour probes below; their own
        // values are never used, since the probes issue their own StationRequest directly.
        using var rocketCase = SolveThroat(name, out var chamber, out var throat, [2.0, 2.0], [ExitSpecification.PressureRatio, ExitSpecification.PressureRatio]);
        var context = rocketCase.Context;
        var flow = context.Problem.Flow == FlowModel.FrozenAtChamber ? StationFlow.Frozen : StationFlow.Shifting;
        var throatTemperature = context.Result.Stations[RocketSolver.Throat].Temperature;
        const int lowStation = RocketLayout.FixedStations;
        const int highStation = RocketLayout.FixedStations + 1;

        StationSolve.CopyComposition(in context, RocketSolver.Throat, lowStation);
        var lowRequest = new StationRequest(lowStation, throat.Pressure * (1.0 - NeighbourFraction), throatTemperature, chamber.Entropy, flow);
        Assert.True(StationSolve.At(in context, in lowRequest), $"{name}: the low-pressure neighbour did not solve");

        StationSolve.CopyComposition(in context, RocketSolver.Throat, highStation);
        var highRequest = new StationRequest(highStation, throat.Pressure * (1.0 + NeighbourFraction), throatTemperature, chamber.Entropy, flow);
        Assert.True(StationSolve.At(in context, in highRequest), $"{name}: the high-pressure neighbour did not solve");

        var lowFlux = MassFlux(context, chamber.Enthalpy, lowStation);
        var highFlux = MassFlux(context, chamber.Enthalpy, highStation);
        Assert.True(throat.MassFlux >= lowFlux, $"{name}: throat flux {throat.MassFlux:R} below the low neighbour's {lowFlux:R}");
        Assert.True(throat.MassFlux >= highFlux, $"{name}: throat flux {throat.MassFlux:R} below the high neighbour's {highFlux:R}");
    }

    /// <summary>
    /// The audit's grid: example 13 over a chamber-pressure and enthalpy sweep never ends ThroatNotFound, and every
    /// case that does not is `Ok` (guards observation O3: the count of `Ok` cases is asserted against the grid's own
    /// dimensions, not only the absence of `ThroatNotFound`). Not marked LongRunning: the 45-case grid over both
    /// flow models completes in under a second (BOOT.md's own condition).
    /// </summary>
    [Theory]
    [MemberData(nameof(SweepFlows))]
    public void TheExample13SweepNeverEndsThroatNotFound(FlowModel flow)
    {
        var baseInputs = RocketInputs.Of(RocketHost.Load("throat", "rp1311-example13-throat_pc5MPa_dh0"));
        double[] pressures = [2.0e6, 5.0e6, 10.0e6, 15.0e6, 20.0e6, 25.0e6, 30.0e6, 35.0e6, 40.0e6];
        double[] enthalpyOffsets = [-400.0e3, -200.0e3, 0.0, 200.0e3, 400.0e3];
        var failures = new List<string>();
        var okCount = 0;
        foreach (var pressure in pressures)
        {
            foreach (var offset in enthalpyOffsets)
            {
                var mixture = new Mixture(baseInputs.Mixture.ElementMoles, baseInputs.Mixture.ReactantEnthalpy + offset);
                var inputs = baseInputs with { ChamberPressure = pressure, Flow = flow, Mixture = mixture, Exits = new ExitPlan([], []) };
                var solution = RocketHost.Solve(CpuFixture.Shared, inputs);
                if (solution.Status == CaseStatus.ThroatNotFound)
                {
                    failures.Add($"p_c={pressure:R} Pa, dh={offset:R} J/kg: ThroatNotFound");
                    continue;
                }

                if (solution.Status != CaseStatus.Ok)
                {
                    continue;
                }

                okCount++;
                failures.AddRange(RocketInvariants.SupersonicAreaRatioExits(solution).Select(v => $"p_c={pressure:R} Pa, dh={offset:R} J/kg: {v}"));
                failures.AddRange(RocketInvariants.PressureRatioMatchesTheSolvedPressure(solution).Select(v => $"p_c={pressure:R} Pa, dh={offset:R} J/kg: {v}"));
            }
        }

        Assert.True(failures.Count == 0, string.Join("; ", failures));
        Assert.Equal(pressures.Length * enthalpyOffsets.Length, okCount);
    }

    /// <summary>The two flow models the sweep covers.</summary>
    public static TheoryData<FlowModel> SweepFlows() => [FlowModel.ShiftingEquilibrium, FlowModel.FrozenAtThroat];

    private static double MassFlux(RocketContext context, double chamberEnthalpy, int station)
    {
        var state = context.Result.Stations[station];
        return state.Density * StationFigures.Velocity(chamberEnthalpy, in state);
    }

    private static RocketCase SolveThroat(string name, out ChamberReference chamber, out ThroatReference throat) =>
        SolveThroat(name, out chamber, out throat, [], []);

    /// <summary>Solves the chamber and the throat of a throat-family fixture, over a case whose exit rows (if any) are
    /// reserved but never solved by <see cref="ExitStations"/>; the caller drives them directly.</summary>
    private static RocketCase SolveThroat(string name, out ChamberReference chamber, out ThroatReference throat,
                                          double[] exitValues, ExitSpecification[] exitKinds)
    {
        var c = RocketHost.Load("throat", name);
        var baseInputs = RocketInputs.Of(c);
        var inputs = baseInputs with { Exits = new ExitPlan(exitValues, exitKinds) };
        var table = SpeciesTable.Build(CpuFixture.Shared.Database, inputs.System.Elements, inputs.System.Products);
        var rocketCase = new RocketCase(CpuFixture.Shared.Accelerator, table, inputs);
        var context = rocketCase.Context;
        Assert.Equal(CaseStatus.Ok, ChamberSolve.At(in context, out chamber));
        Assert.Equal(CaseStatus.Ok, ThroatSearch.At(in context, in chamber, out throat));
        return rocketCase;
    }
}
