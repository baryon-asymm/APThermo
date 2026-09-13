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
and section 2.6 (derivatives). This document fixes every choice the report leaves open
and every limit the implementation needs; it does not restate the report. Whoever
codes this node reads the report's chapters named here. What would lift the deviation:
a full restatement of the equations in this document, which nobody has asked for.

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
  condensed species set, and at most 10 changes of that set per case. After the
  report's tests pass, up to six further steps polish the iterate until the largest
  correction is below `1e-11`, so that the reported state is at rounding level and the
  tolerance table measures the reference's convergence, not this node's.

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
- Property definitions as in RP-1311 section 2.6: `Cp_eq` includes the composition
  derivatives; `γ_s = −(∂ln p/∂ln V)_s`; `a² = n R T γ_s` per unit mass; `M = 1/n`
  with `n` the total gaseous moles per kilogram; `MW = 1/Σ n_j` over all species with
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

## Acceptance criteria

- [x] 2026-09-12 — tp problems: for the product mixtures of the four reference
      propellants and the RP-1311 examples in the fixtures node, the mole fractions of
      every species the reference prints agree within the fixtures node's tolerance
      table; the list of compared species is generated from the fixture, not typed.
      `Equilibrium.Tests`, `FixtureSolveTests.Assigned_temperature_and_pressure_reproduces_the_reference`
      over the enumerated `cases/tp` directory (46 files): every state field the fixture
      carries and every listed species, within the table.
- [x] 2026-09-12 — hp problems: the adiabatic flame temperature and the composition of
      the same cases agree within the tolerance table.
      `FixtureSolveTests.Assigned_enthalpy_and_pressure_reproduces_the_reference` over
      `cases/hp` (34 files).
- [x] 2026-09-12 — sp problems: the temperature and composition at given entropy and
      pressure agree within the tolerance table (the nozzle stations of the reference
      rocket cases serve as fixtures).
      `FixtureSolveTests.Assigned_entropy_and_pressure_reproduces_the_reference` over
      `cases/sp` (26 files).
- [x] 2026-09-12 — Condensed species: the AP/binder/aluminium case includes `AL2O3(L)`
      in the chamber with the reference mass fraction, and the low-temperature RP-1311
      example (example 14, water condensation) reproduces the reference phase changes.
      `CondensedSpeciesTests`: `The_aluminized_propellant_burns_to_liquid_alumina_in_the_chamber`
      (mole fraction 0.07645 and mass fraction 0.304 as the reference),
      `Water_condenses_below_its_dew_point_in_the_low_temperature_example` (liquid at
      300 to 304.3 K, none from 305 K, as the reference), and
      `The_condensed_species_in_the_solution_are_those_of_the_reference` over every
      fixture case with condensed candidates (96 cases), including `AL2O3(a)` at the
      AP/HTPB/Al exits below the melting point.
- [x] 2026-09-12 — Derivatives: `Cp_eq`, `γ_s` and the sound speed agree with the
      reference within the tolerance table for every converged fixture case: part of
      the `FixtureSolveTests` comparison above (`cpEquilibrium`, `cvEquilibrium`,
      `gammaS`, `dlnVdlnT`, `dlnVdlnP`, `soundSpeed` for all 106 cases).
- [x] 2026-09-12 — Element conservation holds for every converged fixture case at
      the invariant's tolerance:
      `ElementConservationTests.Elements_are_conserved_at_the_invariant_tolerance` over
      the machine-generated list of the 106 tp, hp and sp cases.
- [x] 2026-09-12 — A case with an absent element gives the same result as the same
      case solved on a table without that element's species (bit for bit on the same
      accelerator): `AbsentElementTests.A_zero_abundance_equals_a_table_without_the_element`
      (carbon removed from RP-1311 example 1, argon from example 3, carbon from the
      LOX/RP-1 throat; moles, multipliers, state and iteration count bit for bit). A case
      with an empty table or with every abundance zero returns `InvalidInput` and writes
      nothing else: `InvalidInputTests` (empty table, zero abundances, negative
      abundance, zero and negative pressure, tp without temperature).
- [x] 2026-09-12 — The solve runs unchanged inside an ILGPU kernel on the CPU
      accelerator with the same results as the host call:
      `KernelEqualityTests.Kernel_and_host_give_the_same_bits` over the 8 table families
      of the 106 cases (moles, multipliers, state, status and iterations bit for bit).
- [x] 2026-09-13 — Plateau states converge and match the reference: the
      melting-plateau fixture cases (RP-1311 example 13 generated with its insert
      list; the direct plateau stations of AP/HTPB/Al; the latent-heat-band hp
      cases) return `Ok` with both records of the pair in the solution, the
      temperature at the pair's `T*`, and every compared field — `γ_s`, the sound
      speed and the plateau zeros included — within the fixtures node's tolerance
      table: `FixtureSolveTests` over every tp and hp file of that day,
      `Performance.Tests.RocketFixtureTests` and
      `Problems.Tests.RocketTests.The_rocket_case_reproduces_the_reference_end_to_end`
      over `rp1311-example13` and the eight `ap-htpb-al-plateau` rocket files.
- [x] 2026-09-13 — The anti-cycling rule closes the include/remove cycle: an
      assigned enthalpy inside the `ALN(L)` gap of the fuel-rich AP/HTPB/Al chamber
      — where `AL4C3(cr)` near its 2500 K upper bound was included and lost every
      round, seen red before the stand-down rule was added — converges onto the
      pinned pieces
      (`PlateauTests.An_enthalpy_inside_the_ALN_gap_pins_the_pieces_at_the_cut`); a
      record removed for range re-enters when it is the only positive candidate, a
      second escape stands it down, and an `Ok` exit never hides a positive-gain
      candidate
      (`PlateauTests.An_enthalpy_no_admissible_set_can_hold_is_refused_rather_than_lied_about`
      walks exactly that path to an honest `NotConverged`, and
      `An_ok_solution_leaves_no_condensed_candidate_with_positive_inclusion_gain`
      holds over every hp fixture).
- [x] 2026-09-13 — Sweeps across a plateau lose no station: the pressure-ratio band
      across the AL2O3 plateau solves sequentially and one exit at a time onto the
      same stations, on the chamber isentrope throughout
      (`Problems.Tests.SplitRecordTests.A_sweep_across_the_alumina_plateau_stays_on_the_isentrope_by_either_path`);
      example 13's four exits cross the BeO plateau end to end
      (`RocketTests.The_rocket_case_reproduces_the_reference_end_to_end` over
      `rp1311-example13`); and tp solves at the printed bounds pick the record the
      reference picks (`FixtureSolveTests` over `ap-htpb-al-plateau_T2327`,
      `rp1311-example13-mixture_T2851` and `_T2373`), one kelvin beside the `ALN(L)`
      cut the gap test picking each side. The original wording asked for "fine"
      sweeps of both plateaus from both starts; the eight-ratio band and the
      four-exit example are that promise's committed form.

## Taboos

- No equilibrium constants, no reaction sets, no hand-picked species subsets: the
  root forbids them and they would make the results unreviewable.
- No deletion of species from the table; no reallocation of anything during a solve.
- No `float`, no exceptions, no allocations, no virtual calls: kernel code.
- No knowledge of nozzles, chambers or rockets: `Performance` owns those iterations.
- No transport formulas: `Transport` owns them and only takes this node's outputs.
