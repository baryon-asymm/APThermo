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
  choices, and their defaults are documented). A composition that does not weigh one
  kilogram with the database's atomic weights is refused by the front door
  (`Problems` BOOT.md, invariants) and named here by the record's file and position,
  or by the JSON path of `propellant.elementMoles`.

  ⚠ 2026-09-13: the strictness covered the names of fields, not the values of a
  composition: a state record with every element mole doubled, or given in mol/g,
  ran through `states` and produced a document with exit code 0, which is exactly
  the silent unit mistake this invariant promised to catch. The check belongs to the
  front door, where a mixture meets the database's atomic weights; this node only
  names the record.
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
  2026-09-13: `--mass-tolerance X` on the solving commands, the mass tolerance
  declared for every mixture built from element moles (default the library's, 1e-2;
  a propellant by reactants keeps the default); a run option, not a document field,
  because it describes the caller's records and not the physics.
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
  rocket batch. The rules of a record (exactly one target; exits need an enthalpy; a
  flow only with exits; the composition's own rules) are the front door's: this node
  reads the JSON shape only, hands the records to `SolveStates` and
  `SolveRocketStates`, and names a refusal by the record's file and position
  (2026-09-14, decided at the root on the architecture review's F-AR-02; until then
  this node decided the first three rules a second time).

  ⚠ 2026-09-12: stood "one batch": a rocket case and an equilibrium case are
  different programs of the execution node, so a mixed file is two batches, still one
  call each.
- Timings and the accelerator description are always in the output document's
  `run` section, with the command, the input files, the database files and their
  hashes, the threshold and (2026-09-13) the mass tolerance in force, while every
  case's `mixture` section carries the mass of its element moles (`MixtureMass` of
  the library), so that a raised tolerance never hides the figure.

  ⚠ 2026-09-12: the sketch's timings were the engine's (`warmUp`, `upload`, `kernel`,
  `download`), which the front door does not expose; the tool reports its own phases
  (`database` load, `solve`).

## Structure

Decided 2026-09-14 (the clean-code pass; the root's code-shape constraint). The node
had grown four commands, two formats and two input shapes in five files without a
split: `InputDocuments` at 399 lines with an efferent coupling of 18, `Solving` a hub
of 27, `CommandLine.Parse` at 117 lines, and the station written once per format (the
review's F-CL-02 to F-CL-10; the review's measures at `8e36a27`: physical lines and a
textual count of names; 351 and 101 lines of code by the current rule). One node, one
directory: the split is into types, not into sub-nodes, which the root's
one-assembly-per-node rule would turn into several assemblies for one adapter.
Everything is internal except `Program` and `ExitCode`; one type per file, named after
the type.

⚠ 2026-09-15: two rows of the table below had drifted from the code they describe.
`StateRecordReader`'s row said the record files' shape was decided "by the first
non-blank character"; the code decides it by attempting to parse the first JSON value
and checking what follows it (`JsonText.TryParseWhole`), not by inspecting characters.
`DocumentWriter`'s row said only "delivery to the output file or the standard output,
and the non-finite-number rule", omitting that it also renders a case document in the
requested format and decides its exit code, and that the JSON writer the listings
(`species`, `devices`) render through is this type's too. Found by the repair review of
2026-09-15 reading the code against the table.

| Type | Responsibility |
|---|---|
| `Program` | the entry point: dispatches through `CommandRegistry` and turns an exception into its exit code through `Failures` |
| `Failures` | the exception → exit code rule: `InputException` 2; an accelerator failure and every unexpected exception 3 |
| `CommandRegistry` | command name → handler, no logic (it was the class `Commands`) |
| `CommandSpec`, `OptionSpec` | one command: name, arity, usage line, the options and formats that apply; one option: name, takes a value or not, usage line, how it folds into `CommandOptions` |
| `CommandTable` | the two tables, and the usage text generated from them, the defaults read from `CommandOptions.DefaultThreshold` and `ElementalMixture.DefaultMassTolerance` (F-AR-04) |
| `ArgumentScanner` | the token walk: `--name`, `--name=value`, `--help`, positionals, a repeated option |
| `CommandLine` | scan, look up, check arity, apply the options, check what applies; keeps `Parse`, `Usage` and `Commands` as the tests node knows them |
| `CommandOptions`, `OptionValues` | the parsed options; the number parser shared by `--threshold` and `--mass-tolerance`, the second validated by `ElementalMixture.IsValidMassTolerance` |
| `DocumentWords` | every word ↔ enum mapping of the documents and the options, both directions (flow, accelerator, role, amount kind, problem kind), with the place (a JSON path or an option) in the message (F-CL-11) |
| `JsonText` | a text parsed with its source label in the message |
| `StrictObject` | unchanged: the mechanism of the strict-documents invariant |
| `SweepValues` | a list or a `{from, to, step}` range into values, with the step tolerance named and derived (F-CL-12) |
| `ProblemDocumentReader` | the document's root: parses the text, reads `propellant` through `PropellantDocumentReader`, `problem` through `ProblemPartReader` and `sweep` through `SweepDocumentReader`; reads `engine` itself (the accelerator word), finishes the root and assembles the `InputDocument` (2026-09-14, kept the name: the API calls the file a problem document) |
| `PropellantDocumentReader` | the `propellant` object only: reactants or element moles, a custom reactant's formula (2026-09-14, split out of `ProblemDocumentReader` along the document's entities) |
| `ProblemPartReader` | the `problem` object only: one reader per problem kind (2026-09-14, split out of `ProblemDocumentReader` along the document's entities) |
| `SweepDocumentReader` | the `sweep` object only: the ranges the batch's Cartesian product runs over (2026-09-14, split out of `ProblemDocumentReader` along the document's entities) |
| `StateRecordReader` | the record files, their shape decided by reading the first JSON value and what follows it (`JsonText.TryParseWhole`): one value alone is an array or an object, more is JSON Lines; each record read into the front door's `StateRecord` with its `RecordSource` (label, index, the raw JSON for the echo) |
| `InputDocuments` | the façade the tests node uses: delegations only |
| `SolverSession` | the database and the solver of one run, with their timings; disposable |
| `ProblemCommand` | `rocket` and `equilibrium`: read, check the problem type against the command, build the mixtures, expand the sweep, solve, write |
| `StatesCommand` | `states`: the records split by `HasExits`, one call of `SolveStates` and one of `SolveRocketStates` with the run's `StateBatchOptions`, the cases back in input order |
| `RecordNaming` | a refusal of the front door (`StateRecordException`, `MixtureMassException`) renamed from its index to the record's file and position |
| `Sweeps` | the Cartesian product of a sweep, ratio-major, then pressure, then temperature: the rule of this node's input document |
| `Propellants` | a propellant document into builder calls or an elemental mixture; a custom reactant as the front door's `CustomReactantDefinition` |
| `RocketCases`, `EquilibriumCases` | combinations and a problem document into problems, and results into case outputs |
| `CaseInputs` | the `inputs` echo of a case, written once |
| `DatabaseFiles` | unchanged: where the database directory is found |
| `StationFields` | the one projection of a station into named, typed cells (the state, the performance figures with the two conversions to seconds, the transport figures), from the library's structs by reflection; owns `StandardGravity` (F-CL-06, F-CL-07) |
| `JsonOutput`, `CsvOutput` | the cells as nested objects with the compositions above the threshold; the same cells as columns, the header from the same source |
| `RunSection` | the `run` object and the accelerator object, for every command that writes them |
| `DocumentWriter` | a case document rendered in the requested format with the exit code of its cases (`ExitCodes`); the JSON writer the listings render through, with the non-finite-number rule; delivery to the output file or standard output |
| `ExitCodes` | 0 when every case, station and transport evaluation is `ok`, else 1 (F-CL-10) |
| `Names` | unchanged: camel-case names of statuses and kinds |
| `SpeciesRow` | one species flattened once (F-CL-08) |
| `SpeciesCommand` | the `species` command: the database, the name filter, the rows, the run and the delivery (2026-09-14, split out of `SpeciesListing` by the coordinator's review, the way `DeviceListing` already separated the probe from the rendering) |
| `SpeciesListing` | the rendering of `SpeciesCommand`'s rows: JSON or CSV (2026-09-14, kept to rendering only) |
| `DeviceProbe`, `DeviceReport`, `DeviceListing` | what the machine offers, asked once; the report; its rendering |

Decisions taken with the review of 2026-09-14:

- **The state record's rules are the front door's** (F-CL-01, F-AR-02). The reader
  checks the JSON shape (an unknown field, a wrong type, the path) and nothing more, so
  `states-two-targets.json` and `states-rocket-without-enthalpy.json` are refused with
  the front door's reasons behind the record's source, after the database is loaded
  instead of before.
- **The library's refusals are translated where the library is called** (F-CL-13).
  Building a propellant or a mixture and solving map `ArgumentException` and
  `KeyNotFoundException` to `InputException`; `Failures` maps `InputException` to exit
  code 2 and everything else to 3, so a defect of this node is no longer reported as
  invalid input. The errors table of `API.md` already says so; the code did not.
- **An empty `only` stays a reader's rule** (F-AR-04). A list that may not be empty is
  a shape rule of the document, named with its JSON path before any library call; the
  front door applies its own rule again, and the duplication of that one predicate is
  declared here. The mass tolerance's predicate is the front door's
  (`ElementalMixture.IsValidMassTolerance`), and the usage text reads both defaults
  from their constants.
- **The document's defaults are the library's**: a rocket document without `flow`
  leaves `RocketProblem.Flow` unset, and a record without `flow` leaves it null
  (F-CL-11).
- **`run.accelerator` and the `devices` listing carry `cudaSkippedBecause`**: the
  reason an `auto` run fell back to the CPU accelerator (the `Execution` API,
  2026-09-14), `null` when CUDA was bound or never tried, so that a document says why
  it ran on the CPU. A contract change, planned in `API.md`.
- **No other byte of any output document changes.** The decomposition is proved by the
  tests node's snapshot of the example outputs, recorded before any code moved; the
  `run` sections of the `species` and `devices` listings stay as they are (the review's
  open question 5 is answered by leaving the documents alone).
- **Records take at most six positional parameters**: `StateDocument` becomes the front
  door's `StateRecord` with its `RecordSource`; a reactant document carries its custom
  part as a `CustomReactantDefinition`; `RunInfo` takes `Timings` and `RunLimits`;
  `CaseOutput` is declared with init properties (F-CL-09).
- **Names**: `CommandRegistry` and `CommandTable` instead of two `Commands`, and verbs
  for the case builders (F-CL-14).
- **The commands are composition roots.** `ProblemCommand`, `StatesCommand` and
  `SpeciesCommand` turn one command into calls of their collaborators: the input read,
  the cases solved through the front door or the database listed, the run written. They
  hold no formula and no rule of a document or a record, so each names every type its
  path passes through, and a coupling above the root's limit is declared in
  `## Shape exceptions` (the root's exception for a composition root). A reader, a
  rendering or a mapper is not one: above the limit it is split along the document's
  sections or the output's parts, never by moving a responsibility to where the count
  fits.
- **Size.** No type over 400 lines, no method over 60, no nesting deeper than 3, no
  more than 6 parameters, no type with an efferent coupling over 14; a composition root
  or a registry that holds no formula and cannot stay under the coupling limit is
  declared in `## Shape exceptions` with its measured figure and its reason, and any
  other type is split.

  ⚠ 2026-09-14: this bullet stood "over 10" after the root's own limit was recalibrated
  to 14 the same day (the root `BOOT.md`, Constraints): the root's number moved and this
  copy of it did not. It also let any type that could not stay under the coupling limit
  be declared, where the root allows the exception only to a registry or a composition
  root that holds no formula. The protocol tests node's measurement over the tree with
  this decomposition merged found `ProblemDocumentReader` at 21 and `SpeciesListing` at
  17; both were split instead (`PropellantDocumentReader`, `ProblemPartReader` and
  `SweepDocumentReader` out of the first; `SpeciesCommand` and `SpeciesListing`).

## Shape exceptions

The rows below are this node's declared exceptions to the root's code-shape constraint,
in the form the protocol tests node reads; their reasons are decisions of `## Structure`.

| Where | Rule | Measured | Reason |
|---|---|---|---|
| `ProblemCommand` | efferent coupling | 30 | the composition root of `rocket` and `equilibrium`: the document read, the problem type checked, the mixtures and the sweep built by their types, the cases solved through `RocketCases` or `EquilibriumCases`, the run written; holds no formula (the decision "The commands are composition roots") |
| `StatesCommand` | efferent coupling | 21 | the composition root of `states`, as `ProblemCommand`: the records split by `HasExits`, one call of `SolveStates` and one of `SolveRocketStates`, the cases back in input order |
| `SpeciesCommand` | efferent coupling | 15 | the composition root of `species`, as `ProblemCommand`: the database loaded, the entries filtered and flattened, the rows rendered by `SpeciesListing` |

Every other type of the node measures 14 or below by the dependency check's walk
(`JsonOutput`, the highest of the rest), within the root's limit of 14.

## Acceptance criteria

- [x] 2026-09-13 — Every example document in `API.md` and every document of the
      tests node's `documents/` directory runs end to end against the committed data
      files and produces a document that validates against the output schema (schema
      files kept next to the tests): `Cli.Tests.InputDocumentTests.Every_example_of_the_api_document_is_read_or_validates_and_its_records_solve`
      (2026-09-13: the record examples of `API.md` are solved too, since the earlier
      example weighed 706 g),
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
- [x] 2026-09-13 — A record that weighs one kilogram (the record of another
      simulation, `documents/states-ap-al-record.json`) is exit code 0; the same
      record doubled, in mol/g, a record in kmol/kg and a document with a doubled
      `propellant.elementMoles` are exit code 2 with the documented message naming
      the record and the mass, and no document
      (`InputDocumentTests.An_invalid_document_is_exit_2_with_the_documented_message_and_no_output`
      over `documents/invalid/states-two-kilograms.json`, `states-mol-per-gram.json`,
      `states-kmol-per-kg.json`, `elemental-two-kilograms.json`); a doubled record
      behind a good one in a JSON Lines file is named by file and line, not by its
      position in the batch
      (`ExitCodeTests.A_record_that_weighs_one_kilogram_is_exit_0_and_one_that_does_not_is_named_by_its_line`).
- [x] 2026-09-13 — `--mass-tolerance` is parsed like `--threshold`: a negative or
      infinite value and the option on a listing command are exit code 2, the usage
      names it, `=` works and the default is the library's
      (`CommandLineTests.Invalid_command_lines_are_exit_2_naming_the_offender`,
      `The_usage_names_every_command_and_option`, `Options_may_be_given_with_an_equals_sign`);
      it is echoed as `run.massTolerance` and every case of every solving command
      carries `mixture.mass`, the schema files of the tests node listing both
      (`OutputDocumentTests.The_mass_tolerance_is_echoed_and_every_case_reports_the_mass_of_its_mixture`,
      `Every_example_document_runs_and_its_result_validates_against_the_output_schema`),
      and the mass is the library's exactly (`LibraryEqualityTests`); the record of
      another simulation made 2 % heavy is exit code 2 without the option and exit
      code 0 with `--mass-tolerance 0.03`, made 5 % heavy exit code 2 naming `3 %`
      (`ExitCodeTests.The_mass_tolerance_option_is_the_tolerance_the_run_declares`;
      heavy, not light: the front door's BOOT.md records why).
- [x] 2026-09-14 — The decomposition of `## Structure`: every type within the root's
      code-shape constraint except the rows of `## Shape exceptions`, measured by the
      protocol tests node's measurements (`ShapeMeasures`, `CouplingMeasures`) over the
      tree with this decomposition merged, 66 types and 134 methods of the node: no type
      over 400 lines, no method over 60, no nesting deeper than 3, no method over 6
      parameters; the tests node's snapshot of the example outputs unchanged from before
      any code moved (`tests/Cli.Tests/Bits.approved.txt`, empty diff against the
      version recorded by `f795f3c`, before the decomposition); every L0, L1, L2 and
      process fact green (`dotnet test tests/Cli.Tests`: 91 passed, 0 failed); the
      public surface unchanged (`Program`, `ExitCode` the only public types of the
      assembly; `Protocol.Tests.SurfaceTests` green against
      `PublicSurface.approved.txt`).
- [x] 2026-09-14 — The documents follow the front door's contract: the `states`
      example gives the library's numbers field by field through `SolveStates` and
      `SolveRocketStates` (`LibraryEqualityTests.The_states_example_equals_the_library_field_by_field`,
      a records file with and without exits); an invalid record is exit code 2 naming
      its file and position with the front door's reason (the pinned fragments of
      `InputDocumentTests`, `states-two-targets.json` and `states-rocket-without-enthalpy.json`);
      the exception → exit code rule maps an input refusal to 2 and an accelerator
      failure or any other exception to 3
      (`ExitCodeTests.An_exception_maps_to_its_documented_exit_code`); an `auto` run
      with CUDA forbidden writes the reason in `run.accelerator.cudaSkippedBecause` and
      the `devices` listing names the variable too, both schema files listing the field
      (`OutputDocumentTests.An_auto_run_that_fell_back_says_why`, a separate process
      with `APTHERMO_NO_CUDA=1`). Each fact seen red once and reverted: the fallback
      reason not written, an unexpected exception mapped to 2.

## Taboos

- No physics, no unit conversion beyond seconds for specific impulse, no defaults
  for missing physical inputs.
- No output format other than JSON and CSV in version 1; a CEA-like text table is a
  separate decision.
- No network, no telemetry.
- No second list of the result fields: the structs are the list.
- No rule of a state record here (2026-09-14): the front door owns them, and this node
  names the record a refusal is about.
