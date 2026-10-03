# BOOT.md — Equilibrium.Newton

## Purpose

A child node of `src/Equilibrium` (its `BOOT.md`, `## Structure`), split out of that node on
2026-10-02 because the rules of the Newton loop bind only these files and the parent was over the
§15 limit. It converges one
condensed set:

- `IterationMatrix` assembles the reduced system of one step and `DampedStep` applies the damped
  step (equations (2.18), (3.1)–(3.4)).
- `ConvergenceTests` judges the step: the tests (3.5) and (3.6), the element balance, the polish
  test, and whether a gas crossed the retention threshold.
- `NewtonLoopState` keeps the loop's bookkeeping between steps.
- `SingularRemedies` recovers from a singular system: rule B (`CondensedDependency`), rule A
  (`ElementCoupling` and `TieSnapshot`), the resets and the targeted removal.
- `NewtonIteration` runs the loop and holds no formula.

`EquilibriumSolver` reaches the node through `NewtonIteration.Converge`, and the tests and the
condensed-set stage through the types listed in `API.md`. The cluster has a reason of its own to
change: the iteration of RP-1311 chapter 3 and the remedies of its section 3.6, which the
condensed-species rule and the state record do not share.

⚠ 2026-10-02: was every stage of the Newton loop in `src/Equilibrium` itself, now this node
→ HISTORY.md#structure-split-2026-10-02

## Invariants

The invariants of the parent ([BOOT.md](../BOOT.md)) hold here unchanged: the node is one of its
stages, so the method, the element conservation at convergence, the candidate list, the mask,
determinism, failures as values and the bounded work are kept true by these files as by the rest.

- **One convergence changes the condensed set only by a singular remedy's removal.** `Converge`
  never includes a species and never switches a phase; each removal it makes (rule B's, the targeted
  one) is counted in `IterationState.SetChanges` and restarts the step count. Inclusions and phase
  changes are the `Condensed` node's, taken between two convergences.

## Dependencies

- [Equilibrium](../API.md) — the descriptors of its inputs, scratch and outputs, `IterationState`,
  `SystemLayout`, `MixtureSums`, `ElementTie`, `SpeciesMarks`, `Composition`, `DenseSolver` and the
  constants of `EquilibriumSolver`.
- [Condensed](../Condensed/API.md) — `CondensedSet.Remove` and `MarkRemoved` for a singular
  remedy's removal.
- [Thermo](../../Thermo/API.md) — the species table view, `CaseStatus`, `KernelMath` and
  `PhysicalConstants`.

Outside the tree: ILGPU 1.5.3 (`ArrayView<T>`); NASA RP-1311 Part I chapters 2 and 3 as the algorithm's
source. The parent's internal types this node uses (`IterationState`, `SystemLayout`,
`MixtureSums`, `ElementTie`, `SpeciesMarks`, `Composition`, `ElementBalance`, `EquilibriumSolver`'s
constants) are not in the parent's tree contract, which lists what other nodes use; the child
belongs to the parent's assembly and reads them as its own.

## Constraints

Inherited from the parent ([BOOT.md](../BOOT.md)) and, through it, from the root. In addition:

- Every type here is `internal`; none becomes public (root `BOOT.md`, `## Delivery`,
  "Tree contracts").
- No project of its own: the `.cs` files compile into `src/Equilibrium`'s assembly under the
  namespace `APThermo.Equilibrium.Newton`, mirroring this directory from the tree root
  (`AGENTS.md` §1).
- The root's code-shape constraint applies unchanged.
- The retention threshold, the iteration cap's `MaxCondensedSetChanges` and the initial estimates
  are the parent's; "the condensed-species rule", the effective range and the exit guard cited
  below are in [Condensed](../Condensed/BOOT.md).

- The reduced equations are those of RP-1311 tables 2.1 and 2.2 with the gaseous
  corrections of equation (2.18) substituted. For sp the temperature row weighs a
  gaseous species by its entropy in the mixture, `S_j°/R − ln(n_j/n) − ln(p/p°)`, and
  a condensed one by `S_j°/R`; its right-hand side is `s₀/R − s/R + n − Σ n_j`, the
  total-moles equation being absorbed. The data are for 1 bar, so `p°` is `1e5 Pa`.

- Convergence tests and control factor as RP-1311 chapter 3: the `λ` damping of
  equations (3.1)–(3.3) with the two branches for species above and below the trace
  threshold, the tests (3.5) and (3.6) on `Δln n_j`, `Δln n`, `Δln T`, the
  condensed-species mole numbers and the element residuals.

- A singular matrix does not widen the threshold; the reference's widening to 80
  (`1994-1995`) was measured by the second audit to add warm-versus-cold disagreements
  and is not copied.

- After the report's tests pass, up to six further steps polish the iterate until the largest
  correction is below `1e-11`, so that the reported state is at rounding level and the
  tolerance table measures the reference's convergence, not this node's.

  The bookkeeping of the loop (2026-09-26) is a small internal struct, kernel-compatible
  and unit-tested on the host through its transitions. It holds the steps since the
  last change of the condensed set, the mark that the report's tests have passed, and
  the polish steps taken. Its rules:
  - **A verdict is taken over the gases the step leaves above the trace threshold.** A
    step that carries a gas across the threshold, in either direction, is not a
    converged step, whatever its corrections, and the iteration goes on. The tests of
    that step covered a set of gases the final refresh would not report. This binds
    the **second stage only**: under the first, a step that passes the report's tests
    converges whatever crossed, and that convergence only triggers the switch, as the
    reference's `tsize` does (`equilibrium.f90:1203-1300` has no crossing test).
  - **The switch restarts the polish.** Polish steps of the first stage do not count
    toward the second stage's convergence.
  - **Two flips hold the retained set.** Two consecutive second-stage steps that pass
    the report's tests and are refused only because one gas entered while another left
    set `IterationState.RetainedSetHeld` for the rest of the attempt (the parent's
    retention rule says what a held set means). The memory of "once retained" is the
    case's own `Moles[j] > 0`; no scratch is added.

  ⚠ 2026-10-03, the threshold flip of 0.2.0 (the orchestrator's investigation B2 for
  0.2.1): the trace carriers of the direction `π_K − π_Cl` (K, KO against CL, with KCL,
  K2CL2 and O2 retained at K:Cl = 1:1) sat within one e-fold of the first stage's
  threshold on either side, and Newton swapped them every step until the cap: 29 of the
  audit's 968 salt states, 423 of a broad scan of 43 380, `NotConverged`. Measured with
  the prototype: 423 fixed, 0 regressions, 0 violations, 426 of 427 cea states within
  the tolerance table; no bit moved; the 100 000-case sweep's iterations unchanged.
  Hysteresis alone was rejected (310 `NotConverged` and 43 `SingularMatrix`
  regressions), and so was the hold after one flip (one `SingularMatrix`).
  - **A failed verdict clears the mark and the polish count.** Polish counts only an
    unbroken run of passed verdicts, so a later pass polishes afresh. A step cap
    reached after a failed verdict is `NotConverged`, never `Ok`.
  - **A singular remedy that removes a condensed species is a change of the set.** It
    restarts the count of steps and counts toward `MaxCondensedSetChanges`, as every
    other change does.

  ⚠ 2026-09-26: was a verdict over the gases retained at the start of the step, now over
  the gases the step leaves above the threshold → HISTORY.md#loop-bookkeeping
  ⚠ 2026-09-12: was (3.1) symmetrical, now growing only → HISTORY.md#lambda-growing

  Singular matrices are reported as `SingularMatrix` after the remedies have been
  tried: resetting vanished species to `1e-6`, twice (the report's); then removing one
  condensed species chosen by the row whose pivot failed, as the reference does
  (2026-09-28; cea 3.3.4 `equilibrium.f90:2001-2059`):
  - a failed **condensed** row: the condensed species of the solution with the
    smallest mole number that shares an element with the last one added;
  - a failed **element** row: the condensed species of the solution with the smallest
    mole number that carries that element;
  - any other row, or no such species: the last condensed species, as before.

  The removal is a change of the set, as above, and marks the removed record, as a
  removal for range does: the next inclusion passes it over once while another positive
  candidate exists. To know the row, the dense solver gains a second entry that returns
  the index of the row whose pivot failed. `Solve` and its `bool` stay as they are, so
  `Transport`, which calls `Solve`, is untouched; the new entry is internal to this node.

  ⚠ 2026-09-28: was removing the last, now the row's pick → HISTORY.md#singular-removal

  Two rules come before the remedies above (2026-09-28, the orchestrator's
  investigation 6), in this order:
  - **Rule B: a dependent inclusion is a basis change.** When the matrix is singular
    and the condensed species added last is a linear combination of the other condensed
    species of the solution, or, failing that, of them and the gas phase taken as one
    more column with the composition of its retained species (`Σ_j a_ij n_j / Σ_j n_j`),
    the entering species stays. The gas never leaves: at an assigned temperature and
    pressure it is one phase of the set, and the singular matrix is the phase rule
    speaking. The condensed-only test comes first, so every set rule B resolved before
    resolves as before. The least-squares residual on
    the element vectors must be at most 1e-9 per element. The species that the
    favourable reaction uses up first leaves: the smallest `n_p/c_p` over the positive
    coefficients `c_p` of the combination, the simplex ratio test. This is a change of
    the set, and it is not marked for the anti-cycling skip. In hp and sp the
    temperature column keeps such a set non-singular, so in practice this is a tp rule.

    ⚠ 2026-10-03, the alkali-rich perchlorates (the orchestrator's investigation B4 for
    0.2.1): at K:Cl:O = 1:0.9:4 the set reached K2O2(cr), KCL(cr) and an all-O2 gas, and
    the inclusion of KO2 (= ½ K2O2 + ½ O2(g)) made the matrix singular on the `n` row;
    rule B, testing condensed columns only, found no combination, the resets diverged and
    the removal took KO2 out again, until the cap: 4596 of 14 460 broad-scan states
    `NotConverged`. With the gas column: 4751 fixed (155 NaClO4 states at 7–20 MPa among
    them), 0 regressions, 858 cea states within the tolerance table, no bit and no sweep
    iteration moved. Left, and declared: exact KO2/NaO2 stoichiometry (Cl 0, O 2), where
    the gas vanishes and cea does not converge either.
  - **Rule A: an element tie.**
    - **Trigger.** The matrix is singular, the failed pivot is element row `k`, and
      over every species of the sums (the retained gases and the condensed species of
      the solution) row `k` equals a linear combination `Σ c_i a_i` of the other active
      element rows. The coefficients solve the normal equations of those rows over the
      species of the sums (`G c = g`, `G_il = Σ_j a_ij a_lj`, `g_i = Σ_j a_ij a_kj`, by
      `DenseSolver` in the matrix scratch, free during a remedy), and every species must
      satisfy `a_kj = Σ c_i a_ij` to 1e-10 relative; otherwise there is no tie. A pair
      of elements is the case of one coefficient.
    - **Action.** Row `k` is replaced by the linearized balance of `b_k − Σ c_i b_i`,
      summed over every in-play gaseous species, retained or not, plus the condensed
      columns, each with the weight `a_kj − Σ c_i a_ij`. This fixes the one direction of
      the multipliers, `π_k − Σ c_i π_i`, that the retained species leave free, from the
      trace species that fix it at the true equilibrium.

      ⚠ 2026-10-03: was a pair of elements in one common ratio `r`, now any linear
      combination of the other rows (the orchestrator's investigation B1 for 0.2.1):
      with only Ar, CO2, H2O and N2 retained, row O equals 2·C + ½·H, which no pair
      expresses, and 77 tp states of the RP-1311 examples 1 and 12 tables at 300 K and
      600 K ended `SingularMatrix` (cea's own remedy widens the threshold to e^-80 and
      then drops the element's equation, which gives up conservation of that direction,
      6.7e-8 here against the 1e-12 invariant). Measured with the prototype: 168 of 168
      grid states `Ok` and clear of the independent equilibrium conditions, 28 new cea
      fixtures within the tolerance table, no bit snapshot moved, the rocket kernel's CPU
      compile +6 %. The pair search (`ElementCoupling.Find`) is replaced, not kept beside
      it: measured identical, state for state, on the whole fuzz.
    - **When.** At once when a condensed species of the solution holds both elements;
      otherwise only after the two resets, which still handle transient couplings.
    - **Release.** Once the condensed-set update finds no further change and some
      species of the sums breaks the combination, the tie is released, at most once per
      solve, and the settled set converges again on the element's own row.
      When that convergence fails, the tied iterate the release started from is
      restored and closed with the tie in force, as a tie that survived to the close
      (the third pass of 2026-09-28). The iterate is the case's own converged state
      (the logarithms of the moles, `n`, `T`, the condensed set and its moles, the
      tie), kept in the case's scratch at the release.

      ⚠ 2026-09-28: was a release with no way back, now the tied iterate restored when
      the release fails → HISTORY.md#release-way-back
    - **Derivatives.** The unit row a surviving tie gives the derivative system is the state
      record's: [StateRecord](../StateRecord/BOOT.md).

  The tie is per-case state: `ElementTie` in `IterationState` (active, element, released)
  and the coefficients `c` in the case's scratch, live and in the release snapshot, two
  slices of the element count each, grouped, with the multipliers snapshot, in `TieElementSlices`
  so that `EquilibriumScratch`'s constructor does not grow. `ElementCoupling` holds the read-only queries,
  `CondensedDependency` rule B's tests, and `IterationMatrix` stays the only writer of
  Newton rows. `SingularRemedies.Recover` tries rule B, then rule A, then the resets,
  then the targeted removal.

  ⚠ 2026-09-28: was removal alone, now rules B and A first → HISTORY.md#rules-ab

## Structure

The classes of this node, all internal, static and kernel-compatible, one per file, sharing the
parent's view, scratch and result structs: `NewtonIteration`, `IterationMatrix`, `DampedStep`,
`ConvergenceTests`, `SingularRemedies`, `CondensedDependency`, `ElementCoupling`, `TieSnapshot`, and
the carriers `NewtonLoopState` and `ConvergenceVerdict`, each described by the summary of its
declaration. Every floating-point expression keeps the form and the order of evaluation it had
when the node was part of its parent: the split moved files, it rewrote no formula.

| Class | Responsibility | Visibility |
|---|---|---|
| `NewtonIteration` | the Newton loop: the step and polish counts, the order of the stage calls, the status; holds no formula (the decision "The Newton loop holds no formula" below). Named here as this node's second composition root, the root's Ce rule allows above its limit (Ce 17 by the dependency check's walk on 2026-09-14) | internal |

- **The Newton loop holds no formula.** `NewtonIteration` keeps `Converge`: the step and
polish counts, the order of the calls, the status. The formulas live in `DampedStep`
((2.18), (3.1)–(3.4) with the temperature window), `ConvergenceTests` ((3.5), (3.6)
with the element balance, and the polish test, as one verdict) and `SingularRemedies`
(section 3.6), each with its named constants; the polish-step cap stays with the loop.
The loop's coupling is the width of the data it carries and of the stages it calls,
which no split removes, so `NewtonIteration` is this node's second composition root,
its figure in its row. Every expression keeps its form and its order of evaluation,
so the bit snapshot may not move. → HISTORY.md#s-newton

⚠ 2026-09-14: was `Converge` at 56 lines, the largest, now 55 → HISTORY.md#s-largest

## Shape exceptions

The rows below are this node's declared exceptions to the root's code-shape constraint, in the form
the protocol tests node reads; their reasons are decisions of `## Structure`.

| Where | Rule | Measured | Reason |
|---|---|---|---|
| `NewtonIteration` | efferent coupling | 20 | the Newton loop: the step and polish counts, the order of the stage calls, the status; holds no formula (the decision "The Newton loop holds no formula") |

⚠ 2026-09-26: was `NewtonIteration` at 17, now 18 → HISTORY.md#ce-newton-18

## Acceptance criteria

The criteria of the parent ([ACCEPTANCE.md](../ACCEPTANCE.md)) that name these files hold unchanged:
the bit snapshot, `KernelEqualityTests`, `NewtonLoopStateTests`, `SingularRemedyRulesTests` and the
rules A and B criterion.

- [x] 2026-10-02 — On the CPU path the split moved this node's files with no change but the
  namespace, `using` lines and doc references, and moved no bit: the criterion of the same date in
  [ACCEPTANCE.md](../ACCEPTANCE.md), `git diff -M` and the bit snapshot.
- [x] 2026-10-02 — On CUDA, on the reference machine: the second part of the same criterion
      (`../ACCEPTANCE.md`), green on `6dc2370`.
- [x] 2026-10-03 — Rule A ties a linear combination of element rows (`## Constraints`).
      Evidence, merged `a2ff9e2` (coder `a731c5f`, `87f8711`):
      - `tests/Equilibrium.Tests/ThreeElementTieTests.cs` (the O row's coefficients
        `c_C = 2`, `c_H = ½`; the release once H2 or O2 has moles; the tie held by `H2O(L)`;
        the grid of 168 states all `Ok` and clear of `EquilibriumConditions.Violations`) and
        `WarmStartTests.AWarmStartFromTheThreeElementExample1SolutionAtHalfPressureEqualsItsColdSolve`;
        23 tests red with `Find` limited to one coefficient (the old pair), green with it;
      - the four `three-element_*` tp fixtures of the fixtures node green under the
        tolerance table, red on the old rule;
      - no existing bit line moved; four lines added to the Equilibrium, Thermo and Problems
        records on Windows (`87f8711`) and Linux (`904222d`, WSL, 5559 green);
      - the guard with `--cuda` on the reference machine: fast suite 5559, CUDA proofs 382,
        `Execution.Tests` in Release 187, the rocket kernel's compile inside its bound.
- [ ] The threshold flip is closed (2026-10-03, `## Constraints`, the verdict, the switch and
      the hold): unit facts on `NewtonLoopState`'s flip count (a non-flip resets it, two
      flips report the hold) and on `Composition`'s retention of a held gas below the
      threshold; the H/O sp sentinel fact green with the polish restart; the L2 fact on the
      `naclo4_T500` table at Na:Cl:O = 1:1:3, 825 K, 1e3 Pa `Ok` and clear of the
      equilibrium conditions, `NotConverged` with the hold disabled; the salt fixtures
      `kclo4_T1150` (1 bar), `naclo4_T1120` (1 bar) and KClO4 at 1200 K and 10 bar, red
      on the old rule; no existing bit moved; CUDA equal on the reference machine.
- [ ] Rule B's gas column (2026-10-03): a unit fact on the ratio test (K2O2(cr) leaves
      when only O2 is retained) and three solve facts, red on the old rule; a negative
      fact (two condensed phases beside the gas give no combination); the KClO4 1:0.9:4
      fixtures at 300 K and 1e3 Pa, 500 K and 1 bar, 1000 K and 1e3 Pa, 1060 K and 1e3 Pa,
      red on the old rule; no existing bit moved; CUDA equal on the reference machine.

## Taboos

- No public type here: undocumented surface is a contract nobody agreed to.
- No formula in `NewtonIteration`: it keeps the counts, the order of the calls and the status.
- No inclusion of a species and no phase switch in this node: they are the `Condensed` node's.
- No `float`, no exceptions, no allocations, no virtual calls: kernel code (the parent's taboos).
