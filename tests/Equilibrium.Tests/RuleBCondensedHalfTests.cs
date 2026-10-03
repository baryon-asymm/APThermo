namespace APThermo.Equilibrium.Tests;

/// <summary>
/// L1 and L2: the half of rule B that stays in hp and sp (Newton BOOT.md, rule B, 2026-10-03). Rule B's gas column is limited to an
/// assigned temperature, but its condensed-only half is not: cold sp states on the plateaus of calcium carbonate (1e7 Pa) and
/// calcium hydroxide (1e5 Pa) make the matrix singular at the plateau temperature with three condensed species in the solution,
/// the first of which the condensed-only ratio test takes out. Four of the six states below failed when the whole rule was limited to tp, which is the
/// measurement that decided the limit (the same states are the 0.2.2 families of the execution node's batch facts, which also
/// failed then).
/// </summary>
[Collection(CpuFixture.CollectionName)]
public sealed class RuleBCondensedHalfTests
{
    /// <summary>The system, pressure and fractions of the transition of the two families.</summary>
    public static TheoryData<int, double, double> States() => new()
    {
        { 0, 1.0e7, 0.3 },
        { 0, 1.0e7, 0.9 },
        { 1, 1.0e5, 0.1 },
        { 1, 1.0e5, 0.3 },
        { 1, 1.0e5, 0.7 },
        { 1, 1.0e5, 0.9 },
    };

    /// <summary>A cold sp state inside the plateau ends <c>Ok</c> on it, clear of the equilibrium conditions and carrying the pinned convention.</summary>
    [Theory]
    [MemberData(nameof(States))]
    public void AColdSpStateInsideThePlateauEndsOkOnIt(int system, double pressure, double fraction)
    {
        var rig = UnivariantRig.Of(system == 0 ? UnivariantSystem.Calcite : UnivariantSystem.CalciumHydroxide, pressure);

        var solution = rig.SpCold(fraction);

        UnivariantChecks.AssertOnThePlateau(rig, solution, $"{rig.System.Name}, p {pressure:G3}, AssignedEntropyPressure, fraction {fraction}");
    }
}
