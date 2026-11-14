namespace APThermo.Equilibrium.Recovery;

/// <summary>Where the case stands in the distinct-floor search.</summary>
internal enum Recheck
{
    /// <summary>No active floor recheck.</summary>
    None,
    /// <summary>The cold probe at a distinct floor is in flight.</summary>
    Pending,
    /// <summary>The current floor's bracket is searching for a candidate.</summary>
    Held,
    /// <summary>The cursor is exhausted and the bank or honest refusal has been written.</summary>
    Complete,
}
