using AerospacePropellantThermodynamics.Equilibrium;
using AerospacePropellantThermodynamics.Fixtures;
using AerospacePropellantThermodynamics.Thermo;

namespace AerospacePropellantThermodynamics.Problems.Tests;

/// <summary>
/// L1 and L2: results speak in database names even where the table cuts a species into pieces (BOOT.md, results),
/// and the melting-plateau states the reference cannot provide solve through the front door: the ALN(L) enthalpy
/// gap of the record another simulation handed over, and a sweep across the alumina plateau by either path.
/// </summary>
[Collection(SolverCollection.Name)]
public sealed class SplitRecordTests(SolverFixture fixture)
{
    /// <summary>How far a pinned station's temperature may lie from a species record's transition bound: two solves of the tree's own code, the numerical solver's own convergence floor at a pinned pair.</summary>
    private const double TransitionBoundTolerance = 0.01;   // K

    /// <summary>Two solves of the tree's own code on the same isentrope, or the same station reached by two paths (sequential sweep vs. alone): relative agreement to rounding at every step.</summary>
    private const double OwnCodeIsentropeTolerance = 1e-9;

    [Fact]
    public void A_cut_species_reports_one_entry_under_its_database_name()
    {
        var c = FixtureCases.Load("hp", "ap-htpb-al-fuelrich_of0.5_pc7MPa");
        var result = fixture.Solver.Solve(FixtureCases.PropellantOf(fixture.Database, c), FixtureCases.EquilibriumProblemOf(c));
        Assert.Equal(CaseStatus.Ok, result.Status);
        Assert.Equal(1, result.Species.Count(s => s == "ALN(L)"));
        Assert.DoesNotContain(result.Species, s => s.Contains('['));
        Assert.DoesNotContain(result.State.MoleFractions.Keys, k => k.Contains('['));
        Assert.True(result.State.MoleFractions["ALN(L)"] > 0.0, "the fuel-rich chamber holds liquid aluminium nitride");
        Assert.True(result.State.CondensedMassFractions.ContainsKey("ALN(L)"), "the condensed report speaks the database name");
    }

    [Fact]
    public void An_enthalpy_inside_the_ALN_gap_solves_through_the_front_door()
    {
        // The record of RejectionTests with an enthalpy inside the ALN(L) 2700 K gap: the state that had no
        // solution before the record was cut (the ⚠ of 2026-09-13 in BOOT.md); the pieces now pin at the cut.
        var record = fixture.Database["ALN(L)"];
        var bound = record.Intervals[0].THigh;
        var result = Assert.Single(fixture.Solver.SolveStates(
            [new StateRecord(RejectionTests.RecordPressure, RejectionTests.OneKilogram, Enthalpy: -1.7e6)]));
        Assert.Equal(CaseStatus.Ok, result.Status);
        Assert.True(Math.Abs(result.State.State.Temperature - bound) <= TransitionBoundTolerance,
                    $"T = {result.State.State.Temperature:R} against the cut at {bound}");
        Assert.True(result.State.MoleFractions["ALN(L)"] > 0.0, "the pinned pieces sum under the database name");
        Assert.Equal(0.0, result.State.State.CpEquilibrium);
    }

    [Fact]
    public void A_sweep_across_the_alumina_plateau_stays_on_the_isentrope_by_either_path()
    {
        // The single-exit plateau fixtures of the case matrix give the ratio band; the sequential multi-exit solve
        // is the tree's own (the reference's sequential path is what the fixtures node's guard rejects there).
        var cases = FixtureFiles.Enumerate("rocket")
            .Select(path => Path.GetFileNameWithoutExtension(path)!)
            .Where(n => n.StartsWith("ap-htpb-al-plateau_", StringComparison.Ordinal))
            .Select(n => FixtureCases.Load("rocket", n))
            .OrderBy(c => c.Inputs.GetProperty("pressureRatios")[0].GetDouble())
            .ToList();
        Assert.True(cases.Count >= 3, "the case matrix promises the plateau band and both edges");
        var ratios = cases.Select(c => c.Inputs.GetProperty("pressureRatios")[0].GetDouble()).ToList();
        var propellant = FixtureCases.PropellantOf(fixture.Database, cases[0], byMassFractions: true);
        var chamberPressure = cases[0].Inputs.GetProperty("chamberPressure").GetDouble();
        var bound = fixture.Database["AL2O3(a)"].Intervals[^1].THigh;

        var sequential = fixture.Solver.Solve(propellant, new RocketProblem { ChamberPressure = chamberPressure, PressureRatios = ratios });
        Assert.Equal(CaseStatus.Ok, sequential.Status);
        var chamber = sequential.Stations[0].State;
        var pinned = 0;
        foreach (var station in sequential.Stations)
        {
            Assert.Equal(CaseStatus.Ok, station.Status);
            Assert.True(Math.Abs(station.State.Entropy - chamber.Entropy) <= OwnCodeIsentropeTolerance * chamber.Entropy,
                        $"{station.Name}: {station.State.Entropy:R} J/(kg K) leaves the chamber isentrope at {chamber.Entropy:R}");
            if (station.MoleFractions["AL2O3(a)"] > 0.0 && station.MoleFractions["AL2O3(L)"] > 0.0)
            {
                pinned++;
                Assert.True(Math.Abs(station.State.Temperature - bound) <= TransitionBoundTolerance,
                            $"{station.Name}: pinned at {station.State.Temperature:R}, the transition bound is {bound}");
                Assert.True(station.State.GammaS is > 0.0 and < 1.0, $"{station.Name}: gamma_s {station.State.GammaS:R} on the plateau");
                Assert.Equal(0.0, station.State.CpEquilibrium);
                Assert.Equal(0.0, station.State.CvEquilibrium);
                Assert.Equal(0.0, station.State.DlnVdlnT);
            }
        }

        Assert.True(pinned >= 2, $"the ratio band must cross the plateau, {pinned} pinned stations found");

        // Path independence: each exit solved alone from its own chamber lands on the same station.
        for (var k = 0; k < cases.Count; k++)
        {
            var single = fixture.Solver.Solve(propellant, FixtureCases.RocketProblemOf(cases[k]));
            Assert.Equal(CaseStatus.Ok, single.Status);
            var alone = single.Stations[^1];
            var swept = sequential.Stations[2 + k];
            Assert.True(Math.Abs(alone.State.Temperature - swept.State.Temperature) <= OwnCodeIsentropeTolerance * swept.State.Temperature,
                        $"p_c/p {ratios[k]}: {alone.State.Temperature:R} K alone against {swept.State.Temperature:R} K in the sweep");
            var pairAlone = alone.MoleFractions["AL2O3(a)"] > 0.0 && alone.MoleFractions["AL2O3(L)"] > 0.0;
            var pairSwept = swept.MoleFractions["AL2O3(a)"] > 0.0 && swept.MoleFractions["AL2O3(L)"] > 0.0;
            Assert.True(pairAlone == pairSwept, $"p_c/p {ratios[k]}: the paths disagree on the pinned pair");
        }
    }
}
