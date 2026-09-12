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
  condensed species enter and leave the solution by the report's tests; a species is
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
  per-case scratch (mole numbers, logarithms, `G°/RT` per species, the iteration
  matrix and right-hand side, pivot indices) is passed in as views sliced from
  batch-sized buffers by the caller.
- Unknowns of the reduced system: elements + condensed species in the solution + 1
  (total moles) + 1 (temperature, hp and sp only). Limits: at most 20 elements and at
  most 8 condensed species in the solution at once, hence a matrix of at most 30 × 30.
  The dense solve is Gaussian elimination with partial pivoting, internal to this node.
- Initial estimates as in the report: every gaseous species at `0.1 / SpeciesCount`
  kmol per kg when no estimate is given, `T = 3800 K` for hp and sp when no estimate
  is given; callers may pass a previous solution as the estimate (the nozzle does).
- Convergence tests and control factor exactly as RP-1311 chapter 3 (the `λ` damping
  with the two branches for species above and below the trace threshold, the tests on
  `Δln n_j`, `Δln n`, `Δln T`, the condensed-species mole numbers, and the element
  residuals); trace threshold `ln(n_j/n) = −18.420681` (that is `n_j/n = 1e-8`) as in
  the report. Iteration cap: 50 Newton steps after the last change of the condensed
  species set, and at most 10 changes of that set per case.
- Condensed species: inclusion test after convergence of the gaseous problem (the
  species whose `Σ π_i a_ij − g_j/RT` is largest and positive is added, one at a
  time); a condensed species with a negative mole number is removed; a condensed
  species outside its temperature range is not a candidate at that temperature, and
  phase transitions between records of one substance are handled by removing the
  out-of-range record and letting the in-range one enter; singular matrices are
  reported as `SingularMatrix` after the report's remedies (removing the species that
  caused it) have been tried.
- Frozen mode: with the composition fixed, solve for the temperature that gives the
  requested enthalpy or entropy (Newton on `T`) and compute the frozen properties;
  this mode serves frozen nozzle flow.
- Outputs: mole numbers per species (kmol per kg of mixture), the mixture state
  (`MixtureState`: T, p, ρ, h, s, g, M, frozen and equilibrium Cp, the two derivatives,
  γ_s, sound speed), the Lagrange multipliers (needed by `Transport` for the reacting
  conductivity), the iteration count and the status.
- Property definitions as in RP-1311 section 2.6: `Cp_eq` includes the composition
  derivatives; `γ_s = −(∂ln p/∂ln V)_s`; `a² = n R T γ_s` per unit mass; `M = 1/n`
  with `n` the total gaseous moles per kilogram; the condensed species are included
  in h, s and Cp of the mixture as in CEA.

## Acceptance criteria

- [ ] tp problems: for the product mixtures of the four reference propellants and the
      RP-1311 examples in the fixtures node, the mole fractions of every species the
      reference prints agree within the fixtures node's tolerance table; the list of
      compared species is generated from the fixture, not typed.
- [ ] hp problems: the adiabatic flame temperature and the composition of the same
      cases agree within the tolerance table.
- [ ] sp problems: the temperature and composition at given entropy and pressure
      agree within the tolerance table (the nozzle stations of the reference rocket
      cases serve as fixtures).
- [ ] Condensed species: the AP/binder/aluminium case includes `AL2O3(L)` in the
      chamber with the reference mass fraction, and the low-temperature RP-1311
      example (example 14, water condensation) reproduces the reference phase changes.
- [ ] Derivatives: `Cp_eq`, `γ_s` and the sound speed agree with the reference within
      the tolerance table for every converged fixture case.
- [ ] Element conservation holds for every converged fixture case at the invariant's
      tolerance (checked by the test over the machine-generated list of cases).
- [ ] A case with an absent element gives the same result as the same case solved on
      a table without that element's species (bit for bit on the same accelerator); a
      case with an empty table or with every abundance zero returns `InvalidInput` and
      writes nothing else.
- [ ] The solve runs unchanged inside an ILGPU kernel on the CPU accelerator with the
      same results as the host call.

## Taboos

- No equilibrium constants, no reaction sets, no hand-picked species subsets: the
  root forbids them and they would make the results unreviewable.
- No deletion of species from the table; no reallocation of anything during a solve.
- No `float`, no exceptions, no allocations, no virtual calls: kernel code.
- No knowledge of nozzles, chambers or rockets: `Performance` owns those iterations.
- No transport formulas: `Transport` owns them and only takes this node's outputs.
