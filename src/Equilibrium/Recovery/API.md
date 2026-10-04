# API.md — Equilibrium.Recovery

Namespace `APThermo.Equilibrium.Recovery`. Internal to `src/Equilibrium`'s assembly: used by
`EquilibriumSolver` and by the tests node; neither on the package surface nor in the parent's tree contract.

## The attempt plan ✅

```csharp
internal enum AttemptPhase { Warm, Cold }
internal struct AttemptPlan
{
    public EquilibriumProblem Current; public EstimateSource Source; public AttemptPhase Phase;
    public CaseStatus Status; public int Iterations;
    public static AttemptPlan Start(in EquilibriumProblem problem, bool useMolesAsEstimate);
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool Next(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch,
                            in EquilibriumResult result, CaseStatus status, ref AttemptPlan plan);
        // true: run Current from Source next; false: the case ends with Status and Iterations
}
```
