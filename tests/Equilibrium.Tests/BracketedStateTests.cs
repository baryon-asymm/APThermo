namespace APThermo.Equilibrium.Tests;

/// <summary>
/// L1 and L2: the states the report's own iteration cannot reach and the temperature bracket solves (Recovery BOOT.md,
/// acceptance criteria): a state on the gas-participating plateau of calcium carbonate and of magnesite, asked for from the
/// cold start and from a seed above the plateau (the one-condensed side the lever seed does not reach), and the AP/HTPB/Al
/// states below the water band of 0.2.1's known limitations. Each is <c>Ok</c>, clear of the equilibrium conditions, at the
/// temperature the tp states fix.
/// </summary>
[Collection(CpuFixture.CollectionName)]
public sealed class BracketedStateTests
{
    private static readonly UnivariantSystem[] Systems = [UnivariantSystem.Calcite, UnivariantSystem.Magnesite];
    private static readonly double[] Pressures = [1.0e4, 1.0e5, 1.0e6];
    private static readonly double[] Fractions = [0.1, 0.3, 0.5];
    private static readonly bool[] Starts = [true, false];
    private static readonly ProblemKind[] Kinds = [ProblemKind.AssignedEnthalpyPressure, ProblemKind.AssignedEntropyPressure];

    /// <summary>Each system and pressure, both problems, three fractions of the transition, cold and seeded above the plateau.</summary>
    public static TheoryData<int, double, ProblemKind, double, bool> PlateauStates()
    {
        var data = new TheoryData<int, double, ProblemKind, double, bool>();
        var rows = from system in Enumerable.Range(0, Systems.Length)
                   from pressure in Pressures
                   from kind in Kinds
                   from fraction in Fractions
                   from cold in Starts
                   select (system, pressure, kind, fraction, cold);
        foreach (var (system, pressure, kind, fraction, cold) in rows)
        {
            data.Add(system, pressure, kind, fraction, cold);
        }

        return data;
    }

    /// <summary>
    /// A state at a fraction of the transition of calcite or magnesite under carbon dioxide ends <c>Ok</c> on the plateau, at the
    /// plateau temperature, with the pinned convention, whether it starts cold or from the one-condensed side the report's own
    /// attempts could not leave. Red before the bracket: <c>NotConverged</c>, <c>SingularMatrix</c> or
    /// <c>TemperatureOutOfRange</c> for part of the grid.
    /// </summary>
    [Theory]
    [MemberData(nameof(PlateauStates))]
    public void AStateOnAGasParticipatingPlateauEndsOkFromTheColdStartAndFromTheOneCondensedSide(
        int system, double pressure, ProblemKind kind, double fraction, bool cold)
    {
        var rig = UnivariantRig.Of(Systems[system], pressure);
        var hp = kind == ProblemKind.AssignedEnthalpyPressure;
        var label = $"{Systems[system].Name}, p {pressure:G3}, {kind}, fraction {fraction}, {(cold ? "cold" : "seeded")}";

        var solution = hp ? (cold ? rig.HpCold(fraction) : rig.Hp(fraction)) : (cold ? rig.SpCold(fraction) : rig.Sp(fraction));

        UnivariantChecks.AssertOnThePlateau(rig, solution, label);
    }
}
