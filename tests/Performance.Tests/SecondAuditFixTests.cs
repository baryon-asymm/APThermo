using APThermo.Fixtures;
using APThermo.Thermo;

namespace APThermo.Performance.Tests;

/// <summary>
/// L0/L1: the second hidden-defect audit of 2026-09-28 (Performance, `BOOT.md`'s own acceptance criterion),
/// findings F1, F3, F4 and F5 and observations O1 and O2.
///
/// F3 is proven against representative points of the audit's own Li2O (`li-o-h`) and BeO/H2O (`be-o-h`)
/// melting-plateau systems (`scratchpad/audit2/harness/pt/performance/ZzAuditThroatSweep.cs`, read at the
/// coordinator's direction), generated into the `throat` fixture family's `PLATEAU_EDGE_CASES`
/// (`tests/Fixtures/generate/throat_scan.py`; `tests/Fixtures/ACCEPTANCE.md` records the generation). Two of the three
/// (`li2o-throat_pc0.3MPa_h3.29375MJkg`, `li2o-throat_pc3MPa_h2.2375MJkg`) also fall under
/// <see cref="ThroatPlateauEdgeTests.PlateauEdgeCases"/> by the reference's own throat Mach and so are already
/// checked by <see cref="ThroatPlateauEdgeTests.ThePlateauEdgeIsSinglePhaseAndSubsonic"/>; the fixed-name fact
/// below (<see cref="ThePlateauEdgeAcceptsTheChamberSideOnTheAuditsLi2OAndBeOCases"/>) checks all three
/// independently of that filter, including the BeO/H2O point, whose reference throat Mach (1.011) is itself at
/// the edge and so is excluded from the auto-discovered set.
///
/// F4 is proven directly against the degenerate formula
/// (<see cref="TheGammaOneLimitProducesABracketInsteadOfThroatNotFound"/>: a synthetic chamber with `GammaS` set
/// to the literal `1.0`), because the audit's own Li/O/H mixture no longer reaches that exact bit pattern on this
/// branch (the ⚠ below); its "no `ThroatNotFound`" claim over the audit's own system, pressure and 50-case sweep
/// width is proven separately by <see cref="TheThroatSearchNeverEndsThroatNotFoundAcrossTheLiOHPlateauAt7MPa"/>.
///
/// ⚠ 2026-09-28 (found at the coordinator's review): the audit's own `li-o-h` mixture (Li 0.10, O 0.50, H 0.40 by
/// mass) at 7 MPa, over its own 50-case sweep (h −8.1125 MJ/kg upward in 6.25 kJ/kg steps), measures chamber
/// `GammaS` at 0.999999999446852 on every one of the 50 cases here — not exactly 1, though within
/// `RocketSolver.GammaOneTolerance` (1e-6) throughout, and all 50 are `Ok` regardless (0 `ThroatNotFound`, matching
/// the fix's own intent). Reverting both the `γ_s → 1` limit and the u² ≤ 0 step changed none of that: with
/// `GammaS` off by 5.5e-10, `gammaChamber / (gammaChamber - 1.0)` is a large but finite number, not the literal
/// ±∞ that makes `Math.Pow`'s `pow(1, ±∞) = 1` branch fire, so the reverted formula computes the same, correct
/// limit anyway. The audit's own harness measured `GammaS − 1 = 0` exactly on this mixture at `5a732f0`; the
/// Equilibrium rules A and B, merged into this branch afterwards (`da50a0a`, `74d0715`), moved the pinned pair's
/// last few bits enough to lose the exact equality, without moving any AP/HTPB/Al or example-13 bit this node's
/// own `Bits.approved.txt` records. The formula-level fact above reproduces the exact bug directly, unaffected by
/// which mixture or which Equilibrium revision produced the real chamber it borrows its other fields from; the
/// AP/HTPB/Al band's own 50-case sweep (<see cref="TheThroatSearchNeverEndsThroatNotFoundAcrossThePlateauBand"/>)
/// stays as an independent, unrelated system's coverage of the same "no `ThroatNotFound`" claim.
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
    /// family's four F1 cases were drawn from (`tests/Fixtures/generate/BOOT.md`, the `throat` entry of the case matrix, the finding F1 cases).
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
    /// Finding F3: the plateau-edge re-solve is accepted only on the chamber's own (subsonic, single-phase) side
    /// of the edge, over the audit's own Li2O and BeO/H2O systems (the class doc-comment). Shown red once by
    /// reverting <see cref="ThroatBracketSearch.AcceptPlateauEdge"/> to accept its first, unchecked trial: the
    /// 0.3 MPa Li2O case's Mach then measures 1.0674725047617122 (the audit's own reported range, "Mach 1.067 to
    /// 1.124"), against 0.9803150262208528 here; the 3 MPa Li2O and 15 MPa BeO/H2O cases do not move under that
    /// revert (their first trial already lands on the chamber side for this system's exact numbers), so they are
    /// carried as the audit's own further citations, not as independent red-once proofs.
    /// </summary>
    [Theory]
    [InlineData("li2o-throat_pc0.3MPa_h3.29375MJkg")]
    [InlineData("li2o-throat_pc3MPa_h2.2375MJkg")]
    [InlineData("beo-h2o-throat_pc15MPa_h-11.06875MJkg")]
    public void ThePlateauEdgeAcceptsTheChamberSideOnTheAuditsLi2OAndBeOCases(string name)
    {
        using var rocketCase = SolveThroat(name, out _, out var throat);
        var context = rocketCase.Context;
        var throatState = context.Result.Stations[RocketSolver.Throat];
        Assert.True(throatState.Mach < 1.0, $"{name}: throat Mach {throatState.Mach:R}");
        Assert.True(throat.GammaS > 1.05, $"{name}: throat GammaS {throat.GammaS:R} is not a single-phase exponent");
    }

    /// <summary>
    /// Finding F4, at the formula that degenerates: with the chamber's own isentropic exponent set to the literal
    /// `1.0` (the equilibrium node's plateau convention for an undissociated gas, `BOOT.md`), equation (6.15)'s
    /// first estimate is the chamber pressure through its limit, not the chamber pressure itself, and the search
    /// still ends `Ok`. A synthetic <see cref="ChamberReference"/> borrows every other field from a real, ordinary
    /// chamber solve (example 13's), so the only thing under test is the formula's own behaviour at `γ_s = 1`
    /// exactly — not whether some real mixture happens to reach that bit pattern (the class doc-comment's ⚠).
    /// Shown red once by reverting the limit and the u² ≤ 0 step together: the search then ends `ThroatNotFound`,
    /// with the loop's only trial solved at exactly the chamber's own pressure (`Math.Pow(1, ±∞) = 1`, so equation
    /// (6.15) returns 1 and the first candidate is the chamber pressure itself), giving u² = 0 exactly and no
    /// bracket ever tracked.
    /// </summary>
    [Fact]
    public void TheGammaOneLimitProducesABracketInsteadOfThroatNotFound()
    {
        using var rocketCase = SolveThroat("rp1311-example13-throat_pc5MPa_dh0", out var chamber, out _);
        var context = rocketCase.Context;
        var temperature = context.Result.Stations[RocketSolver.Chamber].Temperature;
        var degenerateChamber = new ChamberReference(chamber.Pressure, chamber.Enthalpy, chamber.Entropy, 1.0);
        var query = new ThroatQuery(in context, in degenerateChamber);
        var status = ThroatBracketSearch.Locate(in query, temperature, out var pressureSolved);
        Assert.Equal(CaseStatus.Ok, status);
        Assert.True(pressureSolved < chamber.Pressure, $"the throat pressure {pressureSolved:R} should be below the chamber's {chamber.Pressure:R}");
    }

    /// <summary>
    /// Finding F4's "no `ThroatNotFound`" claim, over the audit's own system, pressure and sweep width: the
    /// `li-o-h` mixture at 7 MPa, over the audit's own 50-case sweep (h −8.1125 MJ/kg upward in 6.25 kJ/kg steps).
    /// Every case is `Ok`. The chamber's own `GammaS` at the plateau's own nine points is checked separately
    /// (<see cref="TheChamberSGammaSIsWithinTheLimitsToleranceOnTheAuditsNamedLiOHPoints"/>): four of these 50
    /// (the highest, h ≥ −7 825 000 J/kg) have already left the plateau, where `GammaS` is not near 1.
    /// </summary>
    [Fact]
    public void TheThroatSearchNeverEndsThroatNotFoundAcrossTheLiOHPlateauAt7MPa()
    {
        var baseInputs = RocketInputs.Of(RocketHost.Load("throat", "li2o-throat_pc0.3MPa_h3.29375MJkg"));
        const double lowEnthalpy = -8112500.0;
        const int caseCount = 50;
        const double stepJPerKg = 6250.0;
        var failures = new List<string>();
        for (var k = 0; k < caseCount; k++)
        {
            var enthalpy = lowEnthalpy + k * stepJPerKg;
            var mixture = new Mixture(baseInputs.Mixture.ElementMoles, enthalpy);
            var inputs = baseInputs with { ChamberPressure = 7.0e6, Mixture = mixture, Exits = new ExitPlan([], []) };
            var table = SpeciesTable.Build(CpuFixture.Shared.Database, inputs.System.Elements, inputs.System.Products);
            using var rocketCase = new RocketCase(CpuFixture.Shared.Accelerator, table, inputs);
            var context = rocketCase.Context;
            if (ChamberSolve.At(in context, out var chamber) != CaseStatus.Ok)
            {
                failures.Add($"h={enthalpy:R} J/kg: chamber did not solve");
                continue;
            }

            var status = ThroatSearch.At(in context, in chamber, out _);
            if (status != CaseStatus.Ok)
            {
                failures.Add($"h={enthalpy:R} J/kg: {status}");
            }
        }

        Assert.Empty(failures);
    }

    /// <summary>
    /// Finding F4's chamber `GammaS`, at the audit's own nine inspected points of the `li-o-h` plateau at 7 MPa
    /// (`ChamberOnPlateau`, `scratchpad/audit2/harness/pt/performance/ZzAuditGammaOne.cs`): within
    /// `RocketSolver.GammaOneTolerance` of 1 on every one (the class doc-comment's ⚠: not exactly 1 here, but
    /// inside the tolerance the fix's own branch reads, after the Equilibrium rules A and B).
    /// </summary>
    [Fact]
    public void TheChamberSGammaSIsWithinTheLimitsToleranceOnTheAuditsNamedLiOHPoints()
    {
        var baseInputs = RocketInputs.Of(RocketHost.Load("throat", "li2o-throat_pc0.3MPa_h3.29375MJkg"));
        double[] enthalpies = [-8125000.0, -8118750.0, -8112500.0, -8106250.0, -8100000.0, -8093750.0, -8087500.0, -8000000.0, -7950000.0];
        var failures = new List<string>();
        foreach (var enthalpy in enthalpies)
        {
            var mixture = new Mixture(baseInputs.Mixture.ElementMoles, enthalpy);
            var inputs = baseInputs with { ChamberPressure = 7.0e6, Mixture = mixture, Exits = new ExitPlan([], []) };
            var table = SpeciesTable.Build(CpuFixture.Shared.Database, inputs.System.Elements, inputs.System.Products);
            using var rocketCase = new RocketCase(CpuFixture.Shared.Accelerator, table, inputs);
            var context = rocketCase.Context;
            if (ChamberSolve.At(in context, out var chamber) != CaseStatus.Ok)
            {
                failures.Add($"h={enthalpy:R} J/kg: chamber did not solve");
                continue;
            }

            if (!(Math.Abs(chamber.GammaS - 1.0) <= RocketSolver.GammaOneTolerance))
            {
                failures.Add($"h={enthalpy:R} J/kg: chamber GammaS {chamber.GammaS:R} outside the tolerance");
            }
        }

        Assert.Empty(failures);
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
    /// oracle lies at a pressure closer to the chamber than the throat's own (BOOT.md, finding F1). A non-`Ok`
    /// chamber or throat fails the fact unless <paramref name="declaredStatus"/> names it, since a silent return
    /// on a non-`Ok` throat lets a real regression (a case that used to end `Ok` and no longer does) pass unnoticed
    /// (the third audit pass of 2026-09-28, part 2's "no vacuous pass": <see cref="SecondAuditFixTests"/> found
    /// exactly this at `5a732f0`, where the fact returned without checking on every one of the Li/O/H band's newly
    /// `ThroatNotFound` cases). No case of the committed fixture set is declared: every rocket and throat fixture,
    /// and the AP/HTPB/Al and element-mixture bands, converge to an `Ok` throat.
    /// </summary>
    internal static void AssertFirstMaximum(RocketInputs inputs, string label, CaseStatus declaredStatus = CaseStatus.Ok)
    {
        // One dummy pressure-ratio exit reserves an extra station row for the oracle's probe; its own value is
        // never used, since the probe issues its own StationRequest directly (mirrors ThroatPlateauEdgeTests).
        var probeInputs = inputs with { Exits = new ExitPlan([2.0], [ExitSpecification.PressureRatio]) };
        var table = SpeciesTable.Build(CpuFixture.Shared.Database, probeInputs.System.Elements, probeInputs.System.Products);
        using var rocketCase = new RocketCase(CpuFixture.Shared.Accelerator, table, probeInputs);
        var context = rocketCase.Context;
        var chamberStatus = ChamberSolve.At(in context, out var chamber);
        if (chamberStatus != CaseStatus.Ok)
        {
            Assert.True(chamberStatus == declaredStatus, $"{label}: chamber ended {chamberStatus}, not the declared {declaredStatus}");
            return;
        }

        var throatStatus = ThroatSearch.At(in context, in chamber, out var throat);
        if (throatStatus != CaseStatus.Ok)
        {
            Assert.True(throatStatus == declaredStatus, $"{label}: throat ended {throatStatus}, not the declared {declaredStatus}");
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
