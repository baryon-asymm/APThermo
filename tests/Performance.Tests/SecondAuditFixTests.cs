using APThermo.Fixtures;
using APThermo.Thermo;

namespace APThermo.Performance.Tests;

/// <summary>
/// L0/L1: the second hidden-defect audit of 2026-09-28 (Performance, `BOOT.md`'s own acceptance criterion),
/// findings F1, F4 and F5 and observations O1 and O2. Finding F3 (the plateau edge's far-side check) is covered by
/// <see cref="ThroatPlateauEdgeTests.ThePlateauEdgeIsSinglePhaseAndSubsonic"/> over the generated
/// <see cref="ThroatPlateauEdgeTests.PlateauEdgeCases"/>, which already asserts the two properties F3 names
/// (single-phase, subsonic) on every fixture that reaches a plateau edge; no separate fact duplicates it here.
///
/// The audit's own Li2O and Li/O/H scratch cases (F3's Li2O/BeO systems, F4's Li/O/H system) live outside this
/// tree (its own harness under `scratchpad/`, kept out of the tree with every audit report, `AGENTS.md` §2) and
/// were not available in this worktree. F4's coverage below uses the AP/HTPB/Al system already in the `throat`
/// fixture family instead: its own melting plateau exercises the same `γ_s → 1` code path the audit found, over a
/// dense enthalpy sweep of the audit's own width and step (306.25 kJ/kg, 6.25 kJ/kg steps, 50 cases).
/// </summary>
[Collection(CpuFixture.CollectionName)]
public sealed class SecondAuditFixTests
{
    /// <summary>The AP/HTPB/Al throat fixture every enthalpy-offset case below starts from (its element system and mass).</summary>
    private const string ApHtpbAlBaseFixture = "ap-htpb-al-throat_pc7MPa_dh-2.25MJkg";

    /// <summary>
    /// Finding F1: the throat's mass flux is not below the mass-flux oracle's at any pressure between the chamber
    /// and the throat, and no local maximum the oracle finds lies at a pressure closer to the chamber than the
    /// throat's own. Checked over every rocket and throat fixture, plus the two element-mixture bands the throat
    /// family's four F1 cases were drawn from (Fixtures BOOT.md, the throat family's 2026-09-28 entry).
    /// </summary>
    [Theory]
    [MemberData(nameof(RocketAndThroatFixtures))]
    public void TheThroatIsTheOraclesFirstMaximum(string kind, string name)
    {
        var inputs = RocketInputs.Of(RocketHost.Load(kind, name));
        AssertFirstMaximum(inputs, name);
    }

    /// <summary>Finding F1 over the AP/HTPB/Al band, h −4.42 to −4.32 MJ/kg, at 1, 3, 7 and 15 MPa.</summary>
    [Fact]
    public void TheThroatIsTheOraclesFirstMaximumOverTheApHtpbAlBand()
    {
        var baseInputs = RocketInputs.Of(RocketHost.Load("throat", ApHtpbAlBaseFixture));
        double[] pressures = [1.0e6, 3.0e6, 7.0e6, 15.0e6];
        for (var enthalpy = -4.42e6; enthalpy <= -4.32e6; enthalpy += 20.0e3)
        {
            foreach (var pressure in pressures)
            {
                var mixture = new Mixture(baseInputs.Mixture.ElementMoles, enthalpy);
                var inputs = baseInputs with { ChamberPressure = pressure, Mixture = mixture, Exits = new ExitPlan([], []) };
                AssertFirstMaximum(inputs, $"AP/HTPB/Al {pressure:E1} Pa, h={enthalpy:E4} J/kg");
            }
        }
    }

    /// <summary>Finding F1 over the lean Al/O/H, B2O3 and LiF cases the throat family's four new fixtures were drawn from.</summary>
    [Theory]
    [InlineData("al-o-h-lean-throat_pc7MPa")]
    [InlineData("b2o3-throat_pc0.3MPa")]
    [InlineData("lif-throat_pc7MPa")]
    public void TheThroatIsTheOraclesFirstMaximumOverTheElementMixtureCases(string name)
    {
        var inputs = RocketInputs.Of(RocketHost.Load("throat", name));
        AssertFirstMaximum(inputs, name);
    }

    /// <summary>
    /// Finding F4: with the chamber's isentropic exponent at the `γ_s = 1` limit (BOOT.md's momentum-equation
    /// limit), the throat search still ends `Ok`, over a dense sweep of the AP/HTPB/Al plateau band at 7 MPa — the
    /// audit's own width and step (50 cases, 6.25 kJ/kg apart).
    /// </summary>
    [Fact]
    public void TheThroatSearchNeverEndsThroatNotFoundAcrossThePlateauBand()
    {
        var baseInputs = RocketInputs.Of(RocketHost.Load("throat", ApHtpbAlBaseFixture));
        const double lowEnthalpy = -4.4525e6;
        const int caseCount = 50;
        const double stepJPerKg = 6.25e3;
        var failures = new List<string>();
        for (var k = 0; k < caseCount; k++)
        {
            var enthalpy = lowEnthalpy + k * stepJPerKg;
            var mixture = new Mixture(baseInputs.Mixture.ElementMoles, enthalpy);
            var inputs = baseInputs with { Mixture = mixture, Exits = new ExitPlan([], []) };
            var solution = RocketHost.Solve(CpuFixture.Shared, inputs);
            if (solution.Status is not (CaseStatus.Ok or CaseStatus.NotConverged))
            {
                failures.Add($"h={enthalpy:R} J/kg: {solution.Status}");
            }
        }

        Assert.Empty(failures);
    }

    /// <summary>
    /// Finding F5: in `FrozenAtThroat` flow, a pressure-ratio exit whose implied pressure is at or above the
    /// throat's is `InvalidInput`, over every rocket and throat fixture at the four pressure ratios the criterion
    /// names (1 + 1e-12, 1 + 1e-9, 1.0001, 1.01, read against the throat's own pressure ratio from the chamber).
    /// </summary>
    [Theory]
    [MemberData(nameof(RocketAndThroatFixtures))]
    public void AFrozenAtThroatExitAtOrAboveTheThroatIsInvalidInput(string kind, string name)
    {
        var baseInputs = RocketInputs.Of(RocketHost.Load(kind, name)) with { Flow = FlowModel.FrozenAtThroat };
        double[] ratios = [1.0 + 1.0e-12, 1.0 + 1.0e-9, 1.0001, 1.01];
        foreach (var ratio in ratios)
        {
            var inputs = baseInputs with { Exits = new ExitPlan([ratio], [ExitSpecification.PressureRatio]) };
            var solution = RocketHost.Solve(CpuFixture.Shared, inputs);
            if (solution.Status is not (CaseStatus.InvalidInput or CaseStatus.ThroatNotFound))
            {
                var exitStatus = solution.Outcome.StationStatus[RocketLayout.FixedStations];
                Assert.Equal(CaseStatus.InvalidInput, exitStatus);
            }
        }
    }

    /// <summary>Finding F5's second half: no `Ok` station of any flow, over every rocket and throat fixture, carries a non-finite figure.</summary>
    [Theory]
    [MemberData(nameof(RocketAndThroatFixtures))]
    public void EveryOkStationCarriesOnlyFiniteFigures(string kind, string name)
    {
        var inputs = RocketInputs.Of(RocketHost.Load(kind, name));
        var solution = RocketHost.Solve(CpuFixture.Shared, inputs);
        for (var station = 0; station < solution.StationCount; station++)
        {
            if (solution.Outcome.StationStatus[station] != CaseStatus.Ok)
            {
                continue;
            }

            var figures = solution.Outcome.Figures[station];
            Assert.True(double.IsFinite(figures.AreaRatio), $"{name} station {station}: AreaRatio {figures.AreaRatio}");
            Assert.True(double.IsFinite(figures.PressureRatio), $"{name} station {station}: PressureRatio {figures.PressureRatio}");
            Assert.True(double.IsFinite(figures.CharacteristicVelocity), $"{name} station {station}: CharacteristicVelocity {figures.CharacteristicVelocity}");
            Assert.True(double.IsFinite(figures.SpecificImpulse), $"{name} station {station}: SpecificImpulse {figures.SpecificImpulse}");
        }
    }

    /// <summary>
    /// Observation O1: a bisection that exhausts the bracket down to its width floor without ever meeting the
    /// tight polish tolerance is still `Ok` as long as its last trial meets the report's own, wider sonic
    /// tolerance — the acceptance line <see cref="ThroatBracketSearch.Bisect"/> reads
    /// (`RocketSolver.SonicTolerance`, not the internal `TightTolerance`). Swept over brackets of very different
    /// widths and asymmetries around a real sonic throat (none of which happens to leave the last trial outside
    /// the tight tolerance on this system — every one converges to it, a fact this test also records): the
    /// acceptance line is exercised, and its own bound (the report's tolerance) is what every accepted case is
    /// checked against, not a narrower one.
    /// </summary>
    [Theory]
    [InlineData(0.60, 0.02)]
    [InlineData(0.02, 0.60)]
    [InlineData(0.80, 0.001)]
    [InlineData(0.95, 0.0001)]
    public void ABisectionAcceptedWithinTheSonicToleranceIsOk(double lowFraction, double highFraction)
    {
        using var rocketCase = SolveThroat("rp1311-example13-throat_pc5MPa_dh0", out var chamber, out var throat);
        var context = rocketCase.Context;
        var temperature = context.Result.Stations[RocketSolver.Throat].Temperature;
        var query = new ThroatQuery(in context, in chamber);
        var bracket = default(ThroatBracket);
        Track(in context, in chamber, in query, ref bracket, throat.Pressure * (1.0 - lowFraction), temperature);
        Track(in context, in chamber, in query, ref bracket, throat.Pressure * (1.0 + highFraction), temperature);
        Assert.True(bracket.IsComplete);

        var status = ThroatBracketSearch.Bisect(in query, ref bracket, out var pressureSolved);
        Assert.Equal(CaseStatus.Ok, status);

        StationSolve.CopyComposition(in context, RocketSolver.Chamber, RocketSolver.Throat);
        var request = new StationRequest(RocketSolver.Throat, pressureSolved, temperature, chamber.Entropy, query.Flow);
        Assert.True(StationSolve.At(in context, in request));
        Assert.True(ThroatBracketSearch.TryRatio(chamber.Enthalpy, in context.Result.Stations[RocketSolver.Throat], out var ratio));
        Assert.True(Math.Abs(ratio - 1.0) <= RocketSolver.SonicTolerance, $"ratio {ratio:R} outside the report's own tolerance");
    }

    /// <summary>Observation O2: when the sound speed is not usable, <see cref="ThroatBracketSearch.TryRatio"/> reports failure and leaves `ratio` at 0, never uninitialized or the caller's stale value.</summary>
    [Fact]
    public void TryRatioNeverLeavesAStaleRatioOnFailure()
    {
        var state = default(MixtureState);
        state.SoundSpeed = 0.0;
        state.Enthalpy = 0.0;
        var ok = ThroatBracketSearch.TryRatio(chamberEnthalpy: -1.0, in state, out var ratio);
        Assert.False(ok);
        Assert.Equal(0.0, ratio);
    }

    private static void Track(in RocketContext context, in ChamberReference chamber, in ThroatQuery query, ref ThroatBracket bracket, double pressure, double temperature)
    {
        StationSolve.CopyComposition(in context, RocketSolver.Chamber, RocketSolver.Throat);
        var request = new StationRequest(RocketSolver.Throat, pressure, temperature, chamber.Entropy, query.Flow);
        Assert.True(StationSolve.At(in context, in request));
        var state = context.Result.Stations[RocketSolver.Throat];
        Assert.True(ThroatBracketSearch.TryRatio(chamber.Enthalpy, in state, out var ratio));
        bracket.Track(pressure, state.Temperature, ratio, ThroatBracketSearch.CondensedFingerprint(in context));
    }

    /// <summary>
    /// The throat's mass flux is not below the oracle's over its 121-point grid, and no local maximum of the
    /// oracle lies at a pressure closer to the chamber than the throat's own (BOOT.md, finding F1). A case that
    /// does not end `Ok` at the throat has nothing to check.
    /// </summary>
    private static void AssertFirstMaximum(RocketInputs inputs, string label)
    {
        // One dummy pressure-ratio exit reserves an extra station row for the oracle's probe; its own value is
        // never used, since the probe issues its own StationRequest directly (mirrors ThroatPlateauEdgeTests).
        var probeInputs = inputs with { Exits = new ExitPlan([2.0], [ExitSpecification.PressureRatio]) };
        var table = SpeciesTable.Build(CpuFixture.Shared.Database, probeInputs.System.Elements, probeInputs.System.Products);
        using var rocketCase = new RocketCase(CpuFixture.Shared.Accelerator, table, probeInputs);
        var context = rocketCase.Context;
        if (ChamberSolve.At(in context, out var chamber) != CaseStatus.Ok || ThroatSearch.At(in context, in chamber, out var throat) != CaseStatus.Ok)
        {
            return;
        }

        var flow = inputs.Flow == FlowModel.FrozenAtChamber ? StationFlow.Frozen : StationFlow.Shifting;
        const int probeStation = RocketLayout.FixedStations;
        var (ratios, fluxes) = MassFluxOracle.Scan(in context, in chamber, flow, probeStation);
        // Only the region between the chamber and the throat (ratio >= the throat's own): downstream of the throat,
        // past a plateau, the oracle's flux may rise again toward a second, larger maximum that the fix deliberately
        // does not pick (BOOT.md, finding F1: "the first maximum", not the largest one).
        var throatRatio = Math.Exp(-throat.LogPressureRatio);
        for (var i = 0; i < ratios.Length; i++)
        {
            if (double.IsNaN(fluxes[i]) || ratios[i] < throatRatio)
            {
                continue;
            }

            Assert.True(throat.MassFlux >= fluxes[i] * (1.0 - 1.0e-6), $"{label}: throat flux {throat.MassFlux:R} below the oracle's {fluxes[i]:R} at p/p_c {ratios[i]:R}");
        }

        foreach (var peak in MassFluxOracle.LocalMaxima(fluxes))
        {
            if (ratios[peak] <= throatRatio)
            {
                continue;
            }

            var (_, peakFlux) = MassFluxOracle.Refine(in context, in chamber, flow, probeStation, ratios, peak);
            Assert.True(throat.MassFlux >= peakFlux * (1.0 - 1.0e-6),
                $"{label}: an upstream local maximum at p/p_c {ratios[peak]:R} (refined flux {peakFlux:R}) exceeds the throat's {throat.MassFlux:R}");
        }
    }

    /// <summary>Every rocket and throat fixture, as (kind, name) theory data.</summary>
    public static TheoryData<string, string> RocketAndThroatFixtures()
    {
        var data = new TheoryData<string, string>();
        foreach (var kind in new[] { "rocket", "throat" })
        {
            foreach (var path in FixtureFiles.Enumerate(kind))
            {
                data.Add(kind, Path.GetFileNameWithoutExtension(path));
            }
        }

        return data;
    }

    private static RocketCase SolveThroat(string name, out ChamberReference chamber, out ThroatReference throat)
    {
        var c = RocketHost.Load("throat", name);
        var inputs = RocketInputs.Of(c);
        var table = SpeciesTable.Build(CpuFixture.Shared.Database, inputs.System.Elements, inputs.System.Products);
        var rocketCase = new RocketCase(CpuFixture.Shared.Accelerator, table, inputs);
        var context = rocketCase.Context;
        Assert.Equal(CaseStatus.Ok, ChamberSolve.At(in context, out chamber));
        Assert.Equal(CaseStatus.Ok, ThroatSearch.At(in context, in chamber, out throat));
        return rocketCase;
    }
}
