using AerospacePropellantThermodynamics.Data;

namespace AerospacePropellantThermodynamics.Thermo;

/// <summary>
/// One table species in the making: its name (the database name, or <c>NAME[TLow-THigh]</c> for a cut piece), the
/// record that provided its first interval, and its intervals in order, each paired with the record that supplied
/// it (the join's diagnostics).
/// </summary>
internal readonly record struct TablePiece(string Name, Species Record, List<(TemperatureInterval Interval, Species Source)> Intervals);
