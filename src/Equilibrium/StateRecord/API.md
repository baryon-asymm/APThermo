# API.md — Equilibrium.StateRecord

Namespace `APThermo.Equilibrium.StateRecord`. Every type is `internal`: the audience is
`src/Equilibrium`'s own file (`EquilibriumSolver`) and its tests node, not a neighbour or a caller
outside the tree. Everything not listed here is internal to this node itself and may change
without notice even to the parent. The parent's types that the signatures name (`IterationState`,
`EquilibriumScratch`, `EquilibriumResult`, `EquilibriumProblem`, `MixtureSums`, `Derivatives`) are
described in [the parent's API.md](../API.md).

## The state record ✅

```csharp
namespace APThermo.Equilibrium.StateRecord;

internal static class DerivativeSystem
{
    public static Derivatives Solve(in SpeciesTableView table, in EquilibriumScratch scratch, in EquilibriumResult result, in IterationState state, int stride);
        // the derivative system of RP-1311 section 2.5; Solved is false when it was singular, Pinned at a pinned pair
}

internal static class MixtureProperties
{
    public static bool WriteEquilibrium(in EquilibriumProblem problem, in EquilibriumResult result, in MixtureSums sums, in Derivatives derivatives);
        // the converged state, or the plateau convention of a pinned pair; false, nothing written, when the state guard fails
    public static bool WriteFrozen(in EquilibriumProblem problem, in EquilibriumResult result, in MixtureSums sums);
        // the frozen state: CpEquilibrium = CpFrozen, the derivatives 1 and −1; false when the state guard fails
}
```

The values written into `result.State[0]` are those of the parent's `API.md` (Units, and the
pinned two-phase state); the state guard is in `BOOT.md`, Constraints.

## Errors

None thrown. A refused state is `false`; the caller reports the status.

## Side effects

None. `WriteEquilibrium` and `WriteFrozen` write `result.State[0]` and nothing else;
`DerivativeSystem.Solve` uses the matrix scratch and restores the caller's condensed order before it
returns.

## Out of scope

- Which condensed species stand in the solution: `Condensed`.
- Solving for the composition: `Newton`.
- Throat, areas and performance figures: `Performance`.
