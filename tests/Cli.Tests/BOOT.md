# BOOT.md — Cli.Tests

## Purpose

The definition of what "`Cli` is ready" means.

| Level | What it checks | Against what (source of truth) | State |
|---|---|---|---|
| L0 | option parsing, the usage text, exit codes; input document reading and its messages with the JSON path; the example documents against the input and states schemas; a composition that does not weigh one kilogram refused naming the record | the schema files of this node; the documented messages and exit codes (`CommandLineTests`, `InputDocumentTests`, `ExitCodeTests`) | ✅ |
| L1 | every example document of `documents/` and of the `Cli` API runs end to end on the CPU accelerator and validates against the output schema; sweeps, states files, thresholds, transport, the listings; the CSV layout against the approved file | schema files; the approved CSV; the documented orders (`OutputDocumentTests`, `CsvTests`) | ✅ |
| L2 | the LOX/LH2 rocket document, the LOX/RP-1 hp document and the elemental tp document give the library's numbers field by field | the `Problems` result of the same case, built from the fixture the document encodes, over reflection-enumerated fields (`LibraryEqualityTests`) | ✅ |
| Process | one run per exit code as a separate process: real exit codes and standard streams | the documented exit codes (`ProcessTests`) | ✅ |
| Protocol | the tree invariant, documents against code | `AGENTS.md`, the surface snapshot | ✅ (2026-09-13, the Protocol.Tests node) |

## Invariants

- Documents in tests are files in this node (`documents/`, generated from the
  fixtures so that they encode real cases; `documents/invalid/` for the rejected
  ones), not strings in code; the examples of the `Cli` API are read from `API.md`
  itself, so they cannot drift from what the reader accepts.
- The CLI is exercised in-process through its entry point and, once per exit code,
  as a separate process, so that exit codes and standard streams are real.
- The schema files are checked by a validator of this node that knows exactly the
  keywords the schemas use and refuses any other, so a schema cannot ask for more
  than is checked; the schema's field lists are compared with the library's structs
  by reflection.
- The approved CSV is compared as numbers (1e-12 relative) and strings, never as
  floating-point text; the library equality is exact, because the numbers pass
  through unchanged.
- The library call of L2 is built from the fixture the document encodes, never from
  the document, so that a unit or a value changed in the document is seen.

## Dependencies

- [Cli](../../src/Cli/API.md) — what is being checked.
- [Problems](../../src/Problems/API.md) — the library result the document is compared with.
- [Data](../../src/Data/API.md) — the database.
- [Fixtures](../Fixtures/API.md) — the reference cases the documents encode, and the repository paths.
- [Execution](../../src/Execution/API.md) — the engine options of the library call and the CUDA variable.
- [Thermo](../../src/Thermo/API.md) — `MixtureState`, `CaseStatus`.
- [Performance](../../src/Performance/API.md) — `PerformanceFigures`.
- [Transport](../../src/Transport/API.md) — `TransportFigures`.
- [Equilibrium](../../src/Equilibrium/API.md) — `ProblemKind`.

Outside the tree: xunit; the `dotnet` host for the process-level runs.

## Constraints

- Part of the default test command; the CPU accelerator only.
- Output documents are written into a temporary directory of the fixture, removed
  after the run; nothing is written into the working directory.
- The process-level tests run the command line's assembly copied next to this test
  assembly (with its runtime configuration), else the `Cli` node's own build output.

## Acceptance criteria

- [x] 2026-09-13 — L0 green: `CommandLineTests` (`No_command_is_exit_2_with_the_usage`,
      `Help_prints_the_usage_and_exits_0`, `Invalid_command_lines_are_exit_2_naming_the_offender`
      over sixteen command lines, `The_usage_names_every_command_and_option`,
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

## Taboos

- Do not compare documents by string equality of floating-point text: parse and
  compare numbers with the documented tolerance (exact for pass-through fields).
- Do not skip when the data files are missing.
- Do not build the library call of L2 from the document under test.
