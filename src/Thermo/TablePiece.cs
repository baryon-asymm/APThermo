using APThermo.Data;

namespace APThermo.Thermo;

/// <summary>
/// One table species in the making: its name (the database name, or <c>NAME[TLow-THigh]</c> for a cut piece), the
/// record that provided its first interval, and its intervals in order, each paired with the record that supplied it,
/// so that a piece cut out of joined records names the record of its own first interval (<see cref="SpeciesTable.Records"/>).
/// </summary>
internal readonly record struct TablePiece(string Name, Species Record, List<(TemperatureInterval Interval, Species Source)> Intervals);
