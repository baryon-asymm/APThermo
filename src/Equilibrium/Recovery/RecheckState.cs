using APThermo.Thermo;

namespace APThermo.Equilibrium.Recovery;

/// <summary>Per-case state of the bounded distinct-floor walk and the best feasible candidate.</summary>
internal struct RecheckState
{
    /// <summary>The floor cursor phase.</summary>
    public Recheck Stage;
    /// <summary>K: the current distinct dead-end floor.</summary>
    public double Floor;
    /// <summary>Best entropy for hp (J/kg/K), best enthalpy for sp (J/kg).</summary>
    public double BestObjective;
    /// <summary>The certificate status of the banked candidate.</summary>
    public CaseStatus BestStatus;
    /// <summary>The exact banked state, with T,p only for a gasless candidate.</summary>
    public MixtureState BestState;

    /// <summary>A branch ended numerically without a certificate excluding its alternative.</summary>
    public bool Unresolved;
}
