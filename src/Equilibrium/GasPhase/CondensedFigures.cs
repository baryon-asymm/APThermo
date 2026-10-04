namespace APThermo.Equilibrium.GasPhase;

/// <summary>
/// The enthalpy, entropy and heat capacity of the condensed minimum of a <see cref="GasVerdict.Gasless"/> verdict: pure
/// condensed phases of no volume, so no mixing and no pressure term.
/// </summary>
internal struct CondensedFigures
{
    /// <summary>J/kg: R T Σ n_j (h°/RT)_j over the condensed minimum.</summary>
    public double Enthalpy;

    /// <summary>J/(kg·K): R Σ n_j (s°/R)_j.</summary>
    public double Entropy;

    /// <summary>J/(kg·K): R Σ n_j (cp°/R)_j.</summary>
    public double HeatCapacity;
}
