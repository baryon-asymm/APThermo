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
    public int TraceGasFinals;                                 // trace-gas finals (seams (b), (b′)): at most one per bracket, reset at a new floor
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
    public readonly bool NarrowEnds { get; }
    public readonly bool GaveUpAtDomainEdge { get; }
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
internal enum Recheck { None, Pending, Held, Complete }
internal struct RecheckState
{
    public Recheck Stage; public double Floor; public double BestObjective;
    public CaseStatus BestStatus; public bool Unresolved;
}
internal static class DeadEnds
{
    public static bool IsDeadEnd(in SpeciesTableView table, int species);
    public static bool IsDeadCeiling(in SpeciesTableView table, int species);
    public static bool BoundNear(in SpeciesTableView table, in EquilibriumScratch scratch, double temperature);
    public static double FloorAbove(in SpeciesTableView table, in EquilibriumScratch scratch, in EquilibriumResult result, double temperature);
    public static double NextFloorAbove(in SpeciesTableView table, in EquilibriumScratch scratch, double cursor);
    public static double FloorBelow(in SpeciesTableView table, in EquilibriumScratch scratch, double temperature);
}
```

`BracketDriver` (the bracket's moves), `DeadEndRecheck`, `BracketSeeds` (the only writer of `BracketEnds`: `Save`,
`Seed`, `Lever`, `Anchor`) and `PassOutcome` (`Finds`, `EndGasless`, `EndGiveUp`, `ClearState`, `Value`, `Slope` and the problem
constructors) are internal to this node.

## Best-candidate bank ✅

The scratch bank preserves the best feasible result while later floor probes run.
Strict objective improvement wins; ties retain the earlier state, original first.
The bank includes the exact condensed slots alongside moles, multipliers and state.
All Newton steps remain counted. An unresolved branch returns `NotConverged` with
zero state and the best known composition; a gasless winner keeps T,p only.

⚠ 2026-11-17: bracket give-up reason is kept separately from its inherited status.
An exhausted numerical probe cannot become a domain-edge certificate merely because
the original failure was `TemperatureOutOfRange`.

```csharp
internal static class DeadEndCandidates
{
    public static void Begin(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch, in EquilibriumResult result, ref AttemptPlan plan);
    public static void Consider(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch, in EquilibriumResult result, ref AttemptPlan plan);
    public static void Complete(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch, in EquilibriumResult result, ref AttemptPlan plan);
}
internal static class RecheckFinal
{
    public static bool End(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch, in EquilibriumResult result, ref AttemptPlan plan);
}
```

⚠ 2026-11-14 (R08): the original successful-attempt gate admitted Cold only.
Initial Warm HP/SP successes now use the same positive-floor bounded bank. The
completed warm result is retained exactly before the first Defaults/Cold probe;
no positive floor leaves it untouched, and a failed Warm still takes its existing
cold retry. Final/TraceGasFinal passes contribute to the active bank through the
existing interception, so they do not recursively start a new bank. This extends
entry eligibility without changing objectives, strict ties, work counters or search
scope; it is not a universal global-optimum claim.
⚠ 2026-11-14: RecheckState previously carried the full MixtureState candidate by
value. R10 stores those exact bits in the required isolated RecoverySlices.BestState
view instead. The local plan retains only the objective/status/cursor and unresolved
flag; strict selection, tie retention, restoration and paid iteration counts are unchanged.
⚠ 2026-11-16: the bank previously left condensed slots from the last probe, which
could differ from the restored candidate. `RecoveryCandidateTests` demonstrates the
discrepancy on a physical SP case and checks its cold and seeded starts.
