# BOOT.md — Cli

## Purpose

The command-line front end `apthermo`: reads a problem from a JSON file, runs it
through `Problems`, and writes the results as JSON or CSV; also lists the database
and the accelerators. It is a thin adapter kept apart so that the library never
depends on console, serialization or file-layout concerns.

## Invariants

- **The JSON schema is the contract.** Input and output documents follow the shapes
  in `API.md`; every field of the library's result structs (`MixtureState`,
  `PerformanceFigures`, `TransportFigures`) reaches the output document under its
  camel-case name and with its unit, enumerated by reflection so that no second list
  exists, plus the presentation conveniences listed there (specific impulse in
  seconds, a mole-fraction threshold). The schema files of the tests node hold the
  same lists, and a test compares them with the structs.
- **Units in documents are SI** unless a field name carries the unit explicitly
  (`specificImpulseSeconds`, `vacuumSpecificImpulseSeconds`); the conversion to
  seconds uses g0 = 9.80665 m/s² and happens only here.
- **Exit codes mean something**: 0 every case and station `ok`; 1 at least one case,
  station or transport evaluation failed numerically (the document is still written);
  2 invalid input (document, option, database path, reactant); 3 accelerator or
  infrastructure error. Messages go to standard error; documents go to the output
  file or standard output.
- **Strict documents.** Unknown fields, missing required fields and values of the
  wrong type are errors naming the JSON path, so that a unit mistake cannot pass
  silently; nothing physical has a default (the flow model and the accelerator are
  choices, and their defaults are documented).
- **No hidden state**: no configuration files, no registry, no environment variable
  except the ones `Execution` reads.

## Dependencies

- [Problems](../Problems/API.md) — propellants, problems, the solver, result records.
- [Data](../Data/API.md) — loading the database and listing species.
- [Execution](../Execution/API.md) — the engine options, the accelerator description, the unavailable exception, the CUDA flag.
- [Thermo](../Thermo/API.md) — `MixtureState` and `CaseStatus` of every station.
- [Performance](../Performance/API.md) — `FlowModel`, `PerformanceFigures`.
- [Transport](../Transport/API.md) — `TransportFigures`.
- [Equilibrium](../Equilibrium/API.md) — `ProblemKind`.

Outside the tree: the .NET base class library (`System.Text.Json`); command-line
parsing is hand-written to avoid a dependency (revisited if the surface grows).

⚠ 2026-09-12: the sketch listed `Problems` and `Data`. The solver is created with the
execution node's options and reports its accelerator, and the result records carry
the numerical nodes' structs, which this node reads field by field; the reflection
dependency check reads types in method bodies, so the links are declared. The root's
decomposition carries the same.

## Constraints

Inherited from the root ([BOOT.md](../../BOOT.md)). In addition:

- Commands: `rocket <input.json>`, `equilibrium <input.json>`, `states <records>...`,
  `species [--find TEXT]`, `devices`; options `--output PATH`, `--format json|csv`,
  `--accelerator auto|cpu|cuda`, `--database DIR` (directory with `thermo.inp` and
  `trans.inp`; default `data/` next to the executable, then `data/` under the current
  directory, then the current directory), `--threshold X` (mole fractions below X are
  omitted from the composition tables; default 5e-6, the reference's print threshold),
  `--transport` (states), `--find TEXT` (species), `--help`. Every option applies to
  the commands `API.md` lists it with; an option that does not apply is an error.
- The assembly is named after its namespace, as the root requires; `apthermo` is the
  tool command name of the package (`dotnet pack` produces a tool package whose
  command is `apthermo`), and a direct run is `dotnet AerospacePropellantThermodynamics.Cli.dll`.
- CSV output flattens one row per case and station with the station's own
  performance figures (the library reports them at every station); compositions are
  not in CSV.

  ⚠ 2026-09-12: stood "with the performance figures of the station's exit"; the
  performance node reports the figures per station.
- Sweeps in the input document expand to one batch (Cartesian product, ratio-major,
  then pressure, then temperature) through the front door's batch over mixtures, so
  the command line is the natural way to use the GPU. A rocket problem sweeps the
  ratio and the chamber pressure; an equilibrium problem the ratio, the pressure and,
  for tp, the temperature.
- `states` reads records in the exchange shape of the `Problems` node (`pressure`,
  `composition`, one of `enthalpy`/`temperature`/`entropy`, optional exits and flow)
  from a JSON array, a single object, a JSON Lines file or several files, and writes
  one result per record in the input order, with the record echoed under `inputs`;
  a record that fails keeps its place with its status. The records without exits are
  one equilibrium batch over the union of their elements, the records with exits one
  rocket batch.

  ⚠ 2026-09-12: stood "one batch": a rocket case and an equilibrium case are
  different programs of the execution node, so a mixed file is two batches, still one
  call each.
- Timings and the accelerator description are always in the output document's
  `run` section, with the command, the input files, the database files and their
  hashes, and the threshold.

  ⚠ 2026-09-12: the sketch's timings were the engine's (`warmUp`, `upload`, `kernel`,
  `download`), which the front door does not expose; the tool reports its own phases
  (`database` load, `solve`).

## Acceptance criteria

- [x] 2026-09-13 — Every example document in `API.md` and every document of the
      tests node's `documents/` directory runs end to end against the committed data
      files and produces a document that validates against the output schema (schema
      files kept next to the tests): `Cli.Tests.InputDocumentTests.Every_example_of_the_api_document_is_read_or_validates`,
      `OutputDocumentTests.Every_example_document_runs_and_its_result_validates_against_the_output_schema`
      (over the directory listing), `The_states_result_validates_and_echoes_every_record_in_order`,
      `The_species_listing_validates_and_finds_names_case_insensitively`, `The_devices_listing_validates`.
- [x] 2026-09-13 — The example rocket document for LOX/LH2 gives the same numbers as
      the library call in the front door tests: the CLI test builds the library call
      from the fixture the document encodes and compares the output document field by
      field over a reflection-generated list, exactly
      (`LibraryEqualityTests.The_rocket_example_equals_the_library_field_by_field`,
      `The_equilibrium_examples_equal_the_library_field_by_field`).
- [x] 2026-09-13 — Exit codes 0, 1, 2, 3 are each produced by a test, in-process and
      as a process: a good document, a document with a failing case, a malformed
      document, `--accelerator cuda` with `APTHERMO_NO_CUDA=1`
      (`ExitCodeTests`, `InputDocumentTests.An_invalid_document_is_exit_2_with_the_documented_message_and_no_output`,
      `ProcessTests`: the four facts).
- [x] 2026-09-13 — CSV output has one row per case and station and the documented
      columns, checked against the approved file `documents/rocket-lox-lh2.approved.csv`
      of the tests node (`CsvTests`: the header exactly, every number as a number).

## Taboos

- No physics, no unit conversion beyond seconds for specific impulse, no defaults
  for missing physical inputs.
- No output format other than JSON and CSV in version 1; a CEA-like text table is a
  separate decision.
- No network, no telemetry.
- No second list of the result fields: the structs are the list.
