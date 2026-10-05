namespace APThermo.Thermo.Elementary;

/// <summary>A triple-double: <see cref="H"/> + <see cref="M"/> + <see cref="L"/>, nearly non-overlapping, largest first.</summary>
/// <param name="h">The leading double.</param>
/// <param name="m">The second term.</param>
/// <param name="l">The third term.</param>
internal readonly struct Td(double h, double m, double l)
{
    /// <summary>The leading double.</summary>
    public double H { get; } = h;

    /// <summary>The second term.</summary>
    public double M { get; } = m;

    /// <summary>The third term.</summary>
    public double L { get; } = l;
}
