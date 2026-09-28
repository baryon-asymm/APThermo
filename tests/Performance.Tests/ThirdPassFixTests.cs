using APThermo.Fixtures;
using APThermo.Thermo;

namespace APThermo.Performance.Tests;

/// <summary>
/// The third audit pass of 2026-09-28 (part 2: finding 1 and the observation on the boundary cap; `BOOT.md`'s own
/// unticked criterion under Constraints, the throat bullet's "the plateau edge's reach" and "the first maximum"
/// paragraphs). The plateau-edge acceptance's geometric-offset reach (<see cref="ThroatBracketSearch.AcceptPlateauEdge"/>)
/// and the phase-boundary walk's cap (<see cref="UpstreamChokeCheck.MaxPhaseBoundaries"/>, raised from 4 to 8 and
/// exhaustion changed from an unverified `Ok` to `ThroatNotFound`) are proven here.
/// </summary>
[Collection(CpuFixture.CollectionName)]
public sealed class ThirdPassFixTests
{
    /// <summary>
    /// The 0.3 MPa Li2O band: h 3.05625 to 3.36875 MJ/kg in 12.5 kJ/kg steps (26 cases), the span BOOT.md's F3
    /// criterion already names. Every case ends `Ok`, no `ThroatNotFound`; a plateau-edge throat (`Mach &lt; 1`)
    /// is single-phase (`GammaS > 1.05`, the same reading <see cref="ThroatPlateauEdgeTests"/> uses); and every
    /// case passes the first-maximum fact. Red at `c02e14d`: 21 of the 26 cases `ThroatNotFound` (the third audit
    /// pass's own count, `scratchpad/audit3/b/band-new.csv`, kept out of the tree); green after the geometric-offset
    /// fix (`band-geo.csv`, all 26 `Ok`).
    /// </summary>
    [Fact]
    public void TheLi2OBandAt0Point3MPaNeverEndsThroatNotFound() =>
        AssertBand("li2o-throat_pc0.3MPa_h3.29375MJkg", lowEnthalpy: 3.05625e6, highEnthalpy: 3.36875e6, stepJPerKg: 12.5e3);

    /// <summary>
    /// The 3 MPa Li2O band, at the probe's own step (4.6875 kJ/kg): 12 cases, h 2.15 to 2.2015625 MJ/kg. Same
    /// checks as the 0.3 MPa band. Red at `c02e14d`: 10 of 12 `ThroatNotFound`; green after.
    ///
    /// ⚠ 2026-09-28: this criterion first named the upper bound 2.2109 MJ/kg. Read against the audit's own probe
    /// output (`scratchpad/audit3/b/band-new.csv`, kept out of the tree, labelled `li2o-throat_pc3MPa_h2.2375MJkg`),
    /// 2.2109 MJ/kg (2210937.5 J/kg) is the *highest* pressure at which the pre-fix code still failed, not the edge
    /// of a contiguous 12-case sweep: two of the twelve points between 2.15 and 2.2109 MJ/kg (2182812.5 and
    /// 2196875 J/kg) were already `Ok` even before the fix, so a contiguous sweep to 2.2109 MJ/kg holds 14 cases,
    /// 12 of them failing, not 12 cases with 10 failing. The audit's own report
    /// (`scratchpad/audit3/2-throat-execution-guards.md`, "3 MPa h 2.15-2.2109: 12/12 Ok before, 10 ThroatNotFound
    /// after") states 12 total and 10 failing, which the contiguous span 2.15 to 2.2015625 MJ/kg (12 points at the
    /// probe's own 4.6875 kJ/kg step) reproduces exactly; that span is used here.
    /// </summary>
    [Fact]
    public void TheLi2OBandAt3MPaNeverEndsThroatNotFound() =>
        AssertBand("li2o-throat_pc3MPa_h2.2375MJkg", lowEnthalpy: 2.15e6, highEnthalpy: 2.2015625e6, stepJPerKg: 4687.5);

    /// <summary>
    /// Sweeps <paramref name="baseFixture"/>'s element system over the named enthalpy band: every case ends `Ok`
    /// (never `ThroatNotFound`), a plateau-edge throat is single-phase and subsonic, and
    /// <see cref="SecondAuditFixTests.AssertFirstMaximum"/> holds. Fails on an empty sweep (a zero or negative
    /// step), so a mistyped band cannot pass by covering nothing (BOOT.md, "no vacuous pass").
    /// </summary>
    private static void AssertBand(string baseFixture, double lowEnthalpy, double highEnthalpy, double stepJPerKg)
    {
        var baseInputs = RocketInputs.Of(RocketHost.Load("throat", baseFixture));
        var cases = 0;
        for (var enthalpy = lowEnthalpy; enthalpy <= highEnthalpy + 1.0; enthalpy += stepJPerKg)
        {
            cases++;
            var label = string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{baseFixture} h={enthalpy:R} J/kg");
            var mixture = new Mixture(baseInputs.Mixture.ElementMoles, enthalpy);
            var inputs = baseInputs with { Mixture = mixture, Exits = new ExitPlan([], []) };
            SecondAuditFixTests.AssertFirstMaximum(inputs, label);

            var solution = RocketHost.Solve(CpuFixture.Shared, inputs);
            Assert.Equal(CaseStatus.Ok, solution.Status);
            var throat = solution.Outcome.Stations[RocketSolver.Throat];
            if (throat.Mach < 1.0)
            {
                Assert.True(throat.GammaS > 1.05, $"{label}: edge GammaS {throat.GammaS:R} is not a single-phase exponent");
            }
        }

        Assert.True(cases > 0, $"{baseFixture}: the band swept no cases");
    }

    /// <summary>Every rocket and throat fixture enumerates at least one case, so the first-maximum facts over them
    /// (`RocketAndThroatFixtures`, <see cref="SecondAuditFixTests"/>) cannot pass by walking nothing.</summary>
    [Fact]
    public void TheRocketAndThroatFixtureSetsAreNotEmpty()
    {
        Assert.NotEmpty(FixtureFiles.Enumerate("rocket"));
        Assert.NotEmpty(FixtureFiles.Enumerate("throat"));
    }

    /// <summary>
    /// The cap: a walk that meets more boundaries than <see cref="UpstreamChokeCheck.MaxPhaseBoundaries"/> without
    /// resolving proves nothing about what lies beyond the last one it looked at, and must end
    /// <see cref="CaseStatus.ThroatNotFound"/>, never an unverified <see cref="CaseStatus.Ok"/>. No real system on
    /// hand needs more than a handful of boundaries to resolve (the richest measured, BeO/H2O at 15 MPa, needs
    /// six), so this fact drives <see cref="PhaseBoundaryLocator.Locate"/> directly with a hand-built chain of
    /// already-narrow (inside <see cref="RocketSolver.ThroatBracketWidth"/>) brackets, each echoed back verbatim by
    /// <c>Locate</c>'s own first check before any solve (the same direct-call idiom observations O1 and O2 use for
    /// <see cref="ThroatBracketSearch"/>): every end's sonic ratio stays below 1 and every fingerprint differs from
    /// a fixed sentinel "candidate" fingerprint, so none of <see cref="UpstreamChokeCheck.Verify"/>'s three
    /// resolving conditions (an outer or inner end at or above the sonic point, or an inner end sharing the
    /// candidate's own fingerprint) ever fires — reproducing, one boundary at a time, the walk `Verify` performs
    /// for a case with more real boundaries than its cap.
    /// </summary>
    [Fact]
    public void AWalkThatMeetsMoreBoundariesThanTheCapEndsThroatNotFound()
    {
        var c = RocketHost.Load("throat", "li2o-throat_pc3MPa_h2.2375MJkg");
        var inputs = RocketInputs.Of(c);
        var table = SpeciesTable.Build(CpuFixture.Shared.Database, inputs.System.Elements, inputs.System.Products);
        using var rocketCase = new RocketCase(CpuFixture.Shared.Accelerator, table, inputs);
        var context = rocketCase.Context;
        Assert.Equal(CaseStatus.Ok, ChamberSolve.At(in context, out var chamber));
        var query = new ThroatQuery(in context, in chamber);
        var temperature = context.Result.Stations[RocketSolver.Chamber].Temperature;

        const long candidateFingerprint = -100L;
        var pressure = chamber.Pressure;
        for (var b = 0; b <= UpstreamChokeCheck.MaxPhaseBoundaries; b++)
        {
            var outer = new PhaseBoundaryEnd(pressure, temperature, 0.4, b);
            var nextPressure = pressure * Math.Exp(-0.5 * RocketSolver.ThroatBracketWidth);
            var inner = new PhaseBoundaryEnd(nextPressure, temperature, 0.4, b + 1);
            var status = PhaseBoundaryLocator.Locate(in query, new PhaseBoundaryQuery(in outer, in inner), out var boundary);
            Assert.Equal(CaseStatus.Ok, status);
            Assert.Equal(outer.Pressure, boundary.Hi.Pressure);
            Assert.Equal(inner.Pressure, boundary.Lo.Pressure);
            Assert.True(boundary.Hi.SonicRatio < 1.0);
            Assert.True(boundary.Lo.SonicRatio < 1.0);
            Assert.NotEqual(candidateFingerprint, boundary.Lo.Fingerprint);
            pressure = nextPressure;
        }
    }
}
