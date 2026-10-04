# API.md — Equilibrium.Recovery

Namespace `APThermo.Equilibrium.Recovery`. Internal to `src/Equilibrium`'s assembly: used by
`EquilibriumSolver` and by the tests node; neither on the package surface nor in the parent's tree contract.

## The attempt plan ✅

```csharp
internal enum AttemptPhase { Warm, Cold, VerdictOnly, Final, TraceGas, TraceGasFinal }
internal struct AttemptPlan
{
    public EquilibriumProblem Current; public EstimateSource Source; public AttemptPhase Phase;
    public CaseStatus Status; public int Iterations; public CaseStatus Judged;   // Judged: the status of the attempt a GasRequired verdict judged
    public int TraceGasFinals;                                 // trace-gas finals launched (seams (b), (b′)): at most one per case
    public TemperatureBracket Bracket; public EndKind Found; public CondensedFigures Figures;
    public RecheckState Recheck;
    public readonly bool RunsAttempt { get; }
    public readonly bool OwesTraceGasFinal { get; }              // seam (b′): the ordinary final just ended NotConverged or SingularMatrix, and none was launched
    public readonly bool RunsTraceGas { get; }                 // TraceGas or TraceGasFinal: Solve runs TraceGasPass.Run, not ConvergenceSequence.Run
    public void BeginBracket(double estimate);
    public void SeekTraceGas();                            // seam (a): sets Phase = TraceGas, Source = PreviousSolution, Judged = Status after a failed NotConverged/SingularMatrix pass
    public void Begin(in EquilibriumProblem problem, bool useMolesAsEstimate);        // on a default plan; never returned by value
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool Next(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch,
                            in EquilibriumResult result, CaseStatus status, ref AttemptPlan plan);
        // true: run Current from Source next (a VerdictOnly pass runs the verdict alone); false: the case ends with Status and Iterations
}
```

## The temperature bracket ✅

```csharp
internal enum EndKind { Gas, Gasless, TraceGas }
internal enum BracketMove { Probe, GiveUp, AttemptFromLever, AttemptFromProbe, TraceGasFromLever, TraceGasFromProbe, GaslessAtProbe, GaslessFromLever }
internal struct TemperatureBracket
{
    public bool Active; public CaseStatus FirstFailure; public CaseStatus GiveUpStatus; public bool Scanning;
    public double LowX; public double HighX; public double LowP; public double HighP;      // ln T and the assigned property of the two ends
    public bool HaveLow; public bool HaveHigh; public EndKind LowKind; public EndKind HighKind;
    public bool LastBelow; public EndKind LastKind; public double ProbeX; public int Probes; public int Retreats;
    public double OlderStep; public double LastStep; public bool Finishing; public BracketMove Final; public double FinalX;
    public readonly double ProbeTemperature { get; }
    public readonly bool NeedsFloor { get; }
    public readonly EndKind NearerKind { get; }
    public void Start(CaseStatus firstFailure, double estimate);
    public readonly bool NearerIsLow(double x);
    public double Record(double target, double value, double slope, EndKind kind);   // the Newton step on ln T
    public BracketMove Advance(double step, double floor);
    public BracketMove Retreat();
    public void BeginScan();
    public BracketMove Scan(double floor);
    public readonly double LeverFraction(double target);
    public readonly double LeverTemperature(double target);
}
```

## The recheck below a dead-end floor ✅

```csharp
internal enum Recheck { None, Pending, Held, Replay }
internal struct RecheckState { public Recheck Stage; public EquilibriumProblem Original; public int Banked; }
internal static class DeadEnds
{
    public static bool IsDeadEnd(in SpeciesTableView table, int species);
    public static bool IsDeadCeiling(in SpeciesTableView table, int species);
    public static bool BoundNear(in SpeciesTableView table, in EquilibriumScratch scratch, double temperature);
    public static double FloorAbove(in SpeciesTableView table, in EquilibriumScratch scratch, in EquilibriumResult result, double temperature);
    public static double FloorBelow(in SpeciesTableView table, in EquilibriumScratch scratch, double temperature);
}
```

`BracketDriver` (the bracket's moves), `DeadEndRecheck`, `BracketSeeds` (the only writer of `BracketEnds`: `Save`,
`Seed`, `Lever`, `Anchor`) and `PassOutcome` (`Finds`, `EndGasless`, `EndGiveUp`, `ClearState`, `Value`, `Slope` and the problem
constructors) are internal to this node.
