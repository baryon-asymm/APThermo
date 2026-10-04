# API.md — Equilibrium.Recovery

Namespace `APThermo.Equilibrium.Recovery`. Internal to `src/Equilibrium`'s assembly: used by
`EquilibriumSolver` and by the tests node; neither on the package surface nor in the parent's tree contract.

## The attempt plan ✅

```csharp
internal enum AttemptPhase { Warm, Cold, VerdictOnly, Final }
internal struct AttemptPlan
{
    public EquilibriumProblem Current; public EstimateSource Source; public AttemptPhase Phase;
    public CaseStatus Status; public int Iterations;
    public TemperatureBracket Bracket; public EndKind Found; public CondensedFigures Figures;
    public RecheckState Recheck;
    public readonly bool RunsAttempt { get; }
    public void BeginBracket(double estimate);
    public static AttemptPlan Start(in EquilibriumProblem problem, bool useMolesAsEstimate);
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool Next(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch,
                            in EquilibriumResult result, CaseStatus status, ref AttemptPlan plan);
        // true: run Current from Source next (a VerdictOnly pass runs the verdict alone); false: the case ends with Status and Iterations
}
```

## The temperature bracket ✅

```csharp
internal enum EndKind { Gas, Gasless }
internal enum BracketMove { Probe, GiveUp, AttemptFromLever, AttemptFromProbe, GaslessAtProbe, GaslessFromLever }
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
`Seed`, `Lever`) and `PassOutcome` (`Finds`, `EndGasless`, `EndGiveUp`, `ClearState`, `Value`, `Slope` and the problem
constructors) are internal to this node.
