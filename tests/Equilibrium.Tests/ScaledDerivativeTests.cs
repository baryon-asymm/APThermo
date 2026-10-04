namespace APThermo.Equilibrium.Tests;

/// <summary>
/// The scaled retry of the derivative system (StateRecord BOOT.md, <c>## Constraints</c>, "Two retries of a singular derivative system"):
/// a trace-gas state, the gas of which is 1e-8 to 1e-6 of the mixture beside condensed species of order one, leaves the multiplier pivots
/// of the plain constant-temperature system below the row scales the condensed columns set, and the system is solved again with the
/// condensed columns carried relative to n. Each state is <c>Ok</c> and its equilibrium heat capacity equals the central difference of the
/// solver's own enthalpies; without the retry the states end <c>SingularMatrix</c>.
/// </summary>
[Collection(CpuFixture.CollectionName)]
public sealed class ScaledDerivativeTests
{
    /// <summary>States whose plain derivative system is singular: found as those of the trace-gas families that end <c>SingularMatrix</c> with the retry removed.</summary>
    public static TheoryData<string> States() => TraceGasCases.Names(
        TraceGasCases.CalciteBelowThePlateau().Where(c => c.Name is "calcite|1E-08|100000|1" or "calcite|1E-08|1000|1" or "calcite|1E-06|100000|50")
            .Concat(TraceGasCases.MagnesiteBelowThePlateau().Where(c => c.Name is "magnesite-below|100000|20" or "magnesite-below|10000|20")));

    /// <summary>The state is <c>Ok</c> and clear of the conditions, and its <c>Cp_eq</c> equals the central difference of the solver's own h at T ± 0.01 K.</summary>
    [Theory]
    [MemberData(nameof(States))]
    public void ATraceGasStateWhosePlainDerivativeSystemIsSingularEndsOkWithTheHeatCapacityOfItsEnthalpy(string name)
    {
        var state = TraceGasCases.Named(name);
        var solution = state.Solve();

        TraceGasChecks.AssertOkAndClear(solution, name);
        Assert.True(solution.State.CpEquilibrium > 0.0, $"{name}: Cp_eq {solution.State.CpEquilibrium:R}");
        TraceGasChecks.AssertHeatCapacityMatchesTheCentralDifference(state, solution, name);
    }
}
