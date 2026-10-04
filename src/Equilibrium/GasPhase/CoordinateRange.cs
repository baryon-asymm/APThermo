namespace APThermo.Equilibrium.GasPhase;

/// <summary>The interval a face coordinate may move over while every eligible condensed record stays out at a gain of at most zero.</summary>
internal readonly struct CoordinateRange(double low, double high)
{
    /// <summary>The lower end.</summary>
    public readonly double Low = low;

    /// <summary>The upper end.</summary>
    public readonly double High = high;
}
