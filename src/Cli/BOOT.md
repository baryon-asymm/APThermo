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
- [Performance](../Performance/API.md) — `FlowModel` (`DocumentWords`' flow words).
- [Equilibrium](../Equilibrium/API.md) — `ProblemKind` (`DocumentWords`' kind words).

Outside the tree: the .NET base class library (`System.Text.Json`); command-line
parsing is hand-written to avoid a dependency (revisited if the surface grows).

⚠ 2026-09-12: the sketch listed `Problems` and `Data`. The solver is created with the
execution node's options and reports its accelerator, and the result records carry
the numerical nodes' structs, which this node reads field by field; the reflection
dependency check reads types in method bodies, so the links are declared. The root's
decomposition carries the same.

⚠ 2026-09-15: this list carried `Transport` (`TransportFigures`) and read
`Performance` as `FlowModel, PerformanceFigures`. The clean-code decomposition moved
`StationFields` and every direct reader of `TransportFigures` and
`PerformanceFigures` into the child node `Output` (its own `BOOT.md` declares
`Transport` and `Performance` now); this node's own remaining code reaches
`Performance` only through `DocumentWords`' flow words. Found by the protocol tests
node's `DependencyTests` after the move (`src/Cli/BOOT.md declares src/Transport, but
no type of src/Cli refers to it`); the child-node attribution
(root `BOOT.md`, Constraints, 2026-09-15) means a dependency used only by a child is
declared there, not repeated at the parent's own level, unless the parent's own code
also uses it.

## Constraints

Inherited from the root ([BOOT.md](../../BOOT.md)). In addition:

- Commands: `rocket <input.json>`, `equilibrium <input.json>`, `states <records>...`,
  `species [--find TEXT]`, `devices`; options `--output PATH`, `--format json|csv`,
  `--accelerator auto|cpu|cuda`, `--database DIR` (directory with `thermo.inp` and
  `trans.inp`; without it, the database embedded in `APThermo`,
  `Data.SpeciesDatabase.LoadBundled()`), `--threshold X` (mole fractions below X are
  omitted from the composition tables; default 5e-6, the reference's print threshold),
  `--transport` (states), `--find TEXT` (species), `--help`, `--version` (prints
  `Program.Version` and exits; applies to no command and may be given alone). Every
  option applies to the commands `API.md` lists it with; an option that does not
  apply is an error. 2026-09-13: `--mass-tolerance X` on the solving commands, the
  mass tolerance declared for every mixture built from element moles (default the
  library's, 1e-2; a propellant by reactants keeps the default); a run option, not a
  document field, because it describes the caller's records and not the physics.

  ⚠ 2026-09-15 (distribution phase): `--database`'s default stood "`data/` next to
  the executable, then `data/` under the current directory, then the current
  directory" (`DatabaseFiles.Resolve`, since removed). A NuGet package and a .NET
  tool have no `data/` directory beside them (root `BOOT.md`, `## Delivery`, `Data`),
  so every consumer without `--database` would first have to find NASA files
  themselves. The search is gone; `DatabaseFiles.Load` now reads
  `SpeciesDatabase.LoadBundled()` when no directory is given, and `run.database`
  reports the path-like markers `"embedded:thermo.inp"`/`"embedded:trans.inp"`
  (`API.md`, Output document) instead of a file path, with the same provenance
  hashes a caller who pointed `--database` at the committed `data/` would get.
- The assembly is named after its namespace, as the root requires; `apthermo` is the
  tool command name of the package (`dotnet pack` produces a tool package whose
  command is `apthermo`), and a direct run is `dotnet APThermo.Cli.dll`.
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

⚠ 2026-09-15: "the split is into types, not into sub-nodes" no longer holds whole.
The root's own-assembly-per-node rule was the reason for it, and the root `BOOT.md`
(Constraints, 2026-09-14 for the decision, dated 2026-09-15 in its own text) now lets a
child node compile into its nearest ancestor's assembly instead of forcing one of its
own, precisely so a large node like this one could be cut into sub-nodes without
widening its public surface. Five clusters of this node passed the child-node test (the
root `BOOT.md`, `## Decomposition`, the "child nodes phase"): `Syntax/`,
`Documents/`, `Cases/`, `Output/` and `Listings/`, each with its own `BOOT.md` and
`API.md` and the namespace of its path
(`APThermo.Cli.Syntax` and so on), compiled into this
node's own assembly. `Program`, `CommandRegistry`, `Failures`, `SolverSession`, the
three command types (`ProblemCommand`, `StatesCommand`, `SpeciesCommand`) and the
shared vocabulary and run-bookkeeping types with no single owning cluster
(`DocumentWords`, `Names`, `InputException`, `InputFile`, `DatabaseFiles`,
`DatabaseInfo`, `RunInfo`, `RunLimits`, `Timings`) stayed at this node's own level.
`DocumentWords` was weighed for `Syntax/` and for `Documents/` (both read it,
`CommandTable` for `--accelerator`, the readers for `flow`, `role`, `amountKind` and
`kind`) and, on inspection, also for `Cases/` (`CaseInputs`'s echo of `kind`) and for
`Output/` (`RunSection`'s echo of the accelerator): four clusters, no dominant owner,
so moving it into any one would turn the other three into its dependents for one
lookup table. It stays here, at this node's own level: every child already names this
node as its ancestor (`[Cli](../API.md)` in its own `## Dependencies`), so reading
`DocumentWords` from any of them costs no new edge. `Names` and the run-bookkeeping
records (`RunInfo` and what it carries) were kept for the same reason, one level less
sharply split: `Names` reaches `Output/` and `Listings/`, `RunInfo` is `SolverSession`'s
own return value read by `Output/`, `Listings/` and the command types alike, and moving
either would still leave at least two of the three as its dependents. `OutputFormat`
and `CaseOutput`/`Combination` moved instead of staying, because each has a genuine
majority owner (`Syntax/` and `Cases/` respectively) that the rest of the node
reaches only through a field already carried by a wider record (`CommandOptions.Format`,
the case's own shape); their own `BOOT.md` records the reasoning. `Syntax/` is not
named `CommandLine/`: its own `BOOT.md` records why (a namespace and a same-named type
inside it force a doubled qualification on every caller in the `Cli` tree).

The move itself was mechanical: `git mv` and a namespace edit per moved file, no logic
touched, one commit per child (`## Structure` of each child names its own moved
types). No `## Shape exceptions` row moved, because the three rows below all name a
composition root that stayed at this node's own level.

⚠ 2026-09-15: four rows of the table below had drifted from the code they describe.
`StateRecordReader`'s row said the record files' shape was decided "by the first
non-blank character"; the code decides it by attempting to parse the first JSON value
and checking what follows it (`JsonText.TryParseWhole`), not by inspecting characters.
`DocumentWriter`'s row said only "delivery to the output file or the standard output,
and the non-finite-number rule", omitting that it also renders a case document in the
requested format and decides its exit code, and that the JSON writer the listings
(`species`, `devices`) render through is this type's too. `Sweeps`' row named three of
the sweep's four axes ("ratio-major, then pressure, then temperature"), silently
dropping chamber pressure, which the code already crossed. `CaseInputs`' row claimed
the whole `inputs` echo was "written once" in this type, while the code wrote only the
swept ratio and left `RocketCases` to add `chamberPressure` and `EquilibriumCases` to
add `kind`, `pressure` and the target (`AddTarget`). Found by the repair review of
2026-09-15 reading the code against the table; its R-Cli-7 and R-Cli-3 also moved the
code of the last two to match the row each already claimed (`Sweeps.Expand` is one
query over the four axes; `CaseInputs.Rocket` and `CaseInputs.Equilibrium` own the
whole echo, `AddTarget` moved in from `EquilibriumCases`).

Types that stayed at this node's own level:

| Type | Responsibility |
|---|---|
| `Program` | the entry point: dispatches through `CommandRegistry` and turns an exception into its exit code through `Failures` |
| `Failures` | the exception → exit code rule: `InputException` 2; an accelerator failure and every unexpected exception 3 |
| `CommandRegistry` | command name → handler, no logic (it was the class `Commands`); the handlers are this node's own `ProblemCommand`/`StatesCommand`/`SpeciesCommand` and `Listings.DeviceListing` |
| `DocumentWords` | every word ↔ enum mapping of the documents and the options, both directions (flow, accelerator, role, amount kind, problem kind), with the place (a JSON path or an option) in the message (F-CL-11); read by all four clusters below, no dominant owner (see the warning above) |
| `SolverSession` | the database and the solver of one run, with their timings; disposable |
| `ProblemCommand` | `rocket` and `equilibrium`: read (`Documents`), check the problem type against the command, build the mixtures, expand the sweep, solve (`Cases`), write (`Output`) |
| `StatesCommand` | `states`: the records split by `HasExits` (`Documents`), one call of `SolveStates` and one of `SolveRocketStates` with the run's `StateBatchOptions`, the cases back in input order, written (`Output`) |
| `DatabaseFiles`, `DatabaseInfo` | where the database is found: `--database DIR`, or (2026-09-15) `Data.SpeciesDatabase.LoadBundled()` when no directory is given, reported as the `"embedded:…"` markers; and the record of what was found |
| `RunInfo`, `RunLimits`, `Timings` | a run's own bookkeeping, assembled by `SolverSession.Stop`; read by `Output` and `Listings` through their fields only |
| `Names` | camel case of the library's names and of statuses; read by `Output` and `Listings`, and by `DocumentWords`' own fallback branch (see the warning above) |
| `SpeciesCommand` | the `species` command: the database, the name filter, the rows (`Listings.SpeciesRow`), the run and the delivery (`Listings.SpeciesListing`, `Output.DocumentWriter`) |
| `InputException`, `InputFile` | the node's own exception, raised throughout; a user-named file read with the missing-file message this node documents |

## Children

Five clusters of `## Structure` above passed the child-node test (the root `BOOT.md`,
`## Decomposition`; the warning above records why each was cut where it was, and why
`DocumentWords`, `Names` and the run-bookkeeping types were not moved). Each child's
own `BOOT.md` names the types it holds and why; the `AGENTS.md` §1 access rules apply
between this node and each of them exactly as between neighbours: reading a child's
code from a sibling child, or from this node past its `API.md`, is not this document's
business to forbid twice.

| Child | Namespace | Holds |
|---|---|---|
| [`Syntax/`](Syntax/BOOT.md) | `Cli.Syntax` | the token walk, the command and option tables, `Invocation`, `CommandOptions`, `OutputFormat` |
| [`Documents/`](Documents/BOOT.md) | `Cli.Documents` | the problem-document and state-record readers, the strict-object mechanism, the document shapes |
| [`Cases/`](Cases/BOOT.md) | `Cli.Cases` | the sweep expansion, the propellant and case builders, `CaseOutput` |
| [`Output/`](Output/BOOT.md) | `Cli.Output` | the JSON and CSV renderers, the station field projection, delivery, the exit-code rule |
| [`Listings/`](Listings/BOOT.md) | `Cli.Listings` | the `species` row and its rendering, the `devices` probe and its rendering |

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

  ⚠ 2026-09-15: for a JSON document's formatting — indentation, line breaks, the final
  newline, string escaping — this claim was not held from 2026-09-14, when the
  decomposition landed, to 2026-09-15: the tests node's snapshot hashed a compact
  re-serialization of the document, which left that formatting unguarded (the repair
  review's R-Cli.Tests-2). It holds now: the snapshot hashes the bytes the command line
  delivers, with `run` cut out by span (`tests/Cli.Tests/BOOT.md`, the Bits level and
  the criterion of 2026-09-15).
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

**Packing (2026-09-15, distribution phase, root `BOOT.md`, `## Delivery`, Packages).**
This node's project packs as `APThermo.Cli`, a .NET tool (`PackAsTool=true`,
`ToolCommandName=apthermo`, both already set before this phase): `IsPackable=true`
already stood, `PackageId=APThermo.Cli` is new, and the version and the shared
package metadata come from the root's `Directory.Build.targets`, as for `Problems`.

Unlike `Problems`, none of this node's seven `ProjectReference`s need
`PrivateAssets` or a merge target: `PackAsTool` packs the *published* output
(`tools/net10.0/any/`), which already carries every referenced assembly (including
`APThermo.Problems.dll` and, through it, the six it merges) and `ILGPU.dll` as plain
files, not as nuspec dependencies — a tool has no consumer to declare dependencies
to. Proved once, read-only: the packed nuspec's `<dependencies>` is absent
entirely, and `tools/net10.0/any/` holds `APThermo.Cli.dll` plus the seven library
assemblies and `ILGPU.dll`, nineteen files including the `.deps.json`,
`.runtimeconfig.json` and `DotnetToolSettings.xml` the SDK's tool packaging adds.

`<Version>1.0.0</Version>`, hardcoded before this phase, is removed: the version is
now the one place, `Directory.Build.props`' `VersionPrefix` (0.1.0), like every other
project; `Program.Version` (already reading the assembly's informational version, `##
Structure` above) needed no change; `ProcessTests` and `CommandLineTests` compare
against it rather than a typed string, so the version's value never had to be pinned
in a test.

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
- [x] 2026-09-15 — Every ticked criterion above re-verified on the decomposed and
      repaired code at `62cd99e`: its tests green in the full suite
      (`APTHERMO_NO_CUDA=1`, every category, 3037 tests, none skipped), and
      CUDA-category evidence on the reference machine (`tests/Execution.Tests`, 41,
      and the long-running sweep and throughput tests).
- [x] 2026-09-15 — `apthermo --version` prints `Program.Version` and exits 0, in
      process and as a separate process
      (`CommandLineTests.Version_prints_the_tool_version_and_exits_0`,
      `ProcessTests.The_executable_prints_its_version_with_exit_0`). Without
      `--database`, a run's `run.database.thermoPath`/`.transPath` are the embedded
      markers `"embedded:thermo.inp"`/`"embedded:trans.inp"` with 64-character SHA-256
      hashes, in process and from an empty working directory as a separate process
      (`CommandLineTests.Without_database_the_run_uses_the_embedded_database`,
      `ProcessTests.The_executable_uses_the_embedded_database_from_an_empty_working_directory`).
      Both facts seen red once (AGENTS.md §13): making `DatabaseFiles.Load` call
      `LoadFromDirectory` on the current directory instead of `LoadEmbedded` when no
      `--database` is given turned the embedded-database fact red, exit code 2, `no
      thermo.inp in the database directory '…'`; disabling the `--version` branch of
      `Program.Run` turned the version fact red, exit code 2, `no command given`
      (`--version` alone then falls through to the ordinary "no command" refusal).
      Reverted, nothing of either mutation committed. `Bits.approved.txt` unchanged: the
      JSON hash of every example is taken with its top-level `run` property cut out
      first (`RunPropertyCut`, above), and the CSV form never carries `run` at all
      (`Output.CsvOutput`), so the database path this criterion moves from a directory
      to the embedded markers reaches neither hash. Proved directly too: the packing
      proof of `Problems`' BOOT.md ran an approved CSV example
      (`documents/rocket-lox-lh2.approved.csv`) from an empty directory without
      `--database`, through the packed tool, and found it byte-for-byte unchanged.

## Taboos

- No physics, no unit conversion beyond seconds for specific impulse, no defaults
  for missing physical inputs.
- No output format other than JSON and CSV in version 1; a CEA-like text table is a
  separate decision.
- No network, no telemetry.
- No second list of the result fields: the structs are the list.
- No rule of a state record here (2026-09-14): the front door owns them, and this node
  names the record a refusal is about.
