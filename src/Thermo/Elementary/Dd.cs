namespace APThermo.Thermo.Elementary;

/// <summary>A double-double: <see cref="Hi"/> + <see cref="Lo"/>, with <c>|Lo|</c> at most about half an ulp of <see cref="Hi"/>.</summary>
/// <param name="hi">The leading double.</param>
/// <param name="lo">The remainder.</param>
internal readonly struct Dd(double hi, double lo)
{
    /// <summary>The leading double.</summary>
    public double Hi { get; } = hi;

    /// <summary>The remainder.</summary>
    public double Lo { get; } = lo;
}
