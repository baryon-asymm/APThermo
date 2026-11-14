namespace APThermo.Equilibrium.Recovery;

/// <summary>What the recheck of an Ok state below a dead-end floor keeps of the attempt it may replace (BOOT.md, "Dead-end floors").</summary>
internal struct RecheckState
{
    /// <summary>Where the case stands in the recheck.</summary>
    public Recheck Stage;

    /// <summary>The cold attempt that ended Ok before the recheck began, rerun when the recheck does not replace it.</summary>
    public EquilibriumProblem Original;

    /// <summary>The Newton steps of every attempt up to and including the original, what a rerun reports.</summary>
    public int Banked;

    /// <summary>The floor currently being checked; successive floors strictly increase.</summary>
    public double Floor;

    /// <summary>The original state's objective: entropy for hp, enthalpy for sp.</summary>
    public double OriginalObjective;
}
