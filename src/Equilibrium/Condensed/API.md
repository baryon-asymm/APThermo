# API.md — Equilibrium.Condensed

Namespace `APThermo.Equilibrium.Condensed`. Every type is `internal`: the audience is
`src/Equilibrium`'s own files (`EquilibriumSolver`), the sibling child node `Newton` and its tests
node and the sibling `TraceGas` (2026-10-04) (the `StateRecord` child stopped using this node on 2026-10-03, `StateRecord/BOOT.md`), not a neighbour or a caller outside the tree. Everything not
listed here is internal to this node itself and may change without notice even to the parent. The
parent's types that the signatures name (`IterationState`, `EquilibriumScratch`,
`EquilibriumResult`, `EquilibriumProblem`) are described in [the parent's API.md](../API.md).

## The condensed set ✅

```csharp
namespace APThermo.Equilibrium.Condensed;

internal static class CondensedSet
{
    public static bool Update(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch, in EquilibriumResult result, ref IterationState state);
        // applies at most one change of the set and says whether it changed; the caller converges again if it did.
        // A negative record that is a balancing record (BOOT.md, "A balancing record stays") is no change: it stays at zero moles
    public static double InclusionGain(in SpeciesTableView table, in EquilibriumScratch scratch, in EquilibriumResult result, int species);
        // the per-mole gain of adding a condensed species at the current multipliers (RP-1311 section 3.4)
    public static bool ExitGuardFindsAPositiveCandidate(in SpeciesTableView table, in EquilibriumScratch scratch, in EquilibriumResult result, in IterationState state);
        // true when an Ok state left a condensed candidate out whose gain exceeds the rounding of the multipliers
    public static int Remove(in EquilibriumScratch scratch, in EquilibriumResult result, int condensedCount, int position);
        // takes the species at the position out of the solution; returns the new count
    public static void MarkRemoved(in EquilibriumScratch scratch, int j, ref IterationState state);
        // remembers a record removed for its range, so that one inclusion pass passes it over
}
```

## Phase geometry ✅

```csharp
namespace APThermo.Equilibrium.Condensed;

internal static class PhaseGeometry
{
    public static bool SameFormula(in SpeciesTableView table, int j, int k);
    public static int PartnerInSolution(in SpeciesTableView table, in EquilibriumScratch scratch, int condensedCount, int species);
    public static bool InSolution(in EquilibriumScratch scratch, int condensedCount, int species);
    public static int Adjacent(in SpeciesTableView table, in EquilibriumScratch scratch, int j, bool above);
    public static double Crossing(in SpeciesTableView table, int j, int k, double bound);
    public static double EffectiveLow(in SpeciesTableView table, in EquilibriumScratch scratch, int j);
    public static double EffectiveHigh(in SpeciesTableView table, in EquilibriumScratch scratch, int j);
    public static bool InEffectiveRange(in SpeciesTableView table, in EquilibriumScratch scratch, int j, double temperature);
    public static int PhaseAt(in SpeciesTableView table, in EquilibriumScratch scratch, int condensedCount, int j, double temperature);
}
```

`BOOT.md`, Constraints, "Effective range" fixes the meaning of the crossing and of the effective
bounds. `Adjacent` and `PhaseAt` skip a record that is not in play (`SpeciesMarks.InPlay`).

## Errors

None thrown. The exit guard's answer is a value the caller turns into `NotConverged`.

## Side effects

None. Writes only into the views of `EquilibriumScratch` and `EquilibriumResult` and into the
`ref` state it is handed.

## Out of scope

- The Newton iteration and the singular remedies: `Newton`.
- The state record: `StateRecord`.
- The species records' ranges as data: `Thermo` (`SpeciesFunctions.RecordLow` and `RecordHigh`).
