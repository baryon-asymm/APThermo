namespace APThermo.Equilibrium.GasPhase;

/// <summary>
/// The face of optimal multipliers a tangent-plane search moves over: how many rows the condensed program has, how many
/// directions span the face, where the directions start in the matrix scratch, and the pressure and temperature of the
/// verdict. One argument in place of five keeps every method of <see cref="TangentPlane"/> within the root's parameter rule.
/// </summary>
internal readonly struct Face(int rows, int directions, int offset, double logPressure, double temperature)
{
    /// <summary>The active elements: rows of the condensed program.</summary>
    public readonly int Rows = rows;

    /// <summary>Directions of the face; zero when the multipliers are unique.</summary>
    public readonly int Directions = directions;

    /// <summary>Offset of the directions (Rows × Directions, row-major by Rows) in the matrix scratch.</summary>
    public readonly int Offset = offset;

    /// <summary>ln(p/p°) of the verdict.</summary>
    public readonly double LogPressure = logPressure;

    /// <summary>K.</summary>
    public readonly double Temperature = temperature;
}
