using APThermo.Thermo;

namespace APThermo.Equilibrium.Tests;

/// <summary>
/// L1 and L2: the gas-participating plateau beyond calcium carbonate (StateRecord BOOT.md, <c>## Constraints</c>,
/// 2026-10-03). A sp march down an isentrope into the two-phase region of nearly pure water, where the single condensed
/// species is parallel to the gas and every station was a failure before the isentropic system, and one state each of the
/// plateaus of ammonium chloride, calcium hydroxide and magnesite. Each plateau state is compared with the isentropic finite
/// difference of the solver's own results.
/// </summary>
[Collection(CpuFixture.CollectionName)]
public sealed class GasPlateauSystemsTests
{
    private const double StartPressure = 1.0e5;
    private const double StartOffset = 10.0;
    private const double PressureRatio = 0.93;
    private const int Stations = 40;
    private const double Pressure = 1.0e5;
    private const double Fraction = 0.7;

    private static readonly UnivariantSystem[] Systems = [UnivariantSystem.AmmoniumChloride, UnivariantSystem.CalciumHydroxide, UnivariantSystem.Magnesite];

    /// <summary>One state of each system at 1e5 Pa, both problems, at 0.7 of the transition.</summary>
    public static TheoryData<int, ProblemKind> States()
    {
        var data = new TheoryData<int, ProblemKind>();
        for (var system = 0; system < Systems.Length; system++)
        {
            data.Add(system, ProblemKind.AssignedEnthalpyPressure);
            data.Add(system, ProblemKind.AssignedEntropyPressure);
        }

        return data;
    }

    /// <summary>
    /// An isentropic expansion of steam from 10 K above its boiling point at 1 bar, warm-started station to station at a
    /// pressure ratio of 0.93: every station ends <c>Ok</c> and clear of the equilibrium conditions, the stations in the
    /// two-phase region carry the pinned convention and <c>γ_s</c> and <c>a</c> of the isentropic difference, and the march
    /// does cross from the vapour into the wet region and stays there. Red on the pinned-set rule alone: the third station
    /// onward ended <c>SingularMatrix</c>.
    /// </summary>
    [Fact]
    public void AnSpMarchThroughTheTwoPhaseRegionOfWaterEndsOkAtEveryStation()
    {
        var rig = UnivariantRig.Of(UnivariantSystem.Water, StartPressure);
        var start = rig.Tp(rig.Temperature + StartOffset);
        Assert.Equal(CaseStatus.Ok, start.Status);
        var entropy = start.State.Entropy;
        var moles = start.Moles;
        var temperature = start.State.Temperature;
        var pressure = StartPressure;
        var wet = new List<bool>();
        for (var station = 0; station < Stations; station++)
        {
            pressure *= PressureRatio;
            var solution = rig.Solve(ProblemKind.AssignedEntropyPressure, pressure, temperature, entropy, moles);
            var label = $"station {station} at {pressure:G5} Pa";
            Assert.True(solution.Status == CaseStatus.Ok, $"{label}: status {solution.Status} after {solution.Iterations} iterations");
            Assert.Empty(EquilibriumConditions.Violations(solution, Tolerances.GasChemicalPotential));
            wet.Add(solution.State.CpEquilibrium == 0.0);
            if (wet[^1])
            {
                UnivariantChecks.AssertPinnedConvention(solution, label);
                UnivariantChecks.AssertMatchesTheIsentropicDifference(rig, solution, UnivariantChecks.PlateauStep, label);
            }

            moles = solution.Moles;
            temperature = solution.State.Temperature;
        }

        Assert.Contains(false, wet);
        Assert.Contains(true, wet);
        Assert.Equal(wet.Count(w => !w), wet.IndexOf(true));
        Assert.DoesNotContain(false, wet.Skip(wet.IndexOf(true)));
    }

    /// <summary>
    /// One state of the sublimation of ammonium chloride, the dehydration of calcium hydroxide and the decomposition of
    /// magnesite: <c>Ok</c> on the plateau, clear of the equilibrium conditions, carrying the pinned convention, with
    /// <c>γ_s</c> and <c>a</c> of the isentropic difference. <c>γ_s</c> may fall below one on such a plateau, as for wet steam.
    /// </summary>
    [Theory]
    [MemberData(nameof(States))]
    public void AStateOnEachOtherPlateauMatchesTheIsentropicFiniteDifference(int system, ProblemKind kind)
    {
        var rig = UnivariantRig.Of(Systems[system], Pressure);
        var label = $"{Systems[system].Name}, {kind}, fraction {Fraction}";

        var solution = kind == ProblemKind.AssignedEnthalpyPressure ? rig.Hp(Fraction) : rig.Sp(Fraction);

        UnivariantChecks.AssertOnThePlateau(rig, solution, label);
        UnivariantChecks.AssertMatchesTheIsentropicDifference(rig, solution, UnivariantChecks.PlateauStep, label);
    }
}
