# BOOT.md — Equilibrium.Recovery

## Purpose

A child node of `src/Equilibrium`, born 2026-10-03 for 0.2.2. It decides what a case does after
each attempt and when it ends:

- `AttemptPlan`, the ladder: the cold fallback of a warm start, the gasless verdict of a failed tp
  attempt ([GasPhase](../GasPhase/BOOT.md)), the temperature bracket of a failed hp or sp case, the
  final attempt, and the state cleared on a failure.
- `TemperatureBracket`: the bracket's ends and arithmetic (the safeguarded Newton on `ln T`, the
  bisection, the retreats, the stops), pure and testable on the host.
- `BracketSeeds`: compositions between the result and the ends (an end saved, a probe seeded, the
  lever mix, the highest record floor below). `PassOutcome`: what a tp pass found (the verdict, the
  value and slope of its assigned property) and what it leaves in the state.

`EquilibriumSolver.Solve` starts the plan with `AttemptPlan.Start` and calls `AttemptPlan.Next` after
every attempt. The cluster has a reason of its own to change: what the tree does when the report's
own iteration fails, which RP-1311 leaves open.

⚠ 2026-10-03: was the warm-start fallback in `src/Equilibrium` itself, now this node → HISTORY.md#recovery-split-2026-10-03

## Invariants

The parent's invariants hold here.

- **An `Ok` attempt ends the case untouched.** The plan acts only after a failure: a case whose
  first attempt or cold fallback ends `Ok` ends with the bits it had before 0.2.2.
- **Every probe is a Gibbs minimization at an assigned temperature.** The probes are tp attempts of
  the same solver and verdicts of the `GasPhase` node; the final attempt is the case itself. No
  formula of the iteration changes and no equation is added.
- **Bounded.** At most 40 probes, 8 retreats, two attempts and two verdicts per probe, one final.
- **No status but `Ok` carries a state**, with one exception: a `NoGasPhase` case carries the
  temperature and the pressure (root `BOOT.md`, "Failures are values"). A case that ends otherwise
  after a probe wrote a state gets `result.State[0] = default`.

## Dependencies

- [Equilibrium](../API.md) — the descriptors, `EstimateSource`, the constants of `EquilibriumSolver`.
- [GasPhase](../GasPhase/API.md) — `GasPhaseVerdict.Decide`, `GasVerdict`, `CondensedFigures`.
- [Thermo](../../Thermo/API.md) — the species table view, `CaseStatus`, `MixtureState`,
  `KernelMath`, `SpeciesFunctions.RecordLow`.

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
  - With one end known: the step clamped to ±ln 2 and to [ln 160, ln 20 000]. Going down, it stops
    at the highest lowest record bound times (1 + 1e-9) among the condensed species of the last probe
    below its temperature, since the data floor makes `P` non-monotone across it. At the domain's
    edge in the target's direction the case gives up `TemperatureOutOfRange`.
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
- **The trace-gas seam** (its own design, 0.2.2):
  - (a) the `GasRequired` arm of step 2 after failed attempts schedules a `TraceGas` pass at the same
    temperature, which `Solve` sends to the trace-gas entry instead of `ConvergenceSequence.Run`,
    and closes with the same `Close`;
  - (b) a bracket end of kind `TraceGas` selects the final in the one switch of
    `TemperatureBracket.LeverFinal`.
  - The trace-gas entry follows the kernel rules, has one `NoInlining` call site, and on `Ok` leaves
    `IterationState`, scratch and result as a converged `ConvergenceSequence` does. It does not
    reorder this ladder or change the bracket's numbers, and does not touch `BracketEnds`.
- **Scratch.** `BracketEnds` (`[2 · species]`), written only by `BracketSeeds`, survives the
  probes; every other slice is an attempt's.

## Structure

`AttemptPlan` (struct: `Current`, `Source`, `Phase`, `Status`, `Iterations`, the `TemperatureBracket`,
what the last pass `Found`; `RunsAttempt` is false for a `VerdictOnly` pass), `AttemptPhase` (enum:
`Warm`, `Cold`, `VerdictOnly`, `Final`), `TemperatureBracket` (struct, pure transitions, each constant
above named), `BracketMove` and `EndKind` (enums), `BracketSeeds` and `PassOutcome` (static). One type
per file.

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
- [x] No line of an `Ok` case moved in any `Bits*.approved.txt` (Equilibrium, Thermo,
      Performance, Problems, Docs, Cli), Windows: every `BitSnapshotTests` fact green against the
      unchanged records (2026-10-04). Linux records not touched (not run here).
- [ ] CUDA on the reference machine: families of bracketed hp/sp states and of hp/sp `NoGasPhase`,
      GPU equal to CPU; one launch of a family where every case brackets stays within
      `LaunchBudget`; the rocket kernel's compile within the guard, its figure recorded; the
      throughput tripwire not below the approved ratio; the fast set within 5 minutes; WSL green.

## Taboos

- No formula of the iteration and no new equation: a probe is a tp solve of the same solver.
- No second call site of `GasPhaseVerdict.Decide`, `ConvergenceSequence.Run` or the trace-gas entry.
- No public type; no `float`, exception, allocation or virtual call.
