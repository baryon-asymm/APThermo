# BOOT.md — Problems

## Purpose

The front door of the library: propellants and reactants, the assembly of the
chemical system (elements, candidate species, element moles and enthalpy per kilogram
of propellant), the problem and result types a user works with, and the
orchestration of `Data`, `Thermo`, `Transport` and `Execution` into one call, with the
result types of `Performance`. The conventions here (what a reactant is, how an
oxidizer-to-fuel ratio becomes mass fractions, which species are candidates) are
knowledge about propellants and about NASA CEA's habits, not about solving, which is
why they are a node of their own.

## Invariants

- **The species set of a batch is fixed by the element set and the species lists**,
  not by the amounts: the candidate list depends only on the elements present in the
  union of the reactants, or in the union of the elemental records of a state batch,
  and on the `Omit` or `Only` list; changing amounts, pressures or exits never changes
  the table. Cases of one batch share one `SpeciesTable`; a case in which an element
  is absent runs with the species containing it inactive (see the `Equilibrium`
  contract), not with another table.
- **Two front doors, one path.** A mixture given by reactants and a mixture given by
  element moles and enthalpy meet in the same `ElementalMixture` before anything else
  happens; every solve starts from element moles per kilogram and an enthalpy, whatever
  the input was.
- **Candidate species are chosen by one rule**: every gaseous product species of the
  database whose elements are all among the mixture's elements, then every condensed
  product species under the same condition, each in database order, minus the `Omit`
  list, or exactly the `Only` list when given; ionized species (the electron
  pseudo-element `E` in the formula) and inert pseudo-element records are never
  candidates in version 1; a name with several records (a condensed species with one
  record per temperature range) is one candidate. An omitted name that is no product
  species is ignored, as the reference ignores it: the RP-1311 example 3 omit list
  names reactant-only species and old spellings.

  ⚠ 2026-09-12: stood "ionized species (names ending in `+` or `-`, and `e-`)". A
  trailing sign is no criterion: the database truncates names such as `C3H4,cyclo-`,
  and forty neutral species end in `-`. The end-to-end comparison reported them "not
  in the table" while the reference's product lists carried them. An ion carries `E`
  in its formula, and that is what the rule reads.
- **Element moles and enthalpy are computed from the database records**, per
  kilogram of propellant: `b_i = Σ_k w_k a_ik / M_k`, `h_0 = Σ_k w_k H_k(T_k) / M_k`,
  with `H_k(T_k)` from the record's polynomial at the reactant's temperature,
  evaluated through the execution node's species-function batch (the tree's one
  implementation of the species functions lives in `Thermo` and runs on an
  accelerator), or the assigned enthalpy for records without intervals and for custom
  reactants. A record with intervals defaults to `Reactant.DefaultTemperature`
  (298.15 K), a record without intervals to its assigned temperature. A reactant
  temperature is accepted within the record's range widened by
  `PropellantBuilder.TemperatureMargin` (10 K) on either side, and the nearest
  interval is evaluated there; beyond it the reactant is rejected by name.

  ⚠ 2026-09-12: stood "a reactant temperature outside the record's range is an error,
  not an extrapolation". The reference's own notion of a record's valid range is the
  fit range, or the assigned temperature ± 10 K for a record without fits, and it
  evaluates the polynomial without a range check: the AP/HTPB/Al fixtures carry
  `AL(cr)` at 298.15 K against its 300 K lower bound, and a strict rule would reject
  the reference's own inputs. The margin mirrors the reference's ± 10 K.
- **Amounts are mass based.** Oxidizer and fuel amounts within their group are
  normalized to one; the oxidizer-to-fuel ratio splits the kilogram as
  `w_ox = OF / (1 + OF)`, `w_fuel = 1 / (1 + OF)`; a propellant given by total mass
  fractions is used as given after normalization to one; mole amounts are converted
  to mass with the record's molar mass before anything else.
- **SI in, SI out, names out.** Public types carry SI units and species names; no
  index leaves this node.
- **Statuses become results or exceptions, once.** A per-case failure is a
  `CaseStatus` in the result record; an infrastructure failure is an exception from
  `Execution` passed through; nothing is retried silently.
- **Immutable inputs.** Propellants and problems are immutable records; a solve never
  mutates them.

## Dependencies

- [Data](../Data/API.md) — the species database and atomic weights.
- [Thermo](../Thermo/API.md) — table building, `MixtureState`, `CaseStatus`, the gas constant.
- [Equilibrium](../Equilibrium/API.md) — `ProblemKind`, the kind of an equilibrium problem.
- [Performance](../Performance/API.md) — `FlowModel`, `ExitSpecification`, `PerformanceFigures`.
- [Transport](../Transport/API.md) — transport table building and `TransportFigures`.
- [Execution](../Execution/API.md) — the engine, the batch containers and the species-function batch.

Outside the tree: the .NET base class library.

⚠ 2026-09-12: the root's decomposition listed this node's dependencies without
`Equilibrium`. The kind of an equilibrium problem is `Equilibrium`'s `ProblemKind`,
which the execution node's batch takes and this node's `EquilibriumProblem` exposes;
a link is truer than a retold enum, and the root records the same.

## Constraints

Inherited from the root ([BOOT.md](../../BOOT.md)). In addition:

- Ordinary .NET code; the only allocations of a solve happen here and in `Execution`.
- Reactants are database records by name, or custom reactants given by name, formula
  (element counts), molar mass (derived from the formula and the atomic weights when
  not given), enthalpy at a temperature (J/mol), and that temperature. This is how
  binders such as HTPB are defined, exactly as CEA's exploded-formula reactants.
- Mixture specifications supported in version 1: oxidizer-to-fuel ratio, total mass
  fractions, per-reactant moles (converted to mass). Equivalence ratios and percent
  fuel are not in version 1 (they need element valences typed into code, which the
  root forbids; a later version may read them from a data file).
- The database is loaded by the caller and passed in; this node never opens files.
- Element order of a chemical system: the order of first appearance in the reactants
  (oxidizers, then fuels, then named reactants, each in the order given), or across
  the records of a state batch; species order: gaseous species in database order,
  then condensed species in database order. Both are reported in the result, because
  compositions are returned by name.
- Results carry the composition of every station as mole fractions over all species
  of the table (`n_j` over the sum of the moles of gaseous and condensed species, the
  reference's convention) and, for condensed species, also as mass fractions
  `n_j M_j`, both by name, without a threshold; thresholds are a presentation concern
  of the command line.

  ⚠ 2026-09-12: stood "mole fractions of the gaseous phase and mass fractions of
  condensed species". The reference reports mole fractions over all species, and a
  result that compares to it without a conversion is worth more than a gas-phase
  convention nobody asked for; the condensed mass fractions stay.
- Batch construction: one propellant definition (reactant set and temperatures) with
  per-case amounts (oxidizer-to-fuel ratio or mass fractions), chamber pressure and
  exit values; the number of exits per batch is fixed by the batch, the values vary
  per case. Rocket problems with different exit layouts given in one call are grouped
  by layout, one batch per group, and the results come back in the order given. Several
  mixtures with one problem each (rocket or equilibrium) are one batch over the union of
  their elements when their species lists agree. In such a batch the results of a case
  equal those of the case solved with its own table to rounding: bit for bit, the
  transport figures included, when the union (elements in order of first appearance)
  keeps the relative order of the case's elements; within 1e-9 relative when it
  reorders them, because the linear solves pivot in element order and the iterates
  then differ by rounding at every step. The transport set of a case counts the gases
  of the case, not of the table (the transport node's `BOOT.md`, 2026-09-13), so the
  larger table changes no figure. A
  state batch is a list of records, each with its own element moles, pressure and one
  target (enthalpy, temperature or entropy); its element set is the union over the
  records.
- Units at this boundary: element abundances are accepted in mol per kg and passed
  to the numerical nodes in kmol per kg (the CEA convention), the one conversion this
  node makes besides mass normalization; enthalpy in J/kg is passed unchanged.
- Element symbols are matched to the database spelling case-insensitively (`Al`,
  `al` and `AL` are the same element); the result reports the database spelling.
- Single-case calls are batches of one.
- The solver keeps the uploaded tables of every element set and species list it has
  seen, and the reactant enthalpies of every propellant instance, until it is disposed.

## Acceptance criteria

- [x] 2026-09-12 — For the RP-1311 examples and the four reference propellants, the
      element moles per kilogram and the reactant enthalpy per kilogram computed here
      equal the reference's (the fixtures record the mass fractions, `elementMoles`
      and `reactantEnthalpy`) within 1e-10 relative:
      `PropellantTests.Element_moles_and_enthalpy_equal_the_reference_from_its_mass_fractions`
      over every rocket, tp, hp and sp file (the list from the directory listing, 195
      that day), the propellant given by the mass fractions the reference recorded.
      The ratio path (`A_ratio_split_reproduces_the_reference_mass_fractions_within_its_single_precision`)
      holds at 1e-7: the reference rounds the ratio to single precision before
      splitting the kilogram (Fixtures BOOT.md), so its own mass fractions carry that
      rounding; mole amounts: `Mole_amounts_are_converted_with_the_record_molar_mass`.
- [x] 2026-09-12 — The candidate species list for each fixture case equals the
      reference's product list under the same `Omit` list, or the `Only` list the
      reference was given (RP-1311 examples 1 and 12), compared as sets and by count:
      `PropellantTests.Candidate_species_equal_the_reference_product_list` over the
      same files; the order rule: `Candidates_are_gases_then_condensed_species_in_database_order`.
- [x] 2026-09-12 — A custom reactant (the AP/binder case's binder) produces the
      reference `b_i` and `h_0`: the AP/HTPB/Al files of the first criterion, and
      `A_custom_reactant_derives_its_molar_mass_from_the_formula_and_the_atomic_weights`.
- [x] 2026-09-12 — An `ElementalMixture` built from the `b_i` and `h_0` of a fixture
      propellant gives the same rocket and equilibrium results as the propellant itself,
      bit for bit on the same accelerator
      (`RocketTests.An_elemental_mixture_reproduces_its_propellant_bit_for_bit`); a
      state batch of the fixture stations reproduces the fixtures within the tolerance
      table, including records where an element of the batch is absent
      (`EquilibriumTests.State_batches_over_the_union_of_elements_reproduce_the_reference`;
      2026-09-13 for the batch over several mixtures:
      `RocketTests.Rocket_and_equilibrium_problems_over_several_mixtures_are_one_batch_over_the_union_of_elements`).
- [x] 2026-09-12 — End-to-end: every rocket fixture (the four reference propellants in
      shifting and frozen flow, with and without transport, and the RP-1311 rocket
      examples) and every tp, hp and sp fixture through this node match the fixtures
      within the tolerance table: `RocketTests.The_rocket_case_reproduces_the_reference_end_to_end`
      and the three `EquilibriumTests` theories of the front door tests node, over the
      directory listings.
- [x] 2026-09-12 — A reactant temperature outside its record's range, an unknown
      reactant, a mixture with a zero-mass group, or an element without an atomic
      weight are rejected with the reactant's name in the exception, before any kernel
      runs: `RejectionTests` (`An_unknown_reactant_is_rejected_by_name`,
      `A_temperature_outside_the_record_range_is_rejected_by_name`,
      `Mixture_rules_that_leave_a_group_empty_or_ambiguous_are_rejected`,
      `A_custom_reactant_with_an_unknown_element_is_rejected_by_name`,
      `An_only_list_beyond_the_elements_is_rejected_and_a_valid_one_is_used_as_given`,
      `Invalid_state_records_are_rejected_by_index_or_element`,
      `Problems_without_the_data_they_need_are_rejected`, `A_disposed_solver_refuses_work`).
- [x] 2026-09-12 — Two identical batches produce identical results (statuses and
      numbers): `RocketTests.Identical_problems_give_identical_results_alone_and_in_one_call`,
      `A_sweep_equals_its_cases_solved_one_by_one`,
      `Problems_with_different_exit_layouts_are_solved_in_one_call_in_order`.

## Taboos

- No numerical formula of the solvers here: this node computes only what a
  propellant definition implies (`b_i`, `h_0`, tables).
- No evaluation of a species polynomial here: a reactant record's enthalpy comes
  from the execution node's species-function batch, the one implementation.
- No file access: the database comes from the caller.
- No silent defaults for missing data: an unknown species or a missing enthalpy is an error.
- No unit other than SI in a public type; no seconds for specific impulse.
- No index-based composition in a result: names only.
