# API.md — Equilibrium

Namespace `APThermo.Equilibrium`. The node exposes one
kernel-compatible solver for the equilibrium composition of one case, plus the
descriptors of its inputs, scratch and outputs. Everything not listed here, in a
package-surface section (one whose heading carries no `(tree contract)` mark), is
internal and may change without notice (root `BOOT.md`, Delivery: Public surface). The
tree-contract section below lists the internal types `Performance`, `Transport` and
`Execution` use (root `BOOT.md`, Delivery: Tree contracts); the assembly grants
`InternalsVisibleTo` to exactly those nodes and to `Execution.Tests`,
`Performance.Tests` and, since 2026-09-28 (the orchestrator's decision under
`AGENTS.md` §11, so that its tests node can cross-check the transport set's
`EquilibriumHeatCapacity` against an independently solved state's `CpEquilibrium`),
`Transport.Tests` (`APThermo.Equilibrium.csproj`).

⚠ 2026-09-15 (distribution phase): the review of that day
(fixed in `4344652`) found no consumer scenario for `EquilibriumProblem`,
`EquilibriumScratch`, `EquilibriumResult`, `EquilibriumSolver`, `ScratchLayout` or
`DenseSolver`: every use is a neighbour numerical node composing the kernel layer, or
this node's own tests. They moved from the package surface into the tree contract
below; only `ProblemKind` stays public, because a consumer builds it into the
`EquilibriumProblem` of `Problems` (a distinct, same-named type: the CS0104 clash the
review found is a defect of every option except this one, section 4, D3 — a consumer
never sees this node's own `EquilibriumProblem` at all now that it is internal). The
correction also fixes a wrong claim: `DenseSolver`'s own ⚠ below said it "became
public" for `Transport` on 2026-09-12; `InternalsVisibleTo` does the same job without
widening the package surface, so it is internal again.

## Problem kind ✅

```csharp
namespace APThermo.Equilibrium;

public enum ProblemKind { AssignedTemperaturePressure, AssignedEnthalpyPressure, AssignedEntropyPressure }
```

## Solver (tree contract) ✅

```csharp
internal readonly struct EquilibriumProblem              // one case
{
    public readonly ProblemKind Kind;
    public readonly double Pressure;                   // Pa
    public readonly double Temperature;                // K for tp; initial estimate for hp/sp (0 = the default of 3800 K)
    public readonly double Target;                     // h in J/kg for hp, s in J/(kg·K) for sp, unused for tp
    public readonly ArrayView<double> ElementMoles;    // [element], kmol per kg of mixture (b_i); zero marks an absent element
    public EquilibriumProblem(ProblemKind kind, double pressure, double temperature, double target, ArrayView<double> elementMoles);
}

internal readonly struct EquilibriumScratch              // slices of batch-sized buffers, sized by ScratchLayout
{
    public readonly ArrayView<double> HOverRT;         // [species], the species functions at the current temperature
    public readonly ArrayView<double> SOverR;          // [species]
    public readonly ArrayView<double> CpOverR;         // [species]
    public readonly ArrayView<double> GOverRT;         // [species]
    public readonly ArrayView<double> LogMoles;        // [species], ln n_j of the gaseous species
    public readonly ArrayView<double> Corrections;     // [species], Δln n_j of the gaseous species
    public readonly TieSlices Tie;                     // rule A's release snapshot (the third pass of 2026-09-28, finding F1), grouped 2026-10-03
    public readonly ArrayView<double> Matrix;          // [MaxUnknowns * MaxUnknowns], row-major
    public readonly ArrayView<double> RightHandSide;   // [MaxUnknowns]; the solution after a solve
    public readonly ArrayView<double> RowScale;        // [MaxUnknowns]
    public readonly ArrayView<int> SpeciesActive;      // [species], the species mark: 0 Absent (an element missing), 1 Active, 2 ForgivenOnce, 3 StoodDown (BOOT.md, the condensed-species rule)
    public readonly ArrayView<int> ElementActive;      // [element], 1 when the abundance is positive
    public readonly ArrayView<int> CondensedInSolution;// [MaxCondensedInSolution], species indices, −1 beyond the count
    public readonly ArrayView<double> BracketEnds;     // [2 · species], the temperature bracket's lower then upper end, after RowScale in the double slice so that no earlier slice moves
    public EquilibriumScratch(ArrayView<double> hOverRT, ArrayView<double> sOverR, ArrayView<double> cpOverR, ArrayView<double> gOverRT,
                              ArrayView<double> logMoles, ArrayView<double> corrections, TieSlices tie,
                              ArrayView<double> matrix, ArrayView<double> rightHandSide, ArrayView<double> rowScale,
                              ArrayView<int> speciesActive, ArrayView<int> elementActive, ArrayView<int> condensedInSolution,
                              ArrayView<double> bracketEnds);
    public static EquilibriumScratch Slice(ArrayView<double> doubles, ArrayView<int> ints, int speciesCount, int elementCount);
        // cuts one case's scratch from views of at least DoublesPerCase and IntsPerCase elements
}

internal readonly struct TieSlices                       // rule A's release snapshot, grouped 2026-10-03 (Newton/BOOT.md, "Release")
{
    public readonly ArrayView<double> LogMoles;        // [species], the release snapshot of LogMoles
    public readonly ArrayView<double> CondensedMoles;  // [MaxCondensedInSolution], the snapshot's condensed moles
    public readonly ArrayView<int> CondensedSet;       // [MaxCondensedInSolution], the snapshot's condensed set
    public readonly TieElementSlices Elements;         // the multipliers snapshot, the live coefficients, their snapshot
    public TieSlices(ArrayView<double> logMoles, ArrayView<double> condensedMoles, ArrayView<int> condensedSet, TieElementSlices elements);
}

internal readonly struct TieElementSlices                // rule A's per-element slices, grouped so that EquilibriumScratch's constructor keeps its parameter count
{
    public readonly ArrayView<double> Multipliers;     // [element], the release snapshot of the Lagrange multipliers
    public readonly ArrayView<double> Coefficients;    // [element], c_i of the live tie's combination; zero for the tied element
    public readonly ArrayView<double> CoefficientSnapshot; // [element], the release's snapshot of Coefficients
    public TieElementSlices(ArrayView<double> multipliers, ArrayView<double> coefficients, ArrayView<double> coefficientSnapshot);
}

internal readonly struct EquilibriumResult               // views the solver writes into
{
    public readonly ArrayView<double> Moles;           // [species], kmol per kg; zero for absent, trace and unincluded condensed species
    public readonly ArrayView<double> Multipliers;     // [element], Lagrange multipliers π_i (dimensionless); zero for an absent element
    public readonly ArrayView<MixtureState> State;     // [1]
    public readonly ArrayView<int> Status;             // [1], CaseStatus
    public readonly ArrayView<int> Iterations;         // [1], Newton steps taken
    public EquilibriumResult(ArrayView<double> moles, ArrayView<double> multipliers, ArrayView<MixtureState> state,
                             ArrayView<int> status, ArrayView<int> iterations);
}

internal static class EquilibriumSolver                  // kernel-compatible
{
    public const double TraceThreshold = 18.420681;    // −ln(1e-8): the retention threshold's first stage, until the case's first convergence
    public const double SecondStageTraceThreshold = 25.328436; // −ln(1e-11): the retention threshold's second stage, for the rest of the solve
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

internal static class ScratchLayout
{
    public const int MaxCondensedInSolution = 20;                              // = TableLimits.MaxElements (2026-09-26; was 8)
    public static int MaxUnknowns(int elementCount);                          // elementCount + MaxCondensedInSolution + 2
    public static int DoublesPerCase(int speciesCount, int elementCount);     // 9 · species + MaxCondensedInSolution + 3 · elements + MaxUnknowns² + 2 · MaxUnknowns
    public static int IntsPerCase(int speciesCount, int elementCount);        // species + elements + 2 · MaxCondensedInSolution
}

internal static class DenseSolver                        // kernel-compatible; shared with Transport
{
    public static bool Solve(ArrayView<double> matrix, ArrayView<double> rhs, ArrayView<double> rowScale, int n, int stride);
        // Gaussian elimination with scaled partial pivoting, in place, on the row-major n×n system held with the given stride;
        // the solution replaces rhs; false when a pivot falls below 1e-13 of its row's largest initial entry
    public static bool Solve(ArrayView<double> matrix, ArrayView<double> rhs, ArrayView<double> rowScale, int n, int stride, out int failedRow);
        // as above, and also names the row whose pivot could not be found (−1 when the matrix is not singular);
        // this node's own targeted singular remedy (2026-09-28); Transport keeps calling the overload above
}
```

⚠ 2026-09-12: `DenseSolver` was internal. The Transport node solves the two linear
systems of its reaction terms and, by the root's first invariant, may not carry a
second elimination; the solver became public and part of this contract.

⚠ 2026-09-28 (the third pass, finding F1): the scratch grew by a seventh per-species
double array and two more `MaxCondensedInSolution`-sized double arrays, plus a second
`MaxCondensedInSolution`-sized int array: rule A's release now keeps the tied
converged iterate (the gaseous logarithms, the condensed set with its mole numbers,
and the Lagrange multipliers) in these snapshot slices, so that a re-convergence
which fails on the element's own row can be undone rather than reported as the
release's own failure ([Newton/BOOT.md](Newton/BOOT.md), "Release"). `n`, `T`, the condensed count and the tie
itself travel as the caller's own locals across the one Newton call the release
makes and need no scratch of their own. `DoublesPerCase` and `IntsPerCase` are
functions, not stored constants, so a caller that sizes its buffers by calling them,
as this node's own tests do, picks up the new size without a change of its own; this
task's evidence is every consuming node's own test suite green after the change
(`Performance.Tests`, `Transport.Tests`, `Problems.Tests`, `Cli.Tests`,
`Execution.Tests`), none of them touched.

⚠ 2026-10-03: was one `MaxCondensedInSolution`-sized slice, `TieMultipliers`, for the release's
snapshot of the multipliers, now the group `TieElements` of three slices of the element count each: that
snapshot, the live coefficients of rule A's linear combination and their snapshot
([Newton/BOOT.md](Newton/BOOT.md), "The tie is per-case state"). `ElementTie` lost its `Partner` and
`Ratio`: the coefficients are the combination's, one per element. The constructor keeps its 16
parameters, the doubles per case change from `2 · MaxCondensedInSolution` to
`MaxCondensedInSolution + 3 · elements`, and a caller that sizes its buffers by `DoublesPerCase` needs no change.

⚠ 2026-10-03: was 16 constructor parameters and seven doubles per species, now 14 and nine: rule A's
snapshot is `Tie` (`TieSlices`), the bracket's ends `BracketEnds`, appended after `RowScale` so that no
earlier slice moves; a caller that sizes by `DoublesPerCase` needs no change.

Units: SI throughout; mole numbers in kmol per kilogram of mixture, so that
`Σ n_j M_j = 1` over the whole mixture. `Multipliers` are the dimensionless `π_i` of
RP-1311. The state written for `Ok`: `MolarMass` is `1/n` over the gaseous moles,
`MixtureMolarMass` one kilogram over the moles of all species (the reference's `MW`);
`CpEquilibrium`, `CvEquilibrium`, `DlnVdlnT`, `DlnVdlnP`, `GammaS` and `SoundSpeed`
carry the equilibrium derivatives of RP-1311 section 2.5. `SolveFrozen` writes the
frozen state: `CpEquilibrium = CpFrozen`, `CvEquilibrium = CvFrozen`, `DlnVdlnT = 1`,
`DlnVdlnP = −1`, `GammaS = Cp/Cv`. `State.Velocity` and `State.Mach` are left at zero
by this node. A case retains a gaseous species down to `1e-8` of the gas until its
first convergence, then down to `1e-11` for the rest of the solve, as a change of the
retained set that needs one more convergence (2026-09-28, `BOOT.md`, the two-stage
retention threshold): since that switch happens before any `Ok` exit, the report
stands for the last `Composition.Refresh` under the second-stage threshold, and a
gaseous species between `1e-11` and `1e-8` of the gas is reported at its converged
amount rather than zeroed. Once the loop holds the retained set (the Newton node's flip
rule of 2026-10-03, [Newton/BOOT.md](Newton/BOOT.md)), a gas once retained stays retained
whatever its amount, so a species below `1e-11` of the gas may be reported too, at its
converged amount.

⚠ 2026-09-28: this paragraph stood "a gaseous species below `1e-8` of the gas is
reported with zero moles", describing a separate zeroing step taken on the final state.
Implemented, that step zeroed the trace species out of the reported moles only, while
the sums, derivatives and mixture state already written stood on the finer, unzeroed
composition: the two disagreed, and `ElementConservationTests` failed on 27 fixtures on
residuals matching exactly the zeroed species' own mass, although no case's `CaseStatus`
moved. The report now stands for whatever `Composition.Refresh` last produced, with no
separate step (`BOOT.md`, the ⚠ 2026-09-28 correction of the retention-threshold
paragraph). A warm start (a previous solution as the
estimate) re-seeds every gas with zero moles one e-fold below the trace threshold, and
does not read the logarithm left in the scratch. A warm start whose convergence fails,
in any way but `InvalidInput`, retries once from the cold start of RP-1311 section 3.1
and reports the combined iteration count; the cold retry starts hp and sp at 3800 K
regardless of `problem.Temperature`, since a positive estimate there is part of the
seed the retry is discarding, not a fresh guess; a cold start never retries (2026-09-27,
widened 2026-09-28, `BOOT.md`, the warm-start fallback).

⚠ 2026-09-28: the fallback fired only when a seeded condensed species was negative at
the moment of failure. A warm start also fails with the seed diverging positive, or
going negative and recovering before any remedy or the step cap sees it; the sign test
missed both. Every failure now retries (the second hidden-defect audit's finding F3).

⚠ 2026-09-26: the sentence ended "its logarithm stays in the scratch for the next
estimate". The logarithm stays, but no estimate reads it (the hidden-defect audit,
finding 3; [Newton/BOOT.md](Newton/BOOT.md), the loop's bookkeeping).

At a pinned state — condensed species in the solution whose element vectors are linearly
dependent: two records of one formula at their transition, or different species on a reaction
plateau (Al(OH)3, Al2O3 and H2O(L)), hp and sp problems only
([Condensed/BOOT.md](Condensed/BOOT.md), the condensed-species rule; [StateRecord/BOOT.md](StateRecord/BOOT.md),
the pinned set) — `CpEquilibrium`, `CvEquilibrium` and `DlnVdlnT` are written as zero, the reference's
convention for derivatives that do not exist on a plateau (decided 2026-09-13), while
`DlnVdlnP`, `GammaS = −1/DlnVdlnP` and `SoundSpeed` carry the real plateau values and
the mole numbers of every species of the set are reported.

⚠ 2026-10-03: was a pinned **pair** (two records of one formula), now a pinned **set** found by
linear dependence. Consumers now see `CpEquilibrium = 0` on a reaction plateau too, where the
state was a failure before (an hp or sp state inside the Al(OH)3/Al2O3/H2O(L) band ended
`TemperatureOutOfRange`); the plateau reference of the Fixtures node's `seeded` kind carries the
reference's frozen second-order values there, which a comparison skips
([StateRecord/BOOT.md](StateRecord/BOOT.md), the ⚠ of that date).

⚠ 2026-10-03: a state is pinned also on a **gas-participating plateau**, where a condensed vector lies
in the span of the gas composition and of the condensed vectors before it (CaCO3 and CaO under CO2, a
two-phase region of nearly pure water, NH4Cl, Ca(OH)2, MgCO3) and the plateau temperature depends on
the pressure. Such a state was a failure before (`TemperatureOutOfRange` or `NotConverged`); it now
carries the same zeros, but `DlnVdlnP` is `(∂ln V/∂ln p)_s`, the derivative along the isentrope, not the
isothermal one, with `GammaS = −1/DlnVdlnP` and `SoundSpeed` from it, and `CpEquilibrium` is zero
because `(∂ln V/∂ln T)_p` does not exist there. On a condensed-only plateau the two derivatives
coincide. `GammaS` may fall below 1 (0.886 for Ca(OH)2 at 1 MPa), as for wet steam. Beside it, an `Ok`
state whose `CpEquilibrium/CvEquilibrium` exceeds 1e6 in magnitude, a composition near a univariant
one, takes `GammaS` from the same isentropic system, and `CvEquilibrium` follows from
`−CpEquilibrium/(GammaS·DlnVdlnP)`; the other fields keep their values
([StateRecord/BOOT.md](StateRecord/BOOT.md), "The gas-participating plateau" and "The near-univariant
sliver").

⚠ 2026-09-14: `SpeciesActive` was documented as "1 when every element of the species
is present", a two-valued mask, while the plateau rules of 2026-09-13 had made it
carry a third value (2, a condensed record removed for its range once) and had
written 0 for a record that stood down, so that an absent element and a stood-down
record were indistinguishable and the honesty guard re-derived element presence by
hand. Found by the clean-code review of 2026-09-14. The slot now carries the four
named values above (`SpeciesMark` in the node's `BOOT.md`, `## Structure`); the
layout of the scratch is unchanged, and a caller that slices the scratch writes
nothing into it.

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
non-positive pressure, a tp temperature that is non-positive or not finite, a
*given* (nonzero) hp or sp temperature estimate that is not finite and positive —
0 stays the sentinel for "none given", defaulting to 3800 K, the third pass of
2026-09-28, observation O1 — more elements than `TableLimits.MaxElements`; for
`SolveFrozen` also a composition without gaseous moles, any mole number negative or
not finite (2026-09-26), or an initial temperature estimate that is non-positive or
not finite (2026-09-28)),
`NotConverged` (the report's tests not met within `MaxNewtonSteps` after the last
change of the condensed set, more than `MaxCondensedSetChanges` changes, the element
conservation invariant violated at the end, or, since 2026-09-26, a condensed candidate
left out of an otherwise converged state by more than 1e-9 per mole: `BOOT.md`, the exit
guard), `SingularMatrix` (only once none of the remedies resolves the singular system:
rule B's dependent-set ratio test, rule A's element tie, the two gaseous resets of RP-1311
section 3.6, then the targeted removal of the species of the row whose pivot failed since
2026-09-28, [Newton/BOOT.md](Newton/BOOT.md), "Two rules come before the remedies above" and the targeted
singular remedy), `TemperatureOutOfRange` (hp/sp iterate left `[100 K, 20000 K]`; since
2026-09-28 also a converged state, tp included, outside the mixture window
`[160 K, 22000 K]`, a `SolveFrozen` temperature below 0.8 times the lowest lower bound
of the fits of the gases present or above `22000 K`, or a converged state whose frozen
or equilibrium heat capacity, `γs` or sound speed is not finite and positive — the
state guard, exempting a pinned set's zero `CpEquilibrium`/`CvEquilibrium` convention:
`BOOT.md`, the mixture window, and [StateRecord/BOOT.md](StateRecord/BOOT.md), the state guard).
On any status but `Ok`, `Moles` hold the last iterate and `State` is not written; on
`InvalidInput` nothing but `Status` and `Iterations` (zero) is written.

⚠ 2026-09-28: the `SingularMatrix` clause named only the targeted removal of 2026-09-28,
as if it followed straight after the two gaseous resets. Rules A and B (BOOT.md, "Two
rules come before the remedies above", 2026-09-28, the orchestrator's investigation 6)
run first: a dependent condensed set (rule B) or a duplicated element row (rule A) is
resolved before any of the report's own remedies are tried, so `SingularMatrix` is now
reported only once neither rule, nor the resets, nor the targeted removal, finds a way
forward.

## Side effects

None. Writes only into the views of `EquilibriumResult` and `EquilibriumScratch`.

## Out of scope

- Throat, area ratios, performance figures: `Performance`.
- Transport properties: `Transport`.
- Building tables, choosing species, computing `ElementMoles` from reactants: `Problems`.
- Allocating and slicing the batch buffers: `Execution`.
