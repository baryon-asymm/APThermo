# API.md — Equilibrium.Newton

Namespace `APThermo.Equilibrium.Newton`. Every type is `internal`: the audience is
`src/Equilibrium`'s own files (`EquilibriumSolver`, `Composition`) and the sibling child nodes
(`Condensed`, `StateRecord`), and its tests node, not a neighbour or a caller outside the tree.
Everything not listed here is internal to this node itself and may change without notice even to
the parent. The types of the parent that the signatures name (`IterationState`, `SystemLayout`,
`MixtureSums`, `ElementTie` (active and element), `EquilibriumScratch` (its `TieElements`), `EquilibriumResult`, `EquilibriumProblem`) are
described in [the parent's API.md](../API.md) or by the summaries of their declarations.

## The loop ✅

```csharp
namespace APThermo.Equilibrium.Newton;

internal static class NewtonIteration
{
    public static CaseStatus Converge(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch, in EquilibriumResult result, double logPressure, ref IterationState state);
        // converges the current condensed set: Ok, NotConverged, SingularMatrix or TemperatureOutOfRange
}

internal enum ConvergenceVerdict
{
    NotConverged,
    ReportTestsMet,
    Polished,
}

internal struct NewtonLoopState
{
    public int Steps;
    public bool Converged;
    public int PolishSteps;
    public int SingularResets;
    public void RecordSetChange();                             // a change of the condensed set restarts the step count
    public void RecordVerdict(ConvergenceVerdict verdict);     // a failed verdict clears the mark and the polish count
}

internal static class ConvergenceTests
{
    public static ConvergenceVerdict Evaluate(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch, in EquilibriumResult result, in SystemLayout layout, in MixtureSums sums);
    public static bool RetentionCrossed(in SpeciesTableView table, in EquilibriumScratch scratch, in EquilibriumResult result, double logN, double traceThreshold);
        // whether the step carried a gas across the retention threshold, in either direction
}
```

`Converge` takes the iterate in `state` and the scratch, runs the steps of `BOOT.md`, Constraints,
and leaves the last iterate in `result.Moles` and the multipliers in `result.Multipliers`. It
never throws; the status is the value.

## Singular matrices ✅

```csharp
namespace APThermo.Equilibrium.Newton;

internal static class SingularRemedies
{
    public static bool Recover(in SpeciesTableView table, in EquilibriumScratch scratch, in EquilibriumResult result, int failedRow, ref NewtonLoopState loop, ref IterationState state);
        // rule B, rule A, the two resets, the targeted removal, in that order; false when nothing is left
}

internal static class ElementCoupling
{
    public static ElementTie Find(in SpeciesTableView table, in EquilibriumScratch scratch, in EquilibriumResult result, int condensedCount, int element);
        // the tie of `element` as a linear combination of the other active rows over the species of the sums; its coefficients are
        // left in scratch.TieElements.Coefficients (one per element, zero for `element`); inactive when no combination holds
    public static bool Coupled(in SpeciesTableView table, in EquilibriumScratch scratch, in EquilibriumResult result, int condensedCount, in ElementTie tie);
        // whether every species of the sums still satisfies the combination of the live coefficients, to 1e-10 relative
    public static bool HeldByCondensed(in SpeciesTableView table, in EquilibriumScratch scratch, int condensedCount, in ElementTie tie);
        // whether a condensed species of the solution carries the tied element and an element of the combination
    public static double Weight(in SpeciesTableView table, in EquilibriumScratch scratch, int element, int species);
        // a_kj - Σ c_i a_ij: the species' weight in the tie row
    public static double Abundance(in EquilibriumProblem problem, in EquilibriumScratch scratch, int elementCount, int element);
        // b_k - Σ c_i b_i: what the tie row balances
}

internal static class CondensedDependency
{
    public static int LeavingPosition(in SpeciesTableView table, in EquilibriumScratch scratch, in EquilibriumResult result, int condensedCount);
        // rule B's ratio test: the position of the species that leaves, or −1
}

internal static class TieSnapshot
{
    public static void Save(in SpeciesTableView table, in EquilibriumScratch scratch, in EquilibriumResult result, int condensedCount);
    public static void Restore(in SpeciesTableView table, in EquilibriumScratch scratch, in EquilibriumResult result, int condensedCount);
        // rule A's release: the tied converged iterate, with the combination's coefficients, kept in the case's scratch and put back when the release fails
}
```

## Errors

None thrown. A step that cannot be taken is a status: `SingularMatrix` once none of the remedies
resolves the system, `TemperatureOutOfRange` when an hp or sp iterate leaves its window,
`NotConverged` when the step cap runs out.

## Side effects

None. Writes only into the views of `EquilibriumScratch` and `EquilibriumResult` and into the
`ref` state it is handed.

## Out of scope

- Which condensed species stand in the solution between two convergences: `Condensed`.
- The state record and the derivatives: `StateRecord`.
- The retention rule's application and the sums: `Composition`, in the parent.
