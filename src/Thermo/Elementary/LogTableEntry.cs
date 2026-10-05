namespace APThermo.Thermo.Elementary;

/// <summary>One row of the log fast path's table: the reduction factor <c>c</c> and −log <c>c</c> as a double-double.</summary>
/// <param name="c">The reduction factor.</param>
/// <param name="hi">The leading double of −log <paramref name="c"/>.</param>
/// <param name="lo">The remainder of −log <paramref name="c"/>.</param>
internal readonly struct LogTableEntry(double c, double hi, double lo)
{
    /// <summary>The reduction factor, RN(1/(1 + i/32)).</summary>
    public double C { get; } = c;

    /// <summary>The leading double of −log <see cref="C"/>.</summary>
    public double Hi { get; } = hi;

    /// <summary>The remainder of −log <see cref="C"/>.</summary>
    public double Lo { get; } = lo;
}
