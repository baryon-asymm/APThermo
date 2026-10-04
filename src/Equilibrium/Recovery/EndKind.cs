namespace APThermo.Equilibrium.Recovery;

/// <summary>What a probe of the temperature bracket found at its temperature: an equilibrium with a gas phase, or one without.</summary>
internal enum EndKind
{
    /// <summary>The tp attempt converged: <c>P</c> and its slope are the state's.</summary>
    Gas,

    /// <summary>The gasless verdict held: <c>P</c> and its slope are the condensed minimum's.</summary>
    Gasless,
}
