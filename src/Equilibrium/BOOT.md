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
  most 8 condensed species in the solution at once, hence a matrix of at most 30 × 30.
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
- Convergence tests and control factor as RP-1311 chapter 3: the `λ` damping of
  equations (3.1)–(3.3) with the two branches for species above and below the trace
  threshold, the tests (3.5) and (3.6) on `Δln n_j`, `Δln n`, `Δln T`, the
  condensed-species mole numbers and the element residuals; trace threshold
  `ln(n_j/n) = −18.420681` (that is `n_j/n = 1e-8`) as in the report, below which a
  gaseous species is held at zero in the sums, keeps its logarithm, and is reported
  with zero moles. Iteration cap: 50 Newton steps after the last change of the
  condensed species set, and at most `MaxCondensedSetChanges` changes of that set per
  case: three per slot of the condensed set, an inclusion, a forgiveness and a
  stand-down for each of the `ScratchLayout.MaxCondensedInSolution` slots (24 today;
  the constant is the number, this document only names it). After the
  report's tests pass, up to six further steps polish the iterate until the largest
  correction is below `1e-11`, so that the reported state is at rounding level and the
  tolerance table measures the reference's convergence, not this node's.

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
     turns the status into `NotConverged` rather than a false equilibrium.
  3. The inclusion test: the species whose `Σ π_i a_ij − g_j/RT` is largest and
     positive is added, one at a time, compared per mole as RP-1311 section 3.4
     words it (the reference's code — cea2.f as 3.3.4 — divides the gain by the
     molar mass; this node follows the report). Two candidates are passed over: a
     species whose formula is already in the solution, because a pair is completed
     by rule 2, never by inclusion; and, once, the record just removed for range
     while another positive candidate exists — the anti-cycling rule; when it is the
     only positive candidate it is taken, so no equilibrium is lost.

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
  Singular matrices are reported as `SingularMatrix` after the report's remedies
  (resetting vanished species to `1e-6`, twice; then removing the last condensed
  species) have been tried.

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
  frozen properties; this mode serves frozen nozzle flow. Its state carries
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
| `EquilibriumSolver` | efferent coupling | 19 | the composition root: `Solve` and `SolveFrozen` as the sequence of stage calls, the exit guards and the status write; holds no formula |
| `NewtonIteration` | efferent coupling | 17 | the Newton loop: the step and polish counts, the order of the stage calls, the status; holds no formula (the decision "The Newton loop holds no formula") |
| `EquilibriumScratch.EquilibriumScratch` | parameters | 12 | lists the slices of the batch-sized scratch buffers `API.md` publishes, one argument per slice; grouping them would move the contract and re-emit the kernels (the decision "The scratch descriptor keeps its constructor"); its one construction site names its arguments |

Every other type of the node measures 10 or below by the dependency check's walk
(`CaseSetup` and `CondensedSet`, tied at 10 as the highest of the rest since the
repair review moved the mark accessors into `CaseSetup`'s own dependencies
2026-09-15, R-Equilibrium-6), well below the root's limit of 14.

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

## Taboos

- No equilibrium constants, no reaction sets, no hand-picked species subsets: the
  root forbids them and they would make the results unreviewable.
- No deletion of species from the table; no reallocation of anything during a solve.
- No `float`, no exceptions, no allocations, no virtual calls: kernel code.
- No knowledge of nozzles, chambers or rockets: `Performance` owns those iterations.
- No transport formulas: `Transport` owns them and only takes this node's outputs.
