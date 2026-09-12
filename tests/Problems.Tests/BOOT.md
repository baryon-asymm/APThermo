# BOOT.md — Problems.Tests

## Purpose

The definition of what "`Problems` is ready" means, and the front door of the whole
tree's acceptance: the end-to-end comparison with the reference implementation runs here.

| Level | What it checks | Against what (source of truth) | State |
|---|---|---|---|
| L0 | mass fractions, element moles and reactant enthalpy per kilogram; the oxidizer-to-fuel split; mole amounts; custom reactants; candidate species selection and order; input validation by name | the fixtures' recorded mass fractions, `elementMoles`, `reactantEnthalpy` / `enthalpy` and `products` (`PropellantTests`); documented behaviour (`RejectionTests`) | ✅ |
| L1 | every rocket, tp, hp and sp fixture solved singly from its propellant through the library | the fixtures node's reference outputs and its tolerance table (`RocketTests.The_rocket_case_reproduces_the_reference_end_to_end`, the three `EquilibriumTests` theories) | ✅ |
| L2 | end to end over every rocket fixture with transport, in shifting and frozen flow; a sweep as one batch against its cases one by one; an elemental mixture against its propellant; identical problems alone and in one call; mixed exit layouts in one call; state batches over unions of elements; a failing station as a status | the fixtures; the single-case results of the same code, bit for bit, or to rounding where a union reorders a case's elements (`RocketTests`, `EquilibriumTests`) | ✅ |
| Protocol | the tree invariant, documents against code | `AGENTS.md`, the surface snapshot | ⏳ (the protocol tests node) |

## Invariants

- Reference is files; tolerances from the fixtures node; every fixture case is
  enumerated from the directory listing at run time (the theories' member data), so
  the root's first criterion is checked against a generated list.
- The reference's documented caveats (Fixtures BOOT.md) are applied by one comparison
  (`Comparison.cs`), never by a test of its own: no `cv` at a frozen station; with
  transport on and condensed species present, the frozen `cp` against the transport
  set's; the reacting conductivity and Prandtl number not compared where the
  reference's value is defective, and at such a station the defect must be visible
  (the tree's reacting conductivity must not agree with the inflated one), so that the
  skip cannot hide a real disagreement; mole fractions by name, with the trace rule
  (`moleFractionTrace` below a printed 5e-6).
- A defective station is identified on the reference's own composition: the tree's
  transport solver, run through a CPU engine of the execution node on the reference's
  mole fractions, eliminates a trace component there (the signature the Transport
  tests use). On the tree's own composition the basis may differ and the signature
  vanish (the LOX/LH2 O/F 4 exits); identification there alone would compare the tree
  against the inflated value and fail.
- The reference rounds an oxidizer-to-fuel ratio to single precision before splitting
  the kilogram (Fixtures BOOT.md): L0 checks the exact path (the propellant given by
  the mass fractions the reference recorded) at 1e-10 relative and the ratio path at
  1e-7, derived from single precision's 6e-8.
- L2 runs on the CPU accelerator in the default command; the same cases on CUDA are
  the execution tests node's business.
- No expected value is typed into a test: everything comes from the fixture files or
  from another solve of the same code; `b_i` and `h_0` are the fixture's.

## Dependencies

- [Problems](../../src/Problems/API.md) — what is being checked.
- [Data](../../src/Data/API.md) — the database.
- [Fixtures](../Fixtures/API.md) — reference cases and the tolerance table.
- [Execution](../../src/Execution/API.md) — the engine options, and a CPU engine for the defect signature on the reference's composition.
- [Thermo](../../src/Thermo/API.md) — `SpeciesTable` for that evaluation, `MixtureState`, `CaseStatus`.
- [Transport](../../src/Transport/API.md) — `TransportTable` for that evaluation, `TransportFigures`.
- [Performance](../../src/Performance/API.md) — `FlowModel`, `PerformanceFigures`.
- [Equilibrium](../../src/Equilibrium/API.md) — `ProblemKind`.

Outside the tree: xunit.

## Constraints

- Part of the default test command; no CUDA.
- Paths from the repository root; no writes into the working directory.
- One solver and one engine on the CPU accelerator are shared by the collection.

## Acceptance criteria

- [x] 2026-09-12 — L0 green: `PropellantTests`
      (`Element_moles_and_enthalpy_equal_the_reference_from_its_mass_fractions`,
      `A_ratio_split_reproduces_the_reference_mass_fractions_within_its_single_precision`,
      `Candidate_species_equal_the_reference_product_list`, each over every rocket,
      tp, hp and sp file; `Mole_amounts_are_converted_with_the_record_molar_mass`,
      `A_custom_reactant_derives_its_molar_mass_from_the_formula_and_the_atomic_weights`,
      `Candidates_are_gases_then_condensed_species_in_database_order`,
      `An_elemental_mixture_normalizes_symbols_and_keeps_the_order`); `RejectionTests`
      (nine facts: unknown reactant, temperature out of range, mixture rules, custom
      reactant with an unknown element, the `Only` list, state records, problems
      without the data they need, a disposed solver).
- [x] 2026-09-12 — L1 green for every fixture case, the list generated from the
      directory listing: `RocketTests.The_rocket_case_reproduces_the_reference_end_to_end`
      over `cases/rocket` (89 files that day),
      `EquilibriumTests.Assigned_temperature_cases_reproduce_the_reference`,
      `Assigned_enthalpy_cases_reproduce_the_reference`,
      `Assigned_entropy_cases_reproduce_the_reference` over `cases/tp`, `cases/hp`,
      `cases/sp` (106 files).
- [x] 2026-09-12 — L2 green: `RocketTests` (`A_sweep_equals_its_cases_solved_one_by_one`,
      `An_elemental_mixture_reproduces_its_propellant_bit_for_bit`,
      `Identical_problems_give_identical_results_alone_and_in_one_call`,
      `Problems_with_different_exit_layouts_are_solved_in_one_call_in_order`,
      `A_failing_station_is_a_status_and_not_an_exception`,
      `Compositions_are_reported_by_name_over_all_species`; 2026-09-13:
      `Rocket_and_equilibrium_problems_over_several_mixtures_are_one_batch_over_the_union_of_elements`);
      `EquilibriumTests`
      (`State_batches_over_the_union_of_elements_reproduce_the_reference`,
      `The_default_enthalpy_of_an_assigned_enthalpy_problem_is_the_propellants`,
      `Transport_figures_are_attached_to_an_equilibrium_state_when_requested`).
- [x] 2026-09-12 — Every check proven non-degenerate once, each mutation applied
      alone and seen red: the oxidizer share of the ratio split perturbed by 1e-6 (the
      ratio-split test and the end-to-end test); a reactant enthalpy per kilogram
      perturbed by 1e-9 (the element-moles-and-enthalpy test); a reference chamber
      temperature altered in a fixture file (the end-to-end test); one species dropped
      from the selection rule (the candidate-species test and the end-to-end test);
      the ion rule reverted to the name's trailing sign (the candidate-species test);
      the temperature margin set to zero (the temperature-range test on `AL(cr)` at
      298.15 K); the defect signature disabled (the end-to-end test at the LOX/LH2
      O/F 4 exits with transport); mole fractions taken over the gaseous phase (the
      end-to-end test on the aluminized propellant).

## Taboos

- Do not loosen a tolerance for green; do not exclude a failing propellant or station.
- Do not skip a reacting field without proving the reference's defect visible at that station.
- Do not compute `b_i` or `h_0` in the test with the code under test.
