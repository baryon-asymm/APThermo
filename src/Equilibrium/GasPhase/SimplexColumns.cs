using APThermo.Thermo;

namespace APThermo.Equilibrium.GasPhase;

/// <summary>
/// Which species are columns of the program <see cref="CondensedSimplex"/> runs, and at what temperature: the condensed records
/// alone (the verdict's program), or the records and every gas, each gas at unit fraction with the cost g/RT + ln(p/p°)
/// (the trace-gas pass's gas basis, 2026-10-05). One value per program, built where the program starts and read through <c>in</c>; never returned by value (the bool, BOOT.md, "No whole-struct copies").
/// </summary>
internal readonly struct SimplexColumns(double temperature, double logPressure, bool withGas)
{
    /// <summary>K: the temperature of the species functions and of the records' effective ranges.</summary>
    public readonly double Temperature = temperature;

    /// <summary>ln(p/p°), added to a gas's cost; zero when the gases are not columns.</summary>
    public readonly double LogPressure = logPressure;

    /// <summary>Whether the gases are columns.</summary>
    public readonly bool WithGas = withGas;

    /// <summary>The first species the entering and replacement scans read: the first gas when the gases are columns, else the first record.</summary>
    public int FirstColumn(in SpeciesTableView table) => WithGas ? 0 : table.GasCount;
}
