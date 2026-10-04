namespace APThermo.Equilibrium.Recovery;

/// <summary>Where an hp or sp case stands in the recheck of an <c>Ok</c> state below a dead-end floor (BOOT.md, "Dead-end floors").</summary>
internal enum Recheck
{
    /// <summary>No recheck: the case ended, or is ending, as its attempts decided.</summary>
    None,

    /// <summary>The tp probe at the floor is in flight; its assigned property decides whether the bracket goes on.</summary>
    Pending,

    /// <summary>The probe at the floor lies at or below the target: the bracket searches upward, and a failure of it reruns the original attempt.</summary>
    Held,

    /// <summary>The original attempt is rerun, its bits unchanged, and ends the case.</summary>
    Replay,
}
