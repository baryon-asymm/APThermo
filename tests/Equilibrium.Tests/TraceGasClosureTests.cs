namespace APThermo.Equilibrium.Tests;

/// <summary>
/// The closure of every <c>Ok</c> at the node's relative invariant, <c>|Σ a_ij n_j − b_i| ≤ 1e-13 · b_i</c> (Equilibrium BOOT.md,
/// "Element conservation"; the leftovers design of 2026-10-04, section 4): the plateau states the reduced iteration walked to a carrier
/// of 1.6e-13 kmol/kg and called converged, and the states whose trace-gas <c>Ok</c> dropped a gas that carried part of a balance.
/// </summary>
[Collection(CpuFixture.CollectionName)]
public sealed class TraceGasClosureTests
{
    private static readonly double[] Pressures = [1.0e4, 1.0e5, 1.0e6];
    private static readonly double[] Fractions = [0.1, 0.3, 0.5];
    private static readonly UnivariantSystem[] Systems = [UnivariantSystem.Magnesite, UnivariantSystem.Calcite];
    private static readonly bool[] Both = [true, false];

    /// <summary>Calcite and magnesite at 1e4, 1e5 and 1e6 Pa, hp and sp, three fractions of the transition, cold and seeded: 72 states.</summary>
    public static TheoryData<int, double, bool, double, bool> PlateauStates()
    {
        var data = new TheoryData<int, double, bool, double, bool>();
        foreach (var (system, pressure, hp, fraction, cold) in
                 from system in Enumerable.Range(0, Systems.Length)
                 from pressure in Pressures
                 from hp in Both
                 from fraction in Fractions
                 from cold in Both
                 select (system, pressure, hp, fraction, cold))
        {
            data.Add(system, pressure, hp, fraction, cold);
        }

        return data;
    }

    /// <summary>
    /// Every plateau state ends <c>Ok</c>, clear of the equilibrium conditions at 1e-9, with every element within the relative invariant.
    /// Red on the absolute form of the invariant (1e-12 · max(1, b_i)): the hp states of magnesite closed carbon at 8e-12 to 1.4e-11 of
    /// its abundance, one CO of 1.2e-13 to 2.1e-13 kmol/kg that the reduced iteration walks down one e-fold per step.
    /// </summary>
    [Theory]
    [MemberData(nameof(PlateauStates))]
    public void EveryPlateauStateClosesEveryElementToTheRelativeInvariant(int system, double pressure, bool hp, double fraction, bool cold)
    {
        var rig = UnivariantRig.Of(Systems[system], pressure);
        var solution = hp ? (cold ? rig.HpCold(fraction) : rig.Hp(fraction)) : (cold ? rig.SpCold(fraction) : rig.Sp(fraction));
        var label = $"{Systems[system].Name} {pressure:G3} Pa {(hp ? "hp" : "sp")} {fraction} {(cold ? "cold" : "seeded")}";

        TraceGasChecks.AssertOkAndClear(solution, label);
        var violations = EquilibriumConditions.ElementConservationViolations(solution);
        Assert.True(violations.Count == 0, $"{label}: {string.Join("; ", violations)}");
    }

    /// <summary>
    /// A trace-gas <c>Ok</c> reports the gases that carry a part of a balance, whatever their share of the gas: Al(OH)3 + 1e-6 O at 10 MPa and
    /// 800 K converges with H2 at 5.2e-12 and AL(OH)3 at 2.3e-12 of the gas, below the report's second retention stage, and the report that
    /// dropped them missed hydrogen by 3.8e-13 kmol/kg (9.8e-12 of b_H). Red on the report that refreshes the moles at the second stage alone.
    /// </summary>
    [Fact]
    public void ATraceGasOkReportsTheGasesThatCarryAPartOfABalance()
    {
        var state = new TraceGasCase("aluminium-hydroxide-excess", ["AL", "H", "O"], [1.0, 3.0, 3.0 * (1.0 + 1.0e-6)], 1.0e7, 800.0);
        var solution = state.Solve();

        TraceGasChecks.AssertOkAndClear(solution, state.Name);
        var violations = EquilibriumConditions.ElementConservationViolations(solution);
        Assert.True(violations.Count == 0, $"{state.Name}: {string.Join("; ", violations)}");
    }
}
