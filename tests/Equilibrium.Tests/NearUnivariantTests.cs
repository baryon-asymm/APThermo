using APThermo.Thermo;

namespace APThermo.Equilibrium.Tests;

/// <summary>
/// L1 and L2: the near-univariant sliver (StateRecord BOOT.md, <c>## Constraints</c>, 2026-10-03). Beside the univariant
/// composition Ca:C:O = 1:1:3 an excess of oxygen <c>d</c> makes the two-phase region a sliver of temperature, and
/// <c>Cv = Cp + n (∂ln V/∂ln T)²/(∂ln V/∂ln p)</c> cancels catastrophically as <c>Cp/Cv</c> grows to 1e10 and beyond; before the
/// isentropic route <c>γ_s</c> of an <c>Ok</c> state was up to 1.7e-3 wrong. The facts walk <c>d</c> over a grid of half
/// decades, hp states at fractions of the transition, and compare <c>γ_s</c> with the isentropic finite difference of the
/// solver's own results and across the boundary at which the constant-temperature system turns singular.
/// </summary>
[Collection(CpuFixture.CollectionName)]
public sealed class NearUnivariantTests
{
    /// <summary>The ratio the isentropic route takes over at (StateRecord BOOT.md), restated here so the facts can tell the two routes apart.</summary>
    private const double SwitchRatio = 1.0e6;

    /// <summary>The ratio beyond which the old route's cancellation moves <c>γ_s</c> by more than the finite difference's own accuracy.</summary>
    private const double CancellingRatio = 1.0e10;

    private static readonly double[] Pressures = [1.0e4, 1.0e5, 1.0e6, 1.0e7];
    private static readonly double[] Fractions = [0.75, 0.9];
    private static readonly double[] ContinuityExcess = [1.0e-7, 1.0e-9, 1.0e-10, 1.0e-11, 0.0];
    private const double ContinuityFraction = 0.9;

    /// <summary>Each pressure at each fraction.</summary>
    public static TheoryData<double, double> Cases()
    {
        var data = new TheoryData<double, double>();
        foreach (var pressure in Pressures)
        {
            foreach (var fraction in Fractions)
            {
                data.Add(pressure, fraction);
            }
        }

        return data;
    }

    /// <summary>Each pressure, for the continuity walk.</summary>
    public static TheoryData<double> PressureCases()
    {
        var data = new TheoryData<double>();
        foreach (var pressure in Pressures)
        {
            data.Add(pressure);
        }

        return data;
    }

    /// <summary>
    /// Over <c>d</c> from 1e-9 to 1e-4 in half decades, every <c>Ok</c> state off the singular boundary has <c>γ_s</c> and
    /// <c>a</c> equal to the isentropic finite difference, whichever route produced it, and the walk holds states on both
    /// sides of the switch at <c>Cp/Cv</c> of 1e6 and states beyond 1e10, where the constant-temperature route alone is
    /// wrong. Red on the old route: <c>γ_s</c> of the states beyond 1e10 is off by 2e-6 and more, up to 1.7e-3. States whose hp solve ends
    /// in a failure are not walked: that is the declared defect of the seed, not <c>γ_s</c>.
    /// </summary>
    [Theory]
    [MemberData(nameof(Cases))]
    public void GammaSMatchesTheIsentropicFiniteDifferenceBesideTheUnivariantComposition(double pressure, double fraction)
    {
        var ratios = new List<double>();
        for (var step = 0; step <= 10; step++)
        {
            var excess = 1.0e-9 * Math.Pow(10.0, 0.5 * step);
            var rig = UnivariantRig.Of(UnivariantSystem.Calcite, [1, 1, 3 + excess], pressure);
            var solution = rig.Hp(fraction);
            if (solution.Status != CaseStatus.Ok || solution.State.CpEquilibrium == 0.0)
            {
                continue;
            }

            ratios.Add(solution.State.CpEquilibrium / solution.State.CvEquilibrium);
            UnivariantChecks.AssertMatchesTheIsentropicDifference(rig, solution, UnivariantChecks.SliverStep, $"p {pressure:G3}, fraction {fraction}, d {excess:G3}");
        }

        Assert.Contains(ratios, ratio => ratio > CancellingRatio);
        Assert.Contains(ratios, ratio => ratio is > SwitchRatio and <= CancellingRatio);
        Assert.Contains(ratios, ratio => ratio <= SwitchRatio);
    }

    /// <summary>
    /// <c>γ_s</c> is continuous across the singular boundary: from <c>d</c> of 1e-7, where the state is off the boundary and
    /// takes the isentropic route, through the states the constant-temperature system cannot solve and the isentropic
    /// system carries as a plateau, to the exact plateau at <c>d</c> of zero, <c>γ_s</c> stays within
    /// <see cref="Tolerances.FiniteDifference"/> of the exact plateau's. The walk holds states of both kinds, told apart by
    /// the pinned convention's zero <c>Cp_eq</c>. Red on the pinned-set rule alone: every state from the boundary on ended
    /// <c>TemperatureOutOfRange</c>.
    /// </summary>
    [Theory]
    [MemberData(nameof(PressureCases))]
    public void GammaSIsContinuousAcrossTheSingularBoundary(double pressure)
    {
        var exact = UnivariantRig.Of(UnivariantSystem.Calcite, [1, 1, 3], pressure).Hp(ContinuityFraction);
        Assert.Equal(CaseStatus.Ok, exact.Status);
        var offBoundary = 0;
        var onPlateau = 0;
        foreach (var excess in ContinuityExcess)
        {
            var rig = UnivariantRig.Of(UnivariantSystem.Calcite, [1, 1, 3 + excess], pressure);
            var solution = rig.Hp(ContinuityFraction);
            Assert.True(solution.Status == CaseStatus.Ok, $"d {excess:G3}: status {solution.Status}");
            Assert.True(Math.Abs(solution.State.GammaS / exact.State.GammaS - 1.0) <= Tolerances.FiniteDifference,
                        $"d {excess:G3}: gamma_s {solution.State.GammaS:R} against the exact plateau's {exact.State.GammaS:R}");
            if (solution.State.CpEquilibrium == 0.0)
            {
                onPlateau++;
            }
            else
            {
                offBoundary++;
            }
        }

        Assert.True(offBoundary > 0 && onPlateau > 0, $"the walk holds {offBoundary} states off the boundary and {onPlateau} on the plateau");
    }
}
