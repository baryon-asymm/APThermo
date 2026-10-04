# BOOT.md — Equilibrium.Recovery

## Purpose

A child node of `src/Equilibrium`, born 2026-10-03 for 0.2.2. It decides what a case does after
each attempt and when it ends:

- `AttemptPlan`, the ladder: the cold fallback of a warm start, the gasless verdict of a failed tp
  attempt ([GasPhase](../GasPhase/BOOT.md)), the temperature bracket of a failed hp or sp case, the
  final attempt, and the state cleared on a failure.
- `TemperatureBracket`: the bracket's ends and arithmetic (the safeguarded Newton on `ln T`, the
  bisection, the retreats, the stops), pure and testable on the host.
- `BracketDriver` carries the bracket's moves out; `BracketSeeds`: compositions between the result and
  the ends (an end saved, a probe seeded, the lever mix). `PassOutcome`: what a tp pass found (the
  verdict, the value and slope of its assigned property) and what it leaves in the state.
- `DeadEnds` and `DeadEndRecheck`: the floors where a condensed record's data stop with no record of
  its formula beyond, and the recheck of an `Ok` state that lies below one ("Dead-end floors").

`EquilibriumSolver.Solve` starts the plan with `AttemptPlan.Begin` and calls `AttemptPlan.Next` after
every attempt. The cluster has a reason of its own to change: what the tree does when the report's
own iteration fails, which RP-1311 leaves open.

⚠ 2026-10-03: was the warm-start fallback in `src/Equilibrium` itself, now this node → HISTORY.md#recovery-split-2026-10-03

## Invariants

The parent's invariants hold here.

- **An `Ok` attempt ends the case untouched**, with the one exception of "Dead-end floors": an `Ok` cold
  hp or sp state below a dead-end floor with a positive inclusion gain is rechecked, and ends with the bits
  it had unless the recheck finds a state of the same h or s above the floor, which replaces it.

  ⚠ 2026-10-04: was "the plan acts only after a failure", now also after an `Ok` below a dead-end floor → HISTORY.md#recheck-ok
- **Every probe is a Gibbs minimization at an assigned temperature.** The probes are tp attempts of
  the same solver and verdicts of the `GasPhase` node; the final attempt is the case itself. No
  formula of the iteration changes and no equation is added.
- **Bounded.** At most 40 probes, 8 retreats, two attempts, two verdicts and one trace-gas pass per
  probe, one final; one trace-gas pass for a tp case.
- **No status but `Ok` carries a state**, with one exception: a `NoGasPhase` case carries the
  temperature and the pressure (root `BOOT.md`, "Failures are values"). A case that ends otherwise
  after a probe wrote a state gets `result.State[0] = default`.

## Dependencies

- [Equilibrium](../API.md) — the descriptors, `EstimateSource`, the constants of `EquilibriumSolver`.
- [GasPhase](../GasPhase/API.md) — `GasPhaseVerdict.Decide`, `GasVerdict`, `CondensedFigures`.
- [Condensed](../Condensed/API.md) — `CondensedSet.InclusionGain` and `PhaseGeometry.SameFormula`, for the dead-end floors.
- [Thermo](../../Thermo/API.md) — the species table view, `CaseStatus`, `MixtureState`,
  `KernelMath`, `SpeciesFunctions.RecordLow`.

Outside the tree: ILGPU 1.5.3. The parent's internal types this node uses are not in the parent's
tree contract; the child belongs to the parent's assembly.

## Constraints

Inherited from the parent and, through it, from the root. In addition:

- Every type is `internal`, under `APThermo.Equilibrium.Recovery`, no project of its own; the
  root's code-shape constraint applies, no row declared; kernel-compatible C#, the math list only,
  NaN-safe comparisons.
- **No whole-struct copies the CUDA post-link rejects** (2026-10-04). ILGPU 1.5.3 carries a struct that a loop or a
  `ref` call keeps alive as one value, and loads it field by field; two adjacent `bool` fields become one vector
  load into predicate registers (`ld.local.v2.b8 {%p, %p}`), which ptxas refuses (`Arguments mismatch for
  instruction 'ld'`), so that no kernel loads on CUDA while every CPU test stays green. So: no struct of this
  node holds two `bool` fields next to each other (a field of another type between them, as `TemperatureBracket`'s
  order does), and a plan is built in place (`default` and `AttemptPlan.Begin`), not returned by value.
  `ByteVectorTests` compiles every entry point and fails on any vector load or store of bytes or predicates.
- **Reached once.** `AttemptPlan.Next` (static, `ref AttemptPlan`, six parameters) carries
  `[MethodImpl(MethodImplOptions.NoInlining)]` and has one call site in `Solve`.
- **The ladder.** After a pass that ended with status `s`:
  1. A `Warm` pass that failed retries once `Cold`: tp keeps its temperature, hp and sp start at
     3 800 K, section 3.1's estimate, taking no part of the seed. A failure found at the close
     (the window, the element invariant, the exit guard, a singular derivative system, the state
     guard) counts. A `Cold` pass never falls back. (The rule of 2026-09-27, widened 2026-09-28.)
     → HISTORY.md#warm-evidence
     ⚠ 2026-09-28: was a warm-start fallback only for a negative seeded condensed species, now a fallback on any failure → HISTORY.md#warm-fallback
  2. A tp pass at a temperature in [160 K, 22 000 K] that failed, or that was `VerdictOnly`, asks
     the gasless verdict. `Gasless` ends the pass `NoGasPhase`. Otherwise a `VerdictOnly` pass goes
     on to its attempts, and a failure stands (the trace-gas seam (a) below).
  3. A tp case ends there; an hp or sp case whose attempts ended `Ok` ends `Ok`; one whose attempts
     failed starts the bracket, which remembers that first failure.
- **The bracket**, on `x = ln T`, for the assigned property `P` (h for hp, s for sp) and its target:
  - The first probe is at `problem.Temperature`, or 3 800 K, cold.
  - A probe is a tp problem at `max(160 K, e^x)`: `VerdictOnly` first when its nearer end is
    `Gasless`, then `Warm` from the nearer end (cold when no end is known), then `Cold`, then the
    verdict.
  - Outcome `Gas`: `P` from `result.State`, slope `T·CpEquilibrium` (hp) or `CpEquilibrium` (sp).
    Outcome `Gasless`: from `CondensedFigures`, slope `T·HeatCapacity` or `HeatCapacity`.
  - A recorded outcome is the lower end when `P` is below the target, else the upper end; its moles
    go to `BracketEnds`, the lower end first, with its kind.
  - The Newton step is `(target − P)/slope`; a slope that is not positive (a plateau's zero) gives
    ±ln 2 toward the target.
  - With both ends known: the Newton point, or the midpoint when that point leaves the bracket or
    the step does not halve the one before last.
  - With one end known: the step clamped to ±ln 2 and to [ln 160, ln 20 000]. Going down, it lands AT the
    highest dead-end floor below the probe (`DeadEnds.FloorBelow`, "Dead-end floors"), since across a floor
    the candidate set changes and `P` is not monotone. A probe that stands on a floor goes on below it, so
    where `P(floor)` is above the target the search continues down. At the domain's edge in the target's
    direction the case gives up `TemperatureOutOfRange`.
  - A failed probe retreats halfway toward the nearer end, warm; with no end known or 8 retreats
    spent, the case gives up with its first failure.
- **The stops and the finals.**
  - Both ends `Gas`, or mixed, within 1e-7, or a `Gas` probe whose step is at most 1e-7: the final
    attempt, the hp or sp case itself, warm, from the lever seed or from that probe, with no fallback;
    its status is the case's.
  - A `Gasless` probe whose step is at most 1e-12: the gasless final at `x + step`.
  - Both ends `Gasless` within 1e-9 (the range rule's relative tolerance): the gasless final by the
    lever rule.
  - With 40 probes spent: the lever final with both ends known, else a give-up with the first failure.
  - The lever rule: `f = clamp((target − P_low)/(P_high − P_low), 0, 1)`, or 0.5 when the
    difference is not positive; `T = T_low + f (T_high − T_low)`; moles `(1 − f) n_low + f n_high`.
  - The gasless final is a `VerdictOnly` pass at the final temperature: `Gasless` ends the case
    `NoGasPhase`, with the lever mix in place of the verdict's moles when it was taken between two
    gasless ends, and a state of that temperature and the pressure; otherwise the final attempt from
    the lever seed (from the last probe when only one end is known).
  - `Iterations` sums every attempt's Newton steps; a verdict counts none.
- **Dead-end floors** (2026-10-04, the no-ice investigation). A condensed record is a dead end when its
  lowest bound is not the gas data floor (200 K, where it is open below) and no record of its formula has
  its upper bound at that bound (`PhaseGeometry.SameFormula`). The records of RP-1311 example 12's list
  that are dead ends are `H2O(L)` (273.15 K, without `H2O(cr)`) and `C(gr)` (300 K); with `H2O(cr)` only
  `C(gr)`. Across the floor of a record whose elements are present, the candidate set gains or loses a
  phase, and the equilibrium enthalpy and entropy jump there.
  - **A floor stops a downward step** wherever a record whose elements are present has it, held by the probe or
    not (a probe of the gas alone stands just above the floor of the phase that is to join). The probe lands
    at the floor itself, which the range tolerance admits; a record adjoined by a lower record of its formula
    is no floor, because the pinned plateau keeps `P` continuous across it.
  - **The recheck.** A cold hp or sp attempt (the first, or the fallback after a failed warm one) that ended `Ok`
    at a temperature below the highest dead-end floor of a record whose inclusion gain at the final multipliers
    is positive (`DeadEnds.FloorAbove`) is a supersaturated vapour; hp equilibrium is the maximum-entropy state
    and sp the minimum-enthalpy one, so a state of the same h or s holding the phase above the floor, if there
    is one, is the equilibrium. One tp probe is taken at that floor, cold. If its `P` (h for hp, s for sp) is at
    or below the target, the bracket starts with it as its lower end and searches upward, and its final replaces
    the `Ok`. If it is above, there is none and the `Ok` stands. If the probe or the bracket fails, or its final
    does, the original attempt is rerun from the cold start, deterministic, and ends the case with the bits and
    the iterations it had. A warm attempt is not rechecked: its seed is gone once a probe has run (the
    declared limit; the case is that of a caller whose seed is itself a state below the floor).
  - **The list is the contract.** tp below a dead-end floor reports the supersaturated gas, the range rule, and
    that is correct (cea does the same). An hp or sp target that no state of the list reaches in the window
    is `TemperatureOutOfRange`; no status is added and `Problems` adds no ice.
- **Dead-end gaps** (2026-10-04, owner's decision on the 2e7 Pa AP/HTPB/Al residue). A dead-end ceiling is the
  mirror of a floor: the upper bound of a record with no record of its formula beginning there (`H2O(L)` at 600 K).
  Across a ceiling the equilibrium enthalpy and entropy jump up with the temperature, so an hp or sp target inside
  the jump has no state at the ceiling. When the final attempt of two narrow ends fails and its temperature lies within
  1e-6 of a dead-end floor or ceiling of a record whose elements are present (`DeadEnds.BoundNear`), the bracket has
  proven that no state lies there, and the failure is not reported.
  - **The scan.** The ends are dropped and the search goes on below the nearest dead-end floor under the gap, for the
    remaining root: one cold tp probe just below that floor (1e-8 relative, outside the range tolerance of its record).
    Across a floor `P` jumps up going down, so a probe on the far side whose `P` is above the target has a root
    below it, and from there the bracket is the one-sided search of "The bracket", landing at the floors below. A
    probe at or below the target has none in its segment, since `P` rises with `T` within a segment, and the scan takes
    the next floor down. With no floor left the case ends `TemperatureOutOfRange`: no state of the table at or above
    160 K has the target. The segment between the gap and that floor holds none, for the same reason.
  - It runs after the recheck's rerun and never for a final that did not fail on a bound; a failed scan probe retreats
    and gives up with the first failure like any probe. Each scan starts below the last gap's temperature, so the
    number of scans is bounded by the number of floors.
  - The 16 AP/HTPB/Al hp targets of the tp states at 200–275 K and 20 MPa, `NotConverged` in 0.2.2's first
    build, are `Ok` at their tp temperatures.
- **The trace-gas seams** ([TraceGas](../TraceGas/BOOT.md), 2026-10-04):
  - (a) A tp pass whose attempts ended `NotConverged` or `SingularMatrix`, at a temperature in the
    window, whose verdict is `GasRequired`, gets one `TraceGas` pass: the same problem, warm from the
    iterate the verdict restored, anchored on the multipliers it left in `Tie.Elements.Multipliers`.
    A `TemperatureOutOfRange` or an `Undecided` stands.
    - Its `Ok` ends a tp case `Ok`, or is a probe's outcome of kind `TraceGas` (P and slope as for
      `Gas`).
    - Any other status leaves the status of the attempt the verdict judged.
  - `Solve` sends a pass with `plan.RunsTraceGas` to `TraceGasPass.Run` instead of
    `ConvergenceSequence.Run`, and closes it the same way.
  ⚠ 2026-10-04: was a seam to be filled, now (a) → HISTORY.md#tracegas-seams-2026-10-04
- **Scratch.** `BracketEnds` (`[2 · species]`), written only by `BracketSeeds`, survives the
  probes; every other slice is an attempt's.

## Structure

`AttemptPlan` (struct: `Current`, `Source`, `Phase`, `Status`, `Iterations`, `Judged` (the status the
verdict judged), the `TemperatureBracket`, what the last pass `Found`, the `RecheckState`; `RunsAttempt`
is false for a `VerdictOnly` pass and `RunsTraceGas` true for a trace-gas one; `Next` is the ladder's one
method), `AttemptPhase` (enum: `Warm`, `Cold`, `VerdictOnly`, `Final`, `TraceGas`, `TraceGasFinal`),
`TemperatureBracket` (struct, pure transitions, each constant above named), `BracketMove` and `EndKind`
(enums; the end kinds are `Gas`, `Gasless` and `TraceGas`), `BracketDriver` (carries the moves out), `BracketSeeds` and `PassOutcome` (static), `DeadEnds` (the
floors), `DeadEndRecheck` (the recheck), `RecheckState` and `Recheck` (its state). One type per file.

## Acceptance criteria

- [x] hp and sp `NoGasPhase`: for each gasless tp state at `T0`, the hp state at its `h` and the sp
      state at its `s` (computed by the test from the moles and the table) end `NoGasPhase`
      with the tp state's moles (1e-12 relative) and multipliers, at `T0` (2026-10-04,
      `RecoveryTests.TheStateOfAGaslessTpCaseSolvedAgainAsHpOrSpEndsNoGasPhaseWithTheSameMoles`; red
      without the bracket: 74 of the 90 facts of this class and of `BracketedStateTests`).
- [x] A gasless melting plateau (KO2(a)/KO2(L) at 1e7 Pa): `NoGasPhase`, both records, the lever
      amounts reproducing `h` within 1e-9 relative (2026-10-04,
      `RecoveryTests.AGaslessMeltingPlateauIsNoGasPhaseWithBothRecordsAndTheLeverAmounts`).
- [x] Bracketed `Ok`: the gas-plateau states that fail today (CaCO3 and MgCO3, cold and seeded at
      0.1–0.5 of the transition), and the AP/HTPB/Al hp and sp states of 0.2.1's known limitations:
      `Ok`, clear of `EquilibriumConditions`, the tp temperature reproduced within 1e-9 (2026-10-04,
      `BracketedStateTests`, `RecoveryTests.AStateOfApHtpbAlBelowTheWaterBandEndsOkAtItsTpTemperature`).
      Left, and declared: magnesite at 1e5 Pa seeded from the one-condensed side ends
      `TemperatureOutOfRange` after the bracket (the thin band of a trace gas, the trace-gas
      coder's; the prototype's own scan gave the same 97 to 123 iterations).
- [x] `TemperatureBracket` on the host: the ln 2 clamp, the bisection safeguard, the floor stop,
      the retreats, the give-up statuses, the lever clamp, the finals (2026-10-04,
      `TemperatureBracketTests`).
- [x] A failed bracket case after an `Ok` probe leaves `State` zero, and `Iterations` count more
      than the first attempt alone (2026-10-04,
      `RecoveryTests.ACaseTheBracketGivesUpOnLeavesAZeroStateAndSumsEveryAttempt`).
- [ ] The five sp warm-430 K states of AP/HTPB/Al that land off the tp temperature in the prototype
      explained, and every `Ok` hp or sp state of the scans clear of `EquilibriumConditions`.
- [x] Dead-end floors: on example 12's list, every hp and sp target of a tp state of the list plus `H2O(cr)` ends `Ok`
      at the list-plus-ice temperature (1e-9 relative) when that state holds no ice, and otherwise
      `TemperatureOutOfRange` or the vapour with the original attempt's iterations; every class walked
      (2026-10-04, `DeadEndFloorTests.EveryTargetOfTheListPlusIceEndsAtItsStateOrBelowTheFloor`; red with the
      recheck off: 2 hp and 4 sp false `Ok`s of 300 states each, and with the floors of the held records only: 4 of 168
      in `ThreeElementTieTests`). The floors themselves: `TheLiquidIsADeadEndFloorExactlyWhenNoIceAdjoinsIt`,
      `ADownwardStepLandsOnTheHighestDeadEndFloorBelowTheProbe`.
- [x] Dead-end gaps: the hp targets of the AP/HTPB/Al tp states at 20 MPa and 200 to 275 K (16), cold and seeded
      from 430 K, end `Ok` at the tp temperature clear of `EquilibriumConditions`; a target a tenth or three
      tenths across the jump of `H2O(L)` at 600 K at 30 MPa, below every state at or above 160 K, ends
      `TemperatureOutOfRange` with a zero state (2026-10-04,
      `RecoveryTests.AnHpTargetOfASupercooledVapourStateAt20MPaEndsOkBelowTheFloors`,
      `RecoveryTests.AnEnthalpyInsideTheJumpAtTheLiquidsUpperBoundWithNoStateBelowEndsTemperatureOutOfRange`; red
      with the scan off: 34 of the 64 facts of the class; the host transitions in
      `TemperatureBracketTests.AScanDropsTheEndsProbesJustBelowEachFloorAndGivesUpWhenNoneIsLeft`).
- [x] No line of an `Ok` case moved in any `Bits*.approved.txt` (Equilibrium, Thermo,
      Performance, Problems, Docs, Cli), Windows: every `BitSnapshotTests` fact green against the
      unchanged records (2026-10-04). Linux records not touched (not run here).
- [x] The rocket kernel's compile stays within the execution node's guard with `Next` reached once: the first
      run allocated 500 109 800 bytes on the CPU accelerator, the program kept 165 661 616 bytes (2026-10-04,
      `RocketCompileTests.TheRocketKernelCompilesWithinItsAllocationBound`).
- [ ] CUDA on the reference machine: families of bracketed hp/sp states and of hp/sp `NoGasPhase`,
      GPU equal to CPU; one launch of a family where every case brackets stays within
      `LaunchBudget`; the rocket kernel's compile within the guard, its figure recorded; the
      throughput tripwire not below the approved ratio; the fast set within 5 minutes; WSL green.

## Taboos

- No formula of the iteration and no new equation: a probe is a tp solve of the same solver.
- No second call site of `GasPhaseVerdict.Decide`, `ConvergenceSequence.Run` or `TraceGasPass.Run`.
- No public type; no `float`, exception, allocation or virtual call.
