using APThermo.Thermo;

namespace APThermo.Equilibrium.Tests;

/// <summary>
/// What a state on a gas-participating plateau, or near one, is checked against, shared by the facts of the plateau and of the
/// near-univariant sliver (StateRecord BOOT.md, "The gas-participating plateau" and "The near-univariant sliver"): the
/// isentropic finite difference of the solver's own results and the properties the pinned convention fixes.
/// </summary>
internal static class UnivariantChecks
{
    /// <summary>The relative pressure step of the central difference on a plateau: its isentrope stays inside the two-phase region at this step.</summary>
    public const double PlateauStep = 1.0e-4;

    /// <summary>The step beside a near-univariant composition, where the two-phase region is a sliver of temperature the stencil must not leave.</summary>
    public const double SliverStep = 1.0e-5;

    /// <summary>
    /// <c>d ln p/d ln ρ</c> along the isentrope through <paramref name="solution"/>, from two sp solves at <c>p(1 ± step)</c>
    /// seeded as the plateau's own are; the sound speed follows as <c>sqrt(γ p/ρ)</c>.
    /// </summary>
    public static (double GammaS, double SoundSpeed) IsentropicDifference(UnivariantRig rig, HostSolution solution, double step)
    {
        var pressure = solution.Case.Pressure;
        var entropy = solution.State.Entropy;
        var up = rig.Solve(ProblemKind.AssignedEntropyPressure, pressure * (1.0 + step), solution.State.Temperature, entropy, solution.Moles);
        var down = rig.Solve(ProblemKind.AssignedEntropyPressure, pressure * (1.0 - step), solution.State.Temperature, entropy, solution.Moles);
        Assert.True(up.Status == CaseStatus.Ok && down.Status == CaseStatus.Ok, $"the isentropic stencil ended {up.Status} and {down.Status}");
        var gamma = (Math.Log(up.State.Pressure) - Math.Log(down.State.Pressure)) / (Math.Log(up.State.Density) - Math.Log(down.State.Density));
        return (gamma, Math.Sqrt(gamma * pressure / solution.State.Density));
    }

    /// <summary><c>γ_s</c> and <c>a</c> of a state equal their isentropic finite difference within <see cref="Tolerances.FiniteDifference"/>.</summary>
    public static void AssertMatchesTheIsentropicDifference(UnivariantRig rig, HostSolution solution, double step, string label)
    {
        var (gamma, soundSpeed) = IsentropicDifference(rig, solution, step);
        var state = solution.State;
        Assert.True(Math.Abs(state.GammaS / gamma - 1.0) <= Tolerances.FiniteDifference,
                    $"{label}: gamma_s {state.GammaS:R} against d ln p / d ln rho {gamma:R}");
        Assert.True(Math.Abs(state.SoundSpeed / soundSpeed - 1.0) <= Tolerances.FiniteDifference,
                    $"{label}: sound speed {state.SoundSpeed:R} against {soundSpeed:R}");
    }

    /// <summary>
    /// The state is <c>Ok</c> on the plateau: at its temperature, with condensed species, clear of the equilibrium conditions,
    /// and carrying the pinned convention with <c>γ_s = −1/DlnVdlnP</c> and <c>a² = n R T γ_s</c>.
    /// </summary>
    public static void AssertOnThePlateau(UnivariantRig rig, HostSolution solution, string label)
    {
        Assert.True(solution.Status == CaseStatus.Ok, $"{label}: status {solution.Status} after {solution.Iterations} iterations");
        var state = solution.State;
        Assert.True(Math.Abs(state.Temperature - rig.Temperature) <= Tolerances.SelfConsistency * rig.Temperature,
                    $"{label}: T {state.Temperature:R} against the plateau's {rig.Temperature:R}");
        Assert.NotEmpty(UnivariantRig.CondensedOf(solution));
        Assert.Empty(EquilibriumConditions.Violations(solution, Tolerances.GasChemicalPotential));
        AssertPinnedConvention(solution, label);
    }

    /// <summary>
    /// The convention of a pinned state: <c>Cp_eq = Cv_eq = (∂ln V/∂ln T)_p = 0</c>, <c>DlnVdlnP</c> real and negative,
    /// <c>γ_s = −1/DlnVdlnP</c> and <c>a² = n R T γ_s</c> with n the gaseous moles.
    /// </summary>
    public static void AssertPinnedConvention(HostSolution solution, string label)
    {
        var state = solution.State;
        Assert.Equal(0.0, state.CpEquilibrium);
        Assert.Equal(0.0, state.CvEquilibrium);
        Assert.Equal(0.0, state.DlnVdlnT);
        Assert.True(state.DlnVdlnP < 0.0 && double.IsFinite(state.DlnVdlnP), $"{label}: DlnVdlnP {state.DlnVdlnP:R}");
        Assert.True(Math.Abs(state.GammaS * state.DlnVdlnP + 1.0) <= Tolerances.Exact, $"{label}: gamma_s {state.GammaS:R} against -1/{state.DlnVdlnP:R}");
        var gasMoles = solution.Moles.Take(solution.Case.Table.GasCount).Sum();
        var expected = Math.Sqrt(gasMoles * PhysicalConstants.R * state.Temperature * state.GammaS);
        Assert.True(Math.Abs(state.SoundSpeed - expected) <= Tolerances.Exact * expected, $"{label}: sound speed {state.SoundSpeed:R} against {expected:R}");
        Assert.True(state.CpFrozen > 0.0, $"{label}: frozen Cp {state.CpFrozen:R}");
    }
}
