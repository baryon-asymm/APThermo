using APThermo.Fixtures;
using APThermo.Thermo;

namespace APThermo.Equilibrium.Tests;

/// <summary>
/// L1 and L2: the pinned set (StateRecord BOOT.md, <c>## Constraints</c>, 2026-10-03; the orchestrator's investigation B3
/// for 0.2.1). Inside the Al(OH)3/Al2O3/H2O(L) band of AP/HTPB/Al the three condensed element vectors are dependent, the
/// constant-temperature derivative system was singular under the old pair rule, and every hp and sp state there ended
/// <c>TemperatureOutOfRange</c>. The facts solve the fixtures of the <c>seeded</c> kind the way the reference did (a tp
/// state at 430 K, then the case warm-started from it), and then the plateau convention and its finite differences.
/// </summary>
[Collection(CpuFixture.CollectionName)]
public sealed class ReactionPlateauTests
{
    /// <summary>The seeded fixtures, from the directory listing.</summary>
    public static TheoryData<string> SeededCases() => HostSolver.Cases("seeded");

    /// <summary>The pressures of the seeded fixtures, from the same listing.</summary>
    public static TheoryData<double> SeededPressures()
    {
        var data = new TheoryData<double>();
        foreach (var pressure in HostSolver.CaseNames("seeded").Select(name => HostSolver.PressureOf(HostSolver.Load("seeded", name))).Distinct().Order())
        {
            data.Add(pressure);
        }

        return data;
    }

    /// <summary>
    /// A seeded case is solved as the reference was and agrees with it in every first-order field and every mole fraction,
    /// and is clear of the independent equilibrium conditions. The six second-order fields are compared where the reference
    /// is not on its singular signature (the band's edges), and skipped where it is, as the singular tp case is.
    /// </summary>
    [Theory]
    [MemberData(nameof(SeededCases))]
    public void ASeededCaseReproducesTheReference(string name)
    {
        var c = HostSolver.Load("seeded", name);
        Assert.True(c.Outputs.GetProperty("converged").GetBoolean(), "the reference case did not converge; it cannot serve as a reference");
        var table = HostSolver.BuildTable(CpuFixture.Shared.Database, c);

        var solution = HostSolver.SolveSeeded(CpuFixture.Shared.Accelerator, table, c);

        Assert.True(solution.Status == CaseStatus.Ok, $"status {solution.Status} after {solution.Iterations} iterations");
        var mismatches = StateComparison.Compare(c, solution, CpuFixture.Shared.Tolerances);
        Assert.True(mismatches.Count == 0, $"{mismatches.Count} mismatches after {solution.Iterations} iterations: " + string.Join("; ", mismatches));
        Assert.Empty(EquilibriumConditions.Violations(solution, Tolerances.GasChemicalPotential));
    }

    /// <summary>
    /// The fixtures hold both the band and its edges, at every pressure, so the skip of the second-order fields cannot
    /// quietly cover every case (AGENTS.md §13): each pressure has a case on the reference's singular signature and a case
    /// off it, and the band's cases all sit at one temperature, the plateau's, whatever the pressure and the problem.
    /// </summary>
    [Fact]
    public void TheBandCasesShareOnePlateauTemperatureAndHoldTheReferencesCondensedSet()
    {
        var band = SolveBand();
        var all = HostSolver.CaseNames("seeded").Select(name => HostSolver.Load("seeded", name)).ToList();
        Assert.NotEmpty(band);
        Assert.Equal(all.Select(HostSolver.PressureOf).Distinct().Count(), band.Select(b => HostSolver.PressureOf(b.Case)).Distinct().Count());
        Assert.True(band.Count < all.Count, "every seeded case is on the singular signature: the edges are missing");
        var plateau = band[0].Solution.State.Temperature;
        foreach (var (c, table, solution) in band)
        {
            Assert.Equal(CaseStatus.Ok, solution.Status);
            Assert.Empty(EquilibriumConditions.Violations(solution, Tolerances.GasChemicalPotential));
            Assert.True(Math.Abs(solution.State.Temperature - plateau) <= Tolerances.SelfConsistency * plateau,
                        $"{c.Name}: T {solution.State.Temperature:R} against the plateau's {plateau:R}");
            Assert.Equal(ReferenceCondensedCount(c, table), TreeCondensedCount(solution));
        }
    }

    /// <summary>
    /// The plateau convention (API.md of the parent, the pinned state): the equilibrium heat capacities and the
    /// temperature derivative are zero, <c>(∂ln V/∂ln p)_T</c> is real, <c>γ_s = −1/(∂ln V/∂ln p)_T</c> and
    /// <c>a² = n R T γ_s</c> with n the gaseous moles. Red under the old pair rule, which ended these states in a failure
    /// before any state was written.
    /// </summary>
    [Fact]
    public void ThePlateauConventionHoldsInTheBand()
    {
        var band = SolveBand();
        Assert.NotEmpty(band);
        foreach (var (c, table, solution) in band)
        {
            var state = solution.State;
            Assert.Equal(0.0, state.CpEquilibrium);
            Assert.Equal(0.0, state.CvEquilibrium);
            Assert.Equal(0.0, state.DlnVdlnT);
            Assert.True(state.DlnVdlnP < 0.0 && double.IsFinite(state.DlnVdlnP), $"{c.Name}: (dlnV/dlnp)_T {state.DlnVdlnP:R}");
            Assert.True(Math.Abs(state.GammaS * state.DlnVdlnP + 1.0) <= Tolerances.Exact, $"{c.Name}: gamma_s {state.GammaS:R} against -1/{state.DlnVdlnP:R}");
            var gasMoles = solution.Moles.Take(table.GasCount).Sum();
            var expected = Math.Sqrt(gasMoles * PhysicalConstants.R * state.Temperature * state.GammaS);
            Assert.True(Math.Abs(state.SoundSpeed - expected) <= Tolerances.Exact * expected, $"{c.Name}: sound speed {state.SoundSpeed:R} against {expected:R}");
            Assert.True(state.CpFrozen > 0.0, $"{c.Name}: frozen Cp {state.CpFrozen:R}");
        }
    }

    /// <summary>
    /// <c>γ_s</c> equals <c>d ln p/d ln ρ</c> along the isentrope and <c>(∂ln V/∂ln p)_T</c> equals the difference of two
    /// isothermal states, on the plateau, at each pressure of the fixtures. The mid-band state is the mean of the entropy
    /// (and of the enthalpy) of the tree's own tp states at 415.9 K and 416.0 K; the states at <c>p(1 ± ε)</c> are solved
    /// from the tp seed of 430 K, as the fixtures are. Two hp states in the band at one enthalpy are isothermal, since the
    /// plateau temperature does not depend on the pressure.
    /// </summary>
    [Theory]
    [MemberData(nameof(SeededPressures))]
    public void GammaSAndTheIsothermalDerivativeMatchTheirFiniteDifferences(double pressure)
    {
        const double epsilon = 1.0e-4;
        var c = HostSolver.Load("seeded", HostSolver.CaseNames("seeded").First(name => HostSolver.PressureOf(HostSolver.Load("seeded", name)) == pressure));
        var table = HostSolver.BuildTable(CpuFixture.Shared.Database, c);
        var accelerator = CpuFixture.Shared.Accelerator;
        var moles = HostSolver.ElementMolesOf(c);
        HostSolution Tp(double temperature) => HostSolver.Solve(
            accelerator, new EquilibriumCase(table, ProblemKind.AssignedTemperaturePressure, pressure, temperature, 0.0, moles));
        HostSolution At(ProblemKind kind, double target, double atPressure, double[] seed) => HostSolver.Solve(
            accelerator, new EquilibriumCase(table, kind, atPressure, 430.0, target, moles), (double[])seed.Clone());

        var seed = Tp(430.0);
        var below = Tp(415.9);
        var above = Tp(416.0);
        var entropy = 0.5 * (below.State.Entropy + above.State.Entropy);
        var enthalpy = 0.5 * (below.State.Enthalpy + above.State.Enthalpy);
        var isentropeUp = At(ProblemKind.AssignedEntropyPressure, entropy, pressure * (1.0 + epsilon), seed.Moles);
        var isentropeDown = At(ProblemKind.AssignedEntropyPressure, entropy, pressure * (1.0 - epsilon), seed.Moles);
        var isothermUp = At(ProblemKind.AssignedEnthalpyPressure, enthalpy, pressure * (1.0 + epsilon), seed.Moles);
        var isothermDown = At(ProblemKind.AssignedEnthalpyPressure, enthalpy, pressure * (1.0 - epsilon), seed.Moles);
        var middleIsentrope = At(ProblemKind.AssignedEntropyPressure, entropy, pressure, seed.Moles);
        var middleIsotherm = At(ProblemKind.AssignedEnthalpyPressure, enthalpy, pressure, seed.Moles);
        foreach (var solution in new[] { isentropeUp, isentropeDown, isothermUp, isothermDown, middleIsentrope, middleIsotherm })
        {
            Assert.Equal(CaseStatus.Ok, solution.Status);
        }

        var gammaDifference = (Math.Log(isentropeUp.State.Pressure) - Math.Log(isentropeDown.State.Pressure))
                              / (Math.Log(isentropeUp.State.Density) - Math.Log(isentropeDown.State.Density));
        var volumeDifference = (Math.Log(1.0 / isothermUp.State.Density) - Math.Log(1.0 / isothermDown.State.Density))
                               / (Math.Log(isothermUp.State.Pressure) - Math.Log(isothermDown.State.Pressure));

        Assert.Equal(0.0, middleIsentrope.State.CpEquilibrium);
        Assert.True(Math.Abs(gammaDifference / middleIsentrope.State.GammaS - 1.0) <= Tolerances.FiniteDifference,
                    $"gamma_s {middleIsentrope.State.GammaS:R} against d ln p / d ln rho {gammaDifference:R}");
        Assert.Equal(0.0, middleIsotherm.State.CpEquilibrium);
        Assert.True(Math.Abs(volumeDifference / middleIsotherm.State.DlnVdlnP - 1.0) <= Tolerances.FiniteDifference,
                    $"(dlnV/dlnp)_T {middleIsotherm.State.DlnVdlnP:R} against the isothermal difference {volumeDifference:R}");
    }

    /// <summary>Every seeded case on the reference's singular signature, solved as the reference was.</summary>
    private static List<(CeaCase Case, SpeciesTable Table, HostSolution Solution)> SolveBand()
    {
        var band = new List<(CeaCase, SpeciesTable, HostSolution)>();
        foreach (var name in HostSolver.CaseNames("seeded"))
        {
            var c = HostSolver.Load("seeded", name);
            var table = HostSolver.BuildTable(CpuFixture.Shared.Database, c);
            if (StateComparison.SecondOrderIsSingular(c, table))
            {
                band.Add((c, table, HostSolver.SolveSeeded(CpuFixture.Shared.Accelerator, table, c)));
            }
        }

        return band;
    }

    private static int ReferenceCondensedCount(CeaCase c, SpeciesTable table) =>
        c.Outputs.GetProperty("moleFractions").EnumerateObject()
            .Count(species => species.Value.GetDouble() > 0.0 && table.IndicesOf(species.Name) is [var first, ..] && first >= table.GasCount);

    private static int TreeCondensedCount(HostSolution solution) =>
        Enumerable.Range(solution.Case.Table.GasCount, solution.Case.Table.SpeciesCount - solution.Case.Table.GasCount)
            .Count(j => solution.Moles[j] > 0.0);
}
