using APThermo.Equilibrium.Newton;
using APThermo.Thermo;
using ILGPU.Runtime;

namespace APThermo.Equilibrium.Tests;

/// <summary>
/// L0 and L2: rule A tied to a linear combination of element rows (Newton BOOT.md, "Rule A: an element tie", 2026-10-03).
/// With only Ar, CO2, H2O and N2 retained on RP-1311 example 1's table, the oxygen row equals 2·C + ½·H, which no pair
/// of elements expresses: the unit facts drive <see cref="ElementCoupling"/> on the tables of the fixtures node's
/// three-element family, and the grid fact solves the states the equilibrium node ended <c>SingularMatrix</c> on before
/// the rule was generalized, 77 of 168 on <c>main</c>, and checks each against the node's own equilibrium conditions.
/// </summary>
[Collection(CpuFixture.CollectionName)]
public sealed class ThreeElementTieTests
{
    private const string Example1 = "three-element_rp1311-example1_r1.0_p1atm_T300";
    private const string Example12 = "three-element_rp1311-example12_p1000psi_T300";

    /// <summary>
    /// How far a coefficient of the combination may sit from its exact value: the normal equations are solved in double
    /// precision, so only the rounding of a small elimination separates them from the integers and halves of the formulas.
    /// </summary>
    private const double CoefficientTolerance = 1.0e-12;

    /// <summary>
    /// The mole-fraction-weighted chemical-potential residual a retained gas may show against Σ a_ij π_i on the grid:
    /// the audit's own bound for a converged, polished state, as in <see cref="TiedReleaseTests"/>.
    /// </summary>
    private const double GasChemicalPotentialResidual = 1.0e-9;

    /// <summary>
    /// K: the half-width of the central difference of the enthalpy that <c>Cp_eq</c> is compared with. Measured on the
    /// grid (the 168 states, the four whose condensed set changes inside the step left out): the largest relative
    /// deviation is 8.2e-7 at 0.001 K (the rounding of the enthalpy, which the difference divides by 2δ), 4.7e-8 at
    /// 0.01 K, 4.9e-6 at 0.1 K, 4.9e-4 at 1 K and of order one at 10 K (the curvature of <c>Cp_eq</c> near the onset of
    /// the liquid, which the wider steps cross), so 0.01 K sits at the minimum.
    /// </summary>
    private const double TemperatureStep = 0.01;

    /// <summary>
    /// The relative deviation of <c>Cp_eq</c> from the central difference that the grid may show: 1e-6, twenty times the
    /// largest deviation measured at <see cref="TemperatureStep"/>, 4.7e-8.
    /// </summary>
    private const double HeatCapacityTolerance = 1.0e-6;

    /// <summary>
    /// The relative deviation of the temperature an hp or sp solve may show against the tp state whose enthalpy or entropy
    /// it was given: 1e-9, two hundred times the largest measured on the grid, 5.2e-12.
    /// </summary>
    private const double TemperatureReproduction = 1.0e-9;

    /// <summary>The pressure factors of the audit's fuzz grid, applied to each table's own fixture pressure.</summary>
    private static readonly double[] PressureFactors = [1.0, 1.0e-3, 1.0e-2, 0.1, 10.0, 100.0];

    /// <summary>The temperatures of the grid, K: the two the families of the fixtures node record.</summary>
    private static readonly double[] Temperatures = [300.0, 600.0];

    /// <summary>
    /// With moles on Ar, CO2, H2O and N2 only, the combination for O is <c>c_C = 2</c>, <c>c_H = ½</c> and every other
    /// coefficient 0, and the tie holds. Red on <c>main</c>, whose <c>Find</c> knows pairs only and returns no tie.
    /// </summary>
    [Fact]
    public void WithMolesOnArCo2H2oAndN2OnlyTheOxygenRowIsTwiceCarbonPlusHalfHydrogen()
    {
        using var rig = TieRig.Of(Example1);
        rig.PutMoles("Ar", "CO2", "H2O", "N2");
        var oxygen = rig.ElementIndex("O");

        var tie = ElementCoupling.Find(rig.View, rig.Scratch, rig.Result, condensedCount: 0, oxygen);

        Assert.True(tie.Active);
        Assert.Equal(oxygen, tie.Element);
        var coefficients = rig.Coefficients();
        for (var i = 0; i < coefficients.Length; i++)
        {
            var expected = i == rig.ElementIndex("C") ? 2.0 : i == rig.ElementIndex("H") ? 0.5 : 0.0;
            Assert.True(Math.Abs(coefficients[i] - expected) <= CoefficientTolerance,
                        $"element {rig.Table.Elements[i]}: coefficient {coefficients[i]:R}, expected {expected:R}");
        }

        Assert.True(ElementCoupling.Coupled(rig.View, rig.Scratch, rig.Result, condensedCount: 0, tie));
    }

    /// <summary>
    /// The tie stops holding once H2 or O2 has moles, the release of rule A: each of them tells the combination apart, H2
    /// by hydrogen without oxygen, O2 by oxygen without carbon or hydrogen. <c>Find</c> then returns no tie either.
    /// </summary>
    [Theory]
    [InlineData("H2")]
    [InlineData("O2")]
    public void TheTieStopsHoldingOnceH2OrO2HasMoles(string breaker)
    {
        using var rig = TieRig.Of(Example1);
        rig.PutMoles("Ar", "CO2", "H2O", "N2");
        var oxygen = rig.ElementIndex("O");
        var tie = ElementCoupling.Find(rig.View, rig.Scratch, rig.Result, condensedCount: 0, oxygen);
        Assert.True(tie.Active);

        rig.PutMoles(breaker);

        Assert.False(ElementCoupling.Coupled(rig.View, rig.Scratch, rig.Result, condensedCount: 0, tie));
        Assert.False(ElementCoupling.Find(rig.View, rig.Scratch, rig.Result, condensedCount: 0, oxygen).Active);
    }

    /// <summary>
    /// On the example 12 table with <c>H2O(L)</c> in the solution the tie of O is held by a condensed species, which
    /// carries the tied element and hydrogen, an element of the combination; with no condensed species in the solution
    /// nothing holds it. This is rule A's "at once" trigger.
    /// </summary>
    [Fact]
    public void WithH2oLiquidInTheSolutionTheTieIsHeldByACondensedSpecies()
    {
        using var rig = TieRig.Of(Example12);
        rig.PutMoles("CO2", "H2O", "N2");
        rig.Scratch.CondensedInSolution[0] = rig.Table.IndexOf("H2O(L)");
        var oxygen = rig.ElementIndex("O");

        var tie = ElementCoupling.Find(rig.View, rig.Scratch, rig.Result, condensedCount: 1, oxygen);

        Assert.True(tie.Active);
        Assert.True(ElementCoupling.HeldByCondensed(rig.View, rig.Scratch, condensedCount: 1, tie));
        Assert.False(ElementCoupling.HeldByCondensed(rig.View, rig.Scratch, condensedCount: 0, tie));
    }

    /// <summary>
    /// Every state of the grid ends <c>Ok</c>, clears every independent equilibrium condition, and its <c>Cp_eq</c> equals
    /// a central difference of the solver's own enthalpy at <c>T ± δ</c> (<see cref="TemperatureStep"/>) to
    /// <see cref="HeatCapacityTolerance"/>: the 14 tp tables of RP-1311 examples 1 and 12 at their own pressure times each
    /// factor, at 300 K and at 600 K. On <c>main</c> 77 of the 168 end <c>SingularMatrix</c>; a tie that survives to the
    /// close, which the release exists to prevent, leaves <c>Cp_eq</c> short of the difference (shown red once, with the
    /// release's condition always false). A state whose condensed set differs between <c>T − δ</c>, <c>T</c> and
    /// <c>T + δ</c> is not compared: the enthalpy then jumps by a latent heat, and the slope of one side is not the
    /// derivative at the other. Those are the states at 600 K that hold <c>H2O(L)</c>, the upper bound of its record, and
    /// the fact allows at most a tenth of the grid to be left out so that it cannot pass over the whole set. The count of
    /// the states walked is the product of the lists, so the fact fails on an empty set as well.
    /// </summary>
    [Fact]
    public void EveryStateOfTheExample1And12GridConvergesAndHoldsTheEquilibriumConditions()
    {
        var names = GridNames();
        var walked = 0;
        var straddling = 0;
        var failures = new List<string>();
        foreach (var (name, problem) in names.SelectMany(GridProblems))
        {
            walked++;
            var solution = HostSolver.Solve(CpuFixture.Shared.Accelerator, problem);
            var violations = solution.Status == CaseStatus.Ok ? EquilibriumConditions.Violations(solution, GasChemicalPotentialResidual) : [];
            var deviation = solution.Status == CaseStatus.Ok ? HeatCapacityDeviation(solution) : null;
            straddling += deviation is null && solution.Status == CaseStatus.Ok ? 1 : 0;
            if (solution.Status != CaseStatus.Ok || violations.Count > 0 || deviation > HeatCapacityTolerance)
            {
                failures.Add($"{name} p={problem.Pressure:R} T={problem.Temperature:R}: {solution.Status} {string.Join("; ", violations)} Cp_eq deviation {deviation:E2}");
            }
        }

        Assert.Equal(names.Count * PressureFactors.Length * Temperatures.Length, walked);
        Assert.True(straddling * 10 < walked, $"{straddling} of {walked} states left out of the heat-capacity comparison");
        Assert.True(failures.Count == 0, $"{failures.Count} of {walked} states: {string.Join(" | ", failures)}");
    }

    /// <summary>
    /// The enthalpy or the entropy of every tp state of the grid, solved back at the same pressure, gives the tp
    /// temperature to <see cref="TemperatureReproduction"/>. A state is left out only when the tp state holds a condensed
    /// species and the hp or sp solve lands on the supercooled vapour branch: its temperature is below the lower bound of
    /// that species' record, or the descent leaves the window (<c>TemperatureOutOfRange</c>). Example 12's table has
    /// <c>H2O(L)</c> from 273.15 K and no <c>H2O(cr)</c>, so below that bound the vapour alone has no enthalpy of the
    /// liquid's value and the descent from the hot start never meets the liquid branch; the same holds for the entropy.
    /// Measured: 155 of the 168 hp solves and 158 of the sp solves reproduce the tp temperature (to 1.1e-12 and 5.2e-12);
    /// the 13 and 10 left out are example 12's states with liquid at 300 K (ten of them in both), and for hp three liquid states at 600 K. The fact
    /// allows a fifth of the grid to be left out so that it cannot pass over the whole set.
    /// </summary>
    [Theory]
    [InlineData("hp")]
    [InlineData("sp")]
    public void TheEnthalpyOrEntropyOfEveryTpStateOfTheGridGivesBackItsTemperature(string kind)
    {
        var names = GridNames();
        var walked = 0;
        var left = 0;
        var failures = new List<string>();
        foreach (var (name, problem) in names.SelectMany(GridProblems))
        {
            walked++;
            var tp = HostSolver.Solve(CpuFixture.Shared.Accelerator, problem);
            var back = HostSolver.Solve(CpuFixture.Shared.Accelerator, Counterpart(tp, kind));
            var deviation = back.Status == CaseStatus.Ok ? Math.Abs(back.State.Temperature - problem.Temperature) / problem.Temperature : double.PositiveInfinity;
            if (tp.Status == CaseStatus.Ok && LandsOnTheSupercooledVapour(tp, back))
            {
                left++;
            }
            else if (tp.Status != CaseStatus.Ok || deviation > TemperatureReproduction)
            {
                failures.Add($"{name} p={problem.Pressure:R} T={problem.Temperature:R}: tp {tp.Status}, {kind} {back.Status}, T {back.State.Temperature:R}");
            }
        }

        Assert.Equal(names.Count * PressureFactors.Length * Temperatures.Length, walked);
        Assert.True(left * 5 < walked, $"{left} of {walked} states left out of the {kind} comparison");
        Assert.True(failures.Count == 0, $"{failures.Count} of {walked} states: {string.Join(" | ", failures)}");
    }

    /// <summary>The fixture names of the grid: the tp tables of RP-1311 examples 1 and 12.</summary>
    private static List<string> GridNames()
    {
        var names = HostSolver.CaseNames("tp")
            .Where(n => n.StartsWith("rp1311-example1_", StringComparison.Ordinal) || n.StartsWith("rp1311-example12_", StringComparison.Ordinal))
            .ToList();
        Assert.NotEmpty(names);
        return names;
    }

    /// <summary>
    /// <c>|Cp_eq − (h(T + δ) − h(T − δ)) / 2δ| / Cp_eq</c> over the solver's own tp solves at the neighbouring temperatures;
    /// positive infinity when a neighbour does not end <c>Ok</c>, and null when the held condensed set differs among the
    /// three states, where no slope is comparable.
    /// </summary>
    private static double? HeatCapacityDeviation(HostSolution centre)
    {
        var problem = centre.Case;
        var up = HostSolver.Solve(CpuFixture.Shared.Accelerator, problem with { Temperature = problem.Temperature + TemperatureStep });
        var down = HostSolver.Solve(CpuFixture.Shared.Accelerator, problem with { Temperature = problem.Temperature - TemperatureStep });
        if (up.Status != CaseStatus.Ok || down.Status != CaseStatus.Ok)
        {
            return double.PositiveInfinity;
        }

        if (HeldCondensed(up) != HeldCondensed(centre) || HeldCondensed(down) != HeldCondensed(centre))
        {
            return null;
        }

        var slope = (up.State.Enthalpy - down.State.Enthalpy) / (2.0 * TemperatureStep);
        return Math.Abs(slope - centre.State.CpEquilibrium) / centre.State.CpEquilibrium;
    }

    /// <summary>One character per condensed species of the table: 1 when the solution holds it.</summary>
    private static string HeldCondensed(HostSolution solution) =>
        string.Concat(Enumerable.Range(solution.Case.Table.GasCount, solution.Case.Table.CondensedCount).Select(j => solution.Moles[j] > 0.0 ? '1' : '0'));

    /// <summary>The problem of the given kind (hp or sp) whose target is the enthalpy or the entropy of <paramref name="tp"/>, at its pressure and element moles.</summary>
    private static EquilibriumCase Counterpart(HostSolution tp, string kind) =>
        kind == "hp"
            ? tp.Case with { Kind = ProblemKind.AssignedEnthalpyPressure, Temperature = 0.0, Target = tp.State.Enthalpy }
            : tp.Case with { Kind = ProblemKind.AssignedEntropyPressure, Temperature = 0.0, Target = tp.State.Entropy };

    /// <summary>
    /// True when <paramref name="tp"/> holds a condensed species and <paramref name="back"/>, its hp or sp counterpart, is
    /// the vapour alone below the lowest lower bound of the records of the species held, or left the window.
    /// </summary>
    private static bool LandsOnTheSupercooledVapour(HostSolution tp, HostSolution back)
    {
        var held = HeldCondensed(tp);
        if (!held.Contains('1', StringComparison.Ordinal))
        {
            return false;
        }

        if (back.Status == CaseStatus.TemperatureOutOfRange)
        {
            return true;
        }

        using var buffers = SpeciesTableBuffers.Upload(CpuFixture.Shared.Accelerator, tp.Case.Table);
        var lowest = Enumerable.Range(0, held.Length)
            .Where(c => held[c] == '1')
            .Min(c => SpeciesFunctions.RecordLow(buffers.View, tp.Case.Table.GasCount + c));
        return back.Status == CaseStatus.Ok && back.State.Temperature < lowest;
    }

    /// <summary>The tp problems of one table of the grid: the fixture's own table and element moles at each pressure factor and temperature.</summary>
    private static IEnumerable<(string Name, EquilibriumCase Problem)> GridProblems(string fixtureName)
    {
        var c = HostSolver.Load("tp", fixtureName);
        var table = HostSolver.BuildTable(CpuFixture.Shared.Database, c);
        foreach (var factor in PressureFactors)
        {
            foreach (var temperature in Temperatures)
            {
                yield return (fixtureName, new EquilibriumCase(table, ProblemKind.AssignedTemperaturePressure,
                                                               HostSolver.PressureOf(c) * factor, temperature, 0.0, HostSolver.ElementMolesOf(c)));
            }
        }
    }

    /// <summary>One table of the fixtures node, with the scratch and the result views of one case over a CPU-accelerator buffer set, for the queries of rule A.</summary>
    private sealed class TieRig : IDisposable
    {
        private readonly SpeciesTableBuffers _tableBuffers;
        private readonly List<IDisposable> _buffers = [];

        private TieRig(SpeciesTable table, SpeciesTableBuffers tableBuffers, EquilibriumScratch scratch, EquilibriumResult result)
        {
            Table = table;
            _tableBuffers = tableBuffers;
            Scratch = scratch;
            Result = result;
        }

        public SpeciesTable Table { get; }

        public SpeciesTableView View => _tableBuffers.View;

        public EquilibriumScratch Scratch { get; }

        public EquilibriumResult Result { get; }

        /// <summary>The table of the named tp fixture, every element active, no moles anywhere.</summary>
        public static TieRig Of(string fixtureName)
        {
            var accelerator = CpuFixture.Shared.Accelerator;
            var table = HostSolver.BuildTable(CpuFixture.Shared.Database, HostSolver.Load("tp", fixtureName));
            var tableBuffers = SpeciesTableBuffers.Upload(accelerator, table);
            var view = tableBuffers.View;
            var doubles = accelerator.Allocate1D<double>(ScratchLayout.DoublesPerCase(view.SpeciesCount, view.ElementCount));
            var ints = accelerator.Allocate1D<int>(ScratchLayout.IntsPerCase(view.SpeciesCount, view.ElementCount));
            var moles = accelerator.Allocate1D<double>(view.SpeciesCount);
            var multipliers = accelerator.Allocate1D<double>(view.ElementCount);
            var state = accelerator.Allocate1D<MixtureState>(1);
            var status = accelerator.Allocate1D<int>(1);
            var iterations = accelerator.Allocate1D<int>(1);
            doubles.MemSetToZero();   // accelerator memory is not zeroed on allocation
            ints.MemSetToZero();
            moles.MemSetToZero();
            var scratch = EquilibriumScratch.Slice(doubles.View, ints.View, view.SpeciesCount, view.ElementCount);
            for (var i = 0; i < view.ElementCount; i++)
            {
                scratch.ElementActive[i] = 1;
            }

            var rig = new TieRig(table, tableBuffers, scratch, new EquilibriumResult(moles.View, multipliers.View, state.View, status.View, iterations.View));
            rig._buffers.AddRange([doubles, ints, moles, multipliers, state, status, iterations]);
            return rig;
        }

        public int ElementIndex(string element) => Table.Elements.ToList().IndexOf(element);

        /// <summary>Gives each named gaseous species a positive mole number: it enters the sums.</summary>
        public void PutMoles(params string[] species)
        {
            foreach (var name in species)
            {
                Result.Moles[Table.IndexOf(name)] = 0.01;
            }
        }

        /// <summary>The coefficients <see cref="ElementCoupling.Find"/> left in the scratch, one per element.</summary>
        public double[] Coefficients() => [.. Enumerable.Range(0, Table.ElementCount).Select(i => Scratch.Tie.Elements.Coefficients[i])];

        public void Dispose()
        {
            foreach (var buffer in _buffers)
            {
                buffer.Dispose();
            }

            _tableBuffers.Dispose();
        }
    }
}
