namespace AerospacePropellantThermodynamics.Equilibrium;

/// <summary>Where the initial estimate of a solve comes from (the translation of the entry point's flag).</summary>
internal enum EstimateSource
{
    /// <summary>Section 3.1 of RP-1311: 0.1/active gaseous species each, n = 0.1, T = 3800 K for hp and sp.</summary>
    Defaults,

    /// <summary>The caller's previous solution in <c>result.Moles</c>, and <c>problem.Temperature</c> when it is positive.</summary>
    PreviousSolution,
}

/// <summary>Which right-hand side the derivative system is solved with: table 2.3 or table 2.4 of RP-1311.</summary>
internal enum DerivativeKind
{
    Temperature,
    Pressure,
}

/// <summary>
/// The four values of a species' slot in <c>scratch.SpeciesActive</c> (the node's API.md records the domain). A gaseous
/// species is only ever Absent or Active; the last two are the memories of the anti-cycling rule for condensed records.
/// </summary>
internal enum SpeciesMark
{
    /// <summary>An element of the species is absent from the case: no row, no column, no moles.</summary>
    Absent = 0,

    /// <summary>In play.</summary>
    Active = 1,

    /// <summary>A condensed record removed for its range once; still in play, skipped by one inclusion pass.</summary>
    ForgivenOnce = 2,

    /// <summary>A condensed record that escaped its range twice in one solve: out of play for the rest of it.</summary>
    StoodDown = 3,
}

/// <summary>
/// The shape of the reduced system of one convergence: how many unknowns, where the total-moles and temperature rows sit,
/// and which problem is being solved. The derivative system of section 2.6 is a tp-shaped layout over the same scratch.
/// </summary>
internal readonly struct SystemLayout
{
    public SystemLayout(ProblemKind kind, int elementCount, int condensedCount, int stride)
    {
        Kind = kind;
        ElementCount = elementCount;
        CondensedCount = condensedCount;
        Stride = stride;
        NRow = elementCount + condensedCount;
        TRow = NRow + 1;
        Unknowns = elementCount + condensedCount + 1 + (kind == ProblemKind.AssignedTemperaturePressure ? 0 : 1);
    }

    public readonly ProblemKind Kind;
    public readonly int ElementCount;
    public readonly int CondensedCount;

    /// <summary>Row stride of the scratch matrix: <see cref="ScratchLayout.MaxUnknowns"/> of the case, not the live count.</summary>
    public readonly int Stride;

    /// <summary>The row and column of Δln n.</summary>
    public readonly int NRow;

    /// <summary>The row and column of Δln T; present only for hp and sp.</summary>
    public readonly int TRow;

    public readonly int Unknowns;

    public bool IsTp => Kind == ProblemKind.AssignedTemperaturePressure;

    public bool IsHp => Kind == ProblemKind.AssignedEnthalpyPressure;
}

/// <summary>
/// The sums over the composition that the iteration matrix and the state record need, accumulated in one pass in ascending
/// species order. Built field by field by <see cref="Composition"/> and read through <c>in</c> afterwards, so that no caller
/// has to line up nine positional doubles (the swap hazard the root's parameter rule is about).
/// </summary>
internal struct MixtureSums
{
    /// <summary>ln n of the iteration; ln of the gaseous moles in the frozen path.</summary>
    public double LogN;

    /// <summary>ln(p/p°), p° being the 1 bar the thermodynamic data are for.</summary>
    public double LogPressure;

    /// <summary>K.</summary>
    public double Temperature;

    /// <summary>e^LogN: the total-moles unknown of the reduced system, equal to SumGas only at convergence; zero in the frozen path, which has no system.</summary>
    public double N;

    /// <summary>Σ n_j over the retained gaseous species, kmol per kg.</summary>
    public double SumGas;

    /// <summary>Σ n_j h_j°/RT over every species.</summary>
    public double HOverRT;

    /// <summary>Σ n_j s_j/R, the gaseous ones carrying their mixing terms.</summary>
    public double SOverR;

    /// <summary>Σ n_j cp_j°/R over every species.</summary>
    public double CpOverR;

    /// <summary>Σ n_j over the condensed species, kmol per kg.</summary>
    public double CondensedMoles;
}

/// <summary>The equilibrium derivatives of RP-1311 section 2.6 at the converged composition.</summary>
internal struct Derivatives
{
    /// <summary>(∂ln n/∂ln T)_p; zero at a pinned pair, where the constant-pressure derivatives do not exist.</summary>
    public double DlnNdlnT;

    /// <summary>(∂ln n/∂ln p)_T.</summary>
    public double DlnNdlnP;

    /// <summary>The reaction part of cp/R, equation (2.59); zero at a pinned pair.</summary>
    public double Reaction;

    /// <summary>True when two records of one formula stand in the solution: the plateau convention of the node's API.md.</summary>
    public bool Pinned;

    /// <summary>False when a derivative system was singular; the caller reports <see cref="Thermo.CaseStatus.SingularMatrix"/>.</summary>
    public bool Solved;
}

/// <summary>
/// The state one case carries from stage to stage and from one convergence to the next: the iterate itself, the temperature
/// its species functions were evaluated at, the counters the caps are measured against, and the two memories of the
/// condensed-species rule (BOOT.md). Passed by <c>ref</c>; it is the whole of the per-case state that is not in the views.
/// </summary>
internal struct IterationState
{
    /// <summary>K.</summary>
    public double Temperature;

    /// <summary>ln of the total gaseous moles per kilogram.</summary>
    public double LogN;

    /// <summary>How many entries of <c>scratch.CondensedInSolution</c> are live.</summary>
    public int CondensedCount;

    /// <summary>The temperature <c>scratch.HOverRT</c> and its neighbours were last evaluated at; −1 before the first evaluation.</summary>
    public double FunctionsAt;

    /// <summary>Newton steps taken over the whole solve, the number reported.</summary>
    public int Iterations;

    /// <summary>Changes of the condensed set, capped by <see cref="EquilibriumSolver.MaxCondensedSetChanges"/>.</summary>
    public int SetChanges;

    /// <summary>The record switched out at the last range switch, which may pair with its neighbour again; −1 if none.</summary>
    public int LastSwitchedOut;

    /// <summary>The record removed for its range at the last convergence, skipped by one inclusion pass; −1 if none.</summary>
    public int LastRemovedForRange;
}
