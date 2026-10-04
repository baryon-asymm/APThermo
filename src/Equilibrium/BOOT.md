# BOOT.md — Equilibrium

## Purpose

The equilibrium composition of one case and its thermodynamic derivatives: given the
element abundances of one kilogram of mixture, a pressure, and one of temperature,
enthalpy or entropy (the tp, hp and sp problems), find the mole numbers of every
candidate species, gaseous and condensed, that minimize the Gibbs energy under
element conservation, then the equilibrium and frozen properties of the mixture. It is
the reusable core: `Performance` calls it at every nozzle station, and it is verified
on its own against the reference implementation.

⚠ Declared deviation (`AGENTS.md` §6, §12): the algorithm is the one of NASA RP-1311
Part I (Gordon and McBride, 1994), chapter 2 (equations of the minimization and of the
iteration), chapter 3 (convergence, control factors, condensed species, trace species)
and sections 2.5 (the derivatives from the matrix solutions) and 2.6 (the other
derivatives). This document fixes every choice the report leaves open
and every limit the implementation needs; it does not restate the report. Whoever
codes this node reads the report's chapters named here. What would lift the deviation:
a full restatement of the equations in this document, which nobody has asked for.

⚠ 2026-09-15: was "section 2.6" for the derivatives, now 2.5, 2.6 → HISTORY.md#sec-2-5

## Invariants

- **One method.** The composition is the minimum of the Gibbs energy under element
  conservation, found by the Newton–Raphson iteration of RP-1311 on the reduced
  system (Lagrange multipliers per element, total moles, temperature for hp/sp, and
  the mole numbers of the condensed species in the solution). No reaction sets, no
  equilibrium constants.
- **Element conservation at convergence.** For every element, `|Σ a_ij n_j − b_i| ≤
  1e-13 · b_i` in kmol per kilogram; a converged case that violates it is
  reported as `NotConverged`, never as `Ok`. A `NoGasPhase` state is held to the gasless
  verdict's own absolute 1e-12 ([GasPhase/BOOT.md](GasPhase/BOOT.md)).

  ⚠ 2026-10-04: was ≤ 1e-12 · max(1, b_i), now ≤ 1e-13 · b_i → HISTORY.md#relative-invariant
- **Gas-level stationarity at convergence** (2026-10-04). Every gas an `Ok` reports sits on its
  stationarity within 1e-9; the close refuses any other state as `NotConverged` ([TraceGas](TraceGas/BOOT.md)).
  An hp or sp state at a data junction (an interval bound where the fits of two ranges disagree) is the tp
  state at the bound or at the next double, the one nearer the target (TraceGas, "The data junction").
- **The candidate list never changes.** Every species of the table is a candidate
  throughout; in the Newton iteration gaseous species stay positive because the unknowns
  are their logarithms, and a `NoGasPhase` result reports them zero
  ([GasPhase/BOOT.md](GasPhase/BOOT.md)); condensed species enter and leave the solution by
  the condensed-species rule of the Constraints (the report's tests, completed on
  2026-09-13); a species is never deleted from the table by this node.

  ⚠ 2026-10-03: was every gas positive in every result, now zero in `NoGasPhase` → HISTORY.md#gas-positive-2026-10-03
- **An absent element is a mask, not an error.** A case whose abundance of an element
  is zero runs with every species containing that element inactive (mole number zero,
  no row or column in the iteration) and that element's equation dropped; the active
  set is decided once per case from the abundances, before the first iteration. A case
  with no active gaseous species is `InvalidInput`.

  ⚠ 2026-09-12: was `InvalidInput` at zero, now a mask → HISTORY.md#absent-element
- **Deterministic and stateless.** All inputs and all scratch are explicit parameters;
  the same inputs give the same bits on the same accelerator.
- **Failures are values.** Every exit path sets a `CaseStatus`; the state record is
  fully written only for `Ok`. `NoGasPhase` writes the condensed moles, the certificate's
  multipliers and a state of the temperature and the pressure only; a state a probe of
  the bracket wrote is cleared ([Recovery/BOOT.md](Recovery/BOOT.md)).
- **Bounded work.** The iteration count is capped (see Constraints); a case that does
  not converge within the cap returns `NotConverged` with the last iterate. The attempts
  after a failure are bounded too ([Recovery/BOOT.md](Recovery/BOOT.md)).

## Dependencies

- [Thermo](../Thermo/API.md) — the species table view, the species functions, the
  gas constant, `MixtureState` and `CaseStatus`.

Outside the tree: ILGPU 1.5.3 (`ArrayView<T>`); NASA RP-1311 Part I as the algorithm's
source.

## Constraints

Inherited from the root ([BOOT.md](../../BOOT.md)). In addition:

- Kernel-compatible C#: the per-case solve is a static method over views; the per-case
  scratch is passed in as views sliced from batch-sized buffers by the caller
  (`EquilibriumScratch.Slice`, `ScratchLayout`). → HISTORY.md#warm-evidence
- Unknowns of the reduced system: elements + condensed species in the solution + 1
  (total moles) + 1 (temperature, hp and sp only). Limits: at most 20 elements and at
  most 20 condensed species in the solution at once
  (`ScratchLayout.MaxCondensedInSolution` equals `TableLimits.MaxElements`, 2026-09-26),
  hence a matrix of at most 42 × 42. The second limit is never the binding one. By the
  phase rule at an assigned or a pinned temperature and pressure, a state holds at most
  one condensed phase per element: fewer beside a gas phase, and one more only on a
  pinned plateau, where the temperature row goes.
  The dense solve is Gaussian elimination with scaled partial pivoting, a tree-contract
  type of this node (`API.md`) that `Transport` calls too.

  ⚠ 2026-09-26: was at most 8 condensed species in the solution (30 × 30), now at most 20 (42 × 42) → HISTORY.md#condensed-limit
  ⚠ 2026-10-02: was `DenseSolver` visible to its tests node only, now `Transport`'s too → HISTORY.md#ds-tree-contract-2026-10-02

- The Newton loop: the reduced equations (RP-1311 tables 2.1 and 2.2, the sp row, `p°`), the damping
  and the convergence tests of chapter 3, the polish, the loop's bookkeeping, the singular-matrix
  remedies with rules B and A and the tie: [Newton/BOOT.md](Newton/BOOT.md), `## Constraints`. The
  failed-row overload of `DenseSolver.Solve` is in [API.md](API.md).

- Initial estimates as in the report: every gaseous species at `0.1 / (active gaseous
  species)` kmol per kg with `n = 0.1` when no estimate is given, `T = 3800 K` for hp
  and sp when no estimate is given; callers may pass a previous solution as the
  estimate (the nozzle does). A temperature estimate that is given for hp or sp and is
  not finite and positive is `InvalidInput`, as for the frozen solve (2026-09-28)
  → HISTORY.md#warm-evidence

  What follows a failed attempt — the cold fallback of a warm start, the gasless verdict
  of a tp attempt, the temperature bracket of an hp or sp case — is the `Recovery` child
  node's ([Recovery/BOOT.md](Recovery/BOOT.md)), the verdict the `GasPhase` node's
  ([GasPhase/BOOT.md](GasPhase/BOOT.md)).
  ⚠ 2026-10-03: was the fallback stated here, now in `Recovery` → HISTORY.md#recovery-split-2026-10-03

- The retention threshold has two stages, as the reference's `tsize`/`xsize` (2026-09-28):
  `ln(n_j/n) = −18.420681` (`n_j/n = 1e-8`, the report's) until the first convergence of
  the case, then `ln(n_j/n) = −25.328436` (`1e-11`) for the rest of the solve. Below the
  threshold a gaseous species is held at zero in the sums and keeps its logarithm. The
  switch recomputes the retained amounts and counts as a change of the retained set: the
  loop must converge once more under the second stage before it may exit, so every `Ok`
  has been converged under 1e-11. It happens once per solve, a warm start included. The
  report stands for the last `Composition.Refresh`, the second-stage one, since no `Ok`
  exit precedes the switch: a gaseous species between 1e-11 and 1e-8 of the gas is reported
  at its converged amount, not zeroed. `Composition` stays the one place the retention rule
  is applied, and the stage is per-case state (`IterationState.RetentionSecondStage`, not
  `NewtonLoopState`: the flag must survive the several `Converge` calls of one `Solve`
  attempt, and `NewtonLoopState` is rebuilt at each of them). Once the retained set is
  held (`IterationState.RetainedSetHeld`, the Newton node's flip rule of 2026-10-03), a gas
  once retained stays retained whatever its amount: holding only adds species to the
  minimized set, and the threshold still governs every species never retained.
  → HISTORY.md#retention-condensed-2026-10-02

  ⚠ 2026-09-28: was the report zeroing species below 1e-8 in a separate step, now the report stands for the last `Composition.Refresh` → HISTORY.md#report-zeroing

  Iteration cap: 50 Newton steps after the last change of the
  condensed species set, and at most `MaxCondensedSetChanges` changes of that set per
  case: three per slot of the condensed set, an inclusion, a forgiveness and a
  stand-down for each of the `ScratchLayout.MaxCondensedInSolution` slots (24 today;
  the constant is the number, this document only names it).
  → HISTORY.md#constraints-newton-split-2026-10-02

  ⚠ 2026-09-28: was one retention threshold, now two stages → HISTORY.md#two-stage
  ⚠ 2026-09-14: was 10 changes, now `MaxCondensedSetChanges` → HISTORY.md#set-changes
- Condensed species: one change per convergence, the open-below rule, the effective range and the
  exit guard: [Condensed/BOOT.md](Condensed/BOOT.md), `## Constraints`.

  The mixture's temperature window (2026-09-28): an `Ok` of any kind, tp included, is
  valid only when the final temperature lies in [160 K, 22 000 K], the reference's
  `T_min` and `T_max` of the solver, checked after convergence. Outside it the status is
  `TemperatureOutOfRange`. The iterate window of hp and sp, [100 K, 20 000 K], is
  unchanged. The window bounds the open-below rule: ice is a candidate below 200 K, and a
  state holding it is valid down to 160 K. → HISTORY.md#cea-refs-2026-10-02

  ⚠ 2026-09-28: was ice at any temperature, now the window → HISTORY.md#ice-window

- Frozen mode: with the composition fixed, solve for the temperature that gives the
  requested enthalpy or entropy (Newton on `T`, to `1e-10` relative) and compute the
  frozen properties; this mode serves frozen nozzle flow. The composition is valid when
  every mole number, gaseous and condensed, is finite and not negative and the gaseous
  ones sum to more than zero; otherwise the case is `InvalidInput` (2026-09-26).
  The temperature is valid when it is finite and positive: for tp the assigned one, for
  hp and sp the estimate, which is replaced as in `Solve` when it is not
  (2026-09-28); otherwise the case is `InvalidInput`. An `Ok` frozen state has a
  temperature not below 0.8 times the lowest lower bound of the fits of the gases
  present, the reference's stop of a frozen expansion (160 K with the committed data). Below
  it, or above the mixture window's 22 000 K, the status is `TemperatureOutOfRange`, and the
  state guard of the equilibrium path applies too. → HISTORY.md#cea-refs-2026-10-02
  ⚠ 2026-09-28: was moles only, now temperature too → HISTORY.md#frozen-temperature
  ⚠ 2026-09-26: was gaseous moles only, now condensed too → HISTORY.md#frozen-moles
- Outputs: mole numbers per species (kmol per kg of mixture), the mixture state
  (`MixtureState`: T, p, ρ, h, u, s, g, M, MW, frozen and equilibrium Cp and Cv, the two
  derivatives, γ_s, sound speed), the Lagrange multipliers (needed by `Transport` for
  the reacting conductivity), the iteration count and the status.
- The state guard, the figures of an `Ok` frozen state, the property definitions of RP-1311 and the
  pinned pair's convention: [StateRecord/BOOT.md](StateRecord/BOOT.md), `## Constraints`.
- Compile size (2026-10-04, the root's Compile size constraint): `Solve` calls its `NoInlining` stages (`TraceGasPass.Run` through `RunTraceGas`, `AttemptPlan.Next`) with copies of its loop-live locals, never `ref` or `in` to them; no bit moves.

## Structure

Decided 2026-09-14 (the clean-code pass; the root's code-shape constraint). The node
is one public facade over internal stage classes, all static and kernel-compatible,
all in this directory or in its child nodes (the rows below), one class per file, sharing the existing view,
scratch and result structs. Every floating-point expression keeps its present form
and its present order of evaluation: the decomposition moves code, it does not
rewrite formulas, and the bit snapshot of the tests node (the acceptance criteria
below) is the proof.
⚠ 2026-10-02: was all stages in this directory and namespace, now child nodes → HISTORY.md#structure-split-2026-10-02

| Class | Responsibility | Visibility |
|---|---|---|
| `EquilibriumSolver` | the composition root: `Solve` and `SolveFrozen` as the sequence of stage calls, the exit guards and the status write; holds no formula. Named here as the composition root the root's Ce rule allows above its limit (Ce 19 by the dependency check's walk on 2026-09-14) | internal (2026-09-15, distribution phase), contract unchanged |
| `Newton` (child node) | converges one condensed set: assembles, damps, judges, keeps the loop's bookkeeping and recovers from a singular system; holds `NewtonIteration`, the second composition root of the node ([Newton/BOOT.md](Newton/BOOT.md)) | internal, no project of its own |
| `Condensed` (child node) | changes the condensed set between two convergences and holds the exit guard ([Condensed/BOOT.md](Condensed/BOOT.md)) | internal, no project of its own |
| `GasPhase` (child node) | the gasless verdict of a failed tp attempt: the condensed-only Gibbs minimum by a two-phase revised simplex and the tangent-plane certificate that no gas lowers it ([GasPhase/BOOT.md](GasPhase/BOOT.md)) | internal, no project of its own |
| `Recovery` (child node) | the attempts of a case after the first and their order: the cold fallback, the verdict's place, the temperature bracket of hp and sp over tp probes, the finals, the state cleared on a failure; `AttemptPlan.Next`, reached from `Solve` through one `NoInlining` method ([Recovery/BOOT.md](Recovery/BOOT.md)) | internal, no project of its own |
| `TraceGas` (child node) | the trace-gas pass that `Recovery` schedules after a `GasRequired` verdict and as a bracket's final, and the close guard of gas-level stationarity ([TraceGas/BOOT.md](TraceGas/BOOT.md)) | internal, no project of its own |
| `StateRecord` (child node) | turns a converged or frozen composition into the `MixtureState` (RP-1311 sections 2.5 and 2.6, the plateau convention, the state guard) ([StateRecord/BOOT.md](StateRecord/BOOT.md)) | internal, no project of its own |

The other stage classes of this directory (`CaseSetup`, `ConvergenceSequence`, `Composition`,
`SpeciesMarks`, `ElementBalance`, `FrozenTemperature`, `DenseSolver`) are internal, each described by the
summary of its declaration; the classes of the child nodes are listed in their `BOOT.md`.
The data flow: `Solve`, `CaseSetup`, then `ConvergenceSequence` (a loop of `Newton.Converge`,
`Composition.Refresh` and `Condensed.Update`, with the tie's release and its way back) or, for a pass
`Recovery` names, `TraceGasPass.Run`; then the close: window, element invariant, exit guard, gas
stationarity, `Composition.Sums`, `TiedDerivatives`, `MixtureProperties`; after every attempt
`Recovery.AttemptPlan.Next` names the next one or the status.
→ HISTORY.md#structure-table-rows

⚠ 2026-09-15: was six types public, now internal with grants → HISTORY.md#visibility

The carriers of `Carriers.cs` (`IterationState` passed by `ref`, `SystemLayout`,
`MixtureSums`, `Derivatives`, the enums `EstimateSource`, `DerivativeKind` and `SpeciesMark`,
and `SpeciesMarks`) are described by the summaries of their declarations
→ HISTORY.md#structure-table-rows

Decisions taken with the review of 2026-09-14:

- **The species mark.** `SpeciesMark { Absent = 0, Active = 1, ForgivenOnce = 2,
  StoodDown = 3 }` names the values of `SpeciesActive`: a stood-down record is
  `StoodDown`, and "in play" is one predicate (`Active` or `ForgivenOnce`); `API.md`
  records the domain. → HISTORY.md#structure-decisions-2026-10-01
- **The flag arguments.** `isTp` and `isHp` come from `SystemLayout.Kind`, the
  derivative flag is `DerivativeKind`, the element-balance flag is two named tests;
  `useMolesAsEstimate` stays public (the contract), `EstimateSource` is its internal
  translation. → HISTORY.md#structure-decisions-2026-10-01
- **The constants.** Every number of the report gets a name in the stage that uses
  it: the control-factor weight 5 and limit 2 of equation (3.1), the frozen step
  limit 0.4, the initial gaseous moles 0.1, the offset of one e-fold below the trace
  threshold for an unestimated species, and a frozen step cap of its own, equal to
  `MaxNewtonSteps` today; values unchanged.
- **Decisions that moved down with their stage**: the pinned representative and the restored
  condensed order of `DerivativeSystem` ([StateRecord/BOOT.md](StateRecord/BOOT.md)); the geometry
  and the record bounds asked of `Thermo` ([Condensed/BOOT.md](Condensed/BOOT.md)); the Newton loop
  holds no formula ([Newton/BOOT.md](Newton/BOOT.md)). No stage of this node reads Thermo's
  interval layout (`IntervalStart`, `IntervalCount`, `IntervalBounds`).

- **The scratch descriptor keeps its constructor** (added 2026-09-14).
  `EquilibriumScratch` (the row below gives its parameter count) lists the slices of the
  batch-sized scratch buffers `API.md` publishes, one argument per slice, rule A's snapshot
  slices grouped in `TieSlices`; grouping the rest would move the contract and re-emit the
  kernels. It is this node's declared exception to the parameter rule, on the root's
  condition that every creation names its arguments; the one construction site, in `Slice`,
  names every argument.
  ⚠ 2026-10-02: was 12 parameters and a positional site, now 16 and named → HISTORY.md#slice-params-2026-10-02
  ⚠ 2026-10-03: was 16 parameters, now 14: rule A's four snapshot slices grouped in `TieSlices`, the bracket's ends one slice → HISTORY.md#scratch-14-2026-10-03
- **Size.** No method over 60 lines and no control flow nested deeper than 3 in every
  stage; should the composition root's `Solve` not fit under 60 lines as a plain
  sequence of stage calls, the exception is declared here with the measured count,
  and it may not exceed 100 lines.

- **The mark accessors moved to `SpeciesMarks`** (`Of`, `Set`, `InPlay`, in
`Carriers.cs` beside `SpeciesMark`); `CaseSetup` keeps the input validation, the initial
marks and the two reductions of the input. → HISTORY.md#s-marks

What the implementation settled, 2026-09-14, in the coding session that followed:

- **The `ref` carrier holds**, and `Solve` and `SolveFrozen` fit under 60 lines → HISTORY.md#s-settled-2026-10-02

- **The carriers are filled by name, not by position** (`MixtureSums`, `Derivatives`; `SystemLayout`
  stays readonly) → HISTORY.md#carriers-by-name-2026-10-04

## Shape exceptions

The rows below are this node's declared exceptions to the root's code-shape constraint,
in the form the protocol tests node reads; their reasons are decisions of `## Structure`.

| Where | Rule | Measured | Reason |
|---|---|---|---|
| `EquilibriumSolver` | efferent coupling | 24 | the composition root: `Solve` and `SolveFrozen` as the sequence of stage calls, the exit guards and the status write; holds no formula |
| `EquilibriumScratch.EquilibriumScratch` | parameters | 14 | lists the slices of the batch-sized scratch buffers `API.md` publishes, one argument per slice, rule A's snapshot grouped in `TieSlices`; grouping the rest would move the contract and re-emit the kernels (the decision "The scratch descriptor keeps its constructor"); its one construction site names its arguments |

The rows of the child nodes' types stand in their own `## Shape exceptions`
([Newton/BOOT.md](Newton/BOOT.md), [StateRecord/BOOT.md](StateRecord/BOOT.md)).

⚠ 2026-10-04: was `EquilibriumSolver` 22, now 24: `TraceGasPass` and `TraceGasStep` added → HISTORY.md#ce-solver-2026-10-04
⚠ 2026-09-28: was `EquilibriumScratch` 12 parameters, now 16 → HISTORY.md#ce-scratch16
⚠ 2026-10-03: was 16, now 14 → HISTORY.md#scratch-14-2026-10-03

Every other type of the node and its children measures 14 or below by the dependency check's
walk, the root's limit; at 14 stands `Newton.ConvergenceTests` and at 13 `ConvergenceSequence`
(2026-10-03) → HISTORY.md#ce-rest-2026-10-02

⚠ 2026-10-03: was every other type 11 or below, now `Newton.ConvergenceTests` 14 → HISTORY.md#ce-convergence-tests-2026-10-03
⚠ 2026-10-03: was `EquilibriumSolver` 24, now 22: `ConvergenceSequence` and `AttemptPlan` took four names → HISTORY.md#ce-solver-2026-10-03
⚠ 2026-09-28: was the rest "10 or below", now 11 → HISTORY.md#ce-rules-ab
⚠ 2026-09-28: was `EquilibriumSolver` at 19 and `NewtonIteration` at 18, now 22 and 19 → HISTORY.md#ce-roots-22
⚠ 2026-09-28: was the roots at 22 and 19, now 24 and 20 → HISTORY.md#ce-roots

## Acceptance criteria

→ [ACCEPTANCE.md](ACCEPTANCE.md)

## Taboos

- No equilibrium constants, no reaction sets, no hand-picked species subsets: the
  root forbids them and they would make the results unreviewable.
- No deletion of species from the table; no reallocation of anything during a solve.
- No `float`, no exceptions, no allocations, no virtual calls: kernel code.
- No knowledge of nozzles, chambers or rockets: `Performance` owns those iterations.
- No transport formulas: `Transport` owns them and only takes this node's outputs.
- No `SpeciesActive[` access outside `SpeciesMarks.Of` and `SpeciesMarks.Set`: every other stage reads and
  writes a mark through them, and `SpeciesMarks.InPlay` is the one "in play" predicate.
