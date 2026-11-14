using APThermo.Thermo;

namespace APThermo.Equilibrium.Recovery;

/// <summary>Scalar per-case cursor and certificate; the full candidate state lives in global typed scratch.</summary>
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

    /// <summary>A branch ended numerically without a certificate excluding its alternative.</summary>
    public bool Unresolved;
}