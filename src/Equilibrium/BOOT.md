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

⚠ 2026-09-15: this paragraph, the "Property definitions" bullet of the Constraints
below and the `## Structure` row of `DerivativeSystem` all read "section 2.6" for the
derivatives the matrix solutions produce. RP-1311's section 2.5, "Thermodynamic
Derivatives From Matrix Solutions", holds the system (2.56)–(2.58), cp by (2.59) and
the pressure system (2.64)–(2.66); section 2.6, "Other Thermodynamic Derivatives",
holds cv, γ_s (2.71, 2.73) and the sound speed (2.74) — figures read off the converged
state, not solved for. `API.md` and the solver's own summary (`EquilibriumSolver.cs`),
which already said 2.5, disagreed with them; the repair review found the disagreement
and the design session checked both sections against the report. Corrected at every
place named above.

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

  ⚠ 2026-09-12: the first draft of this document, written the same day, made a zero
  abundance `InvalidInput`. Wrong because the batch inputs of the front door come from
  other simulations as element abundances per kilogram, where an element is often
  absent from some records of one batch; refusing them would force one table per
  record and defeat batching.
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

  ⚠ 2026-09-26: stood "at most 8 condensed species … a matrix of at most 30 × 30". The
  limit sized the scratch and was below what a table of 20 elements can require. A
  full set made the inclusion test return "no change", and the case closed `Ok` with
  stable phases missing. The audit's case: 17 elements at 350 K, with `BaO(cr)`,
  `CuO(cr)`, `KCL(cr)` and `NaCL(cr)` left out at gains from +52 to +221, and Ba and Cu
  vapour at x = 0.0032. Found by the hidden-defect audit of 2026-09-26 (finding 2).
  The scratch grows by at most `(42² − 30²) + 2·12` doubles and 12 ints per case, at
  20 elements. The budget of set changes grows with the slots (`MaxCondensedSetChanges`
  is three per slot, as before).
  The dense solve is Gaussian elimination with scaled partial pivoting, internal to
  this node (visible to its tests node only).
- The reduced equations are those of RP-1311 tables 2.1 and 2.2 with the gaseous
  corrections of equation (2.18) substituted. For sp the temperature row weighs a
  gaseous species by its entropy in the mixture, `S_j°/R − ln(n_j/n) − ln(p/p°)`, and
  a condensed one by `S_j°/R`; its right-hand side is `s₀/R − s/R + n − Σ n_j`, the
  total-moles equation being absorbed. The data are for 1 bar, so `p°` is `1e5 Pa`.
- Initial estimates as in the report: every gaseous species at `0.1 / (active gaseous
  species)` kmol per kg with `n = 0.1` when no estimate is given, `T = 3800 K` for hp
  and sp when no estimate is given; callers may pass a previous solution as the
  estimate (the nozzle does).

  A warm start that fails, with any status other than `InvalidInput`, falls back once
  to the cold start of section 3.1, with the iterations of both attempts counted in the
  case's total (2026-09-28). A cold start never falls back. The cold start takes no part
  of the seed: for hp and sp it starts at 3 800 K, not at the previous solution's
  temperature, since that temperature is part of the seed.

  ⚠ 2026-09-28: the fallback of 2026-09-27 (the acceptance criteria, the warm-start
  criterion) fired only when a seeded condensed species was still negative at the
  failure. A warm start also fails in two other ways the sign test does not see: the
  iterate diverges with the seeded liquid positive (`ln n` growing by the damping cap to
  the step cap, the liquid at thousands of kmol/kg), or the liquid goes negative and
  comes back positive before the cap. The audit counted 29 warm tp restarts at P/10,
  P/2 and T×1.1 of fixture tables at 300 K and 600 K ending `NotConverged` where a
  fresh cold solve is `Ok` in 12 to 50 steps; retrying on any failure recovered all 29
  and moved no other count. Its observation O3: the "cold" retry of an hp or sp kept
  the caller's temperature estimate, which for a warm start is the previous
  solution's. Found by the second hidden-defect audit of 2026-09-28 (Thermo and
  Equilibrium, finding F3 and observation O3).
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

  ⚠ 2026-09-28: stood "The report is unchanged: a gaseous species below 1e-8 of the gas
  is reported with zero moles, a step applied to the final state only and nowhere in
  the iteration." Implemented literally, as a `Composition.Retain` call at 1e-8 added
  after `Close` had already computed the sums, derivatives and state from the
  second-stage (1e-11) composition, it zeroed every trace species between 1e-11 and
  1e-8 out of the *reported* moles only, while the sums, derivatives and mixture state
  above them stayed the ones the finer composition produced. The two were then
  inconsistent with each other: `ElementConservationTests` failed on 27 fixtures with
  residuals of 9e-11 to 1.5e-9 (kmol/kg), matching the zeroed species' own mass, while
  `ElementBalance.WithinInvariant` (evaluated on the pre-zeroing composition inside
  `Close`, which is what an `Ok` status actually gates) never flagged the same cases,
  and no fixture's `CaseStatus` or CEA-comparison result moved. A separate report step
  the element-conservation invariant does not itself cover is a defect of the step, not
  of the invariant's tolerance: the taboo against loosening a tolerance forbids
  widening `ElementConservationTests`' 1e-12 to hide it. The report now stands for
  whatever the last `Composition.Refresh` produced, with no separate zeroing step;
  since that call is always the case's own active (second-stage, for any `Ok`)
  threshold, a fixture's reported trace composition can only move toward the finer
  value already used for its status and its derivatives, matching the reference (which
  reports at the same threshold it converges to, to the printed digits). Found while
  implementing this paragraph, second hidden-defect audit of 2026-09-28.

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

  ⚠ 2026-09-26: the verdict covered only the gases retained at the start of the step.
  The mark and the polish count were never cleared, and the singular remedy's removal
  did not restart the step count, although this bullet counts steps "after the last
  change of the condensed species set". Found by the hidden-defect audit of 2026-09-26
  (findings 3 and 4):
  - A warm start from a converged solution at half the pressure ended `NotConverged`
    after one step. On the `rp1311-example1` table at 1000 K, atomic H crossed the
    threshold during an already-polished step, and the element guard then rejected
    the state by exactly its 5.76e-10 kmol/kg. The case solves `Ok` from a cold start.
  - A fixture case at tp 300 K (`rp1311-example5`'s table) passed the tests at step
    19, lost them at 24 to a vanishing HCL, and exited unpolished when they passed
    again at 38.

  `API.md`'s sentence that a trace species' logarithm "stays in the scratch for the
  next estimate" was true, but the next estimate never read it; it is corrected there.

  ⚠ 2026-09-28: stood "trace threshold `ln(n_j/n) = −18.420681` … as in the report",
  one threshold for the whole solve. Together with the two rules above it turned
  converging states into failures. In ammonium perchlorate products below about 350 K,
  NH4CL(II) holds all of the N and Cl, leaving HCL, NH3 and N2 between 1e-11 and 2e-7,
  on the threshold:
  - retaining one carrier pushes another across 1e-8, so every step is a crossing and
    the crossing rule overrides every passing verdict to the step cap (RP-1311
    example 5 at 300 K, 1 bar);
  - when the last carrier drops out of the sums, the N and Cl rows see only
    NH4CL(II), the matrix is singular, the resets re-seed the carriers, and the
    cleared polish count lets them fall back through the threshold, until the change
    cap (AP/HTPB/Al at 7 MPa, 300 K, up to 1 907 steps).

  16 tp states that were `Ok` at `9c33398` ended `NotConverged`; for example 5 the old
  answer matches cea 3.3.4 to five digits. The reference retains down to 1e-11 after
  its first convergence, where no carrier sits; the audit's clone with only that
  change converged all 16 and 11 older failures, matched cea to the printed digits,
  and lost no `Ok` in its fuzz. Found by the second hidden-defect audit of 2026-09-28
  (Thermo and Equilibrium, finding F1); the two-stage rule was checked against
  `equilibrium.f90` by the orchestrator.

  ⚠ 2026-09-14: stood "at most 10 changes of that set per case". The plateau rules of
  2026-09-13 need up to three changes per slot, and the code's constant became
  `3 * MaxCondensedInSolution` that day while this sentence kept the old number;
  found by the clean-code review of 2026-09-14 (AGENTS.md §8: a number repeating a
  constant diverges at the constant's first change, so the document now names the
  constant).

  ⚠ 2026-09-12: the report's wording of equation (3.1) does not say that only growing
  species enter the maximum. Read symmetrically, the nozzle exits of NTO/UDMH and
  AP/HTPB/Al and every sp case of LOX/RP-1 spent their 50 steps at `λ ≈ 0.02–0.05`,
  dozens of hydrocarbons shrinking by e⁻⁴⁰ two units at a time. CEA's code limits only
  positive corrections (a species on its way out may fall by any factor in one step);
  so does this node, and the same cases converge in 24 to 44 steps.
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
  In the committed file this is `H2O(cr)` alone. Measured the same day, a tp of
  H2/O2 (O/F 4) at 1 bar:
  - cea 3.3.4 holds ice at 165, 180, 190 and 199 K, with vapour below 1e-6;
  - this node reported supersaturated vapour as `Ok`, −12.5 MJ/kg against −14.9 with
    ice.

  ⚠ 2026-09-26: the rule was missing. The hidden-defect audit did not find it; it came
  up while its finding 1 was being checked against the same line of the reference.

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

  ⚠ 2026-09-28: stood "at any temperature the solver allows below its range". A tp has
  no iterate window, so ice was held at any temperature. The `H2O(cr)` fit evaluated
  below its 200 K bound gives `Cp°/R` −3.53 at 100 K and −44.5 at 60 K. With ice in the
  solution a tp returned `Ok` with negative mixture Cp and Cv at 100 K and below, and
  `γ_s < 0` with a NaN sound speed at 102.6 and 105 K. cea 3.3.4 returns "not
  converged" at 60 to 150 K by the window above, whose floor the rule did not know.
  Found by the second hidden-defect audit of 2026-09-28 (Thermo and Equilibrium,
  finding F2); the window checked against `equilibrium.f90` by the orchestrator.

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

  ⚠ 2026-09-28: stood "removing the last condensed species". On the products of
  AP/HTPB/Al at 420–450 K and 1–7 MPa, `H2O(L)` is included beside `AL2O3(a)` and
  `AL(OH)3(a)`, which are linearly dependent with it (2 Al(OH)3 = Al2O3 + 3 H2O). The
  matrix is singular, the remedy removes `H2O(L)`, and the next inclusion puts it back,
  every 9 steps, until the change cap: `NotConverged` in 8 states where cea 3.3.4
  converges (7 MPa, 430 K: AL2O3(a), C(gr), H2O(L), NH4CL(II), no AL(OH)3(a)). The same
  last-species rule removed `C(gr)`, which had nothing to do with the dependency, in
  the singular cycle of the ⚠ above. Present since the first version; found by the
  second hidden-defect audit of 2026-09-28 (Thermo and Equilibrium, finding F5).

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
    - **Derivatives.** A tie that survives to the close gives the derivative system a
      unit row that fixes the tied element's multiplier derivative at 0.

  The tie is per-case state carried in `IterationState` (`ElementTie`: active, element,
  partner, ratio, released). `ElementCoupling` holds the read-only queries,
  `CondensedDependency` rule B's tests, and `IterationMatrix` stays the only writer of
  Newton rows. `SingularRemedies.Recover` tries rule B, then rule A, then the resets,
  then the targeted removal.

  ⚠ 2026-09-28, the same day: the targeted removal above did not close the AP/HTPB/Al
  states it was written for. At 7 MPa and 430 K, `H2O(L)` enters with gain +0.2105
  beside `AL2O3(a)` and `AL(OH)3(a)`, and the matrix fails on its row. The removal takes
  the smallest species sharing an element with it, `AL2O3(a)`, which is a product of the
  favourable reaction. It does so twice, stands it down, and settles on
  {AL(OH)3(a), C(gr), NH4CL(II)}. That set's Gibbs energy is 64.8 kJ/kg above cea's at
  430 K and 57.9 above at 420 K, and the exit guard rightly returned `NotConverged`.
  cea's state is the equilibrium: `AL(OH)3(a)` gains −0.3158 there, stable only below
  415.948 K. Separately, NaClO4 and KClO4 (Na or K : Cl : O = 1:1:4, tp at 500 and
  800 K) ended `SingularMatrix` in the all-gas trial. The polish drove the last
  alkali carrier without Cl, `Na2O2`/`K2O2`, across the threshold, so the alkali and
  Cl rows became identical, with no condensed species to remove. cea never meets a
  singular matrix there. The last carrier sits at e^−30 to e^−70 at equilibrium, and a
  switch of the threshold before the polish only moves the crossing (measured). Both
  rules measured on a prototype:
  - all six states `Ok`, within 7e-9 of cea;
  - the audit's salt scan went from 1 132 to 1 436 `Ok` of 1 452, with no
    `SingularMatrix` left;
  - the AP scan went from 842 to 850 of 850;
  - warm-start failures in the fuzz went from 13 to 0, with 6 warm/cold disagreements
    at trace level, all inside `WarmStartTests`' tolerance;
  - no committed fixture reaches either rule, so no bit moves.

  A bare widening of the threshold to 80, the reference's remedy, fixed none of the six
  states and added hundreds of warm/cold disagreements; it stays rejected.

  ⚠ 2026-09-13: until this date the rule read "a condensed species outside its
  temperature range is not a candidate at that temperature … when the temperature is
  a variable and the range is missed by less than 50 K both records stay, the
  temperature settles at the transition and the record that turns negative is
  removed after the next convergence". Wrong three ways, found by tracing the
  published verification cases and verified against a scratchpad prototype and
  cea 3.3.4 (sessions of 2026-09-13): the pair settles at `T*`, not at the printed
  bound, and the exact range test removed the returning record at every convergence,
  so every state on a melting plateau ended `NotConverged` (RP-1311 example 13's
  throat at BeO's 2851 K; the AP/Al verification record's exits on AL2O3's 2327 K
  plateau); a state that overshoots a transition by more than the window switched
  records forever instead of pairing (the same throat search, 2794 ↔ 3112 K), hence
  the switch memory, which the reference's code keeps too; and the inclusion test
  could re-add a record just removed for its range forever (AL4C3(cr), whose range
  ends at 2500 K with no record above), hence the anti-cycling rule. The reference
  avoids the last cycle by ranking inclusion per unit mass and by letting a record
  live up to 1.2 × its upper bound — an evaluation of the fit outside its range this
  node does not copy. With these rules the prototype converged every lost case and
  matched the reference's direct solves (plateau `Isp` to 0.001 m/s); the
  reference's own multi-station rocket runs stay unreliable past a plateau (the
  fixtures node records the guard).
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

  ⚠ 2026-09-28: only the mole numbers were validated. A frozen tp at +∞ or 1e-300 K
  returned `Ok` with h, s, g, Cp, Cv, `γ_s` and the sound speed all NaN, and at 1e6 K
  `Ok` with Cp −1.89e12 J/(kg·K). No caller of the tree passes a frozen tp, so nothing
  reached it. Found by the second hidden-defect audit of 2026-09-28 (Thermo and
  Equilibrium, finding F4). `Solve`'s tp check (`CaseSetup`) had the same gap and ended
  in `SingularMatrix`; it now answers `InvalidInput` for a non-finite temperature too.

  ⚠ 2026-09-26: only the gaseous mole numbers were checked. A NaN condensed mole number
  gave a tp `Ok` with h, cp and MW all NaN, and −0.01 kmol/kg of `H2O(L)` gave `Ok`
  with MW 24.39. Found by the hidden-defect audit of 2026-09-26 (finding 5). The only
  in-tree caller passes an `Ok` composition, so no result of the tree moved. Its state carries
  `CpEquilibrium = CpFrozen`, `CvEquilibrium = CvFrozen`, the derivatives 1 and −1 and
  `γ_s = Cp/Cv`.
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

⚠ 2026-09-15 (distribution phase): the Visibility column read "public" for
`EquilibriumSolver` and `DenseSolver`, and `API.md` published `EquilibriumProblem`,
`EquilibriumScratch`, `EquilibriumResult` and `ScratchLayout` too. The API review of
that day (fixed in `4344652`) found no consumer scenario for any of the
six: every use is a neighbour numerical node composing the kernel layer, or this
node's own tests, and `DenseSolver`'s public status also clashed with `Problems`'
same-named `EquilibriumProblem` (CS0104). All six became `internal`, with
`InternalsVisibleTo` grants to `Performance`, `Transport`, `Execution` and their
mirroring test nodes (`APThermo.Equilibrium.csproj`; `API.md`'s tree-contract section
lists them); `ProblemKind` stays public.

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
- **The record bounds are asked of `Thermo`** (added 2026-09-14, after `Thermo`'s
  range questions were merged; the architecture review's F-AR-01).
  `PhaseGeometry.RecordLow` and `RecordHigh` decode `Thermo`'s interval layout a
  second time (`IntervalStart`, `IntervalCount`, `IntervalBounds` and its stride of
  two), and `Thermo` now answers the same two questions, kernel-compatible, as
  `SpeciesFunctions.RecordLow` and `RecordHigh` (its `API.md`, range questions).
  `PhaseGeometry` asks those and keeps no copy of the arithmetic, and no stage of
  this node reads the three layout arrays. The bounds are table reads, not computed
  values, so the tests node's bit snapshot may not move.
- **The Newton loop holds no formula** (added 2026-09-14, after the efferent coupling
  was measured by the dependency check's walk: `NewtonIteration` 16 against the root's
  recalibrated limit of 14). `NewtonIteration` keeps `Converge`: the step and polish
  counts, the order of the calls, the status. What it computes moves to three stages
  named after the sections of RP-1311 chapter 3 they implement: `DampedStep`, the
  multipliers and the gaseous corrections of equation (2.18), the control factor of
  (3.1)–(3.3) and its application (3.4) with the temperature window (today
  `ControlFactor` and `Apply`); `ConvergenceTests`, equation (3.5) on the undamped
  corrections, (3.6) on Δln T with the element balance, and the polish test, as one
  verdict the loop reads (today `Worst` and the conditions inside `Converge`);
  `SingularRemedies`, the remedies of section 3.6 (today `Recover`). Each named
  constant moves with the stage that uses it; the polish-step cap stays with the loop.
  The loop's coupling is the width of the data it carries (the table, the problem, the
  scratch, the result, the iteration state, the sums, the layout) and of the stages it
  calls, which no split removes: should it stay above the limit, `NewtonIteration` is
  this node's second composition root, its measured figure written into its row. Every
  expression keeps its form and its order of evaluation, so the tests node's bit
  snapshot may not move.
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
- **The mark accessors moved to `SpeciesMarks`** (2026-09-15, the repair review's
  R-Equilibrium-6). `CaseSetup` is "what a case needs before its first Newton step",
  but its mark accessors were used by every stage of the iteration, and `CondensedSet`
  wrote through them too (`StandDown`): the type's name covered one job and did
  another. `Of`, `Set` and `InPlay` (the renamed `Mark`, `Mark` and `InPlay`) now sit
  in `Carriers.cs` beside `SpeciesMark`; `CaseSetup` keeps the input validation, the
  initial marks and the two reductions of the input. Measured by the dependency
  check's walk: `CaseSetup` 9 → 10 (it now names `SpeciesMarks` where it used to name
  only itself); every other caller (`DampedStep`, `Composition`, `CondensedSet`,
  `SingularRemedies`) unchanged, since a call to `CaseSetup` became a call to
  `SpeciesMarks` in the same position. `PhaseGeometry`'s own raw scratch read is a
  defect, not a naming choice (R-Equilibrium-1, fixed separately below).

What the implementation settled, 2026-09-14, in the coding session that followed:

- **The `ref` carrier holds.** `IterationState` is passed by `ref` through every
  stage; the kernel compiler takes it, so the fallback of returning it by value is not
  needed. The judges were `KernelEqualityTests` of this node's tests node and the one
  of `Performance.Tests`, which runs `SolveFrozen` - the first stage to take the
  carrier - inside a CPU-accelerator kernel.
- **The composition root fits.** `Solve` is 45 physical lines and `SolveFrozen` 54,
  both under the root's 60, so the exception this section reserved for `Solve` is not
  claimed. The node's largest type is `CondensedSet` at 250 lines, against the root's
  400.

  ⚠ 2026-09-14: this bullet named `NewtonIteration.Converge` at 56 lines as the node's
  largest method. The Newton-loop split below moved its formulas out: `Converge` is now
  55 lines, tied with the new `DampedStep.ControlFactor` (also 55); both stay under the
  root's 60, as does the next-longest, `IterationMatrix.AccumulateGaseous` (54,
  unmoved by this split).
- **The carriers are filled by name, not by position.** `MixtureSums` and
  `Derivatives` are structs whose fields are written at the one place that computes
  them and read through `in` afterwards, rather than readonly structs with a nine- and
  a five-parameter constructor: a carrier whose purpose is to remove the root's
  parameter hazard may not reintroduce it in its own constructor. `SystemLayout` stays
  readonly - its four arguments are the shape of the system and it derives the rest.
- **Where three small pieces landed.** The last term of equation (2.59),
  the sum of `n_j (h_j/RT)^2`, belongs to `DerivativeSystem` with the rest of that
  equation rather than to `MixtureSums`, which does not carry it. The membership test
  `InSolution` sits in `PhaseGeometry` beside the partner lookup that needs it. The
  two reductions of the input (`LogPressure`, `InitialTemperature`) sit in
  `CaseSetup`, which reads the problem; the mark accessors moved to `SpeciesMarks` in
  the repair review (above, 2026-09-15). `Composition` also holds the frozen sums,
  whose gaseous logarithms come from the mole numbers because the frozen path has no
  `LogMoles`.
- **One behaviour changed, deliberately and invisibly.** `DerivativeSystem` restores
  the caller's condensed order before returning on every path, the singular one
  included; the code before the decomposition returned from that path with the scratch
  still permuted. Nothing in the tree reads that order afterwards (`Solve` rebuilds
  the set from `result.Moles` at every entry), so no result moves - the point is that
  a stage may not hand the caller's scratch back reordered.

## Shape exceptions

The rows below are this node's declared exceptions to the root's code-shape constraint,
in the form the protocol tests node reads; their reasons are decisions of `## Structure`.

| Where | Rule | Measured | Reason |
|---|---|---|---|
| `EquilibriumSolver` | efferent coupling | 24 | the composition root: `Solve` and `SolveFrozen` as the sequence of stage calls, the exit guards and the status write; holds no formula |
| `NewtonIteration` | efferent coupling | 20 | the Newton loop: the step and polish counts, the order of the stage calls, the status; holds no formula (the decision "The Newton loop holds no formula") |
| `EquilibriumScratch.EquilibriumScratch` | parameters | 12 | lists the slices of the batch-sized scratch buffers `API.md` publishes, one argument per slice; grouping them would move the contract and re-emit the kernels (the decision "The scratch descriptor keeps its constructor"); its one construction site names its arguments |

Every other type of the node measures 12 or below by the dependency check's walk
(`DerivativeSystem` the highest of the rest, at 12 since rules A and B added its read
of `state.Tie` and its call into `ElementCoupling.Coupled`, up from 10; `CaseSetup`,
`CondensedSet`, `ConvergenceTests` and `SingularRemedies` tied at 10, the first two
since the repair review moved the mark accessors into `CaseSetup`'s own dependencies
2026-09-15, R-Equilibrium-6), well below the root's limit of 14.

⚠ 2026-09-28 (rules A and B): stood "10 or below" with `CaseSetup` and `CondensedSet`
named as the highest of the rest. `DerivativeSystem` now reads rule A's tie from
`IterationState` and calls `ElementCoupling.Coupled` to test whether it still holds,
raising its own count from 10 to 12; `SingularRemedies` gained `CondensedDependency`
and `ElementCoupling` (rule B and rule A's remedies), reaching 10, tied with `CaseSetup`
and `CondensedSet`. All four stay well below the root's limit of 14; measured by the
protocol tests node's own coupling walk the same day.

⚠ 2026-09-26: `NewtonIteration`'s row stood at 17. The hidden-defect audit's loop
bookkeeping fix (the audit's finding 3) added `NewtonLoopState` (Carriers.cs), a small
kernel-compatible struct tracking steps-since-last-set-change, the converged mark and
the polish-step count, and `Converge` now names it directly (`ref NewtonLoopState loop`)
instead of holding that bookkeeping in loose locals; the walk counts the new type,
raising the measurement to 18. `ShapeTests.NoSrcTypeNamesMoreThan14TypesOfTheTree`
found the stale row red; re-measured the same day.

⚠ 2026-09-28: `EquilibriumSolver`'s row stood at 19 and `NewtonIteration`'s at 18. The
second hidden-defect audit's fixes named new types directly at both call sites:
`EquilibriumSolver` now constructs the cold-retry problem and calls `FrozenTemperature`
and `MixtureProperties.IsPhysical` from `SolveFrozen`, and reads `IterationState`'s new
`RetentionSecondStage` flag from `Solve`'s retry loop, raising its count to 22;
`NewtonIteration` now calls `EquilibriumSolver.RetentionThreshold` and reads
`IterationState.RetentionSecondStage` directly, raising its count to 19. Both stay
composition roots that hold no formula of their own; `ShapeTests.NoSrcTypeNamesMoreThan14TypesOfTheTree`
and `EveryShapeExceptionIsMeasuredAndStillNeeded` found the stale rows red; re-measured
the same day.

⚠ 2026-09-28, the same day (rules A and B): `EquilibriumSolver`'s row stood at 22 and
`NewtonIteration`'s at 19. `EquilibriumSolver.RunToConvergence` now names `ElementCoupling`
directly at the tie's release check, raising its count to 24; `NewtonIteration.Converge`
now passes `state.Tie` into `SystemLayout`'s five-argument constructor, naming `ElementTie`
where it did not before, raising its count to 20. Both stay composition roots that hold no
formula of their own; `ShapeTests.NoSrcTypeNamesMoreThan14TypesOfTheTree` and
`EveryShapeExceptionIsMeasuredAndStillNeeded` found the stale rows red; re-measured the
same day.

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
      in the chamber with the reference mass fraction, and the low-temperature RP-1311
      example (example 14, water condensation) reproduces the reference phase changes.
      `CondensedSpeciesTests`: `TheAluminizedPropellantBurnsToLiquidAluminaInTheChamber`
      (mole fraction 0.07645 as the reference; the reference's mass fraction, 0.304,
      follows from that mole fraction and its molar mass, so it is not compared a
      second time),
      `WaterCondensesBelowItsDewPointInTheLowTemperatureExample` (liquid at
      300 to 304.3 K, none from 305 K, as the reference), and
      `TheCondensedSpeciesInTheSolutionAreThoseOfTheReference` over every
      fixture case with condensed candidates (96 cases), including `AL2O3(a)` at the
      AP/HTPB/Al exits below the melting point.

      ⚠ 2026-09-14: until this date the same test also asserted the derived mass
      fraction against an absolute `1e-4` bound typed into the test itself — five
      times looser than the fixtures node's tolerance table applied to the mole
      fraction that mass fraction is built from. Removed: the mole fraction and the
      molar mass are already compared through the table two lines above in the test,
      and a mass fraction computed from both states adds no fact the reference can
      settle beyond them. Found by the test review of 2026-09-14 (F-TK-01).
- [x] 2026-09-12 — Derivatives: `Cp_eq`, `γ_s` and the sound speed agree with the
      reference within the tolerance table for every converged fixture case: part of
      the `FixtureSolveTests` comparison above (`cpEquilibrium`, `cvEquilibrium`,
      `gammaS`, `dlnVdlnT`, `dlnVdlnP`, `soundSpeed` for all 106 cases).
- [x] 2026-09-12 — Element conservation holds for every converged fixture case at
      the invariant's tolerance:
      `ElementConservationTests.ElementsAreConservedAtTheInvariantTolerance` over
      the machine-generated list of the 106 tp, hp and sp cases.
- [x] 2026-09-12 — A case with an absent element gives the same result as the same
      case solved on a table without that element's species (bit for bit on the same
      accelerator): `AbsentElementTests.AZeroAbundanceEqualsATableWithoutTheElement`
      (carbon removed from RP-1311 example 1, argon from example 3, carbon from the
      LOX/RP-1 throat; moles, multipliers, state and iteration count bit for bit). A case
      with an empty table or with every abundance zero returns `InvalidInput` and writes
      nothing else: `InvalidInputTests` (empty table, zero abundances, negative
      abundance, zero and negative pressure, tp without temperature).
- [x] 2026-09-12 — The solve runs unchanged inside an ILGPU kernel on the CPU
      accelerator with the same results as the host call:
      `KernelEqualityTests.KernelAndHostGiveTheSameBits` over the 8 table families
      of the 106 cases (moles, multipliers, state, status and iterations bit for bit).
- [x] 2026-09-13 — Plateau states converge and match the reference: the
      melting-plateau fixture cases (RP-1311 example 13 generated with its insert
      list; the direct plateau stations of AP/HTPB/Al; the latent-heat-band hp
      cases) return `Ok` with both records of the pair in the solution, the
      temperature at the pair's `T*`, and every compared field — `γ_s`, the sound
      speed and the plateau zeros included — within the fixtures node's tolerance
      table: `FixtureSolveTests` over every tp and hp file of that day,
      `Performance.Tests.RocketFixtureTests` and
      `Problems.Tests.RocketTests.TheRocketCaseReproducesTheReferenceEndToEnd`
      over `rp1311-example13` and the eight `ap-htpb-al-plateau` rocket files.
- [x] 2026-09-13 — The anti-cycling rule closes the include/remove cycle: an
      assigned enthalpy inside the `ALN(L)` gap of the fuel-rich AP/HTPB/Al chamber
      — where `AL4C3(cr)` near its 2500 K upper bound was included and lost every
      round, seen red before the stand-down rule was added — converges onto the
      pinned pieces
      (`PlateauTests.AnEnthalpyInsideTheALNGapPinsThePiecesAtTheCut`); a
      record removed for range re-enters when it is the only positive candidate, a
      second escape stands it down, and an `Ok` exit never hides a positive-gain
      candidate
      (`PlateauTests.AnEnthalpyNoAdmissibleSetCanHoldIsRefusedRatherThanLiedAbout`
      walks exactly that path to an honest `NotConverged`, and
      `AnOkSolutionLeavesNoCondensedCandidateWithPositiveInclusionGain`
      holds over every hp fixture).
- [x] 2026-09-13 — Sweeps across a plateau lose no station: the pressure-ratio band
      across the AL2O3 plateau solves sequentially and one exit at a time onto the
      same stations, on the chamber isentrope throughout
      (`Problems.Tests.SplitRecordTests.ASweepAcrossTheAluminaPlateauStaysOnTheIsentropeByEitherPath`);
      example 13's four exits cross the BeO plateau end to end
      (`RocketTests.TheRocketCaseReproducesTheReferenceEndToEnd` over
      `rp1311-example13`); and tp solves at the printed bounds pick the record the
      reference picks (`FixtureSolveTests` over `ap-htpb-al-plateau_T2327`,
      `rp1311-example13-mixture_T2851` and `_T2373`), one kelvin beside the `ALN(L)`
      cut the gap test picking each side. The original wording asked for "fine"
      sweeps of both plateaus from both starts; the eight-ratio band and the
      four-exit example are that promise's committed form.
- [x] 2026-09-14 - The decomposition of `## Structure` is in place and changed no
      number. Shape: no type or method of this node over the root's size limits, no
      control flow deeper than 3 and no method over six parameters, no exception
      declared or needed; covered by the protocol tests node's `ShapeTests`, all ten
      facts green at `62cd99e`. Surface: `Protocol.Tests.SurfaceTests`
      against `tests/Protocol.Tests/PublicSurface.approved.txt`, which this work did
      not touch - every new type is internal. Numbers: the tests node's
      `BitSnapshotTests.EveryFixtureCaseGivesTheRecordedBits` over every
      enumerated tp, hp and sp fixture case against `Bits.approved.txt`, recorded from
      the code of `8e36a27` before the first line moved and unmoved after the last;
      `KernelEqualityTests` green; and the whole fast suite (2142 tests that day)
      green after each of the six extraction steps. The execution tests node's CUDA
      sweep and throughput benchmark are long-running and belong to the root's own
      criteria; they are run on the merge, not here, and this node's evidence is of
      the CPU accelerator.

      ⚠ 2026-09-15: this criterion named `NewtonIteration.Converge` at 56 lines and
      said no shape exception was declared or needed. Both went stale the same day,
      after this tick was written: the Newton-loop split (`29c2200`) moved `Converge`'s
      formulas into `DampedStep`, `ConvergenceTests` and `SingularRemedies`, leaving it
      at 55 lines (the `## Structure` warning above already says so); and the coupling
      recalibration (`9facd7f`) and the named-construction rule (`0c33d1e`) produced the
      three rows `## Shape exceptions` now declares. Found by the repair review of
      2026-09-15 (R-Equilibrium-4). The line figures named that day were physical,
      not lines of code; the criterion above now cites the protocol tests node's
      `ShapeTests` instead, which holds this by machine at `62cd99e`.
- [x] 2026-09-14 - The rules the review of 2026-09-14 found written twice exist once
      each: the inclusion gain of section 3.4 (`CondensedSet.InclusionGain`, called by
      the inclusion test and by the honesty guard), the element abundance
      (`ElementBalance.Abundance`, called by the element rows of `IterationMatrix` and
      by both tolerance tests), the trace retention (`Composition.Retain`, called by
      the sums of every step and by the final iterate of every convergence), the state
      record (`MixtureProperties.Common`, called by the equilibrium and the frozen
      closure). The dead conditional of the frozen target is gone, its unit difference
      now a comment in `FrozenTemperature`. Every number of the report is a named
      constant in the stage that uses it: the control-factor weight and limit of
      equation (3.1), the small-species bound of (3.2), the tests of (3.5) and (3.6),
      the polish threshold and step count, the reset moles and reset count of section
      3.6 (`NewtonIteration`); the step limit, test and step cap of the frozen Newton
      (`FrozenTemperature`); the initial gaseous moles, the default temperature and
      the unestimated offset of section 3.1 (`CaseSetup`); the transition window and
      the residual gain limit (`CondensedSet`); the crossing limit and the range
      tolerance (`PhaseGeometry`); the two element-balance tolerances
      (`ElementBalance`). Checked by reading at the close of the decomposition; the
      bit snapshot proves the reading moved no number.
- [x] 2026-09-14 — The node decodes none of `Thermo`'s interval layout (F-AR-01): no
      `IntervalStart`, `IntervalCount` or `IntervalBounds` in its source files (grep
      over `src/Equilibrium/*.cs` empty), the record bounds asked of
      `SpeciesFunctions.RecordLow` and `RecordHigh` from `PhaseGeometry` (`Adjacent`,
      `EffectiveLow`, `EffectiveHigh`) and from `CondensedSet.Pinnable`; the tests
      node's `BitSnapshotTests.EveryFixtureCaseGivesTheRecordedBits` unchanged
      (463 tests green, the hash of `Bits.approved.txt` unmoved) and
      `KernelEqualityTests` green in the same run. Non-degeneracy, applied alone in
      the worktree and restored: `SpeciesFunctions.RecordHigh` made to return the
      record's lower bound turned 29 fixture cases' recorded bits red
      (`BitSnapshotTests.EveryFixtureCaseGivesTheRecordedBits`), which a node
      still holding its own copy would not.
- [x] 2026-09-14 — The Newton loop holds no formula (`## Structure`, the decision of
      that name): `NewtonIteration`, `DampedStep`, `ConvergenceTests` and
      `SingularRemedies` as the table says, each within the root's code shape
      (`NewtonIteration` 65 lines, `Converge` 55; `DampedStep` 98 lines,
      `ControlFactor` 55, `Apply` 25; `ConvergenceTests` 59 lines, `Evaluate` 15,
      `Worst` 25; `SingularRemedies` 39 lines, `Recover` 27; nesting at most 3, no
      method over 6 parameters, against the root's 400/60/3/6). The efferent coupling
      of the four, measured by the dependency check's walk of 2026-09-14:
      `NewtonIteration` 17, `DampedStep` 7, `ConvergenceTests` 8, `SingularRemedies` 5
      (`PhaseGeometry`, unaffected by this split, 3). `NewtonIteration`'s 17 is above
      the root's limit of 14, so it is named as this node's second composition root in
      its `## Structure` row above, as the decision foresaw (measured 16 there, on the
      code before the split; the split itself adds the coupling of naming the four
      stages it now calls, which the decision's own reasoning already accounted for).
      The tests node's `BitSnapshotTests.EveryFixtureCaseGivesTheRecordedBits`
      unchanged (`Bits.approved.txt` hash unmoved) and `KernelEqualityTests` green in
      the same 463-test run; `Performance.Tests` green (699 tests, `SolveFrozen`'s
      kernel test included); the execution tests node's fast set green on CUDA (41
      tests, no `APTHERMO_NO_CUDA`).
- [x] 2026-09-14 — Every creation of `EquilibriumScratch` in the tree names its
      arguments (the decision "The scratch descriptor keeps its constructor"), the
      protocol tests node's named-construction fact green once it exists; the tests
      node's bit snapshot unchanged. A scan of every `new T(…)` and `T x = new(…)` of
      the name in `src/` and `tests/` (a script outside the tree) finds the one site, in
      `EquilibriumScratch.Slice`, every argument named; the build of `Equilibrium` after
      the change carries the IL of the build before it, method by method, so no
      argument binds to another parameter; `Equilibrium.Tests` (463) green;
      `tests/Equilibrium.Tests/Bits.approved.txt` unchanged (blob `65788e23` before and
      after). The fact,
      `ShapeTests.EveryWideConstructorIsCalledWithNamedArguments`, is designed
      and not yet written; it takes over as the evidence when it is.
- [x] 2026-09-15 — A record stood down by the anti-cycling rule stays out of play "for
      the rest of it" (the condensed-species rule above): `PhaseGeometry.Adjacent` and
      `PhaseGeometry.PhaseAt` test `!SpeciesMarks.InPlay(scratch, k)`, not the raw
      `scratch.SpeciesActive[k] == 0` the clean-code pass carried over unchanged from
      the bodies of `Adjacent` and `PhaseAt` in the pre-decomposition
      `EquilibriumSolver.cs`, at `7661ea9` (a test that was correct only
      while `Absent` was the sole value skipped, before `SpeciesMark.StoodDown` existed;
      found by the repair review, R-Equilibrium-1). No `SpeciesActive[` remains outside
      `SpeciesMarks.Of` and `.Set` (`grep` over `src/Equilibrium/*.cs`, two matches, both
      in `Carriers.cs`).

      Seen red on the code before the fix, then green after it:
      `PlateauTests.AStoodDownRecordIsNeitherAdjacentToNorFoundBesideItsInPlayPartner`
      stands one piece of `ALN(L)` down next to its in-play partner (the fixture and
      pair of the ALN-gap tests above) and asserts `Adjacent` and `PhaseAt` return −1
      for it; before the fix `Adjacent` returned the stood-down piece's own table index
      (231) instead of −1 (`Assert.Equal() Failure: Expected: -1, Actual: 231`, the
      fact's first assertion, on `Adjacent`) — `PhaseAt` was not reached, the same
      defect the report names for both methods.

      No fixture reaches the buggy path (BOOT.md's defect note on the condensed-species
      rule, ⚠ 2026-09-13, and the review's own check): `tests/Equilibrium.Tests/Bits.approved.txt`
      unchanged through the fix (hash `65788e23f4390305763c80ab1f66b2054ff1907a`, same
      before and after), `Equilibrium.Tests` 464/464 green (463 plus the new fact).
- [x] 2026-09-15 — Every ticked criterion above re-verified on the decomposed and
      repaired code at `62cd99e`: its tests green in the full suite
      (`APTHERMO_NO_CUDA=1`, every category, 3037 tests, none skipped), and
      CUDA-category evidence on the reference machine (`tests/Execution.Tests`, 41,
      and the long-running sweep and throughput tests).
- [x] 2026-09-26 — The audit's findings 1 to 5 and the open-below rule (the ⚠ notes of
      this date under Constraints, and the Thermo node's criterion of the same date for
      finding 1).
      - **Below 300 K and below 200 K.** Equilibrium fixtures computed by cea 3.3.4,
        each covered by `Problems.Tests.EquilibriumTests.AssignedTemperatureCasesReproduceTheReference`
        through its directory listing:
        - Si and Li in argon at 298.15, 299, 299.99, 300 and 301 K (the Thermo
          criterion);
        - H2/O2 at O/F 4 and 1 bar at 165, 180, 190 and 199 K, with products H2, O2,
          H2O, `H2O(cr)` and `H2O(L)`. Shown red on the unpatched open-below rule: 4 of
          64 tp cases mismatch, `h2-o2-of4_T180`/`T190`/`T199` reporting supersaturated
          vapour where the reference holds ice (`x(H2O(cr))` reference 0.504, tree 0;
          `enthalpy` reference −14.998 MJ/kg, tree −12.453 MJ/kg).
      - **The full set.**
        - The audit's 17-element case at 350 K returns `Ok` with every stable phase in
          the solution: cea 3.3.4 solves it whole, so it is a fixture too
          (`tests/Fixtures/cases/tp/seventeen-elements-many-condensed-phases_T350.json`),
          covered by the same reference test.
        - `PlateauTests.AnOkSolutionLeavesNoCondensedCandidateWithPositiveInclusionGain`
          ("an `Ok` solution leaves no condensed candidate with a positive inclusion
          gain") now runs over every tp, hp and sp fixture (130 cases), not the hp ones
          alone (44).
        - The exit guard is shown red once: with `MaxCondensedInSolution` set back to 8,
          `AssignedTemperatureCasesReproduceTheReference("seventeen-elements-many-condensed-phases_T350")`
          ends `status NotConverged`, not `Ok`.
      - **Warm starts.** `WarmStartTests.AWarmSolveAtHalfPressureAgreesWithAColdSolveAtThatPressure`
        solves every tp fixture that converges `Ok` from a cold start again from its own
        solution at half its pressure; the warm solve is `Ok` and agrees with a cold
        solve at that pressure (65 cases, `ANamedFixtureCompletesTheFullWarmStartComparison`
        pinning one by name so the theory cannot quietly skip every case). A cold solve
        that itself fails at the arbitrary half pressure (no baseline to compare against,
        4 of the `rp1311-example14` water-plateau cases) or a warm solve a plateau's
        seeded pair makes singular (the ⚠ below) is skipped, not forced.

        `TheAuditsExactCasesWarmStartOkAndAgreeWithAFreshColdSolve` reproduces the
        audit's own three probe cases exactly, over the fixture's own table and element
        moles, cold at the fixture's pressure times a factor and a given temperature
        (the fixture's own enthalpy target for hp), warm from that solution at half that
        pressure:
        - `("tp", "rp1311-example1_r1.5_p0.01atm_T2000", 1.0, 1000 K)`;
        - `("tp", "rp1311-example8_exit5", 0.1, 1000 K)`;
        - `("hp", "rp1311-example8_exit3", 100.0, the fixture's own enthalpy target)`,
          the warm temperature estimate being the cold solution's own converged
          temperature, since hp assigns none of its own.

        All three: `Ok` cold (24, 16, 12 iterations, matching the audit's own run),
        `Ok` warm, agreeing with a fresh cold solve at the halved pressure. Shown red
        once: with `NewtonIteration.cs`'s `if (verdict != NotConverged &&
        RetentionCrossed(...))` replaced by `&& false` (the retention-crossing rule
        off), all three warm solves end `NotConverged` (`Assert.Equal() Failure:
        Expected: Ok, Actual: NotConverged`) — the audit's own run recorded the same
        outcome, atomic H crossing the trace threshold by 5.757753e-10, 9.312757e-10
        and 9.301270e-10 kmol/kg respectively after one iteration
        (`scratchpad/audit/repro1.txt`, not committed).

        ⚠ 2026-09-26: this criterion named "the polish-threshold tier of the fixtures
        node's tolerance table". That did not survive implementation. The fixtures
        node's `ToleranceTable` compares a tree value against the cea reference and
        derives every entry from the reference's own print precision and convergence
        tests (`tests/Fixtures/tolerances.json`); a warm-versus-cold comparison has no
        reference to ask, and `Tolerances.cs`'s own doc comment already says so: "a
        comparison of two paths of this tree against each other has no reference to
        ask, so the number lives here". The warm-start comparison uses
        `Tolerances.SelfConsistency`, not a new fixtures-node entry.

        ⚠ 2026-09-26, corrected on review: this ⚠ first said the audit's probe
        parameters "were not recorded in the design text" and that reconstructing the
        case from the committed `rp1311-example1` fixtures at 1000 K did not reproduce
        the crossing. Both were true of the design text alone, not of the audit's own
        working files: its harness (`scratchpad/audit/harness/ZzAuditRepro.cs`, not
        committed — a fixture's own table and element moles, the fixture's pressure
        times a named factor, a given temperature, warm at half that pressure) and its
        recorded run (`scratchpad/audit/repro1.txt`) name the exact three cases above.
        `TheAuditsExactCasesWarmStartOkAndAgreeWithAFreshColdSolve` reproduces them
        directly; the review that found this also found the general theory's coverage
        of them degenerate (it warm-starts at the fixtures' own committed temperatures
        and pressures, none of which crosses the trace threshold).

        ⚠ 2026-09-26, found implementing this criterion, not one of the audit's five
        findings: four `rp1311-example14` cases (water pinned at its own melting
        plateau) end `SingularMatrix` when warm-started at half pressure.
        `CaseSetup.FromPreviousSolution` seeds both pieces of the cold solution's
        pinned pair into the warm start's condensed set without checking whether they
        are still a valid pair at the new pressure, and the first Newton step's matrix
        is then singular in a way `SingularRemedies` does not recover from. Left open
        for a design session on the plateau-pinning geometry; `WarmStartTests` skips a
        case in this state rather than asserting it.

        ⚠ 2026-09-27, the explanation above is wrong (checked by the orchestrator with a
        scratch trace at `89bb619`): the four cold solutions hold `H2O(L)` alone, no pair.
        At half the pressure (0.0253 bar) the total pressure lies below water's saturation
        pressure at 300–304.3 K, so no state with the liquid exists. The seeded liquid goes
        negative at step 4, the loop never removes it (removal is tested only after
        convergence, as the reference does, `equilibrium.f90:2602`), `ln n` grows by the
        damping cap of 0.4 per step with λ shrinking geometrically, and at step 41–44 the
        matrix is singular; the remedies then drop the liquid from a destroyed gas state
        and fail again. Seeded without the liquid, the same warm starts are `Ok` in 5–11
        steps; at ×0.9 to ×0.99 of the pressure they are `Ok`.

        Design (2026-09-27), implemented the same day: a warm start whose convergence
        fails (the singular remedies exhausted, or the step cap) while a condensed
        species seeded from the previous solution holds negative moles falls back once
        to the cold start of section 3.1, with its iterations counted in the case's
        total. Nothing else changes; a cold start never falls back
        (`EquilibriumSolver.Solve`'s own outer loop only offers the fallback when
        `useMolesAsEstimate` was true, and only once).

        The negative mole is not always there to read at the point of failure: the
        singular remedies' last resort drops the last condensed record unconditionally
        and zeroes its mole number (`CondensedSet.Remove`) before `Converge` returns, so
        by the time `Solve` sees `SingularMatrix` the seed's own negative value is
        already gone. `IterationState.CondensedWentNegative` (`Carriers.cs`) is the one
        place that still sees the sign: `SingularRemedies.Recover` sets it, immediately
        before that removal, when the record being dropped was negative; `Converge`
        resets it to `false` at the start of every call, so a caller reads only what the
        call that just returned did. `EquilibriumSolver.FallsBackToColdStart` triggers on
        either this flag or, for the plain step-cap path where nothing was ever removed,
        a direct read of `scratch.CondensedInSolution`.

        Evidence: `WarmStartTests.AWarmSolveAtHalfPressureAgreesWithAColdSolveAtThatPressure`'s
        skip for this state is removed (`Assert.Equal(CaseStatus.Ok, warm.Status)`
        unconditional); all four `rp1311-example14` cases (T300, T304, T304.2, T304.3),
        warm-started at ×0.5 pressure from their cold solution, end `Ok` and agree with a
        fresh cold solve at that pressure (`Equilibrium.Tests`, 68/68 of that class).
        Shown red once with the fallback disabled (`canFallBack` forced `false`
        regardless of `useMolesAsEstimate`): the same four cases end `SingularMatrix`,
        the rest of the class unaffected (4 failed of 68).

        A temporary `Console.Error.WriteLine` at the fallback's trigger (reverted before
        this commit; `HostSolver.Solve` calls `EquilibriumSolver.Solve` directly, not
        through a compiled kernel, so the print did not disturb that path) counted its
        firings: `dotnet test tests/Equilibrium.Tests --filter
        "FullyQualifiedName!~KernelEqualityTests"` fired it exactly 4 times (the four
        `rp1311-example14` cases above, and no other of the 682 remaining tests), and
        `dotnet test tests/Performance.Tests --filter
        "FullyQualifiedName!~KernelEqualityTests"` (the rocket stations' own warm starts
        across every rocket fixture) fired it 0 times. `KernelEqualityTests` of both
        nodes were excluded only for this temporary print, which ILGPU's kernel compiler
        cannot compile (a `NullReferenceException` inside `IRContext.Optimize`, confirmed
        and then reverted); with the print removed, both nodes' full suites pass,
        `Equilibrium.Tests` 695/695 and `Performance.Tests` 700/700, `KernelEqualityTests`
        included. No `Bits.approved.txt` or `Bits.linux.approved.txt` of `Equilibrium`,
        `Thermo`, `Performance` or `Problems` moved (`git status` before and after,
        unchanged), matching the counter: no fixture besides the four deliberate
        half-pressure probes ever reaches the fallback.
      - **The bookkeeping.** `NewtonLoopStateTests` drives the loop's struct on the
        host, without a table where the rule does not need one:
        - `ANotConvergedVerdictClearsTheConvergedMarkAndThePolishCount` and
          `ReportTestsMetCountsAPolishStepAndPolishedDoesNotCountAnother`: a failed
          verdict after a pass clears the mark and the polish count;
        - `ASpeciesCrossingTheTraceThresholdDuringTheStepIsReportedAsACrossing` and
          `NoCrossingWhenEveryGasSpeciesKeepsItsSideOfTheTraceThreshold`: a crossing of
          the trace threshold fails the verdict;
        - `ASingularRemovalOfACondensedSpeciesRestartsTheStepCountAndCountsTowardTheChangeCap`
          and `RecordSetChangeResetsTheStepCount`: a singular removal restarts the step
          count and counts as a change.

        Each fact shown red once against the rule it guards, by mutating the guarded
        line alone and confirming a fresh build fails the fact (`NotConverged`'s branch
        no longer clearing `PolishSteps`; `RecordSetChange` incrementing instead of
        resetting `Steps`; `RetentionCrossed` never returning `true`).
      - **Frozen mode.** `InvalidInputTests.FrozenModeRejectsAnInvalidMoleNumberGaseousOrCondensed`
        refuses a NaN, an infinite and a negative mole number on a gaseous (`H2`) and a
        condensed (`H2O(L)`) species in `SolveFrozen`, for tp, hp and sp (18 cases: the
        validation runs before any kind-specific branch, so all three take the same
        path). Shown red on the unpatched gas-sum-only check: 4 of 6 of the original
        hp-only cases reported `Ok` or `NotConverged` instead of `InvalidInput`.
      - **Bits.** No `Bits.approved.txt` of `Equilibrium`, `Thermo` or `Problems` moves
        on an existing case: the fifteen new tp fixtures and eleven new thermo species
        add lines, checked field by field against `Bits.actual.txt`, not by eye; no
        existing case's hash differs. `Performance`, `Transport` and `Cli` are outside
        this node's subtree and are unaffected (their own fixtures and code are
        untouched). The four reasons this criterion anticipated (a late re-convergence
        polished, a crossing iterated, a set-change budget widened, a phase admitted)
        do not fire on the committed fixture set: none of them reaches the trace
        threshold at a crossing, a set-change count near the cap, or a ninth stable
        condensed phase. They are exercised by the new fixtures and the unit facts
        above instead of by an existing case's bits moving.

        Every CEA tolerance test stays green (`Problems.Tests` 1186/1186). The
        execution tests node is green on CUDA in Release (`dotnet test
        tests/Execution.Tests -c Release`, 126/126, the sweep and throughput tripwire
        included), because the scratch layout grows.

      Evidence: `APTHERMO_NO_CUDA=1 dotnet test APThermo.sln --filter
      "Category!=LongRunning"` 4278/4278, none skipped (`Equilibrium.Tests` 695);
      `dotnet test tests/Execution.Tests -c Release` 126/126 on CUDA; the protocol
      lint 0 errors/0 warnings; no `Bits*.approved.txt` differs from `main` outside
      the fifteen new tp lines and eleven new thermo lines named above.
- [ ] The second hidden-defect audit of 2026-09-28 (Thermo and Equilibrium, findings
      F1 to F5, and the guards part's O8 and F9) is closed by the rules of that date
      under Constraints.

      **Done, this pass:** the two-stage threshold's fixtures and unit fact (F1,
      below), the report-stands-for-`Composition.Refresh` correction (F1, `API.md`),
      the audit's sixteen regression states and the eleven of example 5 named as
      failing at both commits (F1, below), the targeted singular remedy's
      dense-solver entry and anti-cycling fact (F5), the mixture window and state
      guard (F2), the fallback on any failure (F3), frozen validation (F4), the NaN
      element guard (guards O8), and the bit and approved-output re-approvals across
      every node the change touches (Bits, below). **Open:** the NaClO4 and
      AP/HTPB/Al fixtures of F1, under investigation by the orchestrator since
      2026-09-28 (below); the AP/HTPB/Al convergence claim of F5, blocked by the same
      gap.
      - **The two-stage threshold (F1).**
        - New tp fixtures from cea 3.3.4 through the fixtures node's generator: RP-1311
          example 5's table at 300 K, 1 bar and 70 bar, and 305 K, 1 MPa. Covered by
          `AssignedTemperatureCasesReproduceTheReference` through its directory
          listing; each red at `5a732f0`; all three `Ok` and green (`tests/Fixtures/cases/tp/rp1311-example5_T300_p1bar.json`,
          `..._T300_p70bar.json`, `..._T305_p10bar.json`, `tests/Fixtures/generate/retention_threshold.py`).
        - **NaClO4 and AP/HTPB/Al: under investigation by the orchestrator
          (2026-09-28).** The orchestrator's task also asked for a NaClO4
          decomposition (Na:Cl:O = 1:1:4 from pure elements, since NaClO4 is not a
          thermo.inp reactant) at 500 K and 800 K, 1 bar, and AP/HTPB/Al at
          7 MPa/430 K and 1 MPa/420 K. Both were generated with cea (which converges
          on every one of the four) and tried against this node; neither is
          committed, and both stay out of the fixtures while the orchestrator has
          them investigated separately against the reference (2026-09-28) and
          decides after that. What this pass found, for that investigation:
          - NaClO4 reduces almost entirely to `NaCL(cr)` + `O2` at both temperatures
            (cea's own mole fractions: 0.667 `O2`, 0.333 `NaCL(cr)`, every gaseous
            trace at 1e-17 or below). `CaseSetup.FromDefaults`'s cold start is
            gas-only (RP-1311 section 3.1, unchanged by this audit), and the
            condensed-species inclusion test (`CondensedSet.Update`) runs only after
            `NewtonIteration.Converge` returns `Ok`; here the all-gas trial never
            converges (`SingularMatrix` at iteration 22, `failedRow` an element row,
            `state.CondensedCount == 0`, before `RetentionSecondStage` is ever
            reached), so no condensed candidate is ever tried. Whether the inclusion
            test should run on a stalled or singular gas-only trial, and if so under
            what rule, is a design question above this task.
          - AP/HTPB/Al at 430 K/7 MPa and 420 K/1 MPa converges at the Newton level
            repeatedly (11 changes of the condensed set, oscillating between 3 and 4
            species in solution, `RetentionSecondStage` already true throughout) and
            `CondensedSet.Update` settles (no further change), but
            `CondensedSet.ExitGuardFindsAPositiveCandidate` then finds a `StoodDown`
            candidate still showing a positive inclusion gain in the settled
            composition, so `Close` returns `NotConverged`. The exit guard is
            written to count a stood-down candidate on purpose (the first audit's
            finding 2, `## Structure` above), so this is not the guard
            double-counting; it is the inclusion test (`Update`, during the loop) and
            the exit guard (`ExitGuardFindsAPositiveCandidate`, at `Close`) reaching
            different verdicts on the same settled composition after a candidate has
            cycled in and out enough times to be marked `StoodDown`. Reconciling them
            needs a decision on what a permanently excluded but still-wanted
            candidate means for the case's status, which is beyond a targeted fix.

          Both symptoms are reproducible from `tests/Equilibrium.Tests/HostSolver`
          on the compositions above (Custom pure-element Na/Cl/O 1:1:4 for the first;
          `plateaus.REACTANTS`/`MASS_FRACTIONS` at the stated tp for the second); the
          orchestrator or a design session should decide whether to accept the gap,
          narrow the fixture request, or open a follow-up task naming the inclusion
          test's and the exit guard's relation to a stood-down candidate as its own
          design question.
        - A unit fact on the loop's struct: the switch to the second stage counts as a
          change of the retained set, and an exit needs a convergence after it.
        - The report reflects the case's own last `Composition.Refresh` (the ⚠
          2026-09-28 correction above): a species between 1e-11 and 1e-8 of the gas,
          previously zeroed by a separate 1e-8 report step, is now reported at its
          converged second-stage amount, which is why the Bits bullet below expects
          this node's snapshot to move on existing fixtures, not stay at their zeros.

        - **The audit's sixteen regression states.** The orchestrator supplied the
          states directly (2026-09-28: `Z2Scan2.cs`/`Z2Verify.cs` of the audit's own
          harness, read at the orchestrator's word that they are data for this node's
          tests, not a foreign node's code) — four states on example 5's own table
          (its committed `hp` fixture's table and element moles, solved as tp: 1 bar
          at 300 and 310 K, 1 MPa at 320 K, 7 MPa at 340 K) and twelve on the
          AP/HTPB/Al chamber's own table (its committed `tp` fixture's: 1 MPa at 300,
          305 and 310 K; 7 MPa at 300 to 330 K every 5 K, plus 335 and 350 K), none of
          them a committed fixture file of its own.
          `tests/Equilibrium.Tests/RegressionStateTests.cs`,
          `TheAuditsRegressionStateConvergesAndHoldsTheEquilibriumConditions`, checks
          every one of the sixteen with this node's own equilibrium conditions, not
          the reference: element conservation at the invariant tolerance, every
          retained gas at its own chemical potential (Σ a_ij π_i), and no absent
          condensed candidate in its effective range with a positive inclusion gain
          (`PlateauTests`' own rule, run here on states no fixture covers). Confirmed
          red at `5a732f0`: a temporary probe against a `git worktree add … 5a732f0`
          checkout, discarded after the reading, found all sixteen `NotConverged`
          there; all sixteen are `Ok` and clear of every condition after the fix.
          `TheAuditsElevenExample5StatesThatFailedAtBothCommitsAreOkNow` covers the
          eleven of example 5's states the audit's own scan (`scan2_old.txt`) named
          as failing at both commits it compared (1 MPa at 300, 305 and 310 K; 7 MPa
          across the whole 300–335 K band, every 5 K): every one is `Ok` today: none
          is left unasserted or merely named.
      - **The targeted singular remedy (F5).** The dense solver's new entry returns the
        failed row, a unit fact on a constructed singular matrix
        (`tests/Equilibrium.Tests/DenseSolverTests.cs`,
        `TheFailedRowOverloadNamesTheRowWhosePivotVanished` and
        `TheFailedRowOverloadReportsNoFailureOnARegularMatrix`). The claim "the
        AP/HTPB/Al states at 420–450 K, 1–7 MPa converge" is not demonstrated by this
        pass: see the F1 escalation above, which found two of those states
        (430 K/7 MPa, 420 K/1 MPa) do not converge with the remedy as implemented. A
        singular removal marks its record, a fact on the anti-cycling skip
        (`NewtonLoopStateTests.ASingularRemovalOfACondensedSpeciesRestartsTheStepCountAndCountsTowardTheChangeCap`).
      - **The mixture window and the state guard (F2).** A tp of the `h2-o2-of4` table
        at 1 bar, 60 K to 159 K: `TemperatureOutOfRange`. At 160 K and above: `Ok`,
        with finite positive Cp, Cv, `γ_s` and sound speed. The fixtures at 165–199 K
        stay green. A fact over every `Ok` of the fixtures and of the audit's grids:
        the guard's quantities are finite and positive, the pinned pair's
        `Cp_eq = Cv_eq = 0` excepted.
      - **The fallback on any failure (F3).** `WarmStartTests` extended to P/10, P/2
        and T×1.1 over the fixture tables at 300 K and 600 K (the audit's six named
        cases among them): every warm start whose cold solve is `Ok` ends `Ok` and
        agrees with it. The step-cap trigger has a fact of its own (guards F9: no test
        reached it). The hp and sp cold retry starts at 3 800 K.
      - **Frozen validation (F4).** `InvalidInputTests` gains frozen tp at +∞, NaN and
        1e-300 K (`InvalidInput` for the first two; `TemperatureOutOfRange` for the
        third, below 0.8 of the gas floor), and `Solve` tp at +∞ (`InvalidInput`).
      - **NaN in the element guard (guards O8).** `ElementBalance.WithinInvariant`
        reads a NaN abundance as outside the invariant; a fact drives the method itself,
        not the test node's own `Violations`.
      - **Bits.** The two-stage threshold changes the final iterates, so bit snapshots
        of this node and of the nodes that consume it (`Performance`, `Transport`,
        `Problems`, `Cli`) move. The orchestrator decided on 2026-09-28 that this
        coder re-approves them in the same commit, with a field-by-field report of the
        largest relative change per field, every CEA tolerance test of the tree green,
        and no status or iteration count changed except where the audit named the
        case. The Linux files are recorded by the orchestrator under WSL. The
        execution tests node's CUDA sweep and throughput tripwire run again on the
        reference machine.
      - `API.md` states the changes: the retention stages, the targeted remedy, the
        window, the guard, the non-finite temperatures under `InvalidInput`, the
        warm-start retry on any failure.
- [x] 2026-09-28 — Rules A and B (Constraints, the orchestrator's investigation 6 of 2026-09-28) close
      the open items of the criterion above.
      - [x] 2026-09-28 — **Fixtures through the fixtures node's generator**
        (`tests/Fixtures/generate/retention_threshold.py`, driven by `regenerate.py`):
        - NaClO4 and KClO4 at 500 K and 800 K, 1 bar, from pure elements (the
          generator's new `Custom` pure-element reactants):
          `tests/Fixtures/cases/tp/naclo4_T500.json`, `naclo4_T800.json`,
          `kclo4_T500.json`, `kclo4_T800.json`.
        - AP/HTPB/Al tp at 7 MPa and 430 K, and at 1 MPa and 420 K, on the chamber
          fixture's table and element moles: `tests/Fixtures/cases/tp/ap-htpb-al_pc7MPa_T430.json`,
          `ap-htpb-al_pc1MPa_T420.json`.
        - All six shown red without rules A/B and green with them, by disabling the
          two blocks of `SingularRemedies.Recover` in turn (`if (false && …)`),
          rebuilding and rerunning `tests/Equilibrium.Tests`, then restoring: the salt
          cases fail `SingularMatrix`, the AP/HTPB/Al cases fail `NotConverged`,
          confirmed at this commit.
        - Re-running `regenerate.py` re-provenanced the 322 existing fixture files
          (`generatorSha256`, `scriptSha256`, `generatedOn`) with no output field
          moved, confirmed by a `git diff` restricted to non-provenance keys.
      - [x] 2026-09-28 — **Unit facts**, all green in `tests/Equilibrium.Tests`
        (926/926): `SingularRemedyRulesTests.cs` —
        - `ElementCoupling.Find`/`Coupled`: a coupled pair (N/Cl through NH4CL(II)
          alone, ratio 1), an uncoupled pair (once `HCL` carries a nonzero mole), and
          `HeldByCondensed` true only while the tying condensed species is in the
          solution.
        - `CondensedDependency.LeavingPosition`: the ratio test chooses `AL(OH)3(a)`
          over `AL2O3(a)` at the AP/HTPB/Al products of 430 K (`H2O(L)` entering
          last), and returns no leaving position for an independent three-species set.
        - `WarmStartTests.AWarmStartFromExample5sTenBarSolutionTiesNAndClThroughNH4CLAndEqualsItsColdSolve`:
          a warm start from example 5's 10-bar solution, which ties N/Cl through
          NH4CL(II) at step 0, equals its cold solve. Honestly recorded: rule A's row
          is exercised on this path but disabling it does not turn this particular
          fact red (its doc comment says so); the fixture-level facts above are rule
          A's and B's red-once evidence.
      - [x] 2026-09-28 — **The scans as measurements**, recorded here, not asserted.
        Run as temporary facts inside `tests/Equilibrium.Tests` (never committed: the
        audit's own `Z2Runner.cs`, `Z2Check.cs`, `Z2Verify.cs`, `Z2Scan2.cs`,
        `Z2Scan3.cs`, `Z2Fuzz.cs`, copied in, built at the tree's Diagnostics maximum,
        run, then deleted) and independent equilibrium checks (element conservation,
        every retained species at its own chemical potential, no excluded candidate
        with a positive inclusion gain), not the reference:
        - **The AP scan** (`Z2Scan2.ApTables`, example 5's and the AP/HTPB/Al
          chamber's own tables, 280–700 K by 5 K at five pressures 1 kPa–7 MPa):
          850/850 `Ok`, zero equilibrium-condition violations.
        - **The audit's salt scan** (`Z2Scan3.SaltScans`, kclo4-rich, kclo4-lean,
          naclo4 and ap-htpb tables, 300–1500 K by 10 K at three pressures):
          1436 `Ok`, 16 `NotConverged`, 0 `SingularMatrix`, zero equilibrium-condition
          violations. The 16 `NotConverged` states match the threshold-flip
          limitation named below exactly (KClO4 610–680 K, NaClO4 490–500 K): this is
          the same, already-known and already-declared gap, not a new one. Against
          the audit's own pre-fix baseline (`scan3_old.txt`, kept with the audit's
          reports): 332 `NotConverged` + 122 `SingularMatrix` of 1452 states: rules
          A/B take the salt tables from a 31 % failure rate to 1.1 %, all sixteen
          remaining failures inside the declared plateau bands.
        - **The fuzz counts** (`Z2Fuzz.Fuzz`, every tp/hp/sp fixture, cold at varied
          P/T/target/element moles and warm-started from each cold `Ok`): 40 985
          cold and warm solves; every `Ok` clear of every equilibrium condition
          except the diagnostic's own trace-threshold note (a converged species
          reported below 1e-8 mole fraction rather than dropped to zero — not a
          violation of an equilibrium or conservation condition, the check's own
          margin), 0 Gibbs-residual, element-conservation, non-finite or
          left-out-with-gain violations anywhere in the sweep. `tp:cold:SingularMatrix`
          is 77, matching the three-element-coupling limitation named below exactly
          (same count as the pre-fix baseline: unrelated to rules A/B, RP-1311
          example 1/12 tables at 300 and 600 K). 41 warm/cold disagreements at
          |Δx| just above the 1e-6 threshold (baseline: 34), every one of them
          individually a valid equilibrium on both sides — a multiple-local-solution
          artifact near a degenerate composition, not a violation.
      - [x] 2026-09-28 — **No bit snapshot moves on an existing fixture.** Verified for
        all three nodes whose `BitSnapshotTests` walk the fixture tree
        (`tests/Equilibrium.Tests`, `tests/Thermo.Tests`, `tests/Problems.Tests`): a
        sorted, CRLF-normalized diff of each node's `Bits.approved.txt` against its
        freshly generated `Bits.actual.txt` shows six added lines only (the new fixture
        keys above), and not one existing hash moved. The coder approved
        `tests/Equilibrium.Tests/Bits.approved.txt`. The environment's permission layer
        refused the coder's write to the other two files, which lie outside its subtree.
        With the owner's word of 2026-09-28, the orchestrator approved them at the merge
        (`74d0715`) from its own run, after the same additions-only comparison. Both
        facts are green (`Thermo.Tests` bit facts 239/239, `Problems.Tests`
        `EveryFixtureGivesTheRecordedBits`).
      - [x] 2026-09-28 — **Shape.** No method over 6 parameters; the declared Ce rows
        are re-measured (the ⚠ notes of this date under the Constraints above).
      - [x] 2026-09-28 — `API.md`'s `SingularMatrix` sentence lists the remedies, with
        a ⚠.

      Open and known, measured by the investigation, outside this criterion. The owner
      decided on 2026-09-28 that they do not block 0.2.0: `CHANGELOG.md` names them as
      known limitations of 0.2.0, and they are designed and fixed for 0.2.1:
      - **The threshold flip.** Two carriers cross the threshold alternately every
        step, so the polish never completes: KClO4 at 610–680 K, NaClO4 at 490–500 K,
        16 salt-scan states `NotConverged`. The matrix is never singular.
      - **The three-element coupling.** With only CO2, H2O and N2 retained, row O
        equals 2·C + ½·H. 77 fuzz tp states on example 1 and example 12 tables at
        300 K and 600 K end `SingularMatrix`, which a pair tie cannot express.
      - **The reaction plateau.** hp inside the Al(OH)3/Al2O3/H2O(L) reaction plateau
        (T* = 415.948 K, 157 kJ/kg wide at 7 MPa) ends `SingularMatrix` in the
        derivative system: the pinned-pair convention covers two records of one
        formula only.

## Taboos

- No equilibrium constants, no reaction sets, no hand-picked species subsets: the
  root forbids them and they would make the results unreviewable.
- No deletion of species from the table; no reallocation of anything during a solve.
- No `float`, no exceptions, no allocations, no virtual calls: kernel code.
- No knowledge of nozzles, chambers or rockets: `Performance` owns those iterations.
- No transport formulas: `Transport` owns them and only takes this node's outputs.
