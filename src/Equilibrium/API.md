# API.md — Equilibrium

Namespace `AerospacePropellantThermodynamics.Equilibrium`. The node exposes one
kernel-compatible solver for the equilibrium composition of one case, plus the
descriptors of its inputs, scratch and outputs. Everything not listed here is internal
and may change.

## Solver ⏳

```csharp
namespace AerospacePropellantThermodynamics.Equilibrium;

public enum ProblemKind { AssignedTemperaturePressure, AssignedEnthalpyPressure, AssignedEntropyPressure }

public readonly struct EquilibriumProblem              // one case
{
    public readonly ProblemKind Kind;
    public readonly double Pressure;                   // Pa
    public readonly double Temperature;                // K for tp; initial estimate for hp/sp (0 = default)
    public readonly double Target;                     // h in J/kg for hp, s in J/(kg·K) for sp, unused for tp
    public readonly ArrayView<double> ElementMoles;    // [element], kmol per kg of mixture (b_i)
}

public readonly struct EquilibriumScratch              // slices of batch-sized buffers, sized by ScratchLayout
{
    public readonly ArrayView<double> GOverRT;         // [species]
    public readonly ArrayView<double> LogMoles;        // [species]
    public readonly ArrayView<double> Matrix;          // [MaxUnknowns * MaxUnknowns]
    public readonly ArrayView<double> RightHandSide;   // [MaxUnknowns]
    public readonly ArrayView<int> Pivots;             // [MaxUnknowns]
    public readonly ArrayView<int> CondensedInSolution;// [MaxCondensedInSolution]
}

public readonly struct EquilibriumResult               // views the solver writes into
{
    public readonly ArrayView<double> Moles;           // [species], kmol per kg; zero for absent condensed species
    public readonly ArrayView<double> Multipliers;     // [element], Lagrange multipliers π_i (dimensionless)
    public readonly ArrayView<MixtureState> State;     // [1]
    public readonly ArrayView<int> Status;             // [1], CaseStatus
    public readonly ArrayView<int> Iterations;         // [1]
}

public static class EquilibriumSolver                  // kernel-compatible
{
    public static void Solve(in SpeciesTableView table, in EquilibriumProblem problem,
                             in EquilibriumScratch scratch, in EquilibriumResult result,
                             bool useMolesAsEstimate);
    public static void SolveFrozen(in SpeciesTableView table, in EquilibriumProblem problem,
                                   in EquilibriumScratch scratch, in EquilibriumResult result);
        // composition fixed to result.Moles; solves for temperature (hp, sp) or evaluates (tp)
}

public static class ScratchLayout
{
    public const int MaxCondensedInSolution = 8;
    public static int MaxUnknowns(int elementCount) => elementCount + MaxCondensedInSolution + 2;
    public static int DoublesPerCase(int speciesCount, int elementCount);
    public static int IntsPerCase(int elementCount);
}
```

Units: SI throughout; mole numbers in kmol per kilogram of mixture, so that
`Σ n_j M_j = 1` over the whole mixture. `Multipliers` are the dimensionless `π_i` of
RP-1311. `State.Velocity` and `State.Mach` are left at zero by this node.

## Errors

An element with zero abundance is allowed: the species containing it are inactive
for the case and get mole number zero. The solver never throws. `Status` is one of
`Ok`, `InvalidInput` (every abundance zero, negative abundance, empty table,
non-positive pressure, non-positive temperature for tp),
`NotConverged`, `SingularMatrix`, `TemperatureOutOfRange` (hp/sp iterate left
`[100 K, 20000 K]`). On any status but `Ok`, `Moles` and `State` hold the last iterate
and must not be used as a result.

## Side effects

None. Writes only into the views of `EquilibriumResult` and `EquilibriumScratch`.

## Out of scope

- Throat, area ratios, performance figures: `Performance`.
- Transport properties: `Transport`.
- Building tables, choosing species, computing `ElementMoles` from reactants: `Problems`.
- Allocating and slicing the batch buffers: `Execution`.
