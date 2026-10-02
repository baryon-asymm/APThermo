# BOOT.md — Equilibrium.Condensed

## Purpose

A child node of `src/Equilibrium` (its `BOOT.md`, `## Structure`), split out of that node on
2026-10-02 because the condensed-species rule binds only these files and the parent was over the
§15 limit. It changes the
condensed set between two convergences and holds the exit guard:

- `CondensedSet` applies at most one change per convergence (a negative mole number removes the
  record, a record beyond its range changes phase, the inclusion test adds the best candidate), and
  answers the exit guard's question, whether an `Ok` state left a positive-gain candidate out.
- `PhaseGeometry` is the geometry of the table the rule reads: which records share a bound, the
  crossing of each pair, the effective range of a record, the adjacent record and the phase at a
  temperature.

`EquilibriumSolver` calls `CondensedSet.Update` after each convergence and
`ExitGuardFindsAPositiveCandidate` at the close; the `Newton` node removes a record through
`CondensedSet.Remove` and `MarkRemoved`, and the `StateRecord` node asks `PhaseGeometry.SameFormula`
(`API.md`). The cluster has a reason of its own to change: the report's tests for condensed species
and the reference's own handling of phases.

⚠ 2026-10-02: was the condensed-species rule in `src/Equilibrium` itself, now this node
→ HISTORY.md#structure-split-2026-10-02

## Invariants

The invariants of the parent ([BOOT.md](../BOOT.md)) hold here unchanged; the candidate list never
changes, and this node is where its condensed half enters and leaves the solution.

- **One change per call.** `CondensedSet.Update` applies at most one change of the set and reports
  whether it did; the caller converges again if it did.

## Dependencies

- [Equilibrium](../API.md) — the descriptors of its inputs, scratch and outputs, `IterationState`,
  `SpeciesMark` and `SpeciesMarks`, and the constants of `EquilibriumSolver`.
- [Thermo](../../Thermo/API.md) — the species table view and `SpeciesFunctions` (the record
  bounds, the latent-heat threshold).

Outside the tree: ILGPU 1.5.3 (`ArrayView<T>`); NASA RP-1311 Part I section 3.4 and chapter 3 as the
rule's source. The parent's internal types this node uses (`IterationState`, `SpeciesMarks`,
`EquilibriumSolver`'s constants) are not in the parent's tree contract; the child belongs to the
parent's assembly and reads them as its own.

## Constraints

Inherited from the parent ([BOOT.md](../BOOT.md)) and, through it, from the root. In addition:

- Every type here is `internal`; none becomes public (root `BOOT.md`, `## Delivery`,
  "Tree contracts").
- No project of its own: the `.cs` files compile into `src/Equilibrium`'s assembly under the
  namespace `APThermo.Equilibrium.Condensed`, mirroring this directory from the tree root
  (`AGENTS.md` §1).
- The root's code-shape constraint applies unchanged.
- `PhaseGeometry.Adjacent` and `PhaseGeometry.PhaseAt` test `!SpeciesMarks.InPlay`, not the raw
  `scratch.SpeciesActive[k] == 0`: a record stood down by the anti-cycling rule is out of play for the
  rest of the solve.
- The mixture's temperature window and the state guard, cited below, are the parent's and the
  `StateRecord` node's ([BOOT.md](../BOOT.md), [StateRecord](../StateRecord/BOOT.md)).

- Condensed species: one change per convergence, tested in this order after the
  report's tests pass.
  1. A condensed species with a negative mole number is removed.
  2. A record beyond its effective range (below) changes phase. A record whose
     same-formula partner is in the solution beside it — a pinned pair — is exempt
     from the range test. Otherwise the candidate is the record of the same formula
     whose effective range holds the temperature or, failing that, the adjacent
     record at the crossed bound. The record and the candidate pair up — the
     candidate enters at zero moles, both stay, and the next convergence settles the
     temperature at the crossing `T*` — when the candidate is that adjacent record,
     the temperature is a variable, the latent heat at the shared bound is real
     (`|ΔH°/RT| ≥ SpeciesFunctions.LatentHeatThreshold`, the Thermo node's constant),
     the set has room, and either `|T − T*| ≤ PhaseTransitionWindow` (50 K) or the
     candidate is the record switched out at the previous switch. Otherwise the
     record is switched for the candidate, and the record switched out is
     remembered; with no candidate at all it is removed and remembered as removed
     for range. A record removed for range a second time in one solve stands down
     for the rest of it — the temperature keeps leaving its range, and re-adding it
     forever is the cycle the reference aborts on (its "reinsertion likely to cause
     singularity" stop) — and an `Ok` exit is then guarded: a stood-down record
     that would qualify at the final state (in effective range, no partner in the
     solution, per-mole gain above the 1e-9 rounding of the converged multipliers)
     turns the status into `NotConverged` rather than a false equilibrium. Since
     2026-09-26 that guard covers every condensed record, not the stood-down ones only
     (the exit guard below).
  3. The inclusion test: the species whose `Σ π_i a_ij − g_j/RT` is largest and
     positive is added, one at a time, compared per mole as RP-1311 section 3.4
     words it (the reference's code — cea2.f as 3.3.4 — divides the gain by the
     molar mass; this node follows the report). Two candidates are passed over: a
     species whose formula is already in the solution, because a pair is completed
     by rule 2, never by inclusion; and, once, the record just removed for range
     while another positive candidate exists — the anti-cycling rule; when it is the
     only positive candidate it is taken, so no equilibrium is lost.

  Open below (2026-09-26): a condensed record whose lowest lower bound equals the gas
  data floor has no lower bound in any of these rules. The floor is 200 K,
  `EquilibriumSolver.GasDataFloor`: the reference's `T_min` parameter, "minimum gas
  temperature defined in thermo data", cea 3.3.4 `equilibrium.f90:1845`, applied at
  1913–1914; it is also the first standard range bound in the committed `thermo.inp`
  header. The record is a candidate, and stays in the solution, at any temperature
  the solver allows below its range, unless a record of its formula adjoins it below.
  In the committed file this is `H2O(cr)` alone. Measured the same day (H2/O2, O/F 4, 1
  bar, 165 to 199 K): cea 3.3.4 holds ice where this node reported supersaturated vapour
  as `Ok`, −12.5 MJ/kg against −14.9. → HISTORY.md#open-below-measure

  ⚠ 2026-09-26: was no gas-floor rule, now open-below → HISTORY.md#open-below-missing

  Effective range: where two records of one formula share a bound `T_b` and the
  latent heat there is real, the boundary between them is the crossing of their
  linearized Gibbs curves, `T* = T_b (1 + Δg/Δh)` with `Δg` and `Δh` the differences
  of `G°/RT` and `H°/RT` at `T_b`. The committed fits differ at their shared bounds
  by up to 1e-8 in `G°/RT`, so the pair's equilibrium sits at `T*`, not at `T_b`
  (AL2O3 a/L +1.241e-5 K, BeO b/L +1.447e-5 K, BeO a/b −2.705e-3 K, H2O cr/L
  −0.028 K). A crossing farther than 1 K from its bound means inconsistent fits
  (NaCN) and the printed bound stands; a shared bound below the latent-heat
  threshold moves nothing and its records switch without pairing; range comparisons
  carry a relative tolerance of 1e-9. In tp problems there is no pair — the
  temperature is assigned — and between `T_b` and `T*` the effective ranges hand the
  temperature to the record with the lower Gibbs energy. The memories (switched out,
  removed for range once, stood down) are per-case state — the stand-down mark
  lives in the species mask — and the scratch layout is unchanged.
  Exit guard (2026-09-26): an `Ok` exit is re-checked over every condensed record whose
  elements are present (every mark but `Absent`, the stood-down ones included) that is
  not in the solution, lies in its effective range at the final state, and has no record
  of its formula in the solution. If one of them would gain more
  than 1e-9 per mole, the rounding of the converged multipliers, the status becomes
  `NotConverged`. The guard is the stood-down guard of rule 2 widened; it reports
  whatever the rules above failed to include, a full set among it, instead of
  returning a false equilibrium.

  ⚠ 2026-10-02: was "every condensed record in play", now every mark but `Absent` → HISTORY.md#exit-guard-scope-2026-10-02

  ⚠ 2026-09-13: was both records within 50 K, now pinned pairs → HISTORY.md#range-rule

## Structure

The classes of this node, all internal, static and kernel-compatible: `CondensedSet` and
`PhaseGeometry`, each described by the summary of its declaration. Every floating-point expression
keeps the form and the order of evaluation it had when the node was part of its parent.

- **The geometry stays here.** The pure part of `PhaseGeometry` (which records share
  a bound, the crossing of each pair) is a property of the table and could live in
  `Thermo` beside the join-and-cut rule it already owns; moving it changes `Thermo`'s
  contract and the arithmetic path on CUDA (host-computed crossings against
  kernel-computed ones), so it is a later design session of the root, not part of
  this decomposition.

- **The record bounds are asked of `Thermo`.** `PhaseGeometry` asks
`SpeciesFunctions.RecordLow` and `RecordHigh` (its `API.md`, range questions) and keeps
no copy of `Thermo`'s interval-layout arithmetic (`IntervalStart`, `IntervalCount`,
`IntervalBounds` and its stride of two); no stage of this node reads the three layout
arrays. The bounds are table reads, so the bit snapshot may not move.
→ HISTORY.md#s-bounds

## Acceptance criteria

The criteria of the parent ([ACCEPTANCE.md](../ACCEPTANCE.md)) that name these files hold unchanged:
the condensed-species facts, the anti-cycling rule, the plateau facts and the exit guard.

- [x] 2026-10-02 — On the CPU path the split moved this node's files with no change but the
  namespace, `using` lines and doc references, and moved no bit: the criterion of the same date in
  [ACCEPTANCE.md](../ACCEPTANCE.md), `git diff -M` and the bit snapshot.
- [ ] On CUDA, on the reference machine: the second part of the same criterion.

## Taboos

- No public type here: undocumented surface is a contract nobody agreed to.
- No copy of `Thermo`'s interval-layout arithmetic (`IntervalStart`, `IntervalCount`,
  `IntervalBounds`): the record bounds are asked of `SpeciesFunctions.RecordLow` and `RecordHigh`.
- No raw read of `scratch.SpeciesActive[`: a mark is read through `SpeciesMarks`.
- No Newton step and no state record in this node: they are the `Newton` and `StateRecord` nodes'.
