using APThermo.Data;
using APThermo.Thermo;

namespace APThermo.Equilibrium.Tests;

/// <summary>
/// L2: states the reference cannot reach: the pinned pair at a cut, the refusal where no admissible set exists, no
/// condensed candidate with positive gain left out of an <c>Ok</c> status — checked against the node's own rule, not
/// against the reference.
/// </summary>
public sealed class PlateauTests
{
    /// <summary>Candidates this far inside their range are clear of the effective-bound shift at a crossing (CrossingLimit).</summary>
    private const double RangeMargin = 1.5;

    /// <summary>An enthalpy inside the ALN gap pins the pieces at the cut.</summary>
    [Fact]
    public void AnEnthalpyInsideTheALNGapPinsThePiecesAtTheCut()
    {
        var c = HostSolver.Load("hp", "ap-htpb-al-fuelrich_of0.5_pc7MPa");
        var table = HostSolver.BuildTable(CpuFixture.Shared.Database, c);
        var pieces = table.IndicesOf("ALN(L)");
        Assert.Equal(2, pieces.Count);
        var bound = table.Arrays.IntervalBounds[table.Arrays.IntervalStart[pieces[0]] * 2 + 1];   // the cut of the committed record
        var pressure = HostSolver.PressureOf(c);
        var elementMoles = HostSolver.ElementMolesOf(c);

        var below = HostSolver.Solve(CpuFixture.Shared.Accelerator, new EquilibriumCase(table, ProblemKind.AssignedTemperaturePressure, pressure, bound - 1.0, 0.0, elementMoles));
        var above = HostSolver.Solve(CpuFixture.Shared.Accelerator, new EquilibriumCase(table, ProblemKind.AssignedTemperaturePressure, pressure, bound + 1.0, 0.0, elementMoles));
        Assert.Equal(CaseStatus.Ok, below.Status);
        Assert.Equal(CaseStatus.Ok, above.Status);
        Assert.True(below.Moles[pieces[0]] > 0.0, "the lower piece must hold the aluminium nitride just under the cut");
        Assert.True(above.Moles[pieces[1]] > 0.0, "the upper piece must hold the aluminium nitride just over the cut");

        // The fits disagree at the cut by a real latent heat: the enthalpy gap dwarfs the smooth two-kelvin span,
        // so a target in the middle has no single-temperature solution and the rule pins both pieces at the cut.
        var gap = above.State.Enthalpy - below.State.Enthalpy;
        Assert.True(gap > 5.0 * (2.0 * below.State.CpEquilibrium), $"the cut must carry a real enthalpy gap, got {gap} J/kg");
        var target = 0.5 * (below.State.Enthalpy + above.State.Enthalpy);
        var pinned = HostSolver.Solve(CpuFixture.Shared.Accelerator, new EquilibriumCase(table, ProblemKind.AssignedEnthalpyPressure, pressure, 0.0, target, elementMoles));
        Assert.Equal(CaseStatus.Ok, pinned.Status);
        Assert.True(pinned.Moles[pieces[0]] > 0.0 && pinned.Moles[pieces[1]] > 0.0,
                    $"both pieces must stand in the solution (n = {pinned.Moles[pieces[0]]:R}, {pinned.Moles[pieces[1]]:R})");
        Assert.True(Math.Abs(pinned.State.Temperature - bound) <= Tolerances.PlateauCutTolerance, $"T = {pinned.State.Temperature:R} against the cut at {bound}");
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
    public void AnEnthalpyNoAdmissibleSetCanHoldIsRefusedRatherThanLiedAbout()
    {
        var c = HostSolver.Load("hp", "ap-htpb-al-fuelrich_of0.5_pc7MPa");
        var full = HostSolver.BuildTable(CpuFixture.Shared.Database, c);
        var pieces = full.IndicesOf("ALN(L)");
        var bound = full.Arrays.IntervalBounds[full.Arrays.IntervalStart[pieces[0]] * 2 + 1];
        var pressure = HostSolver.PressureOf(c);
        var elementMoles = HostSolver.ElementMolesOf(c);
        var below = HostSolver.Solve(CpuFixture.Shared.Accelerator, new EquilibriumCase(full, ProblemKind.AssignedTemperaturePressure, pressure, bound - 1.0, 0.0, elementMoles));
        var above = HostSolver.Solve(CpuFixture.Shared.Accelerator, new EquilibriumCase(full, ProblemKind.AssignedTemperaturePressure, pressure, bound + 1.0, 0.0, elementMoles));
        var target = 0.5 * (below.State.Enthalpy + above.State.Enthalpy);

        var duo = SpeciesTable.Build(CpuFixture.Shared.Database, HostSolver.ElementsOf(c),
                                     [.. HostSolver.ProductsOf(c).Where(s => CpuFixture.Shared.Database[s].Phase == SpeciesPhase.Gas
                                                                             || s == "AL4C3(cr)" || s == "ALN(L)")]);
        var solution = HostSolver.Solve(CpuFixture.Shared.Accelerator, new EquilibriumCase(duo, ProblemKind.AssignedEnthalpyPressure, pressure, 0.0, target, elementMoles));
        Assert.Equal(CaseStatus.NotConverged, solution.Status);
    }

    /// <summary>
    /// The anti-cycling memories may only postpone an inclusion, never lose one: after an Ok status no condensed
    /// candidate that is inside its range and without a phase partner in the solution may still offer a positive
    /// per-mole inclusion gain g_j/RT − Σ a_ij π_i &lt; 0 (BOOT.md, the condensed-species rule).
    /// </summary>
    [Theory]
    [MemberData(nameof(HostSolver.Cases), "hp", MemberType = typeof(HostSolver))]
    public void AnOkSolutionLeavesNoCondensedCandidateWithPositiveInclusionGain(string name)
    {
        var c = HostSolver.Load("hp", name);
        var table = HostSolver.BuildTable(CpuFixture.Shared.Database, c);
        var solution = HostSolver.Solve(CpuFixture.Shared, c);
        Assert.Equal(CaseStatus.Ok, solution.Status);
        using var buffers = SpeciesTableBuffers.Upload(CpuFixture.Shared.Accelerator, table);
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

            Assert.True(gain <= Tolerances.ResidualInclusionGain, $"{name}: {table.Species[j]} left out with inclusion gain {gain:R} at {t} K");
        }
    }

    /// <summary>
    /// A record stood down by the anti-cycling rule is out of play "for the rest of it" (BOOT.md): it may not be found as an
    /// adjacent record of its own formula, nor as a phase at a temperature inside its own range, so it cannot be paired or
    /// switched back in. Exercised directly on the geometry, since no fixture reaches a state where a stood-down record has
    /// an adjacent, in-play record of its own formula to be wrongly rediscovered beside (BOOT.md, the defect note on this
    /// rule's own history).
    /// </summary>
    [Fact]
    public void AStoodDownRecordIsNeitherAdjacentToNorFoundBesideItsInPlayPartner()
    {
        var c = HostSolver.Load("hp", "ap-htpb-al-fuelrich_of0.5_pc7MPa");
        var table = HostSolver.BuildTable(CpuFixture.Shared.Database, c);
        var pieces = table.IndicesOf("ALN(L)");
        Assert.Equal(2, pieces.Count);
        var lower = pieces[0];
        var upper = pieces[1];

        using var buffers = SpeciesTableBuffers.Upload(CpuFixture.Shared.Accelerator, table);
        var speciesCount = table.SpeciesCount;
        var elementCount = table.ElementCount;
        using var doubles = CpuFixture.Shared.Accelerator.Allocate1D<double>(ScratchLayout.DoublesPerCase(speciesCount, elementCount));
        using var ints = CpuFixture.Shared.Accelerator.Allocate1D<int>(ScratchLayout.IntsPerCase(speciesCount, elementCount));
        var scratch = EquilibriumScratch.Slice(doubles.View, ints.View, speciesCount, elementCount);
        for (var j = 0; j < speciesCount; j++)
        {
            SpeciesMarks.Set(scratch, j, SpeciesMark.Active);
        }

        var view = buffers.View;
        var effectiveHigh = PhaseGeometry.EffectiveHigh(in view, in scratch, lower);
        SpeciesMarks.Set(scratch, lower, SpeciesMark.StoodDown);

        Assert.Equal(-1, PhaseGeometry.Adjacent(in view, in scratch, upper, above: false));
        Assert.Equal(-1, PhaseGeometry.PhaseAt(in view, in scratch, 0, upper, effectiveHigh - 0.001));
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
