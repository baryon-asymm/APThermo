# BOOT.md — Cli.Tests

## Purpose

The definition of what "`Cli` is ready" means.

| Level | What it checks | Against what (source of truth) | State |
|---|---|---|---|
| L0 | option parsing, the usage text, exit codes; input document reading and its messages with the JSON path; the example documents against the input and states schemas; a composition that does not weigh one kilogram refused naming the record | the command line's embedded schemas; the documented messages and exit codes (`CommandLineTests`, `InputDocumentTests`, `ExitCodeTests`) | ✅ |
| L1 | every example document of `documents/` and of the `Cli` API runs end to end on the CPU accelerator and validates against the output schema; sweeps, states files, thresholds, transport, the listings; the CSV layout against the approved file | the command line's embedded schemas; the approved CSV; the documented orders (`OutputDocumentTests`, `CsvTests`) | ✅ |
| L2 | the LOX/LH2 rocket document, the LOX/RP-1 hp document and the elemental tp document give the library's numbers field by field | the `Problems` result of the same case, built from the fixture the document encodes, over reflection-enumerated fields (`LibraryEqualityTests`) | ✅ |
| Process | one run per exit code as a separate process: real exit codes and standard streams | the documented exit codes (`ProcessTests`) | ✅ |
| L0 | the exception → exit code rule; the usage's defaults are the library's constants; every command of the table has a handler; `cudaSkippedBecause` in `run.accelerator` and in the devices listing; the invalid state records refused with the front door's reasons behind their source | the `Cli` `API.md` of 2026-09-14 (`ExitCodeTests`, `CommandLineTests`, `OutputDocumentTests`, `InputDocumentTests`) | ✅ (2026-09-14) |
| L2 | the states example gives the library's numbers field by field: the records without exits through `SolveStates`, the records with exits through `SolveRocketStates`, the library call built from the fixtures the records encode | the `Problems` results (`LibraryEqualityTests`) | ✅ (2026-09-14) |
| Bits | the output of every example that runs (the problem and states documents of `documents/`, the problem and record examples of the `Cli` API, the `species` listing): the SHA-256 of the bytes the command line delivers for the JSON document with the top-level `run` property cut out (`RunPropertyCut`, the span found with a `Utf8JsonReader`, never by searching the text), and the SHA-256 of the CSV text as written, one of each per example in `Bits.approved.txt`; `run` is left out because it carries the machine, the paths, the version and the timings | the approved snapshot, recorded before any code of the decomposition of 2026-09-14 moved and re-approved 2026-09-15 for the hash definition alone (the criterion below) | ✅ (2026-09-15) |
| L0 | `apthermo schema`: every embedded schema is delivered byte for byte to standard output and to `--output`; the embedded names (`SchemaResources.Names`) equal a directory listing of `src/Cli/Schemas/`; a missing or an unknown name is exit code 2, no document, every embedded name in the message | a directory listing of `src/Cli/Schemas/`, never a typed list (`SchemaCommandTests`) | ✅ (2026-09-17) |
| Protocol | the tree invariant, documents against code | `AGENTS.md`, the surface snapshot | ✅ (2026-09-13, the Protocol.Tests node) |

## Invariants

- Documents in tests are files in this node (`documents/`, generated from the
  fixtures so that they encode real cases; `documents/invalid/` for the rejected
  ones), not strings in code; the examples of the `Cli` API are read from `API.md`
  itself, so they cannot drift from what the reader accepts.
- The CLI is exercised in-process through its entry point and, once per exit code,
  as a separate process, so that exit codes and standard streams are real.
- The schemas are checked by the Harness `JsonSchema` validator that knows exactly the
  keywords the schemas use and refuses any other, so a schema cannot ask for more
  than is checked; the schema's field lists are compared with the library's structs
  by reflection.

  ⚠ 2026-09-17: this bullet stood "a validator of this node", and the level table above
  ("What it checks") stood "the schema files of this node"; both wordings were rewritten
  in place for the schemas' move on 2026-09-16 with no correction note (the audit's T1).
  The root's Delivery decision of 2026-09-15 (committed `cb765c6`) moved the schema files
  from this node's `schemas/` directory to `src/Cli/Schemas/`, embedded in the assembly,
  and the JSON-document helpers from this node to `tests/Harness`, their second consumer
  `tests/Docs.Tests` belonging there (`tests/Harness/BOOT.md`, Purpose): the schemas are
  no longer this node's own, and neither is the validator.
- The approved CSV is compared as numbers (1e-12 relative) and strings, never as
  floating-point text; the library equality is exact, because the numbers pass
  through unchanged.
- The library call of L2 is built from the fixture the document encodes, never from
  the document, so that a unit or a value changed in the document is seen.
- g0 is transcribed from the root here on purpose (`LibraryEqualityTests.StandardGravity`):
  the library equality computes its expectation without the code under test, so that
  a changed g0 in the node is seen (the mutation "g0 changed" below). The architecture
  review of 2026-09-14 proposed reading the node's constant instead (F-AR-08); declined
  for that reason, and the two transcriptions of the root's figure are the check.
- **The grams of a refusal are derived, the wording is pinned** (2026-09-14): the mass a
  message reports is computed from the document's composition with the database's
  atomic weights, never typed, while the sentence around it is the documented message
  (the review's F-TF-12 found `2000.03 g` typed in two files).
- **The node owns the tolerances of comparisons that are not a document's contract**
  (2026-09-14): ratios and values read back from a document, a mass against its
  expectation. They are named constants with their origin in a comment, never literals
  in an assertion (the review's F-TF-10).
- **The bits are a tripwire, not a contract** (2026-09-14): the Bits level guards the
  documents against unnoticed change the way the surface snapshot guards the contract
  (`AGENTS.md` §13). A moved line in `Bits.approved.txt` is legitimate only with the
  change of the documents that moved it named in the same commit; a decomposition, a
  renaming or a reordering of code moves no line. An example absent from the snapshot
  fails the test with instructions, as the surface snapshot does. The reverse direction
  is checked too (2026-09-15, R-Cli.Tests-1): an approved line whose example no longer
  runs is a stale key, found through `Harness.ApprovedSnapshot.StaleKeys` and reported
  the same way, `{key}: recorded in {ApprovedPath}, but no example produces it; delete
  the line in the commit that removed the example`, so a deleted example's line cannot
  survive unnoticed either.
- **The snapshot mechanics go through the harness** (2026-09-14): the hand-rolled
  tab-delimited reader/writer (`BitFile`) and the line-by-line comparison this node
  wrote for its own three-field lines (a name, then a JSON and a CSV SHA-256,
  tab-separated: the one format of the tree's bit snapshots a fixture path never
  needs, since an example name can hold a space) are gone; `BitSnapshotTests` reads
  `Harness.ApprovedSnapshot`, whose own delimiter detection reads a file exactly this
  shape. The two SHA-256 digests per example are still this node's own
  (`BitExamples.Sha256`), now through `Harness.BitHash` instead of a local call to
  `System.Security.Cryptography`, since a plain string's UTF-8 bytes hash the same
  way either way.

## Dependencies

- [Cli](../../src/Cli/API.md) — what is being checked.
- [Cli.Syntax](../../src/Cli/Syntax/API.md) — `CommandLine.Parse`, `CommandTable`, `CommandOptions`, `Invocation`, `OutputFormat`, exercised directly by `CommandLineTests`.
- [Cli.Documents](../../src/Cli/Documents/API.md) — `ProblemDocumentReader`, `StateRecordReader`, `SweepValues`, the document shapes, exercised directly by `InputDocumentTests` and `BitSnapshotTests`.
- [Problems](../../src/Problems/API.md) — the library result the document is compared with.
- [Data](../../src/Data/API.md) — the database.
- [Fixtures](../Fixtures/API.md) — the reference cases the documents encode, and the repository paths.
- [Execution](../../src/Execution/API.md) — the engine options of the library call and the CUDA variable.
- [Thermo](../../src/Thermo/API.md) — `MixtureState`, `CaseStatus`.
- [Performance](../../src/Performance/API.md) — `PerformanceFigures`.
- [Transport](../../src/Transport/API.md) — `TransportFigures`.
- [Equilibrium](../../src/Equilibrium/API.md) — `ProblemKind`.
- [Harness](../Harness/API.md) — the bit-snapshot mechanics (`BitHash`, `ApprovedSnapshot`) and (2026-09-16) the JSON-document helpers (`JsonSchema`, `RunPropertyCut`).

Outside the tree: xunit; the `dotnet` host for the process-level runs.

## Constraints

- Part of the default test command; the CPU accelerator only.
- Output documents are written into a temporary directory of the fixture, removed
  after the run; nothing else is written into the working directory but the
  `Bits.actual.txt` of a failed snapshot comparison, next to the approved file and
  git-ignored (2026-09-14).
- The process-level tests run the command line's assembly copied next to this test
  assembly (with its runtime configuration), else the `Cli` node's own build output.

## Acceptance criteria

- [x] 2026-09-13 — L0 green: `CommandLineTests` (`No_command_is_exit_2_with_the_usage`,
      `Help_prints_the_usage_and_exits_0`, `Invalid_command_lines_are_exit_2_naming_the_offender`
      over the command lines of its theory, `The_usage_names_every_command_and_option`,
      `Options_may_be_given_with_an_equals_sign`); `InputDocumentTests`
      (`An_invalid_document_is_exit_2_with_the_documented_message_and_no_output` over
      `documents/invalid/` (2026-09-13: four more documents there, a state record
      doubled, in mol/g, one in kmol/kg and a doubled `propellant.elementMoles`, each
      refused naming the record and the mass), `Every_example_document_validates_against_the_input_schema`,
      `Every_states_document_validates_against_the_states_schema`,
      `Every_example_of_the_api_document_is_read_or_validates_and_its_records_solve`
      (2026-09-13: the record examples are solved, not only read, since one of them
      weighed 706 g), `State_records_may_come_as_an_array_a_single_object_or_lines`,
      `A_range_expands_inclusively_and_lands_on_its_end`); `ExitCodeTests` (six facts;
      2026-09-13, a seventh: the record of another simulation is exit 0, and the same
      record doubled behind it in a JSON Lines file is named by file and line,
      `A_record_that_weighs_one_kilogram_is_exit_0_and_one_that_does_not_is_named_by_its_line`).

  ⚠ 2026-09-14: stood "over sixteen command lines"; the theory had nineteen rows since
  the three of `--mass-tolerance` (the review's F-TF-16). A number repeating the length
  of a list, dropped: the theory's rows are the list.
- [x] 2026-09-13 — L1 green: `OutputDocumentTests`
      (`Every_example_document_runs_and_its_result_validates_against_the_output_schema`
      over the directory listing, `The_states_result_validates_and_echoes_every_record_in_order`,
      `States_from_an_array_a_lines_file_and_several_files_give_the_same_cases`,
      `A_rocket_sweep_expands_ratio_major_then_pressure_in_one_document`,
      `An_equilibrium_sweep_expands_pressure_then_temperature`,
      `The_threshold_omits_small_mole_fractions_and_the_default_is_the_reference_print_threshold`,
      `Transport_figures_are_present_only_when_requested`,
      `The_species_listing_validates_and_finds_names_case_insensitively`,
      `The_devices_listing_validates`, `The_schema_lists_every_field_of_the_library_result_structs`);
      `CsvTests` (`The_csv_of_the_rocket_example_matches_the_approved_file`,
      `The_csv_has_one_row_per_case_and_station_and_no_compositions`,
      `An_equilibrium_csv_has_one_row_per_case_with_empty_performance_and_transport_cells`).
- [x] 2026-09-13 — L2 green: `LibraryEqualityTests.The_rocket_example_equals_the_library_field_by_field`,
      `The_equilibrium_examples_equal_the_library_field_by_field`; process level:
      `ProcessTests` (exit codes 0, 1, 2 and 3, the last with `APTHERMO_NO_CUDA=1` and
      `--accelerator cuda`).
- [x] 2026-09-13 — Every check proven non-degenerate once, each mutation applied
      alone and seen red: a field renamed in the output schema (the schema tests and
      the field-list test); the exit code of an invalid document swapped for 3 (the
      exit code tests); the chamber pressure of the LOX/LH2 document changed to bar
      (the library equality and the approved CSV); g0 changed (the library equality);
      a CSV column dropped (the approved CSV); the threshold no longer applied (the
      threshold test and the library equality); the sweep's ratio order reversed (the
      sweep order test).
- [x] 2026-09-13 — The mass check and its naming proven non-degenerate, each mutation
      alone: the front door's check removed (its comparison made never true): the
      invalid-document theory over the four unit-error documents and the line-naming
      test red; the record's source no longer substituted for the library's subject
      in `states`: the same theory over the three state-record documents (`record 0:`
      absent from the messages) and the line-naming test red.
- [x] 2026-09-13 — `--mass-tolerance` and the mass report (the `Cli` BOOT.md's design
      of the same day): `CommandLineTests` (the usage names the option; a negative
      value, `inf` and the option on a listing command are exit 2; `=` and the
      default); `OutputDocumentTests.The_mass_tolerance_is_echoed_and_every_case_reports_the_mass_of_its_mixture`
      (`run.massTolerance` echoes the option and its default, `mixture.mass` in every
      case of the states and sweep documents; both fields in `schemas/output.schema.json`
      and `run.massTolerance` in `species.schema.json`, since the listing writes the
      same run section); `LibraryEqualityTests` (`mixture.mass` exactly the library's
      `MixtureMass`); `ExitCodeTests.The_mass_tolerance_option_is_the_tolerance_the_run_declares`
      (the record of another simulation made 2 % heavy: exit 2 without the option,
      exit 0 with `--mass-tolerance 0.03` and the mass 1.02 reported; made 5 % heavy,
      exit 2 naming `3 %`).
- [x] 2026-09-13 — Proven non-degenerate: the option not passed to the solver (the
      state records built at the library's default), applied alone, and
      `ExitCodeTests.The_mass_tolerance_option_is_the_tolerance_the_run_declares` seen
      red (the echo test stays green, as it should: the echo is not the check); the
      same test also red under the front door's own mutations (that node's BOOT.md).
- [x] 2026-09-14 — Bits level green: `BitSnapshotTests.Every_example_gives_the_recorded_output`
      over the examples the level table lists (enumerated from `documents/` and from
      the `Cli` API, not typed), against `Bits.approved.txt`, recorded before any code
      of the decomposition moved (`f795f3c`) and unchanged after it (empty diff at the
      close); seen red once by a CSV column moved (that example's CSV line red) and by
      an example absent from the snapshot (red with the instruction to approve), both
      at the recording commit.
- [x] 2026-09-15 — The Bits level's JSON half hashes the delivered bytes with `run` cut
      by span, not the review's compact re-serialization (R-Cli.Tests-2): the bytes
      hashed are the bytes captured in process, proven equal to what `--output` writes
      to a file for the LOX/LH2 rocket example
      (`BitSnapshotTests.The_captured_text_matches_the_bytes_delivered_to_the_output_file`,
      cutting `run` from both before comparing, since `run.timings` alone differs run to
      run); the cut is exact with `run` first, in the middle and last, each against the
      same document written without it
      (`RunPropertyCutTests.The_top_level_run_property_is_cut_wherever_it_appears`),
      seen red once with the cut's span shifted by one byte
      (`RunPropertyCut.Cut`, `document.AsSpan((int)cutEnd + 1)`): all three cases failed
      on `Assert.Equal() Failure: Strings differ`, each missing exactly the one byte
      immediately after the removed span, reverted; a document with no top-level `run`
      property or with more than one fails instead of hashing
      (`RunPropertyCutTests.A_missing_top_level_run_property_fails_instead_of_hashing`,
      `A_duplicated_top_level_run_property_fails_instead_of_hashing`). Every line of
      `Bits.approved.txt` re-approved once, with two proofs, both over all 18 examples:
      the same run computed the old, compact-reserialization hash and the new one for
      every example from the same captured text, and every old hash still equalled its
      approved line (the values, keys and order were still the approved ones at the
      moment of re-approval; a temporary fact, never committed); the JSON document of
      every example produced at `f795f3c`, before the clean-code decomposition, in a
      scratch worktree of that commit removed afterward, hashes under the new
      definition to the same 18 new lines, no example present on only one side (a
      temporary fact, never committed). The CSV half of every line is unchanged in both
      proofs, since its hash definition did not change. `git hash-object
      tests/Cli.Tests/Bits.approved.txt`: `483c979b75b5c98b5e11ddc4359f28812e225e15`
      (18 lines) before, `b6e7844aa27ee16b9e2c1cbb84396d043e44386d` (18 lines) after.
- [x] 2026-09-14 — The facts of 2026-09-14 (the level table's new L0 and L2 rows):
      `LibraryEqualityTests.The_states_example_equals_the_library_field_by_field` (a
      records file with and without exits); `ExitCodeTests.An_exception_maps_to_its_documented_exit_code`
      (an input refusal 2, an accelerator failure 3, an unexpected exception 3);
      `CommandLineTests.The_usage_states_the_library_defaults` and
      `Every_command_of_the_table_has_a_handler`;
      `OutputDocumentTests.An_auto_run_that_fell_back_says_why` (with
      `APTHERMO_NO_CUDA=1`: `run.accelerator.cudaSkippedBecause` and the devices
      listing name the variable, the schema files list the field); the pinned messages
      of `states-two-targets.json` and `states-rocket-without-enthalpy.json` the front
      door's reasons behind the record's source. Each of the five facts seen red once
      and reverted: `StatesCommand.SolveRockets` writing a with-exits case to the
      batch's first slot instead of its own index (the states example's rocket case
      overwrites its equilibrium one); an unexpected exception mapped to 2; the
      threshold's usage line typed as a literal instead of read from
      `CommandOptions.DefaultThreshold`; `states` dropped from `CommandRegistry`'s
      table (`unknown command 'states'`, exactly the drift the test guards against);
      the fallback reason not written.
- [x] 2026-09-14 — The support code in shape (the review's F-TF-03, F-TF-07, F-TF-10,
      F-TF-12, F-TF-13): `JsonSchema.Check` split into `CheckKeywords`, `CheckType`,
      `CheckValue`, `CheckNumber`, `CheckObject` (with `CheckProperty`), `CheckArray`
      and `CheckCombinators`, the keyword whitelist unchanged;
      `LibraryEqualityTests.AssertCase` split into `AssertMixture`, `AssertStation` and
      `AssertComposition`; the schema-declaration `AssertFields<T>` of
      `OutputDocumentTests` renamed `AssertSchemaListsFields<T>` so only one method of
      the node keeps that name, both it and `LibraryEqualityTests` reading one
      camel-case rule of this node (`CliFixture.Camel`); the grams of the four refusal
      messages that report one derived from the document's own composition
      (`CliFixture.GramsOf`), compared numerically rather than as text since the message
      itself rounds the figure it reports (`InputDocumentTests.AssertMassReported`); the
      tolerance literals of `OutputDocumentTests`, `ExitCodeTests` and
      `InputDocumentTests` named constants with their origin in a comment; no method
      over 60 lines or nested deeper than 3 (the protocol tests node's `ShapeMeasures`
      over the tree with this pass merged). The
      recorded mutations still red, each proven again and reverted: a field renamed in
      the output schema (both the schema validator and the reflection-based field-list
      test red) and g0 changed (both rocket-example tests red); and, for the camel-case
      rule, `Names.Camel` returning its argument unchanged (the three
      `LibraryEqualityTests` facts red).
- [x] 2026-09-17 — `apthermo schema` (the audit's C1, C2, C3, C4, C5): `SchemaCommandTests`
      (`Standard_output_is_exactly_the_file_bytes`, `The_output_option_writes_exactly_the_file_bytes`
      over a directory listing of `src/Cli/Schemas/`, `The_embedded_names_equal_the_directory_listing`,
      `A_missing_name_is_exit_2_listing_every_embedded_name`,
      `An_unknown_name_is_exit_2_listing_every_embedded_name`). Each fact seen red once and
      reverted: the assembly's glob narrowed to `devices.schema.json` alone turned nine of the
      thirteen facts red at once (both byte-equality theories over the four other names, and
      the embedded-names-equal-the-listing fact, since `SchemaResources.Names` then read from
      the manifest a set the directory listing no longer matched); a name filtered out of
      `SchemaResources.Names` by hand turned the embedded-names fact red on its own,
      `["devices","output","species","states"]` against the directory's five; the missing-name
      and the unknown-name branches of `SchemaCommand` each written to `output.WriteLine` and an
      ordinary return instead of `throw new InputException(...)` turned their own fact red,
      `Assert.Empty()` finding the message on standard output instead of standard error.

## Taboos

- Do not compare documents by string equality of floating-point text: parse and
  compare numbers with the documented tolerance (exact for pass-through fields).
- Do not skip when the data files are missing.
- Do not build the library call of L2 from the document under test.
