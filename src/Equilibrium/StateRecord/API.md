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
    public static Derivatives Solve(in SpeciesTableView table, in EquilibriumScratch scratch, in EquilibriumResult result, in IterationState state, int stride, in MixtureSums sums);
        // the derivative system of RP-1311 section 2.5 at the converged composition, whose sums the isentropic system reads;
        // Solved is false when it was singular and no gas-participating plateau explains it, Pinned at a pinned set or a
        // gas-participating plateau, Isentropic (with DlnVdlnPIsentropic) where gamma_s comes from the isentropic system;
        // a singular pass is solved once more with the condensed columns carried relative to n
}

internal static class TiedDerivatives
{
    public static Derivatives Solve(in SpeciesTableView table, in EquilibriumScratch scratch, in EquilibriumResult result, ref IterationState state, int stride, in MixtureSums sums);
        // DerivativeSystem.Solve; when unsolved with no tie in force, again with the first tie ElementCoupling.Find shows,
        // recorded in state.Tie (field by field, as a surviving tie is)
}

internal static class PlateauIsentrope
{
    public static SystemLayout Assemble(in SpeciesTableView table, in EquilibriumScratch scratch, in EquilibriumResult result, in MixtureSums sums, in SystemLayout layout);
        // the sp-shaped system at the converged composition for d ln p = 1; returns its layout, which the caller solves
    public static double DlnVdlnP(in EquilibriumScratch scratch, in SystemLayout system);
        // (dlnV/dlnp)_s = d ln n + d ln T - 1, read from the solved system
}

internal static class MixtureProperties
{
    public static bool WriteEquilibrium(in EquilibriumProblem problem, in EquilibriumResult result, in MixtureSums sums, in Derivatives derivatives);
        // the converged state, or the plateau convention of a pinned set; false, nothing written, when the state guard fails
    public static bool IsNearUnivariant(in MixtureSums sums, in Derivatives derivatives);
        // true when |Cp/Cv| of the constant-temperature route exceeds 1e6: gamma_s then comes from the isentropic system
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
`DerivativeSystem.Solve` and `PlateauIsentrope.Assemble` use the matrix scratch, and `Solve` restores the
caller's condensed order before it returns.

## Out of scope

- Which condensed species stand in the solution: `Condensed`.
- Solving for the composition: `Newton`.
- Throat, areas and performance figures: `Performance`.
