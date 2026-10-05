using APThermo.Equilibrium.Condensed;
using ILGPU;
using ILGPU.Runtime;

namespace APThermo.Equilibrium.Tests;

/// <summary>
/// The balancing record (Condensed BOOT.md, "A balancing record stays"): CaCO3 with a trace of extra oxygen has CaO(cr) at 1e-34 to 1e-14
/// kmol/kg beside it, a record the calcium balance cannot resolve, and the only carrier of the direction of the multipliers that no gas
/// carries; a Newton step returns its amount as rounding of either sign, and the removal of a record that came out negative left CaCO3
/// alone, a singular system. The facts: the states end <c>Ok</c> and clear, and the rule's three tests, each on its own.
/// </summary>
[Collection(CpuFixture.CollectionName)]
public sealed class BalancingRecordTests
{
    private const double Temperature = 300.0;

    private static readonly double[] Excesses = [1.0e-7, 1.0e-8, 1.0e-9];
    private static readonly double[] Pressures = [1.0e5, 1.0e6, 1.0e7];
    private static readonly double[] Temperatures = [300.0, 350.0, 400.0, 450.0, 500.0];
    private static readonly double[] ElementRatio = [1.0, 1.0, 3.0];

    /// <summary>The neighbourhood of the target: CaCO3 with 1e-7 to 1e-9 of extra oxygen at 100 kPa to 10 MPa and 300 to 500 K.</summary>
    public static TheoryData<string> Neighbourhood() =>
        TraceGasCases.Names(Calcite(Excesses, Pressures, Temperatures));

    /// <summary>
    /// The target, CaCO3 + 1e-7 O at 10 MPa and 300 K, which ended <c>NotConverged</c> after every start of the pass until 2026-10-05, ends
    /// <c>Ok</c>, clear of the equilibrium conditions at 1e-9 with every element within the relative invariant of 1e-13, and its heat
    /// capacity is finite and positive. CaCO3(cr) has no record below 300 K, so the neighbours of the target below it are another state;
    /// the central difference of the heat capacity is taken at 350 K, one of the three states the rule settles
    /// (<see cref="ARecordBelowTheRoundingOfItsBalanceDoesNotEndTheStateInASingularSystem"/>), where the record kept at zero moles stands in the
    /// derivative system.
    /// </summary>
    [Fact]
    public void TheTargetEndsOkAndClearAndTheHeatCapacityOfItsKindMatchesTheCentralDifference()
    {
        var state = Calcite([1.0e-7], [1.0e7], [Temperature]).Single();
        var solution = state.Solve();
        TraceGasChecks.AssertOkAndClear(solution, state.Name);
        Assert.True(double.IsFinite(solution.State.CpEquilibrium) && solution.State.CpEquilibrium > 0.0, $"{state.Name}: Cp {solution.State.CpEquilibrium}");
        var warm = Calcite([1.0e-6], [1.0e7], [350.0]).Single();
        TraceGasChecks.AssertHeatCapacityMatchesTheCentralDifference(warm, warm.Solve(), warm.Name);
    }

    /// <summary>
    /// Every state of the neighbourhood ends <c>Ok</c> and clear. Red without the rule: the target and CaCO3 + 1e-9 O at 1 MPa and 300 K end
    /// <c>NotConverged</c>, after the removal of a record of −5e-48 and −2e-49 kmol/kg.
    /// </summary>
    [Theory]
    [MemberData(nameof(Neighbourhood))]
    public void EveryStateOfTheNeighbourhoodEndsOkAndClear(string name) =>
        TraceGasChecks.AssertOkAndClear(TraceGasCases.Named(name).Solve(), name);

    /// <summary>
    /// The three states the rule settles, by name: the target, 1e-9 of extra oxygen at 1 MPa and 300 K, and 1e-6 at 10 MPa and 350 K (the
    /// last is no state of the neighbourhood, whose excesses stop at 1e-7). Each ended <c>NotConverged</c> after every start of the pass,
    /// the removal of a record of 1e-43 to 1e-49 kmol/kg having left CaCO3 alone.
    /// </summary>
    [Theory]
    [InlineData(1.0e-7, 1.0e7, 300.0)]
    [InlineData(1.0e-9, 1.0e6, 300.0)]
    [InlineData(1.0e-6, 1.0e7, 350.0)]
    public void ARecordBelowTheRoundingOfItsBalanceDoesNotEndTheStateInASingularSystem(double excess, double pressure, double temperature)
    {
        var state = Calcite([excess], [pressure], [temperature]).Single();
        TraceGasChecks.AssertOkAndClear(state.Solve(), state.Name);
    }

    /// <summary>
    /// A record at −1e-40 kmol/kg beside CaCO3 at 1e-2, with the oxygen of the gas and no gas that carries calcium or carbon, is a balancing
    /// record: <c>Update</c> changes nothing, the record stays in the set and its amount is exactly zero.
    /// </summary>
    [Fact]
    public void ARecordNegativeByLessThanTheRoundingAndTheOnlyCarrierOfADirectionStaysAtZero()
    {
        using var rig = Rig(-1.0e-40, ["O2"]);
        Assert.False(rig.Update());
        Assert.Equal(["CaCO3(cr)", "CaO(cr)"], rig.NamesInSolution());
        Assert.Equal(0.0, rig.Moles("CaO(cr)"));
        Assert.Equal(9.99e-3, rig.Moles("CaCO3(cr)"));
    }

    /// <summary>
    /// The same record at −1e-6, far above the rounding of the balances it carries, is removed: the rule keeps what the balances cannot
    /// resolve and nothing they can.
    /// </summary>
    [Fact]
    public void ARecordNegativeByMoreThanTheRoundingIsRemoved()
    {
        using var rig = Rig(-1.0e-6, ["O2"]);
        Assert.True(rig.Update());
        Assert.Equal(["CaCO3(cr)"], rig.NamesInSolution());
        Assert.Equal(0.0, rig.Moles("CaO(cr)"));
    }

    /// <summary>
    /// With atomic calcium in the gas beside the oxygen the element rows are independent without the record, so a negative one of 1e-40 is
    /// removed as before: the balancing record is kept only where its removal leaves the element system singular.
    /// </summary>
    [Fact]
    public void ARecordWhoseRemovalLeavesTheElementRowsIndependentIsRemoved()
    {
        using var rig = Rig(-1.0e-40, ["O2", "Ca"]);
        Assert.True(rig.Update());
        Assert.Equal(["CaCO3(cr)"], rig.NamesInSolution());
    }

    /// <summary>
    /// With carbon dioxide as the one gas, CaCO3 = CaO + CO2 makes the three vectors dependent whether or not the record stands, so its
    /// removal makes the system no more singular than it is and a negative record of 1e-40 is removed as before: the rule tests the
    /// rank the removal costs, not the rank of what remains.
    /// </summary>
    [Fact]
    public void ARecordThatTheSetCouldNotDoWithoutAndDoesNotNeedIsRemoved()
    {
        using var rig = Rig(-1.0e-40, ["CO2"]);
        Assert.True(rig.Update());
        Assert.Equal(["CaCO3(cr)"], rig.NamesInSolution());
    }

    private static IEnumerable<TraceGasCase> Calcite(double[] excesses, double[] pressures, double[] temperatures) =>
        excesses.SelectMany(excess => pressures.SelectMany(pressure => temperatures.Select(temperature =>
            new TraceGasCase($"calcite-oxygen|{excess:E0}|{pressure:F0}|{temperature:F0}", ["CA", "C", "O"], [1.0, 1.0, 3.0 * (1.0 + excess)], pressure, temperature))));

    /// <summary>The set CaCO3(cr) at 9.99e-3, then CaO(cr) at <paramref name="calciumOxide"/>, over the gases named at 1e-3 kmol/kg each.</summary>
    private static UpdateRig Rig(double calciumOxide, string[] gases) => new(calciumOxide, gases);

    /// <summary>A condensed set of two records over the elements Ca, C and O at 300 K, and the gases of a test, put through <c>CondensedSet.Update</c>.</summary>
    private sealed class UpdateRig : IDisposable
    {
        private readonly DerivativeRig _rig;
        private readonly MemoryBuffer1D<double, Stride1D.Dense> _elements;
        private IterationState _state;

        public UpdateRig(double calciumOxide, string[] gases)
        {
            var table = TraceGasCases.TableOver(["CA", "C", "O"]);
            var gasIndices = gases.Select(table.IndexOf).ToHashSet();
            _rig = DerivativeRig.Of(table, Temperature, j => gasIndices.Contains(j) ? 1.0e-3 : 0.0, [("CaCO3(cr)", 9.99e-3), ("CaO(cr)", calciumOxide)]);
            _elements = CpuFixture.Shared.Accelerator.Allocate1D(ElementRatio);
            foreach (var name in gases.Concat(["CaCO3(cr)", "CaO(cr)"]))
            {
                SpeciesMarks.Set(_rig.Scratch, table.IndexOf(name), SpeciesMark.Active);
            }

            _state = new IterationState { CondensedCount = 2, Temperature = Temperature, LastRemovedForRange = -1 };
            Composition.EvaluateFunctions(_rig.View, _rig.Scratch, Temperature);
        }

        public bool Update()
        {
            var problem = new EquilibriumProblem(ProblemKind.AssignedTemperaturePressure, 1.0e7, Temperature, 0.0, _elements.View);
            return CondensedSet.Update(_rig.View, problem, _rig.Scratch, _rig.Result, ref _state);
        }

        public string[] NamesInSolution() =>
            [.. Enumerable.Range(0, _state.CondensedCount).Select(c => _rig.Table.Species[_rig.Scratch.CondensedInSolution[c]])];

        public double Moles(string species) => _rig.Result.Moles[_rig.Table.IndexOf(species)];

        public void Dispose()
        {
            _elements.Dispose();
            _rig.Dispose();
        }
    }
}
