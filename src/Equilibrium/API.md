# API.md — Equilibrium

Namespace `AerospacePropellantThermodynamics.Equilibrium`. The node exposes one
kernel-compatible solver for the equilibrium composition of one case, plus the
descriptors of its inputs, scratch and outputs. Everything not listed here is internal
and may change.

## Solver ✅

```csharp
namespace AerospacePropellantThermodynamics.Equilibrium;

public enum ProblemKind { AssignedTemperaturePressure, AssignedEnthalpyPressure, AssignedEntropyPressure }

public readonly struct EquilibriumProblem              // one case
{
    public readonly ProblemKind Kind;
    public readonly double Pressure;                   // Pa
    public readonly double Temperature;                // K for tp; initial estimate for hp/sp (0 = the default of 3800 K)
    public readonly double Target;                     // h in J/kg for hp, s in J/(kg·K) for sp, unused for tp
    public readonly ArrayView<double> ElementMoles;    // [element], kmol per kg of mixture (b_i); zero marks an absent element
    public EquilibriumProblem(ProblemKind kind, double pressure, double temperature, double target, ArrayView<double> elementMoles);
}

public readonly struct EquilibriumScratch              // slices of batch-sized buffers, sized by ScratchLayout
{
    public readonly ArrayView<double> HOverRT;         // [species], the species functions at the current temperature
    public readonly ArrayView<double> SOverR;          // [species]
    public readonly ArrayView<double> CpOverR;         // [species]
    public readonly ArrayView<double> GOverRT;         // [species]
    public readonly ArrayView<double> LogMoles;        // [species], ln n_j of the gaseous species
    public readonly ArrayView<double> Corrections;     // [species], Δln n_j of the gaseous species
    public readonly ArrayView<double> Matrix;          // [MaxUnknowns * MaxUnknowns], row-major
    public readonly ArrayView<double> RightHandSide;   // [MaxUnknowns]; the solution after a solve
    public readonly ArrayView<double> RowScale;        // [MaxUnknowns]
    public readonly ArrayView<int> SpeciesActive;      // [species], 1 when every element of the species is present
    public readonly ArrayView<int> ElementActive;      // [element], 1 when the abundance is positive
    public readonly ArrayView<int> CondensedInSolution;// [MaxCondensedInSolution], species indices, −1 beyond the count
    public EquilibriumScratch(ArrayView<double> hOverRT, ArrayView<double> sOverR, ArrayView<double> cpOverR, ArrayView<double> gOverRT,
                              ArrayView<double> logMoles, ArrayView<double> corrections,
                              ArrayView<double> matrix, ArrayView<double> rightHandSide, ArrayView<double> rowScale,
                              ArrayView<int> speciesActive, ArrayView<int> elementActive, ArrayView<int> condensedInSolution);
    public static EquilibriumScratch Slice(ArrayView<double> doubles, ArrayView<int> ints, int speciesCount, int elementCount);
        // cuts one case's scratch from views of at least DoublesPerCase and IntsPerCase elements
}

public readonly struct EquilibriumResult               // views the solver writes into
{
    public readonly ArrayView<double> Moles;           // [species], kmol per kg; zero for absent, trace and unincluded condensed species
    public readonly ArrayView<double> Multipliers;     // [element], Lagrange multipliers π_i (dimensionless); zero for an absent element
    public readonly ArrayView<MixtureState> State;     // [1]
    public readonly ArrayView<int> Status;             // [1], CaseStatus
    public readonly ArrayView<int> Iterations;         // [1], Newton steps taken
    public EquilibriumResult(ArrayView<double> moles, ArrayView<double> multipliers, ArrayView<MixtureState> state,
                             ArrayView<int> status, ArrayView<int> iterations);
}

public static class EquilibriumSolver                  // kernel-compatible
{
    public const double TraceThreshold = 18.420681;    // −ln(1e-8): below this mole fraction a gaseous species is reported as zero
    public const int MaxNewtonSteps = 50;              // after the last change of the condensed set
    public const int MaxCondensedSetChanges = 3 * ScratchLayout.MaxCondensedInSolution;   // include, forgive and stand down every slot
    public static void Solve(in SpeciesTableView table, in EquilibriumProblem problem,
                             in EquilibriumScratch scratch, in EquilibriumResult result,
                             bool useMolesAsEstimate);
        // useMolesAsEstimate: result.Moles (and problem.Temperature for hp/sp) are the initial estimate
    public static void SolveFrozen(in SpeciesTableView table, in EquilibriumProblem problem,
                                   in EquilibriumScratch scratch, in EquilibriumResult result);
        // composition fixed to result.Moles; solves for the temperature (hp, sp) or evaluates at it (tp)
}

public static class ScratchLayout
{
    public const int MaxCondensedInSolution = 8;
    public static int MaxUnknowns(int elementCount);                          // elementCount + MaxCondensedInSolution + 2
    public static int DoublesPerCase(int speciesCount, int elementCount);     // 6 · species + MaxUnknowns² + 2 · MaxUnknowns
    public static int IntsPerCase(int speciesCount, int elementCount);        // species + elements + MaxCondensedInSolution
}

public static class DenseSolver                        // kernel-compatible; shared with Transport
{
    public static bool Solve(ArrayView<double> matrix, ArrayView<double> rhs, ArrayView<double> rowScale, int n, int stride);
        // Gaussian elimination with scaled partial pivoting, in place, on the row-major n×n system held with the given stride;
        // the solution replaces rhs; false when a pivot falls below 1e-13 of its row's largest initial entry
}
```

⚠ 2026-09-12: `DenseSolver` was internal. The Transport node solves the two linear
systems of its reaction terms and, by the root's first invariant, may not carry a
second elimination; the solver became public and part of this contract.

Units: SI throughout; mole numbers in kmol per kilogram of mixture, so that
`Σ n_j M_j = 1` over the whole mixture. `Multipliers` are the dimensionless `π_i` of
RP-1311. The state written for `Ok`: `MolarMass` is `1/n` over the gaseous moles,
`MixtureMolarMass` one kilogram over the moles of all species (the reference's `MW`);
`CpEquilibrium`, `CvEquilibrium`, `DlnVdlnT`, `DlnVdlnP`, `GammaS` and `SoundSpeed`
carry the equilibrium derivatives of RP-1311 section 2.5. `SolveFrozen` writes the
frozen state: `CpEquilibrium = CpFrozen`, `CvEquilibrium = CvFrozen`, `DlnVdlnT = 1`,
`DlnVdlnP = −1`, `GammaS = Cp/Cv`. `State.Velocity` and `State.Mach` are left at zero
by this node. A gaseous species whose mole fraction fell below `1e-8` at convergence
is reported with zero moles, as the reference prints it; its logarithm stays in the
scratch for the next estimate.

At a pinned two-phase state — two records of one formula in the solution at their
transition, hp and sp problems only (`BOOT.md`, the condensed-species rule) —
`CpEquilibrium`, `CvEquilibrium` and `DlnVdlnT` are written as zero, the reference's
convention for derivatives that do not exist on a plateau (decided 2026-09-13), while
`DlnVdlnP`, `GammaS = −1/DlnVdlnP` and `SoundSpeed` carry the real plateau values and
both records' mole numbers are reported.

⚠ 2026-09-12: the sketch had `EquilibriumScratch { GOverRT, LogMoles, Matrix,
RightHandSide, Pivots, CondensedInSolution }` and `IntsPerCase(int elementCount)`.
The implementation keeps all four species functions and the corrections per species
(the temperature changes every hp/sp step, and the λ test needs every correction at
once), scales rows instead of recording pivots, and masks species and elements per
case, so the ints per case depend on the species count too. `Slice` was added so that
callers cut a case's scratch the same way everywhere.

## Errors

An element with zero abundance is allowed: the species containing it are inactive
for the case and get mole number zero. The solver never throws. `Status` is one of
`Ok`, `InvalidInput` (every abundance zero, negative abundance, empty table,
non-positive pressure, non-positive temperature for tp, more elements than
`TableLimits.MaxElements`; for `SolveFrozen` also a composition without gaseous moles),
`NotConverged` (the report's tests not met within `MaxNewtonSteps` after the last
change of the condensed set, more than `MaxCondensedSetChanges` changes, or the element
conservation invariant violated at the end), `SingularMatrix` (after the remedies of
RP-1311 section 3.6), `TemperatureOutOfRange` (hp/sp iterate left `[100 K, 20000 K]`).
On any status but `Ok`, `Moles` hold the last iterate and `State` is not written; on
`InvalidInput` nothing but `Status` and `Iterations` (zero) is written.

## Side effects

None. Writes only into the views of `EquilibriumResult` and `EquilibriumScratch`.

## Out of scope

- Throat, area ratios, performance figures: `Performance`.
- Transport properties: `Transport`.
- Building tables, choosing species, computing `ElementMoles` from reactants: `Problems`.
- Allocating and slicing the batch buffers: `Execution`.
