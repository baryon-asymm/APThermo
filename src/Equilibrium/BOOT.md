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

⚠ 2026-09-15: was "section 2.6" for every derivative of the matrix solutions, now
sections 2.5 and 2.6 → HISTORY.md#sec-2-5

## Invariants

- **One method.** The composition is the minimum of the Gibbs energy under element
  conservation, found by the Newton–Raphson iteration of RP-1311 on the reduced
  system (Lagrange multipliers per element, total moles, temperature for hp/sp, and
  the mole numbers of the condensed species in the solution). No reaction sets, no
  equilibrium constants.
- **Element conservation at convergence.** For every element, `|Σ a_ij n_j − b_i| ≤
  1e-12 · max(1, b_i)` in kmol per kilogram; a converged case that violates it is
  reported as `NotConverged`, never as `Ok`.
- **The candidate list never changes.** Every species of the table is a candidate
  throughout; gaseous species stay positive because the unknowns are their logarithms;
  condensed species enter and leave the solution by the condensed-species rule of the
  Constraints (the report's tests, completed on 2026-09-13); a species is
  never deleted from the table by this node.
- **An absent element is a mask, not an error.** A case whose abundance of an element
  is zero runs with every species containing that element inactive (mole number zero,
  no row or column in the iteration) and that element's equation dropped; the active
  set is decided once per case from the abundances, before the first iteration. A case
  with no active gaseous species is `InvalidInput`.

  ⚠ 2026-09-12: was a zero abundance `InvalidInput`, now an absent element is a mask
  → HISTORY.md#absent-element
- **Deterministic and stateless.** All inputs and all scratch are explicit parameters;
  the same inputs give the same bits on the same accelerator.
- **Failures are values.** Every exit path sets a `CaseStatus`; the state record is
  fully written only for `Ok`.
- **Bounded work.** The iteration count is capped (see Constraints); a case that does
  not converge within the cap returns `NotConverged` with the last iterate.

## Dependencies

- [Thermo](../Thermo/API.md) — the species table view, the species functions, the
  gas constant, `MixtureState` and `CaseStatus`.

Outside the tree: ILGPU 1.5.3 (`ArrayView<T>`); NASA RP-1311 Part I as the algorithm's
source.

## Constraints

Inherited from the root ([BOOT.md](../../BOOT.md)). In addition:

- Kernel-compatible C#: the per-case solve is a static method over views; the
  per-case scratch (the four species functions, the logarithms and corrections of the
  gaseous mole numbers, the iteration matrix, its right-hand side and row scales, the
  species and element masks, the condensed set) is passed in as views sliced from
  batch-sized buffers by the caller (`EquilibriumScratch.Slice`, `ScratchLayout`).
- Unknowns of the reduced system: elements + condensed species in the solution + 1
  (total moles) + 1 (temperature, hp and sp only). Limits: at most 20 elements and at
  most 20 condensed species in the solution at once
  (`ScratchLayout.MaxCondensedInSolution` equals `TableLimits.MaxElements`, 2026-09-26),
  hence a matrix of at most 42 × 42. The second limit is never the binding one. By the
  phase rule at an assigned or a pinned temperature and pressure, a state holds at most
  one condensed phase per element: fewer beside a gas phase, and one more only on a
  pinned plateau, where the temperature row goes.
  The dense solve is Gaussian elimination with scaled partial pivoting, internal to
  this node (visible to its tests node only).

  ⚠ 2026-09-26: was at most 8 condensed species in the solution (30 × 30), now at most
  20 (42 × 42) → HISTORY.md#condensed-limit

- The reduced equations are those of RP-1311 tables 2.1 and 2.2 with the gaseous
  corrections of equation (2.18) substituted. For sp the temperature row weighs a
  gaseous species by its entropy in the mixture, `S_j°/R − ln(n_j/n) − ln(p/p°)`, and
  a condensed one by `S_j°/R`; its right-hand side is `s₀/R − s/R + n − Σ n_j`, the
  total-moles equation being absorbed. The data are for 1 bar, so `p°` is `1e5 Pa`.
- Initial estimates as in the report: every gaseous species at `0.1 / (active gaseous
  species)` kmol per kg with `n = 0.1` when no estimate is given, `T = 3800 K` for hp
  and sp when no estimate is given; callers may pass a previous solution as the
  estimate (the nozzle does). A temperature estimate that is given for hp or sp and is
  not finite and positive is `InvalidInput`, as for the frozen solve (the third pass
  of 2026-09-28: +∞ and 1e-300 K gave `SingularMatrix` after no iteration, part 1,
  observation O1).

  A warm start that fails, with any status other than `InvalidInput`, falls back once
  to the cold start of section 3.1, with the iterations of both attempts counted in the
  case's total (2026-09-28). A failure found at the close counts as well: the
  mixture window, the element invariant, the exit guard, a singular derivative system
  and the state guard (the third pass of 2026-09-28; the code tested only the Newton
  loop's status, while this sentence and `API.md` said "any status"). A cold start never falls back. The cold start takes no part
  of the seed: for hp and sp it starts at 3 800 K, not at the previous solution's
  temperature, since that temperature is part of the seed.

  ⚠ 2026-09-28: was a warm-start fallback only for a negative seeded condensed species,
  now a fallback on any failure → HISTORY.md#warm-fallback
- Convergence tests and control factor as RP-1311 chapter 3: the `λ` damping of
  equations (3.1)–(3.3) with the two branches for species above and below the trace
  threshold, the tests (3.5) and (3.6) on `Δln n_j`, `Δln n`, `Δln T`, the
  condensed-species mole numbers and the element residuals. The retention threshold
  has two stages, as the reference's `tsize`/`xsize` (2026-09-28; cea 3.3.4
  `equilibrium.f90:60-64`, switched at 1293-1304): `ln(n_j/n) = −18.420681`
  (`n_j/n = 1e-8`, the report's) until the first convergence of the case, then
  `ln(n_j/n) = −25.328436` (`1e-11`) for the rest of the solve. Below the threshold a
  gaseous species is held at zero in the sums and keeps its logarithm. The switch
  recomputes the retained amounts and counts as a change of the retained set: the loop
  must converge once more under the second stage before it may exit, so every `Ok`
  has been converged under 1e-11. The switch happens once per solve, including a
  warm start. The report stands for the last `Composition.Refresh` under the
  second-stage threshold: since an `Ok` exit is never reached before the switch (the
  paragraph above), every reported composition is the second-stage one, and a gaseous
  species between 1e-11 and 1e-8 of the gas is reported at its converged amount, not
  zeroed. `Composition` stays the one place the retention rule is applied, and the
  stage is per-case state (`IterationState.RetentionSecondStage`, not the loop's own
  bookkeeping struct: the flag must survive across the several `Converge` calls one
  `Solve` attempt can make, and `NewtonLoopState` is rebuilt fresh at each of them).

  ⚠ 2026-09-28: was the report zeroing species below 1e-8 in a separate step, now the
  report stands for the last `Composition.Refresh` → HISTORY.md#report-zeroing

  A singular matrix does not widen the threshold; the reference's widening to 80
  (`1994-1995`) was measured by the second audit to add warm-versus-cold disagreements
  and is not copied. Iteration cap: 50 Newton steps after the last change of the
  condensed species set, and at most `MaxCondensedSetChanges` changes of that set per
  case: three per slot of the condensed set, an inclusion, a forgiveness and a
  stand-down for each of the `ScratchLayout.MaxCondensedInSolution` slots (24 today;
  the constant is the number, this document only names it). After the
  report's tests pass, up to six further steps polish the iterate until the largest
  correction is below `1e-11`, so that the reported state is at rounding level and the
  tolerance table measures the reference's convergence, not this node's.

  The bookkeeping of the loop (2026-09-26) is a small internal struct, kernel-compatible
  and unit-tested on the host through its transitions. It holds the steps since the
  last change of the condensed set, the mark that the report's tests have passed, and
  the polish steps taken. Its rules:
  - **A verdict is taken over the gases the step leaves above the trace threshold.** A
    step that carries a gas across the threshold, in either direction, is not a
    converged step, whatever its corrections, and the iteration goes on. The tests of
    that step covered a set of gases the final refresh would not report.
  - **A failed verdict clears the mark and the polish count.** Polish counts only an
    unbroken run of passed verdicts, so a later pass polishes afresh. A step cap
    reached after a failed verdict is `NotConverged`, never `Ok`.
  - **A singular remedy that removes a condensed species is a change of the set.** It
    restarts the count of steps and counts toward `MaxCondensedSetChanges`, as every
    other change does.

  ⚠ 2026-09-26: was a verdict over the gases retained at the start of the step, now over
  the gases the step leaves above the threshold → HISTORY.md#loop-bookkeeping
  ⚠ 2026-09-28: was one retention threshold for the whole solve, now two stages, 1e-8
  then 1e-11 → HISTORY.md#two-stage
  ⚠ 2026-09-14: was at most 10 changes of the condensed set, now the constant
  `MaxCondensedSetChanges` → HISTORY.md#set-changes
  ⚠ 2026-09-12: was equation (3.1) read symmetrically, now only growing species enter
  the maximum → HISTORY.md#lambda-growing
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

  ⚠ 2026-09-26: was no rule for a record at the gas data floor, now the open-below rule
  → HISTORY.md#open-below-missing

  The mixture's temperature window (2026-09-28): an `Ok` of any kind, tp included, is
  valid only when the final temperature lies in [160 K, 22 000 K], the reference's
  `T_min` and `T_max` of the solver (cea 3.3.4 `equilibrium.f90:78-80`, checked after
  convergence at 2682-2685, where the state is then not converged). Outside it the
  status is `TemperatureOutOfRange`. The iterate window of hp and sp, [100 K,
  20 000 K], is unchanged. The window bounds the open-below rule: ice is a candidate
  below 200 K, and a state holding it is valid down to 160 K.

  A state guard (2026-09-28), this node's own and not the reference's: an `Ok` whose
  frozen or equilibrium heat capacity (Cp or Cv), `γ_s` or sound speed is not finite
  and positive is `TemperatureOutOfRange`. The window above keeps every case the
  committed data can serve, and the guard catches what a polynomial evaluated outside
  its fit may still produce inside the window. The pinned pair's `Cp_eq = Cv_eq = 0`
  convention (Property definitions below) is exempt for the equilibrium pair only.

  ⚠ 2026-09-28: was ice held at any temperature below its range, now bounded by the
  mixture window → HISTORY.md#ice-window

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
  Exit guard (2026-09-26): an `Ok` exit is re-checked over every condensed record in
  play that is not in the solution, lies in its effective range at the final state,
  and has no record of its formula in the solution. If one of them would gain more
  than 1e-9 per mole, the rounding of the converged multipliers, the status becomes
  `NotConverged`. The guard is the stood-down guard of rule 2 widened; it reports
  whatever the rules above failed to include, a full set among it, instead of
  returning a false equilibrium.

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

  ⚠ 2026-09-28: was removing the last condensed species, now the species the failed row
  picks → HISTORY.md#singular-removal

  Two rules come before the remedies above (2026-09-28, the orchestrator's
  investigation 6), in this order:
  - **Rule B: a dependent inclusion is a basis change.** When the matrix is singular
    and the condensed species added last is a linear combination of the other condensed
    species of the solution, the entering species stays. The least-squares residual on
    the element vectors must be at most 1e-9 per element. The species that the
    favourable reaction uses up first leaves: the smallest `n_p/c_p` over the positive
    coefficients `c_p` of the combination, the simplex ratio test. This is a change of
    the set, and it is not marked for the anti-cycling skip. In hp and sp the
    temperature column keeps such a set non-singular, so in practice this is a tp rule.
  - **Rule A: an element tie.**
    - **Trigger.** The matrix is singular, the failed pivot is element row `k`, and
      another active element `i` appears in one common ratio `r = a_kj/a_ij` in every
      species of the sums (the retained gases and the condensed species of the
      solution).
    - **Action.** Row `k` is replaced by the linearized balance of `b_k − r·b_i`,
      summed over every in-play gaseous species, retained or not, plus the condensed
      columns. This fixes the one direction of the multipliers, `π_k − r·π_i`, that the
      retained species leave free, from the trace species that fix it at the true
      equilibrium.
    - **When.** At once when a condensed species of the solution holds both elements;
      otherwise only after the two resets, which still handle transient couplings.
    - **Release.** Once the condensed-set update finds no further change and some
      species of the sums tells the pair apart, the tie is released, at most once per
      solve, and the settled set converges again on the element's own row.
      When that convergence fails, the tied iterate the release started from is
      restored and closed with the tie in force, as a tie that survived to the close
      (the third pass of 2026-09-28). The iterate is the case's own converged state
      (the logarithms of the moles, `n`, `T`, the condensed set and its moles, the
      tie), kept in the case's scratch at the release.

      ⚠ 2026-09-28: was a release with no way back, now the tied iterate restored when
      the release fails → HISTORY.md#release-way-back
    - **Derivatives.** A tie that survives to the close gives the derivative system a
      unit row that fixes the tied element's multiplier derivative at 0.

  The tie is per-case state carried in `IterationState` (`ElementTie`: active, element,
  partner, ratio, released). `ElementCoupling` holds the read-only queries,
  `CondensedDependency` rule B's tests, and `IterationMatrix` stays the only writer of
  Newton rows. `SingularRemedies.Recover` tries rule B, then rule A, then the resets,
  then the targeted removal.

  ⚠ 2026-09-28: was the targeted removal alone, now rules B and A before it
  → HISTORY.md#rules-ab
  ⚠ 2026-09-13: was the range rule keeping both records within 50 K and removing after
  convergence, now pinned pairs at `T*`, the switch memory and anti-cycling
  → HISTORY.md#range-rule
- Frozen mode: with the composition fixed, solve for the temperature that gives the
  requested enthalpy or entropy (Newton on `T`, to `1e-10` relative) and compute the
  frozen properties; this mode serves frozen nozzle flow. The composition is valid when
  every mole number, gaseous and condensed, is finite and not negative and the gaseous
  ones sum to more than zero; otherwise the case is `InvalidInput` (2026-09-26).
  The temperature is valid when it is finite and positive: for tp the assigned one, for
  hp and sp the estimate, which is replaced as in `Solve` when it is not
  (2026-09-28); otherwise the case is `InvalidInput`. An `Ok` frozen state has a
  temperature not below 0.8 times the lowest lower bound of the fits of the gases
  present, the reference's stop of a frozen expansion (cea 3.3.4 `rocket.f90:331-341`,
  0.8 × 200 K = 160 K with the committed data). Below it, or above the mixture window's
  22 000 K, the status is `TemperatureOutOfRange`, and the state guard of the
  equilibrium path applies to a frozen `Ok` too.
  An `Ok` frozen state carries `CpEquilibrium = CpFrozen`, `CvEquilibrium = CvFrozen`,
  the derivatives 1 and −1 and `γ_s = Cp/Cv`.
  ⚠ 2026-09-28: was only the mole numbers of a frozen case validated, now the
  temperature too → HISTORY.md#frozen-temperature
  ⚠ 2026-09-26: was only the gaseous mole numbers of a frozen case checked, now the
  condensed ones too → HISTORY.md#frozen-moles
- Outputs: mole numbers per species (kmol per kg of mixture), the mixture state
  (`MixtureState`: T, p, ρ, h, u, s, g, M, MW, frozen and equilibrium Cp and Cv, the two
  derivatives, γ_s, sound speed), the Lagrange multipliers (needed by `Transport` for
  the reacting conductivity), the iteration count and the status.
- Property definitions of RP-1311: `Cp_eq` includes the reaction contribution of the
  composition derivatives ((2.49), (2.59), section 2.5); `γ_s = −(∂ln p/∂ln V)_s` and
  `a² = n R T γ_s` per unit mass (2.71, 2.74, section 2.6); `M = 1/n` (2.3a) with `n`
  the total gaseous moles per kilogram; `MW = 1/Σ n_j` (2.4a) over all species with
  the condensed ones counted as moles (the reference's MW, see the Thermo `API.md`);
  the condensed species are included in h, s and Cp of the mixture as in CEA.
  At a pinned pair the constant-pressure derivatives do not exist: the derivative
  system is assembled once, at constant temperature, with one record of the pair as
  the representative (RP-1311 section 3.5; Gordon 1970), and the state carries the
  reference's convention `Cp_eq = Cv_eq = (∂ln V/∂ln T)_p = 0` with
  `(∂ln V/∂ln p)_T` real, `γ_s = −1/(∂ln V/∂ln p)_T` and `a² = n R T γ_s` — the
  plateau values the throat search needs (the AP/Al verification record: `γ_s` 0.816
  on the plateau against 1.09 beside it; the zeros against NaN-plus-flag decided in
  the design session of 2026-09-13, so that the state struct, the surface snapshot
  and the reference comparisons stay unchanged).

## Structure

Decided 2026-09-14 (the clean-code pass; the root's code-shape constraint). The node
is one public facade over internal stage classes, all static and kernel-compatible,
all in this directory and namespace, one class per file, sharing the existing view,
scratch and result structs. Every floating-point expression keeps its present form
and its present order of evaluation: the decomposition moves code, it does not
rewrite formulas, and the bit snapshot of the tests node (the acceptance criteria
below) is the proof.

| Class | Responsibility | Visibility |
|---|---|---|
| `EquilibriumSolver` | the composition root: `Solve` and `SolveFrozen` as the sequence of stage calls, the exit guards and the status write; holds no formula. Named here as the composition root the root's Ce rule allows above its limit (Ce 19 by the dependency check's walk on 2026-09-14) | internal (2026-09-15, distribution phase), contract unchanged |
| `CaseSetup` | input validation, the element mask, the initial species marks, the active-gas count, the initial estimates (the defaults or a previous solution) | internal |
| `Composition` | the four species functions at the case temperature; the retained gaseous moles (the trace rule, one place); the mixture sums the system and the state need (`MixtureSums`) | internal |
| `IterationMatrix` | the reduced Newton system of RP-1311 tables 2.1 and 2.2, one method per row family (the gaseous contributions, the total-moles row, the element rows, the condensed rows, the temperature row), accumulated in the present order | internal |
| `NewtonIteration` | the Newton loop: the step and polish counts, the order of the stage calls, the status; holds no formula (the decision "The Newton loop holds no formula" below). Named here as this node's second composition root, the root's Ce rule allows above its limit (Ce 17 by the dependency check's walk on 2026-09-14) | internal |
| `DampedStep` | the multipliers and the gaseous corrections of (2.18), the control factor of (3.1)–(3.3), the application (3.4), the temperature update and its range check | internal |
| `ConvergenceTests` | the tests (3.5) and (3.6) with the element balance, and the polish test, as one verdict | internal |
| `SingularRemedies` | the remedies of section 3.6: the reset of vanished gaseous species, then the removal of the last condensed record | internal |
| `CondensedSet` | membership of the condensed records between convergences: removal of a negative record, the range rule with pinned pairs, switching and stand-down, the inclusion test with the anti-cycling skip, the honesty guard of an `Ok` exit; `InclusionGain` is the one source of the section 3.4 gain, used by the test and by the guard | internal |
| `PhaseGeometry` | where two records of one formula meet: the record bounds as `Thermo` answers them, adjacency, the crossing `T*`, the effective range, the partner in the solution | internal |
| `SpeciesMarks` | the mark accessors (`Of`, `Set`, `InPlay`), used by every stage that reads or writes a species' mark, `CondensedSet` and `CaseSetup` included | internal |
| `ElementBalance` | the abundance `Σ a_ij n_j` of an element in the composition (one place, used by the matrix's residual `b_i° − Σ a_ij n_j` and by both tests) and its two tolerance tests, as two named methods | internal |
| `DerivativeSystem` | the derivative system of section 2.5 at the converged composition, the two right-hand sides (`DerivativeKind`: temperature, pressure), the pinned-pair representative, the reaction sum of (2.59); returns `Derivatives` | internal |
| `MixtureProperties` | the state record: the assignments common to both paths written once, then the frozen closure or the equilibrium or pinned closure | internal |
| `FrozenTemperature` | Newton on the temperature at a fixed composition, to the frozen test, with its own step cap | internal |
| `DenseSolver` | contract unchanged; `Solve` split into scaling, elimination and back substitution | internal (2026-09-15, distribution phase), contract unchanged |
| `TieSnapshot` | rule A's way back (the third pass of 2026-09-28, finding F1): saves and restores the gaseous logarithms, the condensed set with its mole numbers, and the Lagrange multipliers of the tied converged iterate a release starts from | internal |

⚠ 2026-09-15: was six types public, now internal, with `InternalsVisibleTo` grants
→ HISTORY.md#visibility

Carriers (`Carriers.cs`): `IterationState`, the per-case state carried between the
stages (temperature, `ln n`, the condensed count, the temperature the functions were
evaluated at, the step and set-change counts, the switched-out and removed-for-range
memories), passed by `ref`, or returned by value should the kernel compiler refuse a
`ref` struct, which the kernel-equality test decides; `SystemLayout` (the unknown
count, the stride, the rows of the total-moles and temperature equations, the
problem kind); `MixtureSums`; `Derivatives`; the enums `EstimateSource`,
`DerivativeKind`, `SpeciesMark` and `ConvergenceVerdict` (added 2026-09-14 with the
Newton-loop split below, the verdict `ConvergenceTests` returns and `NewtonIteration`
reads); `SpeciesMarks` beside `SpeciesMark` (added 2026-09-15, the repair review's
R-Equilibrium-6, below).

Decisions taken with the review of 2026-09-14:

- **The species mark.** `SpeciesActive` keeps its slot and gains named values,
  `SpeciesMark { Absent = 0, Active = 1, ForgivenOnce = 2, StoodDown = 3 }`: a
  stood-down record is `StoodDown`, no longer `Absent`, so the honesty guard reads
  the mark instead of re-deriving element presence, and "in play" is one predicate
  (`Active` or `ForgivenOnce`) instead of three spellings. The scratch layout is
  unchanged; `API.md` records the domain.
- **The flag arguments.** `isTp` and `isHp` come from `SystemLayout.Kind`; the
  derivative flag becomes `DerivativeKind`; the element-balance flag becomes the two
  named tests. `useMolesAsEstimate` stays on the public entry point: it is the
  contract, and `EstimateSource` is its internal translation.
- **The pinned representative.** `DerivativeSystem` swaps the representative into the
  last slot exactly as today, so that the assembled rows and the pivoting keep their
  order, and restores the caller's order before returning: the scratch is not
  permuted behind the caller's back.
- **The constants.** Every number of the report gets a name in the stage that uses
  it: the control-factor weight 5 and limit 2 of equation (3.1), the frozen step
  limit 0.4, the initial gaseous moles 0.1, the offset of one e-fold below the trace
  threshold for an unestimated species, and a frozen step cap of its own, equal to
  `MaxNewtonSteps` today; values unchanged.
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

- **The Newton loop holds no formula.** `NewtonIteration` keeps `Converge`: the step and
polish counts, the order of the calls, the status. What it computes lives in three
stages named after the sections of RP-1311 chapter 3 they implement: `DampedStep` (the
multipliers and the gaseous corrections of (2.18), the control factor of (3.1)–(3.3),
its application (3.4) with the temperature window), `ConvergenceTests` ((3.5) on the
undamped corrections, (3.6) on Δln T with the element balance, and the polish test, as
one verdict the loop reads) and `SingularRemedies` (section 3.6). Each named constant
moves with the stage that uses it; the polish-step cap stays with the loop. The loop's
coupling is the width of the data it carries and of the stages it calls, which no split
removes, so `NewtonIteration` is this node's second composition root, its measured
figure in its row. Every expression keeps its form and its order of evaluation, so the
bit snapshot may not move. → HISTORY.md#s-newton

- **The scratch descriptor keeps its constructor** (added 2026-09-14).
  `EquilibriumScratch` (12 parameters) lists the slices of the batch-sized scratch
  buffers `API.md` publishes, one argument per slice; grouping them would move the
  contract and re-emit the kernels. It is this node's declared exception to the
  parameter rule, on the root's condition that every creation names its arguments; a
  scan of the construction sites found the one site, in `Slice`, positional.
- **Size.** No method over 60 lines and no control flow nested deeper than 3 in every
  stage; should the composition root's `Solve` not fit under 60 lines as a plain
  sequence of stage calls, the exception is declared here with the measured count,
  and it may not exceed 100 lines.

- **The mark accessors moved to `SpeciesMarks`.** `CaseSetup` is "what a case needs
before its first Newton step", but its mark accessors were used by every stage of the
iteration and `CondensedSet` wrote through them (`StandDown`). `Of`, `Set` and `InPlay`
(the renamed `Mark`, `Mark` and `InPlay`) now sit in `Carriers.cs` beside `SpeciesMark`;
`CaseSetup` keeps the input validation, the initial marks and the two reductions of the
input. → HISTORY.md#s-marks

What the implementation settled, 2026-09-14, in the coding session that followed:

- **The `ref` carrier holds.** `IterationState` is passed by `ref` through every stage
and the kernel compiler takes it (`KernelEqualityTests` of this node and of
`Performance.Tests`). → HISTORY.md#s-settled
- **The composition root fits.** `Solve` and `SolveFrozen` are plain sequences of stage
calls under the root's 60 lines, so no exception is claimed for them.
→ HISTORY.md#s-settled

⚠ 2026-09-14: was `NewtonIteration.Converge` named the largest method at 56 lines, now
the Newton-loop split left it at 55 → HISTORY.md#s-largest

- **The carriers are filled by name, not by position.** `MixtureSums` and `Derivatives`
are structs whose fields are written at the one place that computes them and read
through `in` afterwards, not readonly structs with a nine- and a five-parameter
constructor: a carrier whose purpose is to remove the parameter hazard may not
reintroduce it in its own constructor. `SystemLayout` stays readonly: its arguments are
the shape of the system and it derives the rest. → HISTORY.md#s-settled
- **Where the small pieces landed** (the last term of (2.59), `InSolution`,
`LogPressure`, `InitialTemperature`, the frozen sums): the table above and the code hold
it. → HISTORY.md#s-settled
- **One behaviour changed, deliberately and invisibly.** `DerivativeSystem` restores the
caller's condensed order before returning on every path, the singular one included;
nothing in the tree reads that order afterwards (`Solve` rebuilds the set from
`result.Moles` at every entry), so no result moves. → HISTORY.md#s-settled

## Shape exceptions

The rows below are this node's declared exceptions to the root's code-shape constraint,
in the form the protocol tests node reads; their reasons are decisions of `## Structure`.

| Where | Rule | Measured | Reason |
|---|---|---|---|
| `EquilibriumSolver` | efferent coupling | 24 | the composition root: `Solve` and `SolveFrozen` as the sequence of stage calls, the exit guards and the status write; holds no formula |
| `NewtonIteration` | efferent coupling | 20 | the Newton loop: the step and polish counts, the order of the stage calls, the status; holds no formula (the decision "The Newton loop holds no formula") |
| `EquilibriumScratch.EquilibriumScratch` | parameters | 16 | lists the slices of the batch-sized scratch buffers `API.md` publishes, one argument per slice; grouping them would move the contract and re-emit the kernels (the decision "The scratch descriptor keeps its constructor"); its one construction site names its arguments |

⚠ 2026-09-28: was the `EquilibriumScratch` row at 12 parameters, now 16 (rule A's tie
snapshot) → HISTORY.md#ce-scratch16

Every other type of the node measures 11 or below by the dependency check's walk, well
below the root's limit of 14: `DerivativeSystem` the highest of the rest at 11,
`CaseSetup`, `CondensedSet`, `ConvergenceTests` and `SingularRemedies` at 10.
→ HISTORY.md#ce-rest

⚠ 2026-09-28: was the rest "10 or below", now 11 (`DerivativeSystem`, rules A and B)
→ HISTORY.md#ce-rules-ab
⚠ 2026-09-28: was `DerivativeSystem` at 12, now 11 (no `ElementCoupling.Coupled` call)
→ HISTORY.md#ce-third-pass
⚠ 2026-09-26: was `NewtonIteration` at 17, now 18 (`NewtonLoopState`)
→ HISTORY.md#ce-newton-18
⚠ 2026-09-28: was `EquilibriumSolver` at 19 and `NewtonIteration` at 18, now 22 and 19
→ HISTORY.md#ce-roots-22
⚠ 2026-09-28: was the roots at 22 and 19, now 24 and 20, the rows above
→ HISTORY.md#ce-roots

## Acceptance criteria

- [x] 2026-09-12 — tp problems: for the product mixtures of the four reference
      propellants and the RP-1311 examples in the fixtures node, the mole fractions of
      every species the reference prints agree within the fixtures node's tolerance
      table; the list of compared species is generated from the fixture, not typed.
      `Equilibrium.Tests`, `FixtureSolveTests.AssignedTemperatureAndPressureReproducesTheReference`
      over the enumerated `cases/tp` directory (46 files): every state field the fixture
      carries and every listed species, within the table.
- [x] 2026-09-12 — hp problems: the adiabatic flame temperature and the composition of
      the same cases agree within the tolerance table.
      `FixtureSolveTests.AssignedEnthalpyAndPressureReproducesTheReference` over
      `cases/hp` (34 files).
- [x] 2026-09-12 — sp problems: the temperature and composition at given entropy and
      pressure agree within the tolerance table (the nozzle stations of the reference
      rocket cases serve as fixtures).
      `FixtureSolveTests.AssignedEntropyAndPressureReproducesTheReference` over
      `cases/sp` (26 files).
- [x] 2026-09-12 — Condensed species: the AP/binder/aluminium case includes `AL2O3(L)`
      in the chamber with the reference mole fraction (0.07645), and the low-temperature
      RP-1311 example 14 reproduces the reference phase changes.
      `CondensedSpeciesTests`:
      `TheAluminizedPropellantBurnsToLiquidAluminaInTheChamber`,
      `WaterCondensesBelowItsDewPointInTheLowTemperatureExample` (liquid at 300 to 304.3
      K, none from 305 K) and `TheCondensedSpeciesInTheSolutionAreThoseOfTheReference`
      over every fixture case with condensed candidates, `AL2O3(a)` at the AP/HTPB/Al
      exits included.
      ⚠ 2026-09-14: was a derived mass fraction asserted against a typed 1e-4, now
      removed → HISTORY.md#crit-condensed
- [x] 2026-09-12 — Derivatives: `Cp_eq`, `γ_s` and the sound speed agree with the
      reference within the tolerance table for every converged fixture case: part of
      the `FixtureSolveTests` comparison above (`cpEquilibrium`, `cvEquilibrium`,
      `gammaS`, `dlnVdlnT`, `dlnVdlnP`, `soundSpeed` for all 106 cases).
- [x] 2026-09-12 — Element conservation holds for every converged fixture case at
      the invariant's tolerance:
      `ElementConservationTests.ElementsAreConservedAtTheInvariantTolerance` over
      the machine-generated list of the 106 tp, hp and sp cases.
- [x] 2026-09-12 — A case with an absent element gives the same result as the same case
      solved on a table without that element's species, bit for bit on the same
      accelerator: `AbsentElementTests.AZeroAbundanceEqualsATableWithoutTheElement`; an
      empty table or every abundance zero returns `InvalidInput` and writes nothing
      else: `InvalidInputTests`. → HISTORY.md#crit-absent
- [x] 2026-09-12 — The solve runs unchanged inside an ILGPU kernel on the CPU
      accelerator with the same results as the host call:
      `KernelEqualityTests.KernelAndHostGiveTheSameBits` over the 8 table families
      of the 106 cases (moles, multipliers, state, status and iterations bit for bit).
- [x] 2026-09-13 — Plateau states converge and match the reference: the melting-plateau
      fixture cases (RP-1311 example 13, the direct plateau stations of AP/HTPB/Al, the
      latent-heat-band hp cases) return `Ok` with both records of the pair in the
      solution at the pair's `T*`, every compared field (`γ_s`, the sound speed and the
      plateau zeros included) within the tolerance table: `FixtureSolveTests` over every
      tp and hp file, `Performance.Tests.RocketFixtureTests` and
      `Problems.Tests.RocketTests.TheRocketCaseReproducesTheReferenceEndToEnd` over
      `rp1311-example13` and the eight `ap-htpb-al-plateau` rocket files.
      → HISTORY.md#crit-plateau
- [x] 2026-09-13 — The anti-cycling rule closes the include/remove cycle: an assigned
      enthalpy inside the `ALN(L)` gap of the fuel-rich AP/HTPB/Al chamber converges
      onto the pinned pieces
      (`PlateauTests.AnEnthalpyInsideTheALNGapPinsThePiecesAtTheCut`); a record removed
      for range re-enters when it is the only positive candidate, a second escape stands
      it down, and an `Ok` exit never hides a positive-gain candidate
      (`PlateauTests.AnEnthalpyNoAdmissibleSetCanHoldIsRefusedRatherThanLiedAbout`,
      `AnOkSolutionLeavesNoCondensedCandidateWithPositiveInclusionGain` over every hp
      fixture). → HISTORY.md#crit-anti-cycling
- [x] 2026-09-13 — Sweeps across a plateau lose no station: the pressure-ratio band
      across the AL2O3 plateau solves sequentially and one exit at a time onto the same
      stations, on the chamber isentrope
      (`Problems.Tests.SplitRecordTests.ASweepAcrossTheAluminaPlateauStaysOnTheIsentropeByEitherPath`);
      example 13's four exits cross the BeO plateau end to end
      (`RocketTests.TheRocketCaseReproducesTheReferenceEndToEnd` over
      `rp1311-example13`); tp solves at the printed bounds pick the reference's record
      (`FixtureSolveTests` over `ap-htpb-al-plateau_T2327`,
      `rp1311-example13-mixture_T2851` and `_T2373`).
      ⚠ 2026-09-13: was fine sweeps of both plateaus from both starts, now the
      eight-ratio band and the four-exit example → HISTORY.md#crit-sweeps
- [x] 2026-09-14 - The decomposition of `## Structure` is in place and changed no
      number. Shape: `ShapeTests`, all ten facts green at `62cd99e`. Surface:
      `Protocol.Tests.SurfaceTests` against
      `tests/Protocol.Tests/PublicSurface.approved.txt`, untouched, every new type
      internal. Numbers: `BitSnapshotTests.EveryFixtureCaseGivesTheRecordedBits` against
      `Bits.approved.txt`, recorded from `8e36a27` before the first line moved, unmoved
      after the last; `KernelEqualityTests` green; the fast suite green after each of
      the six extraction steps (2142 tests that day). This node's evidence is of the CPU
      accelerator.
      ⚠ 2026-09-15: was `Converge` at 56 lines and no shape exception, now `ShapeTests`
      → HISTORY.md#crit-decomposition
- [x] 2026-09-14 - The rules found written twice exist once each: the inclusion gain of
      section 3.4 (`CondensedSet.InclusionGain`), the element abundance
      (`ElementBalance.Abundance`), the trace retention (`Composition.Retain`), the
      state record (`MixtureProperties.Common`); every number of the report is a named
      constant in the stage that uses it. Checked by reading at the close of the
      decomposition; the bit snapshot proves the reading moved no number.
      → HISTORY.md#crit-once-each
- [x] 2026-09-14 — The node decodes none of `Thermo`'s interval layout (F-AR-01): no
      `IntervalStart`, `IntervalCount` or `IntervalBounds` in `src/Equilibrium/*.cs`
      (grep empty), the record bounds asked of `SpeciesFunctions.RecordLow` and
      `RecordHigh`; `BitSnapshotTests.EveryFixtureCaseGivesTheRecordedBits` unmoved. Red
      once: `RecordHigh` returning the lower bound turned 29 fixture cases red.
      → HISTORY.md#crit-interval
- [x] 2026-09-14 — The Newton loop holds no formula (`## Structure`): `NewtonIteration`,
      `DampedStep`, `ConvergenceTests` and `SingularRemedies` as the table says, each
      within the root's code shape, `NewtonIteration` named the second composition root;
      `BitSnapshotTests.EveryFixtureCaseGivesTheRecordedBits` unchanged and
      `KernelEqualityTests` green in the same run. → HISTORY.md#crit-newton
- [x] 2026-09-14 — Every creation of `EquilibriumScratch` in the tree names its
      arguments (the decision "The scratch descriptor keeps its constructor"): the one
      site, `EquilibriumScratch.Slice`, names every argument;
      `ShapeTests.EveryWideConstructorIsCalledWithNamedArguments` holds it;
      `Bits.approved.txt` unchanged. → HISTORY.md#crit-named
- [x] 2026-09-15 — A record stood down by the anti-cycling rule stays out of play "for
      the rest of it": `PhaseGeometry.Adjacent` and `PhaseGeometry.PhaseAt` test
      `!SpeciesMarks.InPlay(scratch, k)`, not the raw `scratch.SpeciesActive[k] == 0`
      (R-Equilibrium-1); no `SpeciesActive[` remains outside `SpeciesMarks.Of` and
      `.Set`. Red before the fix, green after:
      `PlateauTests.AStoodDownRecordIsNeitherAdjacentToNorFoundBesideItsInPlayPartner`;
      `Bits.approved.txt` unchanged. → HISTORY.md#crit-stood-down
- [x] 2026-09-15 — Every ticked criterion above re-verified on the decomposed and
      repaired code at `62cd99e`: its tests green in the full suite
      (`APTHERMO_NO_CUDA=1`, every category, 3037 tests, none skipped), and
      CUDA-category evidence on the reference machine (`tests/Execution.Tests`, 41,
      and the long-running sweep and throughput tests).
- [x] 2026-09-26 — The audit's findings 1 to 5 and the open-below rule (the ⚠ notes of
      this date under Constraints, and the Thermo node's criterion of the same date for
      finding 1).
      - **Below 300 K and below 200 K.** Fixtures computed by cea 3.3.4, covered by
        `Problems.Tests.EquilibriumTests.AssignedTemperatureCasesReproduceTheReference`
        through its directory listing: Si and Li in argon at 298.15 to 301 K; H2/O2 at
        O/F 4 and 1 bar at 165, 180, 190 and 199 K, red on the unpatched open-below
        rule.
      - **The full set.** The audit's 17-element case at 350 K is `Ok` with every stable
        phase
        (`tests/Fixtures/cases/tp/seventeen-elements-many-condensed-phases_T350.json`,
        the same reference test);
        `PlateauTests.AnOkSolutionLeavesNoCondensedCandidateWithPositiveInclusionGain`
        runs over every tp, hp and sp fixture; the exit guard red once with
        `MaxCondensedInSolution` back at 8.
      - **Warm starts.**
        `WarmStartTests.AWarmSolveAtHalfPressureAgreesWithAColdSolveAtThatPressure` over
        every tp fixture that converges
        (`ANamedFixtureCompletesTheFullWarmStartComparison` pins one by name), and
        `TheAuditsExactCasesWarmStartOkAndAgreeWithAFreshColdSolve` for the audit's
        three probe cases; the four `rp1311-example14` cases end `Ok` through the
        fallback to the cold start of 2026-09-27, `SingularMatrix` with it disabled.
      - **The bookkeeping.** `NewtonLoopStateTests`:
        `ANotConvergedVerdictClearsTheConvergedMarkAndThePolishCount`,
        `ReportTestsMetCountsAPolishStepAndPolishedDoesNotCountAnother`,
        `ASpeciesCrossingTheTraceThresholdDuringTheStepIsReportedAsACrossing`,
        `NoCrossingWhenEveryGasSpeciesKeepsItsSideOfTheTraceThreshold`,
        `ASingularRemovalOfACondensedSpeciesRestartsTheStepCountAndCountsTowardTheChangeCap`,
        `RecordSetChangeResetsTheStepCount`, each red once against the line it guards.
      - **Frozen mode.**
        `InvalidInputTests.FrozenModeRejectsAnInvalidMoleNumberGaseousOrCondensed` (NaN,
        infinite, negative; gaseous and condensed; tp, hp, sp).
      - **Bits.** No `Bits.approved.txt` of `Equilibrium`, `Thermo` or `Problems` moves
        on an existing case.
      Evidence: `APTHERMO_NO_CUDA=1 dotnet test APThermo.sln --filter
      "Category!=LongRunning"` 4278/4278, none skipped (`Equilibrium.Tests` 695);
      `dotnet test tests/Execution.Tests -c Release` 126/126 on CUDA; the protocol lint
      0 errors/0 warnings.
      ⚠ 2026-09-26: was the warm-start comparison named the fixtures node's polish tier,
      now `Tolerances.SelfConsistency` → HISTORY.md#crit-audit-2026-09-26
      ⚠ 2026-09-26: was the audit's probe parameters "not recorded", now its three exact
      cases reproduced → HISTORY.md#crit-audit-2026-09-26
      ⚠ 2026-09-26: was four `rp1311-example14` warm starts left open as a plateau-pair
      defect, now the cold-start fallback → HISTORY.md#crit-audit-2026-09-26
      ⚠ 2026-09-27: was that explanation (a seeded plateau pair), now a seeded liquid
      above its saturation pressure → HISTORY.md#crit-audit-2026-09-26
- [x] 2026-09-30 — The second hidden-defect audit of 2026-09-28 (Thermo and Equilibrium,
      findings F1 to F5, and the guards part's O8 and F9) is closed by the rules of that
      date under Constraints.
      Evidence at `9284418` and after, on the reference machine: `dotnet build
      APThermo.sln` 0 warnings, 0 errors; the fast suite green on Windows and under WSL2
      (Equilibrium 944 of 944 under WSL2); the protocol lint 0 and 0; `dotnet test
      tests/Execution.Tests -c Release` on CUDA 171 of 171 on Windows and 170 of 170
      under WSL2; the release job's filter green on Windows in Release. Known and
      outside the criterion, by the owner's decision of 2026-09-28: the three classes at
      the end of the rules A and B criterion below, for 0.2.1.
      ⚠ 2026-09-30: was the criterion unticked with an "Open" list, now ticked, the list
      closed by rules A and B → HISTORY.md#crit-second-audit
      - **F1, the two-stage threshold.** Fixtures from cea 3.3.4 through the fixtures
        node's generator, covered by `AssignedTemperatureCasesReproduceTheReference`,
        each red at `5a732f0`:
        `tests/Fixtures/cases/tp/rp1311-example5_T300_p1bar.json`,
        `..._T300_p70bar.json`, `..._T305_p10bar.json`
        (`tests/Fixtures/generate/retention_threshold.py`); a unit fact on the loop's
        struct (the switch counts as a change of the retained set);
        `tests/Equilibrium.Tests/RegressionStateTests.cs`:
        `TheAuditsRegressionStateConvergesAndHoldsTheEquilibriumConditions` (sixteen
        states, red at `5a732f0`) and
        `TheAuditsElevenExample5StatesThatFailedAtBothCommitsAreOkNow`.
      - **F5.** `tests/Equilibrium.Tests/DenseSolverTests.cs`:
        `TheFailedRowOverloadNamesTheRowWhosePivotVanished`,
        `TheFailedRowOverloadReportsNoFailureOnARegularMatrix`;
        `NewtonLoopStateTests.ASingularRemovalOfACondensedSpeciesRestartsTheStepCountAndCountsTowardTheChangeCap`.
      - **F2.** A tp of the `h2-o2-of4` table at 1 bar, 60 K to 159 K:
        `TemperatureOutOfRange`; 160 K and above `Ok` with finite positive Cp, Cv, `γ_s`
        and sound speed; the fixtures at 165–199 K stay green; a fact over every `Ok` of
        the fixtures and of the audit's grids, the pinned pair's zeros excepted.
      - **F3.** `WarmStartTests` extended to P/10, P/2 and T×1.1 over the fixture tables
        at 300 K and 600 K; the step-cap trigger has a fact of its own (guards F9); the
        hp and sp cold retry starts at 3 800 K.
      - **F4.** `InvalidInputTests`: frozen tp at +∞, NaN and 1e-300 K, and `Solve` tp
        at +∞.
      - **O8.** `ElementBalance.WithinInvariant` reads a NaN abundance as outside the
        invariant; a fact drives the method itself.
      - **Bits.** Moved by the two-stage threshold in this node and in `Performance`,
        `Transport`, `Problems` and `Cli`; re-approved on the owner's decision of
        2026-09-28 with the field-by-field report of the largest relative change per
        field, every CEA tolerance test green; `API.md` states the changes.
        → HISTORY.md#crit-second-audit
- [x] 2026-09-28 — Rules A and B (Constraints, the orchestrator's investigation 6 of
      2026-09-28) close the open items of the criterion above.
      - [x] 2026-09-28 — **Fixtures** through the fixtures node's generator
        (`tests/Fixtures/generate/retention_threshold.py`):
        `tests/Fixtures/cases/tp/naclo4_T500.json`, `naclo4_T800.json`,
        `kclo4_T500.json`, `kclo4_T800.json`, `ap-htpb-al_pc7MPa_T430.json`,
        `ap-htpb-al_pc1MPa_T420.json`; all six red without rules A and B (the two blocks
        of `SingularRemedies.Recover` disabled in turn) and green with them.
      - [x] 2026-09-28 — **Unit facts** in `tests/Equilibrium.Tests`:
        `SingularRemedyRulesTests.cs` (`ElementCoupling.Find`/`Coupled`,
        `CondensedDependency.LeavingPosition`) and
        `WarmStartTests.AWarmStartFromExample5sTenBarSolutionTiesNAndClThroughNH4CLAndEqualsItsColdSolve`.
      - [x] 2026-09-28 — **The scans** as measurements, recorded, not asserted: the AP
        scan 850/850 `Ok`; the audit's salt scan 1436 `Ok`, 16 `NotConverged`, 0
        `SingularMatrix` against 332 and 122 of 1452 before; the fuzz of 40 985 solves
        with no equilibrium-condition violation.
      - [x] 2026-09-28 — **No bit snapshot moves on an existing fixture**
        (`BitSnapshotTests` of `Equilibrium.Tests`, `Thermo.Tests`, `Problems.Tests`:
        six added lines, no existing hash moved).
      - [x] 2026-09-28 — **Shape.** No method over 6 parameters; the declared Ce rows
        are re-measured.
      - [x] 2026-09-28 — `API.md`'s `SingularMatrix` sentence lists the remedies, with a
        ⚠.
      Open and known, outside this criterion; the owner decided on 2026-09-28 that they
      do not block 0.2.0 (`CHANGELOG.md`), designed and fixed for 0.2.1:
      - **The threshold flip.** Two carriers cross the threshold alternately every
        step, so the polish never completes: KClO4 at 610–680 K, NaClO4 at 490–500 K,
        16 salt-scan states `NotConverged`. The matrix is never singular.
        ⚠ 2026-09-28: was the threshold flip a band of two perchlorate compositions, now
        a mechanism of the all-gas first stage (29 of 968 states)
        → HISTORY.md#crit-rules-ab
      - **The three-element coupling.** With only CO2, H2O and N2 retained, row O
        equals 2·C + ½·H. 77 fuzz tp states on example 1 and example 12 tables at
        300 K and 600 K end `SingularMatrix`, which a pair tie cannot express.
      - **The reaction plateau.** hp inside the Al(OH)3/Al2O3/H2O(L) reaction plateau
        (T* = 415.948 K, 157 kJ/kg wide at 7 MPa) ends `SingularMatrix` in the
        derivative system: the pinned-pair convention covers two records of one
        formula only.
- [x] 2026-09-29 — The third audit pass of 2026-09-28 (part 1: findings F1 to F3,
      observation O1) is closed by the rules of that date.
      - **F1, the way back from a release.**
        `tests/Equilibrium.Tests/TiedReleaseTests.cs`,
        `TheReleasedTieRestoresAndClosesOk`: the six named salt states `Ok` and clear of
        every independent equilibrium condition (`EquilibriumConditions.Violations`),
        red at `c02e14d`; the audit's salt sweep, cold `NotConverged` 36 before and 30
        after, the thirty the declared threshold flip, zero violations.
      - **F2, the fallback covers the close.**
        `WarmStartTests.AFailureFoundAtTheCloseRetriesFromTheColdStart`, red at
        `c02e14d`.
      - **F3, no state on failure.**
        `tests/Equilibrium.Tests/MixturePropertiesTests.cs`, three facts: the state
        guard decides before `State` is written.
      - **O1, the hp/sp estimate.**
        `InvalidInputTests.AGivenHpOrSpEstimateThatIsNotFiniteAndPositiveIsInvalidInput`
        and `AZeroHpOrSpEstimateStaysTheNoEstimateSentinel`; a tp temperature follows
        the same rule with no sentinel.
      - **Bits.** No `Bits*.approved.txt` or `Throughput*.approved.txt` differs from
        `main`.
      Evidence: `dotnet build APThermo.sln` 0 warnings, 0 errors; the protocol lint 0
      and 0; `APTHERMO_NO_CUDA=1 dotnet test APThermo.sln --filter
      "Category!=LongRunning"` run project by project, 5243 of 5243, none skipped.
      `Cli.Tests` and `Docs.Tests` were not run to completion that pass: neither node's
      code, fixtures or approved output is touched, and `Problems.Tests`' own
      bit-for-bit fact is unchanged; left for the next session that touches this node to
      confirm.
      ⚠ 2026-09-28: was an hp/sp estimate of 0 refused, now a given nonzero estimate
      refused, 0 the sentinel → HISTORY.md#crit-third-pass

## Taboos

- No equilibrium constants, no reaction sets, no hand-picked species subsets: the
  root forbids them and they would make the results unreviewable.
- No deletion of species from the table; no reallocation of anything during a solve.
- No `float`, no exceptions, no allocations, no virtual calls: kernel code.
- No knowledge of nozzles, chambers or rockets: `Performance` owns those iterations.
- No transport formulas: `Transport` owns them and only takes this node's outputs.
