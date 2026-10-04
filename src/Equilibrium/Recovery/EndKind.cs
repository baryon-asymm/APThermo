namespace APThermo.Equilibrium.Recovery;

/// <summary>What a probe of the temperature bracket found at its temperature: an equilibrium with a gas phase, or one without.</summary>
internal enum EndKind
{
    /// <summary>The tp attempt converged: <c>P</c> and its slope are the state's.</summary>
    Gas,

    /// <summary>The gasless verdict held: <c>P</c> and its slope are the condensed minimum's.</summary>
    Gasless,

    /// <summary>The tp state was found by the trace-gas pass after the verdict required a gas: <c>P</c> and its slope are the state's, as for <see cref="Gas"/>.</summary>
    TraceGas,
}
