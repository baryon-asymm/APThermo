namespace APThermo.Equilibrium.GasPhase;

/// <summary>What the gasless test found at one temperature and pressure.</summary>
internal enum GasVerdict
{
    /// <summary>The test could not decide (a degenerate pivot, the pivot cap, a negative basic value, the margin): the caller keeps its failure.</summary>
    Undecided = 0,

    /// <summary>The condensed species alone cannot hold the elements, or a gas phase would lower the Gibbs energy.</summary>
    GasRequired = 1,

    /// <summary>The minimum is condensed only and a gas phase of any composition would raise the Gibbs energy.</summary>
    Gasless = 2,
}
