using APThermo.Thermo;

namespace APThermo.Equilibrium.Tests;

/// <summary>
/// L1 and L2: the magnesite plateau, MgCO3 = MgO + CO2 under carbon dioxide (Mg:C:O = 1:2:5), solved from a seed on the plateau
/// itself, the way a temperature search that has bracketed it would hand the final attempt (<see cref="UnivariantRig.HpFromLever"/>).
/// Two of the singular remedies (Newton BOOT.md, rules B and A) bound what such a seed reaches (0.2.2, 2026-10-03):
/// rule B's gas column took MgCO3 out of a true univariant state in hp and sp, where the singular direction is rule A's element
/// tie, not a removal; and the tie's release, once the gas and both condensed phases are in the solution, can converge by the
/// report's tests and still leave an element's balance beyond the invariant, which only the restore of the tied iterate cures.
/// Pressures stop at 1e6 Pa: at 1e7 Pa the plateau lies above the magnesite record's range.
/// </summary>
[Collection(CpuFixture.CollectionName)]
public sealed class MagnesiteLeverSeedTests
{
    private static readonly double[] Pressures = [1.0e4, 1.0e5, 1.0e6];
    private static readonly double[] Fractions = [0.1, 0.5, 0.7];
    private static readonly ProblemKind[] Kinds = [ProblemKind.AssignedEnthalpyPressure, ProblemKind.AssignedEntropyPressure];

    /// <summary>Each pressure, both problems, three fractions of the transition.</summary>
    public static TheoryData<double, ProblemKind, double> Grid()
    {
        var data = new TheoryData<double, ProblemKind, double>();
        var rows = from pressure in Pressures
                   from kind in Kinds
                   from fraction in Fractions
                   select (pressure, kind, fraction);
        foreach (var (pressure, kind, fraction) in rows)
        {
            data.Add(pressure, kind, fraction);
        }

        return data;
    }

    /// <summary>
    /// A state seeded on the plateau ends <c>Ok</c> at the plateau temperature with both condensed species in the solution, clear
    /// of the equilibrium conditions, and carries the pinned convention. Red with rule B's gas column in force in hp and sp: 14 of
    /// the 20 facts of this class failed, the column having removed MgCO3 from a true univariant state.
    /// </summary>
    [Theory]
    [MemberData(nameof(Grid))]
    public void AStateSeededOnTheMagnesitePlateauEndsOkWithBothCondensedPhases(double pressure, ProblemKind kind, double fraction)
    {
        var rig = UnivariantRig.Of(UnivariantSystem.Magnesite, pressure);
        var label = $"p {pressure:G3}, {kind}, fraction {fraction}";

        var solution = kind == ProblemKind.AssignedEnthalpyPressure ? rig.HpFromLever(fraction) : rig.SpFromLever(fraction);

        UnivariantChecks.AssertOnThePlateau(rig, solution, label);
        Assert.Contains("MgCO3", UnivariantRig.CondensedOf(solution), StringComparison.Ordinal);
        Assert.Contains("MgO", UnivariantRig.CondensedOf(solution), StringComparison.Ordinal);
    }

    /// <summary>
    /// An hp state at half the transition, seeded on the plateau, ends <c>Ok</c>: with rule B limited to tp the release of rule A's
    /// tie converges at the right composition and passes the report's tests, but breaks the element invariant, and the case ended
    /// <c>TemperatureOutOfRange</c> after 67 to 70 iterations without the restore of the tied iterate, which conserves the
    /// elements through its combination row: the way back.
    /// </summary>
    [Theory]
    [InlineData(1.0e4)]
    [InlineData(1.0e6)]
    public void AnHpStateAtHalfTheTransitionNeedsTheReleasesWayBackOnABrokenInvariant(double pressure)
    {
        var rig = UnivariantRig.Of(UnivariantSystem.Magnesite, pressure);

        var solution = rig.HpFromLever(0.5);

        Assert.True(solution.Status == CaseStatus.Ok, $"p {pressure:G3}: status {solution.Status} after {solution.Iterations} iterations");
        UnivariantChecks.AssertOnThePlateau(rig, solution, $"p {pressure:G3}, hp, fraction 0.5");
    }
}
