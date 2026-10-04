namespace APThermo.Equilibrium.Tests;

/// <summary>
/// Trace excesses (TraceGas BOOT.md, <c>## Acceptance criteria</c>, seam (a) and the starts): a condensed phase beside a gas that is
/// a trace of the mixture, where the failed iterate of the reduced iteration holds no condensed species or the wrong ones. Each
/// state is <c>Ok</c> and clear of the equilibrium conditions at 1e-9, every reported gas on its stationarity included.
/// </summary>
[Collection(CpuFixture.CollectionName)]
public sealed class TraceExcessTests
{
    /// <summary>CaCO3 + d CO2 (1e-8 to 1e-5) at 1 kPa to 10 MPa, 0.01 to 300 K below its plateau.</summary>
    public static TheoryData<string> Calcite() => TraceGasCases.Names(TraceGasCases.CalciteBelowThePlateau());

    /// <summary>MgCO3 + 1e-6 CO2 below its plateau at 1 kPa to 1 MPa.</summary>
    public static TheoryData<string> Magnesite() => TraceGasCases.Names(TraceGasCases.MagnesiteBelowThePlateau());

    /// <summary>Al2O3 + 1e-10 and 1e-8 O, KCl + 1e-10 Cl and CaCO3 + 1e-6 O.</summary>
    public static TheoryData<string> Degenerate() => TraceGasCases.Names(TraceGasCases.DegenerateExcesses());

    /// <summary>The calcite family: red on the reduced iteration alone.</summary>
    [Theory]
    [MemberData(nameof(Calcite))]
    public void CalciteWithATraceOfCarbonDioxideBelowItsPlateauIsOkAndClear(string name) =>
        TraceGasChecks.AssertOkAndClear(TraceGasCases.Named(name).Solve(), name);

    /// <summary>The magnesite family: red on the reduced iteration alone.</summary>
    [Theory]
    [MemberData(nameof(Magnesite))]
    public void MagnesiteWithATraceOfCarbonDioxideBelowItsPlateauIsOkAndClear(string name) =>
        TraceGasChecks.AssertOkAndClear(TraceGasCases.Named(name).Solve(), name);

    /// <summary>The degenerate family, whose failed iterate holds no condensed species or the wrong ones: the starts from the phase-one point.</summary>
    [Theory]
    [MemberData(nameof(Degenerate))]
    public void ADegenerateExcessIsOkAndClear(string name) =>
        TraceGasChecks.AssertOkAndClear(TraceGasCases.Named(name).Solve(), name);
}
