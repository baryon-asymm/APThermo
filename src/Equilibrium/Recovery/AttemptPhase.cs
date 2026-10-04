namespace APThermo.Equilibrium.Recovery;

/// <summary>Which attempt of a case a pass of <see cref="AttemptPlan"/> is.</summary>
internal enum AttemptPhase
{
    /// <summary>The first attempt of a case given a previous solution: seeded by it.</summary>
    Warm,

    /// <summary>The attempt from the defaults of RP-1311 section 3.1: the first of a case given none, or the retry of a failed warm one. It never falls back.</summary>
    Cold,
}
