namespace APThermo.Equilibrium.Recovery;

/// <summary>Which pass of a case a step of <see cref="AttemptPlan"/> is: an attempt of the iteration, or the verdict alone.</summary>
internal enum AttemptPhase
{
    /// <summary>The first attempt of a case or of a probe given a previous solution: seeded by it.</summary>
    Warm,

    /// <summary>The attempt from the defaults of RP-1311 section 3.1: the first of a case or probe given none, or the retry of a failed warm one. It never falls back.</summary>
    Cold,

    /// <summary>
    /// A tp pass that runs the verdict and not the iteration: a probe whose nearer end holds no gas, or the final of a bracket
    /// that ended on such ends. The certificate is a proof, so a <c>Gasless</c> answer needs no attempt.
    /// </summary>
    VerdictOnly,

    /// <summary>The hp or sp case itself after the bracket, seeded from its ends. Its status is the case's; it never falls back.</summary>
    Final,
}
