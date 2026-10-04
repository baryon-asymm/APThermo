namespace APThermo.Equilibrium.Tests;

/// <summary>
/// The gas basis (TraceGas BOOT.md, "The starts", the fifth, 2026-10-05): the tp states no earlier start settled, each an excess of the
/// anion-forming element beside its oxide (Li2O + 1e-10 O, CaCO3 + 1e-8 O) or an Al(OH)3 deficit of oxygen at 500 K, end <c>Ok</c>, clear of
/// the equilibrium conditions at 1e-9 with every element within the relative invariant. Red on the four starts alone: all nine end
/// <c>NotConverged</c> (Li2O on the metal-rich root of the gas, CaCO3 without its balancing CaO, Al(OH)3 with liquid water for vapour).
/// </summary>
[Collection(CpuFixture.CollectionName)]
public sealed class GasBasisStartTests
{
    /// <summary>The nine states declared <c>NotConverged</c> until 2026-10-05.</summary>
    internal static readonly string[] Settled =
    [
        "Al(OH)3|-1E-08|1000|500", "Al(OH)3|-1E-10|100000|500", "Al(OH)3|-1E-12|100000|500",
        "Li2O|1E-10|1000|800", "Li2O|1E-10|100000|800",
        "CaCO3|1E-08|1000|500", "CaCO3|1E-08|1000|800", "CaCO3|1E-08|100000|800", "CaCO3|1E-08|10000000|800",
    ];

    /// <summary>The nine states, from the scans they belong to.</summary>
    public static TheoryData<string> States() =>
        TraceGasCases.Names(TraceGasCases.TraceScan().Concat(TraceGasCases.ScanFamilies()).DistinctBy(state => state.Name).Where(state => Settled.Contains(state.Name)));

    /// <summary>Each state ends <c>Ok</c> and clear.</summary>
    [Theory]
    [MemberData(nameof(States))]
    public void AStateNoEarlierStartSettlesIsOkAndClearFromTheGasBasis(string name) =>
        TraceGasChecks.AssertOkAndClear(TraceGasCases.Named(name).Solve(), name);

    /// <summary>The theory walks all nine: a name that left its scan would otherwise drop out silently.</summary>
    [Fact]
    public void TheNineStatesAreAllInTheScans() => Assert.Equal(Settled.Length, States().Count);

    /// <summary>
    /// The program with the gases as columns: for Li2O + 1e-10 O at 800 K and 1 kPa the basis is Li2O(cr) holding the lithium and O2 the
    /// excess of oxygen, b_O − b_Li/2, as O2; for CaCO3 + 1e-8 O at 500 K and 1 kPa it holds CaO(cr) at zero level beside CaCO3(cr) and O2,
    /// the balancing phase; for Al(OH)3 − 1e-8 O at 500 K and 1 kPa it holds water as vapour and the hydrogen the oxygen cannot bind as H2.
    /// </summary>
    [Fact]
    public void TheProgramWithTheGasesAsColumnsHoldsTheCarrierAndTheBalancingPhase()
    {
        var all = TraceGasCases.TraceScan().ToDictionary(state => state.Name);
        var lithia = all["Li2O|1E-10|1000|800"];
        var basis = GasPhaseRig.GasBasisOf(lithia.AsCase());
        Assert.NotNull(basis);
        Assert.Equal(["Li2O(cr)", "O2"], basis.Keys.Order(StringComparer.Ordinal));
        var b = lithia.ElementMoles;
        Assert.Equal(b[0] / 2.0, basis["Li2O(cr)"], b[0] * 1.0e-14);
        var excess = (b[1] - b[0] / 2.0) / 2.0;
        Assert.Equal(excess, basis["O2"], excess * 1.0e-4);

        var calcite = GasPhaseRig.GasBasisOf(all["CaCO3|1E-08|1000|500"].AsCase());
        Assert.NotNull(calcite);
        Assert.Equal(["CaCO3(cr)", "CaO(cr)", "O2"], calcite.Keys.Order(StringComparer.Ordinal));
        Assert.True(calcite["CaO(cr)"] == 0.0, $"CaO(cr) at {calcite["CaO(cr)"]}");

        var gibbsite = GasPhaseRig.GasBasisOf(all["Al(OH)3|-1E-08|1000|500"].AsCase());
        Assert.NotNull(gibbsite);
        Assert.Equal(["AL2O3(a)", "H2", "H2O"], gibbsite.Keys.Order(StringComparer.Ordinal));
    }
}
