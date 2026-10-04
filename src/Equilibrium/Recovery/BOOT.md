# BOOT.md — Equilibrium.Recovery

## Purpose

A child node of `src/Equilibrium`, born 2026-10-03 for 0.2.2. It decides what a case does after
each attempt and when it ends:

- `AttemptPlan`, the ladder: the cold fallback of a warm start, the gasless verdict of a failed tp
  attempt ([GasPhase](../GasPhase/BOOT.md)), and the status the case ends with.

`EquilibriumSolver.Solve` starts the plan with `AttemptPlan.Start` and calls `AttemptPlan.Next` after
every attempt. The cluster has a reason of its own to change: what the tree does when the report's
own iteration fails, which RP-1311 leaves open.

⚠ 2026-10-03: was the warm-start fallback in `src/Equilibrium` itself, now this node → HISTORY.md#recovery-split-2026-10-03

## Invariants

The parent's invariants hold here.

- **An `Ok` attempt ends the case untouched.** The plan acts only after a failure: a case whose
  first attempt or cold fallback ends `Ok` ends with the bits it had before 0.2.2.
- **Bounded.** Two attempts and one verdict.
- **No status but `Ok` carries a state**, with one exception: a `NoGasPhase` case carries the
  temperature and the pressure (root `BOOT.md`, "Failures are values").

## Dependencies

- [Equilibrium](../API.md) — the descriptors, `EstimateSource`, the constants of `EquilibriumSolver`.
- [GasPhase](../GasPhase/API.md) — `GasPhaseVerdict.Decide`, `GasVerdict`.
- [Thermo](../../Thermo/API.md) — the species table view, `CaseStatus`, `MixtureState`.

Outside the tree: ILGPU 1.5.3. The parent's internal types this node uses are not in the parent's
tree contract; the child belongs to the parent's assembly.

## Constraints

Inherited from the parent and, through it, from the root. In addition:

- Every type is `internal`, under `APThermo.Equilibrium.Recovery`, no project of its own; the
  root's code-shape constraint applies, no row declared; kernel-compatible C#, the math list only,
  NaN-safe comparisons.
- **Reached once.** `AttemptPlan.Next` (static, `ref AttemptPlan`, six parameters) carries
  `[MethodImpl(MethodImplOptions.NoInlining)]` and has one call site in `Solve`.
- **The ladder.** After a pass that ended with status `s`:
  1. A `Warm` pass that failed retries once `Cold`: tp keeps its temperature, hp and sp start at
     3 800 K, section 3.1's estimate, taking no part of the seed. A failure found at the close
     (the window, the element invariant, the exit guard, a singular derivative system, the state
     guard) counts. A `Cold` pass never falls back. (The rule of 2026-09-27, widened 2026-09-28.)
     → HISTORY.md#warm-evidence
     ⚠ 2026-09-28: was a warm-start fallback only for a negative seeded condensed species, now a fallback on any failure → HISTORY.md#warm-fallback
  2. A tp pass at a temperature in [160 K, 22 000 K] that failed asks the gasless verdict. `Gasless`
     ends the pass `NoGasPhase`, the result holding the condensed minimum, its multipliers, and a
     state of the temperature and the pressure only. Otherwise the failure stands (the trace-gas
     seam (a) below).
  3. A tp case ends there; an hp or sp case ends with the status of its last attempt.
- **The trace-gas seam** (its own design, 0.2.2):
  - (a) the `GasRequired` arm of step 2 schedules a `TraceGas` pass at the same temperature, which
    `Solve` sends to the trace-gas entry instead of `ConvergenceSequence.Run`, and closes with the
    same `Close`;
  - the trace-gas entry follows the kernel rules, has one `NoInlining` call site, and on `Ok` leaves
    `IterationState`, scratch and result as a converged `ConvergenceSequence` does. It does not
    reorder this ladder and does not touch `BracketEnds`.
- **Scratch.** `BracketEnds` (`[2 · species]`) is the bracket's, written only by this node.

## Structure

`AttemptPlan` (struct: `Current`, `Source`, `Phase`, `Status`, `Iterations`) and `AttemptPhase` (enum:
`Warm`, `Cold`). One type per file.

## Acceptance criteria

- [x] The ladder keeps every bit: no line of an `Ok` case moved in any `Bits*.approved.txt` (2026-10-04, every `BitSnapshotTests` fact of the Equilibrium, Problems, Performance and Cli tests nodes green against the unchanged Windows records).

## Taboos

- No formula of the iteration and no new equation.
- No second call site of `GasPhaseVerdict.Decide`, `ConvergenceSequence.Run` or the trace-gas entry.
- No public type; no `float`, exception, allocation or virtual call.
