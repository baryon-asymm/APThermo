# BOOT.md — Problems.Tests

## Purpose

The definition of what "`Problems` is ready" means, and the front door of the whole
tree's acceptance: the end-to-end comparison with the reference implementation runs here.

| Level | What it checks | Against what (source of truth) | State |
|---|---|---|---|
| L0 | mass fractions, element moles and reactant enthalpy per kilogram; the oxidizer-to-fuel split; mole amounts; custom reactants; candidate species selection and order; input validation by name; the mass of a composition against one kilogram | the fixtures' recorded mass fractions, `elementMoles`, `reactantEnthalpy` / `enthalpy` and `products` (`PropellantTests`); documented behaviour, and the database's atomic weights for the mass a message reports (`RejectionTests`) | ✅ |
| L1 | every rocket, tp, hp and sp fixture solved singly from its propellant through the library | the fixtures node's reference outputs and its tolerance table (`RocketTests.The_rocket_case_reproduces_the_reference_end_to_end`, the three `EquilibriumTests` theories) | ✅ |
| L2 | end to end over every rocket fixture with transport, in shifting and frozen flow; a sweep as one batch against its cases one by one; an elemental mixture against its propellant; identical problems alone and in one call; mixed exit layouts in one call; state batches over unions of elements; a failing station as a status | the fixtures; the single-case results of the same code, bit for bit, or to rounding where a union reorders a case's elements (`RocketTests`, `EquilibriumTests`) | ✅ |
| L2 | the melting-plateau states through the front door: a cut record reported once under its database name; an assigned enthalpy inside the `ALN(L)` gap solves; a sweep across the alumina plateau stays on the isentrope by either path | the node's own rules where the reference cannot follow: the join-and-cut of the `Thermo` node, the plateau of the `Equilibrium` node, the isentrope of the station's own chamber (`SplitRecordTests`) | ✅ 2026-09-13 (the row written 2026-09-14, the ⚠ below) |
| L2 | the contract of 2026-09-14: a state record with exits against its case through the batch over mixtures; the refusals of the record's shape; a batch mixing transport and none against each problem alone; a ratio and pressure product as one batch against its cases one by one; every public method of a disposed solver; the tolerance rule against `Create` | the same code's single-case results, bit for bit; the `Problems` `API.md`; reflection over the solver's methods (`RocketTests`, `RejectionTests`) | ✅ 2026-09-14 |
| Bits | the front door's result of every rocket, tp, hp and sp fixture solved singly from its propellant, as the L1 theories solve it, gives the recorded bits: one line per fixture in `Bits.approved.txt`, the fixture path and the SHA-256 of the raw bits of the mixture's element moles in element order, its enthalpy and mass, then per station the state, the performance figures, the transport figures, the mole fractions and condensed mass fractions in the result's species order and the statuses, then the case status, in that order; and the reverse, an approved line no enumerated fixture produces, fails the test naming the stale key | the approved snapshot, recorded before any code of the front door's decomposition of 2026-09-14 moved | ✅ 2026-09-14 |
| Protocol | the tree invariant, documents against code | `AGENTS.md`, the surface snapshot | ✅ (2026-09-13, the Protocol.Tests node) |

⚠ 2026-09-14: the plateau row was missing. `SplitRecordTests` came with the
melting-plateau commit (`8e36a27`), whose criteria in the `Problems` node cite its three
facts while this node's table, criteria and mutations did not name them; found by the
clean-code review (F-TF-04). The evidence keeps the day it was obtained; the row and the
criterion below carry the day they were written.

## Invariants

- Reference is files; tolerances from the fixtures node; every fixture case is
  enumerated from the directory listing at run time (the theories' member data), so
  the root's first criterion is checked against a generated list.
- The reference's documented caveats (Fixtures BOOT.md) are applied by one comparison
  (`ReferenceComparison`, the caveats themselves and their field sets in
  `ReferenceCaveats`; both lived in `Comparison.cs` until the decomposition of
  2026-09-14), never by a test of its own: no `cv` at a frozen station; with
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
  from another solve of the same code; `b_i` and `h_0` are the fixture's. The trace
  threshold of the mole-fraction comparison is the fixtures node's
  `ToleranceTable.MoleFractionField` (2026-09-14: it first stood as
  `Comparison.TracePrintThreshold`, 5e-6 retyped under a comment naming the table, the
  review's F-TF-11; then as this node's own `ReferenceCaveats.TracePrintThreshold`
  reading the table's `moleFraction` entry and a selection line typed at the one call
  site; the architecture review's F-AR-03 found the same selection line typed here and
  in `Equilibrium.Tests` and `Performance.Tests` alike, and moved it to the fixtures
  node once for all three).
- **This node keeps its own reader of a fixture's outputs** (2026-09-14, the
  architecture review's F-AR-03): the field-name mapping (`ReferenceComparison.FieldValue`)
  and the set of fields that carry transport (`ReferenceCaveats.TransportFields`) stay
  here, not in the harness, which holds no formula and no tolerance. This node reads a
  station with transport figures on top of the state `Equilibrium.Tests` reads alone
  and the performance figures `Performance.Tests` reads on top of that; a shared
  reader would have to know all three shapes, which would put it above the nodes its
  readers' own consumers test.
- **The node owns the tolerances of comparisons that are not with the reference**
  (2026-09-14): two solves of the tree's own code that agree to rounding, a station on
  its chamber's isentrope, a pinned temperature on its transition bound. They are named
  constants of the node with their origin in a comment, never literals in an assertion
  (the review's F-TF-10 found thirteen such literals).
- **The bits are a tripwire, not a contract** (2026-09-14): the Bits level guards the
  front door's orchestration against unnoticed change the way the surface snapshot
  guards the contract (`AGENTS.md` §13). A moved line in `Bits.approved.txt` is
  legitimate only with the numerical change that moved it named in the same commit; a
  decomposition, a regrouping of batches, a renaming or a reordering of code moves no
  line. The snapshot is of the CPU accelerator on the reference machine's runtime; a
  runtime update that moves lines is re-approved with that reason recorded here. A
  fixture absent from the snapshot fails the test with instructions, as the surface
  snapshot does; and the reverse, an approved line no enumerated fixture produces
  (a deleted or renamed fixture), fails the test naming the stale key
  (`ApprovedSnapshot.StaleKeys`, the harness's own contract), so a fixture cannot
  drop out of the directory listing and out of this level's coverage unnoticed.

  ⚠ 2026-09-17: this bullet assumed one snapshot file. The root's platform constraint
  now keeps a Windows and a Linux record, since the CPU accelerator's `System.Math`
  calls the platform's C runtime and the two do not round the last bit alike;
  `ApprovedPath` resolves through `Harness.ApprovedSnapshot.ApprovedPathFor`
  (`tests/Harness/API.md`), which picks `Bits.approved.txt` or `Bits.linux.approved.txt`
  for the running platform, so this node's own code names no platform. The first Linux
  run (2026-09-17, WSL2 Ubuntu 24.04, `f67b1a9` plus this task's harness change) did not
  reproduce the Windows bits, within the tolerance the root BOOT.md records for the
  difference; `Bits.linux.approved.txt` was approved from that run.

  ⚠ 2026-09-19: the root BOOT.md's platform constraint (⚠ 2026-09-18, the declared
  deviation from "every test runs on both platforms") holds that the bits are a
  record of the reference machine, not of the platform alone: the first release run
  failed on a hosted Windows runner with one rocket case of this node's own snapshot
  changed in its last bits, every CEA tolerance test green. `Every_fixture_gives_the_recorded_bits`
  now carries `[Trait("Category", "BitSnapshot")]`, so it runs in every local run
  (`CLAUDE.md`'s fast set) and in the release's self-hosted jobs
  (`.github/workflows/release.yml`'s `cuda-windows` and `cuda-linux`, filter
  `Category=Cuda|Category=BitSnapshot`), and is filtered out of the hosted fast suite
  (`ci.yml`; `release.yml`'s `matrix` job; filter
  `Category!=LongRunning&Category!=BitSnapshot`), where the reference comparison of
  the front door's own tolerance tests holds correctness instead.
- **Bit comparison goes through the harness** (2026-09-14): `StationEquality`'s
  internal field-by-field bit comparison of `MixtureState`, `PerformanceFigures` and
  `TransportFigures` was, field for field, the harness's `Bits.Differences<T>`; its
  own `SameBits` was `Bits.Same`. `StationEquality.BitDifferences` and
  `RelativeDifferences` keep their signatures (a `Station` is not a flat struct: mole
  fractions and condensed mass fractions are dictionaries, and performance and
  transport figures are optional), but read the harness for the bit-exact leaves
  instead of repeating the comparison locally.
- ⚠ 2026-09-14, a declared duplication (`AGENTS.md` §12): the comparison of a case in
  a union batch that reorders its elements used the mole-fraction floor (1e-8) and the
  polish-threshold tier (1e-9) of the GPU/CPU table, copied from the execution tests
  node, whose code this node may not read. Resolved the same day (the review's
  F-TF-05): `RocketTests`' own `ReorderedElementsTolerance` and `MoleFractionFloor`
  constants are gone; the two facts that used them
  (`Rocket_problems_over_several_mixtures_are_one_batch_over_the_union_of_elements`,
  `Equilibrium_problems_over_several_mixtures_are_one_batch_over_the_union_of_elements`)
  now read `polishThresholdRelative` and `moleFractionFloor` from the fixtures node's
  tolerance table, the same two entries the execution tests node's own table also
  stopped duplicating.

## Dependencies

- [Problems](../../src/Problems/API.md) — what is being checked.
- [Data](../../src/Data/API.md) — the database.
- [Fixtures](../Fixtures/API.md) — reference cases and the tolerance table.
- [Execution](../../src/Execution/API.md) — the engine options, and a CPU engine for the defect signature on the reference's composition.
- [Thermo](../../src/Thermo/API.md) — `SpeciesTable` for that evaluation, `MixtureState`, `CaseStatus`.
- [Transport](../../src/Transport/API.md) — `TransportTable` for that evaluation, `TransportFigures`.
- [Performance](../../src/Performance/API.md) — `FlowModel`, `PerformanceFigures`.
- [Equilibrium](../../src/Equilibrium/API.md) — `ProblemKind`.
- [Harness](../Harness/API.md) — bit comparison (`Bits.Same`, `Bits.Differences`).

Outside the tree: xunit.

## Constraints

- Part of the default test command; no CUDA.
- Paths from the repository root; the only writes into the working directory are the
  `Bits.actual.txt` of a failed bit comparison, next to the approved file and
  git-ignored (2026-09-14; until that day the node wrote nothing), and, since
  2026-09-18 (the bits-diagnostics task), one `Bits.actual.<sanitized fixture path>.fields.txt`
  per differing fixture, beside it and git-ignored too (`tests/Harness/BOOT.md`, "a
  field dump is a caller's opt-in").
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
      (the facts: unknown reactant, temperature out of range, mixture rules, custom
      reactant with an unknown element, the `Only` list, state records, problems
      without the data they need, a disposed solver; 2026-09-13, two more: the mass
      of a composition against one kilogram through every front door, with the grams
      of the message checked against the database's atomic weights and the tolerance
      pinned by a record 0.9 % and one 1.1 % heavy,
      `A_composition_that_does_not_weigh_one_kilogram_is_rejected_with_its_mass_and_the_tolerance`;
      the propellant path through the committed file's `ADN` record, whose molar
      mass contradicts its formula,
      `A_reactant_record_whose_molar_mass_contradicts_its_formula_is_caught_at_the_solve`;
      that fact goes when the record is corrected upstream).

  ⚠ 2026-09-14: stood "nine facts" over a list of eight; a number repeating the length
  of a list, dropped (the clean-code review's F-TF-16).
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
- [x] 2026-09-13 — The mass check proven non-degenerate: the check removed from the
      solver (its comparison made never true), and
      `RejectionTests.A_composition_that_does_not_weigh_one_kilogram_is_rejected_with_its_mass_and_the_tolerance`
      and `A_reactant_record_whose_molar_mass_contradicts_its_formula_is_caught_at_the_solve`
      seen red, together with the Cli tests node's four unit-error documents and its
      line-naming test (that node's BOOT.md).
- [x] 2026-09-13 — The declared tolerance and the mass report (the `Problems`
      BOOT.md's design of the same day): `RejectionTests.The_tolerance_a_mixture_declares_is_the_one_applied`
      on the record made 2 % heavy (refused at the default, solved at 3 % through
      `Create` and through `StateBatchOptions` for every record of a batch; made 5 %
      heavy, refused at 3 % with the message naming `3 %`; the propellant path at the
      default; an invalid tolerance refused by name); `PropellantTests` on the report
      (`The_recorded_element_moles_of_every_fixture_weigh_one_kilogram_within_the_derivation_figure`:
      `Solver.MassOf` against `Σ n_i A_i` from `SpeciesDatabase.AtomicWeight` and
      within 1.7e-5 of one kilogram over the directory listing;
      `Results_carry_the_mass_of_their_mixture`: `MixtureMass` of every result against
      `MassOf`, and a mixture made 0.5 % heavy reporting 1.005). Heavy, not light:
      the record made 1 % to 10 % light does not converge as an hp state at 6.5 MPa
      (the `ALN(L)` record's 2700 K interval boundary, the front door's BOOT.md), and a
      case that fails numerically would not show that the check let it through.
- [x] 2026-09-13 — The declared tolerance and the mass report proven non-degenerate,
      each mutation alone and seen red: the check reading the default instead of the
      mixture's tolerance (`The_tolerance_a_mixture_declares_is_the_one_applied`, and
      the Cli tests node's option test); the equilibrium results reporting one
      kilogram instead of the measured mass, and the rocket results likewise
      (`Results_carry_the_mass_of_their_mixture`, each; the first also the Cli tests
      node's option test through the reported mass).
- [x] 2026-09-13 — The melting-plateau states through the front door (the level
      table's plateau row, written 2026-09-14): `SplitRecordTests`
      (`A_cut_species_reports_one_entry_under_its_database_name`,
      `An_enthalpy_inside_the_ALN_gap_solves_through_the_front_door`,
      `A_sweep_across_the_alumina_plateau_stays_on_the_isentrope_by_either_path`), the
      evidence the `Problems` node's criterion of that day cites. Their non-degeneracy
      was never recorded; the last criterion below records it.
- [x] 2026-09-14 — Bits level green: `BitSnapshotTests.Every_fixture_gives_the_recorded_bits`
      over the enumerated rocket, tp, hp and sp directories against `Bits.approved.txt`
      (213 fixtures), recorded before any code of the front door's decomposition moved
      (on a tree whose numerical nodes are bit for bit as at `8e36a27`, by their own
      Bits levels) and confirmed unchanged after every step of it:
      `git diff 8f8263c HEAD -- tests/Problems.Tests/Bits.approved.txt` and the
      working-tree diff both empty, checked repeatedly through steps 2 to 4 and last
      after the contract commit's collaborator-DRY pass and the mutation testing
      below; the test itself green in every fast-suite run of this node (1111/1111 the
      last time). Seen red once, at the snapshot's own commit (`8f8263c`, 2026-09-14):
      a reactant enthalpy per kilogram perturbed by a relative 1e-9 turned every one of
      the 213 fixtures red; a fixture line removed from `Bits.approved.txt` turned only
      that fixture red, naming it as missing. Both reverted before the commit.

      2026-09-15 (the clean-code repair's R-Problems.Tests-1): the reverse direction
      closed, the gap the Harness `BOOT.md` already recorded — an approved line no
      enumerated fixture produces now fails the test too, naming the stale key
      (`ApprovedSnapshot.StaleKeys`, over the same keys the fact enumerates). Seen red
      once: a fabricated line
      (`tests/Fixtures/cases/rocket/__mutation-stale-key-does-not-exist.json`, a fake
      hash) appended to `Bits.approved.txt` turned the fact red naming exactly that key
      as stale ("recorded in Bits.approved.txt but no enumerated fixture produced it");
      reverted, green again (`dotnet test tests/Problems.Tests --filter
      FullyQualifiedName~BitSnapshotTests`, 1/1 before, red with the fabricated line,
      1/1 after the revert). `Bits.approved.txt` itself unchanged (213 lines).
- [x] 2026-09-14 — The contract facts of 2026-09-14 (the level table's second new row):
      `RocketTests.A_state_record_with_exits_equals_its_case_through_the_batch_over_mixtures`
      (bit for bit, transport figures included);
      `RejectionTests.A_state_record_that_breaks_a_rule_of_its_shape_is_refused_with_its_index`
      (one case per rule: no target, two targets, exits without an enthalpy, a flow
      without exits, a record with exits given to `SolveStates`, one without given to
      `SolveRocketStates`, a negative abundance, a duplicated symbol; each a
      `StateRecordException` whose `Index` is the record's
      and whose `Reason` names the rule);
      `RocketTests.A_batch_mixing_transport_and_none_equals_each_problem_solved_alone`
      (bit for bit, and `TransportStatus` null where none was asked) with
      `Cases_are_grouped_by_exit_layout_and_transport_flag` over the runner's internal
      grouping, which is where the narrowed pass is visible;
      `RocketTests.A_ratio_and_pressure_product_as_one_batch_equals_its_cases_solved_one_by_one`,
      the sweep's fact on the batch over mixtures, replacing
      `A_sweep_equals_its_cases_solved_one_by_one`;
      `RejectionTests.Every_public_method_of_a_disposed_solver_throws` with
      `The_disposal_facts_cover_every_public_method_of_the_solver` (the list of methods
      from reflection); `RejectionTests.The_tolerance_rule_is_the_one_Create_applies`
      (`IsValidMassTolerance` false exactly where `Create` refuses). Each seen red once,
      reverted after: the kind check — `StateRecords.Validate`'s
      `record.HasExits != expectsExits` — removed, both rows of
      `A_state_record_that_breaks_a_rule_of_its_shape_is_refused_with_its_index` for the
      two routing reasons red ("no exception was thrown"), the other six rows
      unaffected; the grouping by the transport flag removed — `RocketRunner.Solve`'s
      per-layout `GroupBy(k => cases[k].Problem.Transport)` collapsed to one flag per
      exit-layout group (`groups[key].Any(...)`) — both
      `A_batch_mixing_transport_and_none_equals_each_problem_solved_alone` and
      `Cases_are_grouped_by_exit_layout_and_transport_flag` red, transport figures
      attached to a station whose own case never asked; one method's disposal guard
      removed — `CandidateSpeciesFor`'s `ThrowIfDisposed()` taken out,
      `Every_public_method_of_a_disposed_solver_throws` red for that entry ("no
      exception was thrown"). The same removal tried first on `SolveRocketStates`
      stayed green: its own guard is masked by the engine's disposal check reached
      through `RocketRunner.Solve`, so the fact the criterion asks for — every public
      method throws — still held; recorded here so a guard whose own removal is
      unobservable is not mistaken for one never tried. `dotnet test` on
      `Problems.Tests` after every revert: 1111/1111, `Bits.approved.txt` and
      `PublicSurface.approved.txt` unmoved.
- [x] 2026-09-14 — The support code in shape (the review's F-TF-01, F-TF-09, F-TF-10,
      F-TF-11, F-TF-14): `Comparison` becomes `SpeciesList` (the names with the gas
      count, `SpeciesList.cs`), `ReferenceCaveats` (the caveat facts of a station and
      their field sets, each citing the fixtures node, `ReferenceCaveats.cs`),
      `ReferenceComparison` (one station against one fixture station in six parameters
      — `reference, station, species, label, tolerances, caveats` — split into
      `TransportMismatches`, `StateAndPerformanceMismatches` and
      `MoleFractionMismatches`, the messages byte for byte as before,
      `ReferenceComparison.cs`) and `StationEquality` (bit and relative equality of two
      stations of the tree's own code, `StationEquality.cs`); the union test becomes
      three facts:
      `RocketTests.Rocket_problems_over_several_mixtures_are_one_batch_over_the_union_of_elements`,
      `Equilibrium_problems_over_several_mixtures_are_one_batch_over_the_union_of_elements`
      and `RejectionTests.A_batch_over_mismatched_mixtures_or_problem_counts_is_rejected`;
      every tolerance of a comparison with the tree's own code a named constant with its
      origin (eight added: `MoleFractionSumTolerance`, `MassBitRoundingTolerance`,
      `ReportedMassPrintTolerance`, `FormulaMassRoundingTolerance`,
      `MassOfSummationTolerance`, `ScaledMassSummationTolerance`,
      `TransitionBoundTolerance`, `OwnCodeIsentropeTolerance`, each with an origin
      comment, across ten former bare-literal sites); no method over 60 lines or nested
      deeper than 3 (unchanged by `dotnet build`, run clean every commit).

      The recorded mutations "the defect signature disabled"
      (`FixtureCases.DefectiveStationsOf`'s `TraceEliminations > 0` check disabled) and
      "mole fractions taken over the gaseous phase" (`StationFactory.Create`'s
      fractions loop narrowed from `speciesCount` to `table.GasCount`) red again after
      the split, both on `The_rocket_case_reproduces_the_reference_end_to_end`: the
      first over the three `lox-lh2_of4_*` and three `lox-lh2_of5_*` shifting-flow
      cases with transport (the reacting-conductivity and Prandtl skip no longer
      guarded, the tree's figures compared against the reference's own documented
      defect and found to disagree with it, as the skip exists to catch); the second
      over all ten `ap-htpb-al*` cases (every condensed species "not in the table",
      `Species` and `MoleFractions` collapsed to the gas phase). Each reverted; the
      fast suite green unchanged after (1111/1111 `Problems.Tests`).

      Each plateau fact of `SplitRecordTests` seen red once, none previously recorded
      (the level table's ⚠ of 2026-09-14 above): the cut-species collapsing in
      `StationFactory.SpeciesNames` disabled (always appended instead of collapsed) —
      `A_cut_species_reports_one_entry_under_its_database_name` red, `ALN(L)` counted
      twice, not once; the state record's pressure doubled on its way into the
      equilibrium problem in `StateRecords.ToEquilibriumProblems` (its enthalpy tried
      first: passing `null` instead of `record.Enthalpy` left the fact green, because
      `ProblemValidation.Equilibrium` defaults an unset assigned-enthalpy target to the
      mixture's own enthalpy, which for a state record is `record.Enthalpy` again — a
      finding in itself, recorded so the same non-mutation is not retried) —
      `An_enthalpy_inside_the_ALN_gap_solves_through_the_front_door` red, T =
      2770.839051872195 K against the 2700 K cut, no longer pinned;
      `TransitionBoundTolerance` tightened from 0.01 to 0 —
      `A_sweep_across_the_alumina_plateau_stays_on_the_isentrope_by_either_path` red, a
      pinned station at 2327.000012414645 K against the exact bound 2327, the residual
      the 0.01 K tolerance exists to absorb. Each reverted; `dotnet test` on
      `Problems.Tests` after every revert: 1111/1111, `Bits.approved.txt` unmoved
      throughout (`git diff` empty against `8f8263c` and in the working tree).
- [x] 2026-09-18 — The Bits level's per-case field dump (bits-diagnostics task): `Record`
      passes its `BitHash`'s `Fields` to `ApprovedSnapshot.Problem`
      (`tests/Harness/BOOT.md`, "a field dump is a caller's opt-in"), so a fixture that
      disagrees with `Bits.approved.txt` also gets its own
      `Bits.actual.<sanitized path>.fields.txt`, every hashed field as a round-trip
      double or an int/bool, one per line, in the order `HashOf` adds them. Shown red
      once and the dump inspected: the last hex digit of
      `tests/Fixtures/cases/rocket/lox-lh2_of4_pc5MPa_frozenAtThroat.json`'s line in
      `Bits.approved.txt` changed from `3` to `0` (the exact fixture the release run of
      2026-09-17 flagged, `SCRATCH/nondeterminism-report.txt`), `dotnet test
      tests/Problems.Tests -c Release --filter FullyQualifiedName~BitSnapshotTests` red
      on that one key, and
      `Bits.actual.tests_Fixtures_cases_rocket_lox-lh2_of4_pc5MPa_frozenAtThroat.json.fields.txt`
      written beside `Bits.actual.txt` with the case's 173 fields; copied out as
      `SCRATCH/bits-diag/reference-lox-lh2_of4_pc5MPa_frozenAtThroat.txt` before the
      approved line was restored byte for byte (`git diff` empty) and the test green
      again, 1/1. No approved file moved by this criterion.

## Taboos

- Do not loosen a tolerance for green; do not exclude a failing propellant or station.
- Do not skip a reacting field without proving the reference's defect visible at that station.
- Do not compute `b_i` or `h_0` in the test with the code under test.
