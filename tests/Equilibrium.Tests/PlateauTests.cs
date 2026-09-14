using AerospacePropellantThermodynamics.Data;
using AerospacePropellantThermodynamics.Thermo;

namespace AerospacePropellantThermodynamics.Equilibrium.Tests;

/// <summary>
/// L2: states the reference cannot reach: the pinned pair at a cut, the refusal where no admissible set exists, no
/// condensed candidate with positive gain left out of an <c>Ok</c> status — checked against the node's own rule, not
/// against the reference.
/// </summary>
public sealed class PlateauTests(CpuFixture fixture) : IClassFixture<CpuFixture>
{
    /// <summary>Candidates this far inside their range are clear of the effective-bound shift at a crossing (CrossingLimit).</summary>
    private const double RangeMargin = 1.5;

    [Fact]
    public void An_enthalpy_inside_the_ALN_gap_pins_the_pieces_at_the_cut()
    {
        var c = HostSolver.Load("hp", "ap-htpb-al-fuelrich_of0.5_pc7MPa");
        var table = HostSolver.BuildTable(fixture.Database, c);
        var pieces = table.IndicesOf("ALN(L)");
        Assert.Equal(2, pieces.Count);
        var bound = table.Arrays.IntervalBounds[table.Arrays.IntervalStart[pieces[0]] * 2 + 1];   // the cut of the committed record
        var pressure = HostSolver.PressureOf(c);
        var elementMoles = HostSolver.ElementMolesOf(c);

        var below = HostSolver.Solve(fixture.Accelerator, new EquilibriumCase(table, ProblemKind.AssignedTemperaturePressure, pressure, bound - 1.0, 0.0, elementMoles));
        var above = HostSolver.Solve(fixture.Accelerator, new EquilibriumCase(table, ProblemKind.AssignedTemperaturePressure, pressure, bound + 1.0, 0.0, elementMoles));
        Assert.Equal(CaseStatus.Ok, below.Status);
        Assert.Equal(CaseStatus.Ok, above.Status);
        Assert.True(below.Moles[pieces[0]] > 0.0, "the lower piece must hold the aluminium nitride just under the cut");
        Assert.True(above.Moles[pieces[1]] > 0.0, "the upper piece must hold the aluminium nitride just over the cut");

        // The fits disagree at the cut by a real latent heat: the enthalpy gap dwarfs the smooth two-kelvin span,
        // so a target in the middle has no single-temperature solution and the rule pins both pieces at the cut.
        var gap = above.State.Enthalpy - below.State.Enthalpy;
        Assert.True(gap > 5.0 * (2.0 * below.State.CpEquilibrium), $"the cut must carry a real enthalpy gap, got {gap} J/kg");
        var target = 0.5 * (below.State.Enthalpy + above.State.Enthalpy);
        var pinned = HostSolver.Solve(fixture.Accelerator, new EquilibriumCase(table, ProblemKind.AssignedEnthalpyPressure, pressure, 0.0, target, elementMoles));
        Assert.Equal(CaseStatus.Ok, pinned.Status);
        Assert.True(pinned.Moles[pieces[0]] > 0.0 && pinned.Moles[pieces[1]] > 0.0,
                    $"both pieces must stand in the solution (n = {pinned.Moles[pieces[0]]:R}, {pinned.Moles[pieces[1]]:R})");
        Assert.True(Math.Abs(pinned.State.Temperature - bound) <= 0.01, $"T = {pinned.State.Temperature:R} against the cut at {bound}");
        Assert.True(Math.Abs(pinned.State.Enthalpy - target) <= Tolerances.SelfConsistency * Math.Abs(target), "the assigned enthalpy is met on the plateau");
        Assert.Equal(0.0, pinned.State.CpEquilibrium);
        Assert.Equal(0.0, pinned.State.CvEquilibrium);
        Assert.Equal(0.0, pinned.State.DlnVdlnT);
        Assert.True(pinned.State.GammaS > 0.0 && Math.Abs(pinned.State.GammaS * pinned.State.DlnVdlnP + 1.0) <= Tolerances.Exact,
                    $"gamma_s = -1/dlnVdlnP on the plateau, got {pinned.State.GammaS:R} and {pinned.State.DlnVdlnP:R}");
    }

    /// <summary>
    /// With only the greedy carbide and the cut record among the condensed candidates, the target enthalpy has no
    /// admissible state: the carbide's data end at 2500 K, the pieces pin at 2700 K, and the gas balance in between
    /// wants the carbide. On the way there the solver exercises the whole anti-cycling rule (BOOT.md): the first
    /// escape through the bound is forgiven and the record re-enters as the only positive candidate, the second
    /// escape stands it down, and the stand-down guard then refuses the final state rather than reporting an Ok
    /// that hides the positive-gain candidate.
    /// </summary>
    [Fact]
    public void An_enthalpy_no_admissible_set_can_hold_is_refused_rather_than_lied_about()
    {
        var c = HostSolver.Load("hp", "ap-htpb-al-fuelrich_of0.5_pc7MPa");
        var full = HostSolver.BuildTable(fixture.Database, c);
        var pieces = full.IndicesOf("ALN(L)");
        var bound = full.Arrays.IntervalBounds[full.Arrays.IntervalStart[pieces[0]] * 2 + 1];
        var pressure = HostSolver.PressureOf(c);
        var elementMoles = HostSolver.ElementMolesOf(c);
        var below = HostSolver.Solve(fixture.Accelerator, new EquilibriumCase(full, ProblemKind.AssignedTemperaturePressure, pressure, bound - 1.0, 0.0, elementMoles));
        var above = HostSolver.Solve(fixture.Accelerator, new EquilibriumCase(full, ProblemKind.AssignedTemperaturePressure, pressure, bound + 1.0, 0.0, elementMoles));
        var target = 0.5 * (below.State.Enthalpy + above.State.Enthalpy);

        var duo = SpeciesTable.Build(fixture.Database, HostSolver.ElementsOf(c),
                                     HostSolver.ProductsOf(c).Where(s => fixture.Database[s].Phase == SpeciesPhase.Gas
                                                                         || s == "AL4C3(cr)" || s == "ALN(L)").ToArray());
        var solution = HostSolver.Solve(fixture.Accelerator, new EquilibriumCase(duo, ProblemKind.AssignedEnthalpyPressure, pressure, 0.0, target, elementMoles));
        Assert.Equal(CaseStatus.NotConverged, solution.Status);
    }

    /// <summary>
    /// The anti-cycling memories may only postpone an inclusion, never lose one: after an Ok status no condensed
    /// candidate that is inside its range and without a phase partner in the solution may still offer a positive
    /// per-mole inclusion gain g_j/RT − Σ a_ij π_i &lt; 0 (BOOT.md, the condensed-species rule).
    /// </summary>
    [Theory]
    [MemberData(nameof(HostSolver.Cases), "hp", MemberType = typeof(HostSolver))]
    public void An_ok_solution_leaves_no_condensed_candidate_with_positive_inclusion_gain(string name)
    {
        var c = HostSolver.Load("hp", name);
        var table = HostSolver.BuildTable(fixture.Database, c);
        var solution = HostSolver.Solve(fixture, c);
        Assert.Equal(CaseStatus.Ok, solution.Status);
        using var buffers = SpeciesTableBuffers.Upload(fixture.Accelerator, table);
        var view = buffers.View;
        var t = solution.State.Temperature;
        var count = table.SpeciesCount;
        for (var j = table.GasCount; j < count; j++)
        {
            var start = table.Arrays.IntervalStart[j];
            var last = start + table.Arrays.IntervalCount[j] - 1;
            var interior = t >= table.Arrays.IntervalBounds[start * 2] + RangeMargin
                           && t <= table.Arrays.IntervalBounds[last * 2 + 1] - RangeMargin;
            if (solution.Moles[j] > 0.0 || !interior || PartnerInSolution(table, solution, j))
            {
                continue;
            }

            var gain = -SpeciesFunctions.GOverRT(view, j, t);
            for (var i = 0; i < table.ElementCount; i++)
            {
                gain += table.Arrays.Stoichiometry[i * count + j] * solution.Multipliers[i];
            }

            Assert.True(gain <= Tolerances.SelfConsistency, $"{name}: {table.Species[j]} left out with inclusion gain {gain:R} at {t} K");
        }
    }

    /// <summary>A condensed species with the same stoichiometry column and positive moles: the other phase of a pinned pair.</summary>
    private static bool PartnerInSolution(SpeciesTable table, HostSolution solution, int j)
    {
        for (var k = table.GasCount; k < table.SpeciesCount; k++)
        {
            if (k == j || !(solution.Moles[k] > 0.0))
            {
                continue;
            }

            var same = true;
            for (var i = 0; i < table.ElementCount && same; i++)
            {
                same = table.Arrays.Stoichiometry[i * table.SpeciesCount + k] == table.Arrays.Stoichiometry[i * table.SpeciesCount + j];
            }

            if (same)
            {
                return true;
            }
        }

        return false;
    }
}
