using APThermo.Thermo;

namespace APThermo.Equilibrium.Tests;

/// <summary>
/// L2: the second hidden-defect audit's 2026-09-28 two-stage-threshold regression grid (BOOT.md, F1) — the
/// orchestrator's sixteen states, each a tp built directly on one of two fixtures' own table and element moles at a
/// pressure and temperature the audit named, none of them a committed fixture file of its own. Every state is
/// checked against this node's own equilibrium conditions, not against the reference (the same kind of independent
/// check <see cref="PlateauTests"/> and <see cref="ElementConservationTests"/> already run against committed
/// fixtures): element conservation at the node's invariant tolerance, every retained gas at its own chemical
/// potential (Σ a_ij π_i, RP-1311's stationarity condition, (2.15)), and no absent condensed candidate in its
/// effective range with a positive inclusion gain
/// (<see cref="PlateauTests.AnOkSolutionLeavesNoCondensedCandidateWithPositiveInclusionGain"/>'s rule, run here on
/// states no fixture covers).
///
/// Confirmed red at `5a732f0` (before the two-stage threshold, `f7c1a2d`-equivalent baseline of this task): all
/// sixteen states end `NotConverged` there (a temporary probe against a `git worktree add … 5a732f0` checkout,
/// discarded after the reading — not committed, since a scratch probe is not part of this node's contract). Green
/// after the fix (`88c1522`), all sixteen `Ok` and clear of every condition below.
/// </summary>
[Collection(CpuFixture.CollectionName)]
public sealed class RegressionStateTests
{
    /// <summary>The element-conservation invariant's tolerance (Equilibrium BOOT.md, matching <see cref="ElementConservationTests"/>).</summary>
    private const double ElementInvariant = 1e-12;

    /// <summary>
    /// The mole-fraction-weighted chemical-potential residual a retained gas at or above 1e-6 of the gas may show
    /// against Σ a_ij π_i: rounding through <c>Exp</c>/<c>Log</c> at the reported composition, not a missed
    /// stationarity condition.
    /// </summary>
    private const double GasChemicalPotentialResidual = 1e-5;

    /// <summary>Candidates this far inside their range are clear of the effective-bound shift at a crossing (matching <see cref="PlateauTests"/>).</summary>
    private const double RangeMargin = 1.5;

    private const string Example5 = "rp1311-example5_p0.344737bar";
    private const string ApHtpbAl = "ap-htpb-al_pc7MPa_shiftingEquilibrium_chamber";

    /// <summary>
    /// The audit's sixteen regression states: four on example 5's own table (loaded as its committed `hp` fixture,
    /// solved here as tp) and twelve on the AP/HTPB/Al chamber's own table (loaded as its committed `tp` fixture),
    /// at the pressures and temperatures the orchestrator named (2026-09-28).
    /// </summary>
    public static TheoryData<string, string, double, double> SixteenRegressionStates()
    {
        var data = new TheoryData<string, string, double, double>
        {
            { "hp", Example5, 1e5, 300.0 },
            { "hp", Example5, 1e5, 310.0 },
            { "hp", Example5, 1e6, 320.0 },
            { "hp", Example5, 7e6, 340.0 },
            { "tp", ApHtpbAl, 1e6, 300.0 },
            { "tp", ApHtpbAl, 1e6, 305.0 },
            { "tp", ApHtpbAl, 1e6, 310.0 },
        };
        for (var t = 300.0; t <= 330.0; t += 5.0)
        {
            data.Add("tp", ApHtpbAl, 7e6, t);
        }

        data.Add("tp", ApHtpbAl, 7e6, 335.0);
        data.Add("tp", ApHtpbAl, 7e6, 350.0);
        return data;
    }

    /// <summary>Every one of the audit's sixteen regression states converges Ok and holds every equilibrium condition below.</summary>
    [Theory]
    [MemberData(nameof(SixteenRegressionStates))]
    public void TheAuditsRegressionStateConvergesAndHoldsTheEquilibriumConditions(string fixtureKind, string fixtureName, double pressure, double temperature)
    {
        var solution = Solve(fixtureKind, fixtureName, pressure, temperature);
        Assert.Equal(CaseStatus.Ok, solution.Status);
        Assert.Empty(Violations(solution));
    }

    /// <summary>
    /// The audit's own scan (`Z2Scan2.cs`/its `scan2_old.txt` output, read at the orchestrator's direction) named
    /// eleven of example 5's states at 1 MPa and 7 MPa, 300-335 K, as failing at both commits it compared: 1 MPa at
    /// 300, 305 and 310 K, and 7 MPa across the whole band, 300 to 335 K every 5 K. Every one of the eleven is `Ok`
    /// on this node today (confirmed by this fact); none is named as still failing, so none is left unasserted.
    /// </summary>
    [Fact]
    public void TheAuditsElevenExample5StatesThatFailedAtBothCommitsAreOkNow()
    {
        var states = new List<(double Pressure, double Temperature)>
        {
            (1e6, 300.0), (1e6, 305.0), (1e6, 310.0),
        };
        for (var t = 300.0; t <= 335.0; t += 5.0)
        {
            states.Add((7e6, t));
        }

        var notOk = new List<string>();
        foreach (var (pressure, temperature) in states)
        {
            var solution = Solve("hp", Example5, pressure, temperature);
            if (solution.Status != CaseStatus.Ok)
            {
                notOk.Add($"p={pressure:R} T={temperature:R}: {solution.Status}");
                continue;
            }

            Assert.Empty(Violations(solution));
        }

        Assert.True(notOk.Count == 0, $"still failing, named rather than asserted: {string.Join("; ", notOk)}");
    }

    /// <summary>Solves the fixture's own table and element moles as a tp problem at the given pressure and temperature.</summary>
    private static HostSolution Solve(string fixtureKind, string fixtureName, double pressure, double temperature)
    {
        var c = HostSolver.Load(fixtureKind, fixtureName);
        var table = HostSolver.BuildTable(CpuFixture.Shared.Database, c);
        var elementMoles = HostSolver.ElementMolesOf(c);
        var problem = new EquilibriumCase(table, ProblemKind.AssignedTemperaturePressure, pressure, temperature, 0.0, elementMoles);
        return HostSolver.Solve(CpuFixture.Shared.Accelerator, problem);
    }

    /// <summary>Every equilibrium condition this fact checks independently of the reference; empty when the state is honest.</summary>
    private static List<string> Violations(HostSolution solution)
    {
        var table = solution.Case.Table;
        using var buffers = SpeciesTableBuffers.Upload(CpuFixture.Shared.Accelerator, table);
        var view = buffers.View;

        var violations = ElementConservationViolations(solution);
        violations.AddRange(GasChemicalPotentialViolations(solution, view));
        violations.AddRange(CondensedInclusionGainViolations(solution, view));
        return violations;
    }

    /// <summary>Every element whose conservation residual exceeds the node's own invariant tolerance.</summary>
    private static List<string> ElementConservationViolations(HostSolution solution)
    {
        var violations = new List<string>();
        var table = solution.Case.Table;
        var arrays = table.Arrays;
        var speciesCount = table.SpeciesCount;
        for (var i = 0; i < table.ElementCount; i++)
        {
            var sum = 0.0;
            for (var j = 0; j < speciesCount; j++)
            {
                sum += arrays.Stoichiometry[i * speciesCount + j] * solution.Moles[j];
            }

            var residual = Math.Abs(sum - solution.Case.ElementMoles[i]);
            var bound = ElementInvariant * Math.Max(1.0, solution.Case.ElementMoles[i]);
            if (!(residual <= bound))
            {
                violations.Add($"element {table.Elements[i]} residual {residual:E3} above {bound:E3}");
            }
        }

        return violations;
    }

    /// <summary>Every retained gas at or above 1e-6 of the gas whose chemical potential departs from Σ a_ij π_i.</summary>
    private static List<string> GasChemicalPotentialViolations(HostSolution solution, SpeciesTableView view)
    {
        var violations = new List<string>();
        var table = solution.Case.Table;
        var arrays = table.Arrays;
        var speciesCount = table.SpeciesCount;
        var temperature = solution.State.Temperature;
        var totalGasMoles = solution.Moles.Take(table.GasCount).Sum();
        var logPressureRatio = Math.Log(solution.Case.Pressure / 1e5);
        for (var j = 0; j < table.GasCount; j++)
        {
            if (!(solution.Moles[j] > 0.0))
            {
                continue;
            }

            var moleFraction = solution.Moles[j] / totalGasMoles;
            if (!(moleFraction > 1e-6))
            {
                continue;
            }

            var chemicalPotential = SpeciesFunctions.GOverRT(view, j, temperature) + Math.Log(moleFraction) + logPressureRatio;
            var elementPotential = 0.0;
            for (var i = 0; i < table.ElementCount; i++)
            {
                elementPotential += arrays.Stoichiometry[i * speciesCount + j] * solution.Multipliers[i];
            }

            var residual = Math.Abs(chemicalPotential - elementPotential);
            if (residual > GasChemicalPotentialResidual)
            {
                violations.Add($"gas {table.Species[j]} chemical-potential residual {residual:E3}");
            }
        }

        return violations;
    }

    /// <summary>Every absent condensed candidate, interior to its effective range, still showing a positive inclusion gain.</summary>
    private static List<string> CondensedInclusionGainViolations(HostSolution solution, SpeciesTableView view)
    {
        var violations = new List<string>();
        var table = solution.Case.Table;
        var arrays = table.Arrays;
        var speciesCount = table.SpeciesCount;
        var temperature = solution.State.Temperature;
        for (var j = table.GasCount; j < speciesCount; j++)
        {
            if (solution.Moles[j] > 0.0)
            {
                continue;
            }

            var start = arrays.IntervalStart[j];
            var last = start + arrays.IntervalCount[j] - 1;
            var interior = temperature >= arrays.IntervalBounds[start * 2] + RangeMargin
                           && temperature <= arrays.IntervalBounds[last * 2 + 1] - RangeMargin;
            if (!interior)
            {
                continue;
            }

            var gain = -SpeciesFunctions.GOverRT(view, j, temperature);
            for (var i = 0; i < table.ElementCount; i++)
            {
                gain += arrays.Stoichiometry[i * speciesCount + j] * solution.Multipliers[i];
            }

            if (gain > Tolerances.ResidualInclusionGain)
            {
                violations.Add($"condensed {table.Species[j]} left out with inclusion gain {gain:E3} at {temperature:F3} K");
            }
        }

        return violations;
    }
}
