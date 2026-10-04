namespace APThermo.Equilibrium.Recovery;

/// <summary>What the temperature bracket asks for after a probe, or after a failed one.</summary>
internal enum BracketMove
{
    /// <summary>Another tp probe at the bracket's next temperature.</summary>
    Probe,

    /// <summary>The search failed: the case ends with the bracket's give-up status.</summary>
    GiveUp,

    /// <summary>The final attempt, the hp or sp case itself, seeded by the lever rule between the two ends.</summary>
    AttemptFromLever,

    /// <summary>The final attempt, seeded by the last probe, which Newton's step converged on.</summary>
    AttemptFromProbe,

    /// <summary>The gasless final at the temperature Newton's step converged on: the verdict alone.</summary>
    GaslessAtProbe,

    /// <summary>The gasless final at the lever temperature of two gasless ends, with their lever mix for moles.</summary>
    GaslessFromLever,
}
