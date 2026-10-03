using APThermo.Thermo;

namespace APThermo.Equilibrium.Tests;

/// <summary>
/// L1 and L2: the gas-participating plateau (StateRecord BOOT.md, <c>## Constraints</c>, 2026-10-03; the orchestrator's
/// investigation for 0.2.2). Under carbon dioxide, calcium carbonate and calcium oxide coexist at one temperature per pressure;
/// the two condensed vectors are independent, the constant-temperature derivative system is singular, and before the
/// isentropic system every hp and sp state inside the plateau ended <c>TemperatureOutOfRange</c> or <c>NotConverged</c>. The
/// facts solve the plateau's states the way the fixtures of the <c>seeded</c> kind are solved (a tp seed above the plateau,
/// then the case warm-started from it) and compare <c>γ_s</c> and <c>a</c> with the isentropic finite difference of the
/// solver's own results and, at the pressures where the condensed volume is negligible, with the closed form of a pure gas.
/// The fractions of the transition are those the seed reaches: an hp or sp state seeded on the one-condensed side of a
/// plateau cannot leave it below some fraction (StateRecord BOOT.md, "Left, and declared").
/// </summary>
[Collection(CpuFixture.CollectionName)]
public sealed class GasPlateauTests
{
    private static readonly double[] Pressures = [1.0e4, 1.0e5, 1.0e6, 1.0e7];
    private static readonly double[][] Ratios = [[1, 1, 3], [1, 2, 5]];
    private static readonly double[] Fractions = [0.7, 0.9];
    private static readonly ProblemKind[] Kinds = [ProblemKind.AssignedEnthalpyPressure, ProblemKind.AssignedEntropyPressure];

    /// <summary>Ca:C:O of 1:1:3 (the carbonate alone) and 1:2:5 (carbon dioxide in excess), each pressure, both problems, two fractions of the transition.</summary>
    public static TheoryData<double, int, ProblemKind, double> Grid() => Rows(Pressures);

    /// <summary>The same grid at the two lowest pressures, where the pure-gas closed form holds.</summary>
    public static TheoryData<double, int, ProblemKind, double> LowPressureGrid() => Rows(Pressures[..2]);

    private static TheoryData<double, int, ProblemKind, double> Rows(double[] pressures)
    {
        var data = new TheoryData<double, int, ProblemKind, double>();
        var rows = from pressure in pressures
                   from ratio in Enumerable.Range(0, Ratios.Length)
                   from kind in Kinds
                   from fraction in Fractions
                   select (pressure, ratio, kind, fraction);
        foreach (var (pressure, ratio, kind, fraction) in rows)
        {
            data.Add(pressure, ratio, kind, fraction);
        }

        return data;
    }

    /// <summary>
    /// A seeded hp or sp state inside the plateau ends <c>Ok</c> at the plateau temperature, with both condensed species in
    /// the solution, clear of the equilibrium conditions, and carries the pinned convention with <c>γ_s = −1/DlnVdlnP</c> and
    /// <c>a² = n R T γ_s</c>. Red on the pinned-set rule alone: the singular constant-temperature system ended every one of
    /// these in a failure before any state was written.
    /// </summary>
    [Theory]
    [MemberData(nameof(Grid))]
    public void AStateInsideThePlateauEndsOkAtThePlateauTemperature(double pressure, int ratio, ProblemKind kind, double fraction)
    {
        var rig = UnivariantRig.Of(UnivariantSystem.Calcite, Ratios[ratio], pressure);
        var label = $"p {pressure:G3}, Ca:C:O {string.Join(':', Ratios[ratio])}, {kind}, fraction {fraction}";

        var solution = Solve(rig, kind, fraction);

        UnivariantChecks.AssertOnThePlateau(rig, solution, label);
        Assert.Contains("CaCO3", UnivariantRig.CondensedOf(solution), StringComparison.Ordinal);
        Assert.Contains("CaO", UnivariantRig.CondensedOf(solution), StringComparison.Ordinal);
    }

    /// <summary>
    /// <c>γ_s</c> and the sound speed of the plateau state equal the isentropic finite difference of the solver's own results,
    /// <c>d ln p/d ln ρ</c> at constant entropy, within <see cref="Tolerances.FiniteDifference"/>: the isentrope on a
    /// gas-participating plateau is not an isotherm, and only the isentropic system sees it.
    /// </summary>
    [Theory]
    [MemberData(nameof(Grid))]
    public void GammaSAndTheSoundSpeedMatchTheIsentropicFiniteDifference(double pressure, int ratio, ProblemKind kind, double fraction)
    {
        var rig = UnivariantRig.Of(UnivariantSystem.Calcite, Ratios[ratio], pressure);
        var solution = Solve(rig, kind, fraction);
        Assert.Equal(CaseStatus.Ok, solution.Status);

        UnivariantChecks.AssertMatchesTheIsentropicDifference(rig, solution, UnivariantChecks.PlateauStep, $"p {pressure:G3}, {kind}, fraction {fraction}");
    }

    /// <summary>
    /// With carbon dioxide the only gas, a plateau state's <c>γ_s</c> equals <c>1/(1 − 2/L + c/L²)</c> with
    /// <c>L = Δh_r/(ν_g R T)</c> and <c>c = Cp_frozen/(n R)</c>: the Clausius–Clapeyron slope of the plateau and the heat
    /// capacity of the mixture, neither of which the solver's derivative system is part of. The enthalpy of the reaction
    /// is the species functions' at the plateau temperature. Compared at the two pressures where the condensed volume,
    /// which the closed form neglects, is negligible (<see cref="Tolerances.ClosedForm"/>).
    /// </summary>
    [Theory]
    [MemberData(nameof(LowPressureGrid))]
    public void GammaSMatchesTheClosedFormOfAPureParticipatingGas(double pressure, int ratio, ProblemKind kind, double fraction)
    {
        var rig = UnivariantRig.Of(UnivariantSystem.Calcite, Ratios[ratio], pressure);
        var solution = Solve(rig, kind, fraction);
        Assert.Equal(CaseStatus.Ok, solution.Status);
        var state = solution.State;
        var gasMoles = solution.Moles.Take(rig.Table.GasCount).Sum();
        var gasStoichiometry = rig.System.Reaction.Where(term => term.Nu > 0.0 && rig.Table.IndicesOf(term.Species)[0] < rig.Table.GasCount).Sum(term => term.Nu);
        var enthalpyOverRt = rig.TransitionEnthalpy / (gasStoichiometry * PhysicalConstants.R * state.Temperature);
        var heatCapacityOverGas = state.CpFrozen / (gasMoles * PhysicalConstants.R);

        var closedForm = 1.0 / (1.0 - 2.0 / enthalpyOverRt + heatCapacityOverGas / (enthalpyOverRt * enthalpyOverRt));

        Assert.True(Math.Abs(state.GammaS / closedForm - 1.0) <= Tolerances.ClosedForm,
                    $"gamma_s {state.GammaS:R} against the closed form {closedForm:R} at {pressure:G3} Pa");
    }

    private static HostSolution Solve(UnivariantRig rig, ProblemKind kind, double fraction) =>
        kind == ProblemKind.AssignedEnthalpyPressure ? rig.Hp(fraction) : rig.Sp(fraction);
}
