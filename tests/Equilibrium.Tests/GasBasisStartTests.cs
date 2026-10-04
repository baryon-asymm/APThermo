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
}
