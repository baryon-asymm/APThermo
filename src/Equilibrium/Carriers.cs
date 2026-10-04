namespace APThermo.Equilibrium;

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

/// <summary>The mark accessors of <c>scratch.SpeciesActive</c> (<see cref="SpeciesMark"/>), used by every stage of the iteration.</summary>
internal static class SpeciesMarks
{
    /// <summary>The mark of a species in the scratch (the domain is in the node's API.md).</summary>
    public static SpeciesMark Of(in EquilibriumScratch scratch, int species) => (SpeciesMark)scratch.SpeciesActive[species];

    /// <summary>Writes a species' mark.</summary>
    public static void Set(in EquilibriumScratch scratch, int species, SpeciesMark mark) => scratch.SpeciesActive[species] = (int)mark;

    /// <summary>
    /// Whether the species takes part in this case at all: its elements are present and the anti-cycling rule has not stood
    /// it down. A record forgiven once is still in play — it may be skipped by one inclusion pass, not removed from the case.
    /// </summary>
    public static bool InPlay(in EquilibriumScratch scratch, int species)
    {
        var mark = Of(scratch, species);
        return mark is SpeciesMark.Active or SpeciesMark.ForgivenOnce;
    }
}

/// <summary>
/// Rule A's tie (BOOT.md of the Newton child node, "Rule A: an element tie"): the one active element whose failed row was
/// found to equal a linear combination of the other active rows over every species of the sums. The coefficients of the
/// combination are not part of the carrier: they live in the case's scratch (<see cref="TieElementSlices.Coefficients"/>),
/// one per element. Per-case state, carried in <see cref="IterationState"/> and copied into <see cref="SystemLayout"/> for
/// the one Newton step that assembles the tie row (<see cref="Newton.IterationMatrix"/> is the only writer of it).
/// </summary>
internal struct ElementTie
{
    public bool Active;
    public int Element;
}

/// <summary>
/// The shape of the reduced system of one convergence: how many unknowns, where the total-moles and temperature rows sit,
/// and which problem is being solved. The derivative system of section 2.5 is a tp-shaped layout over the same scratch.
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
        Tie = default;
        CarrierLogN = double.NegativeInfinity;
    }

    /// <summary>As above, with rule A's tie for the Newton step that assembles the tie row.</summary>
    public SystemLayout(ProblemKind kind, int elementCount, int condensedCount, int stride, ElementTie tie)
        : this(kind, elementCount, condensedCount, stride)
    {
        Tie = tie;
    }

    /// <summary>
    /// As above, for a derivative system over a state that holds trace carriers: <paramref name="carrierLogN"/> is the ln n the
    /// carriers are measured against, <c>−∞</c> for a state that holds none (<see cref="IterationState.TraceCarriers"/>).
    /// </summary>
    public SystemLayout(ProblemKind kind, int elementCount, int condensedCount, int stride, ElementTie tie, double carrierLogN)
        : this(kind, elementCount, condensedCount, stride, tie)
    {
        CarrierLogN = carrierLogN;
    }

    /// <summary>As above, the tie and the carriers' ln n taken from the state a derivative system is built over.</summary>
    public SystemLayout(ProblemKind kind, int elementCount, int condensedCount, int stride, in IterationState state)
        : this(kind, elementCount, condensedCount, stride, state.Tie, state.TraceCarriers > 0 ? state.LogN : double.NegativeInfinity)
    {
    }

    public readonly ProblemKind Kind;
    public readonly int ElementCount;
    public readonly int CondensedCount;

    /// <summary>Rule A's tie for this Newton step, if any; inactive outside the Newton loop (the derivative system reads its own from <see cref="IterationState"/>).</summary>
    public readonly ElementTie Tie;

    /// <summary>Row stride of the scratch matrix: <see cref="ScratchLayout.MaxUnknowns"/> of the case, not the live count.</summary>
    public readonly int Stride;

    /// <summary>The row and column of Δln n.</summary>
    public readonly int NRow;

    /// <summary>The row and column of Δln T; present only for hp and sp.</summary>
    public readonly int TRow;

    public readonly int Unknowns;

    /// <summary>
    /// ln n of a state that holds trace carriers, the gases a trace-gas <c>Ok</c> reports below the second retention stage because
    /// they carry a part of a balance (BOOT.md, "Element conservation"); <c>−∞</c> for every other state. The derivative systems read no
    /// gas below that stage of it (<see cref="GasMoles"/>); the sums of h, s and M include them.
    /// </summary>
    public readonly double CarrierLogN;

    /// <summary>The moles of gas <paramref name="j"/> the derivative systems read: the result's, and zero for a trace carrier.</summary>
    public double GasMoles(in EquilibriumScratch scratch, in EquilibriumResult result, int j) =>
        scratch.LogMoles[j] - CarrierLogN <= -EquilibriumSolver.SecondStageTraceThreshold ? 0.0 : result.Moles[j];

    public bool IsTp => Kind == ProblemKind.AssignedTemperaturePressure;

    public bool IsHp => Kind == ProblemKind.AssignedEnthalpyPressure;
}

/// <summary>
/// The point one Newton step is linearized at and the sums over the composition taken there, in ascending species order.
/// <c>LogN</c> and <c>Temperature</c> are the iterate the system was assembled at; <see cref="Newton.DampedStep.Apply"/> then moves
/// <see cref="IterationState"/> while this copy stays put, so the convergence tests read the n and the gaseous sum of the
/// step's own linearization, as the report's tests do. The state record reads the same sums at the converged iterate; the
/// frozen path fills them from a given composition, with no system and <c>N</c> zero. Built field by field by
/// <see cref="Composition"/> and read through <c>in</c> afterwards.
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

/// <summary>The equilibrium derivatives of RP-1311 section 2.5 at the converged composition.</summary>
internal struct Derivatives
{
    /// <summary>(∂ln n/∂ln T)_p; zero at a pinned set, where the constant-pressure derivatives do not exist.</summary>
    public double DlnNdlnT;

    /// <summary>(∂ln n/∂ln p)_T.</summary>
    public double DlnNdlnP;

    /// <summary>The reaction part of cp/R, equation (2.59); zero at a pinned set.</summary>
    public double Reaction;

    /// <summary>True when the condensed species of the solution have linearly dependent element vectors (the pinned set), or a condensed vector lies in the span of the gas composition and the vectors before it (a gas-participating plateau): the plateau convention of the node's API.md.</summary>
    public bool Pinned;

    /// <summary>False when a derivative system was singular; the caller reports <see cref="Thermo.CaseStatus.SingularMatrix"/>.</summary>
    public bool Solved;

    /// <summary>True when <see cref="DlnVdlnPIsentropic"/> was solved from the isentropic system: at a gas-participating plateau (<see cref="Pinned"/>), or at a near-univariant state whose constant-temperature route cancels.</summary>
    public bool Isentropic;

    /// <summary>(∂ln V/∂ln p)_s from the isentropic system; meaningful only when <see cref="Isentropic"/>.</summary>
    public double DlnVdlnPIsentropic;
}

/// <summary>
/// The state one case carries from stage to stage and from one convergence to the next: the iterate itself, the temperature
/// its species functions were evaluated at, the counters the caps are measured against, and the two memories of the
/// condensed-species rule (BOOT.md). Passed by <c>ref</c>; it is the whole of the per-case state that is not in the views.
/// No two <c>bool</c> fields stand next to each other: ILGPU 1.5.3 stores and loads two adjacent ones as one vector of bytes or
/// predicates, which ptxas refuses once a <c>ref</c> to a non-inlined pass keeps the struct in local memory (Recovery BOOT.md,
/// "No whole-struct copies").
/// </summary>
internal struct IterationState
{
    /// <summary>K.</summary>
    public double Temperature;

    /// <summary>ln of the total gaseous moles per kilogram.</summary>
    public double LogN;

    /// <summary>The temperature <c>scratch.HOverRT</c> and its neighbours were last evaluated at; −1 before the first evaluation.</summary>
    public double FunctionsAt;

    /// <summary>How many entries of <c>scratch.CondensedInSolution</c> are live.</summary>
    public int CondensedCount;

    /// <summary>
    /// Whether the case has already switched its gaseous retention threshold to the second stage (BOOT.md, the
    /// two-stage retention threshold, 2026-09-28): the case starts at the first stage (1e-8) and switches once, at
    /// its first convergence, to the second (1e-11) for the rest of the solve — one attempt of
    /// <see cref="EquilibriumSolver.Solve"/>, a warm start's own cold retry included, since each attempt gets a fresh
    /// <see cref="IterationState"/>.
    /// </summary>
    public bool RetentionSecondStage;

    /// <summary>Newton steps taken over the whole solve, the number reported.</summary>
    public int Iterations;

    /// <summary>
    /// Whether the retained set is held (BOOT.md, the threshold flip, 2026-10-03): set at most once per attempt, when two
    /// consecutive second-stage steps passed the report's tests and were refused only because one gas entered the retained
    /// set while another left it. From then on a gas once retained stays retained whatever its amount
    /// (<see cref="Composition.IsRetained"/>), so the carriers of a direction of the multipliers that the retained
    /// species leave free are summed together rather than one side at a time. Never cleared within an attempt.
    /// </summary>
    public bool RetainedSetHeld;

    /// <summary>Changes of the condensed set, capped by <see cref="EquilibriumSolver.MaxCondensedSetChanges"/>.</summary>
    public int SetChanges;

    /// <summary>
    /// How many gases a trace-gas <c>Ok</c> reports below the second retention stage because they carry a part of a balance (TraceGas
    /// BOOT.md, "An Ok reports its balance carriers"); zero for every other state. <see cref="SystemLayout.CarrierLogN"/> keeps them out of the derivatives.
    /// </summary>
    public int TraceCarriers;

    /// <summary>
    /// Rule A's tie (BOOT.md of the Newton child node), once a tied element row made the matrix singular; inactive until
    /// then. <see cref="StateRecord.DerivativeSystem"/> reads it from here, not from a parameter of its own.
    /// </summary>
    public ElementTie Tie;

    /// <summary>Whether the tie has already been released once in this solve (BOOT.md, rule A, "Release"): at most once per solve.</summary>
    public bool TieReleased;

    /// <summary>The record switched out at the last range switch, which may pair with its neighbour again; −1 if none.</summary>
    public int LastSwitchedOut;

    /// <summary>
    /// Whether the temperature is assigned (a tp problem), set by <see cref="CaseSetup.Begin"/> (BOOT.md of the Newton child
    /// node, rule B, 2026-10-03). Rule B's gas column binds an assigned temperature only: with the temperature a variable, a
    /// set that is a linear combination of its own condensed species and the gas phase is a gas-participating plateau, whose
    /// singular direction is rule A's element tie, not a removal.
    /// </summary>
    public bool AssignedTemperature;
    /// <summary>The record removed for its range at the last convergence, skipped by one inclusion pass; −1 if none.</summary>
    public int LastRemovedForRange;
}
