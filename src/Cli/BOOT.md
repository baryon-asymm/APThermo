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
  seconds, a mole-fraction threshold). The JSON Schema files live in this node's
  `Schemas/` directory, embedded in the assembly and catalogued by `SchemaResources`;
  `apthermo schema [name]` prints one of them, or, with no name or an unknown one,
  refuses naming every embedded name. A test compares the schemas' field lists with the
  structs.

  ⚠ 2026-09-16: was tests-node schemas, now `Schemas/` here → HISTORY.md#schemas-moved

  ⚠ 2026-09-17: was "the tests nodes read the schemas through the command", now only
  `tests/Docs.Tests` does → HISTORY.md#schemas-readers
- **Units in documents are SI** unless a field name carries the unit explicitly
  (`specificImpulseSeconds`, `vacuumSpecificImpulseSeconds`); the conversion to
  seconds uses g0 = 9.80665 m/s² and happens only here.
- **Exit codes mean something**: 0 every case and station `ok`; 1 at least one case,
  station or transport evaluation failed numerically (the document is still written);
  2 invalid input (document, option, database path, reactant, schema name); 3 accelerator or
  infrastructure error. Messages go to standard error; documents go to the output
  file or standard output.
- **Strict documents.** Unknown fields, missing required fields and values of the
  wrong type are errors naming the JSON path, so that a unit mistake cannot pass
  silently; nothing physical has a default (the flow model and the accelerator are
  choices, and their defaults are documented). A composition that does not weigh one
  kilogram with the database's atomic weights is refused by the front door
  (`Problems` BOOT.md, invariants) and named here by the record's file and position,
  or by the JSON path of `propellant.elementMoles`.

  ⚠ 2026-09-13: was names-only strictness, now a mass check → HISTORY.md#strict-values
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

⚠ 2026-09-12: was a Problems and Data list, now six links → HISTORY.md#deps-sketch

⚠ 2026-09-15: was Transport listed here, now in `Output` → HISTORY.md#deps-transport

## Constraints

Inherited from the root ([BOOT.md](../../BOOT.md)). In addition:

- Commands and options are those of `API.md`, "Command line"; every option applies to
  the commands listed there with it, and an option that does not apply is an error.
  - `schema [name]` prints the embedded JSON Schema to standard output or `--output`; a
    missing or unknown name is exit code 2 naming every embedded name, read from the
    assembly manifest, not from a typed list.
  - `--database DIR` holds `thermo.inp` and `trans.inp`; without it the database is the
    one embedded in `APThermo`, `Data.SpeciesDatabase.LoadBundled()`.
  - `--threshold X`: mole fractions below X are omitted from the composition tables;
    default 5e-6, the reference's print threshold.
  - `--version` prints `Program.Version` and exits; applies to no command, may be given
    alone.
  - `--mass-tolerance X` on the solving commands: the mass tolerance declared for every
    mixture built from element moles (default the library's, 1e-2; a propellant by
    reactants keeps the default); a run option, not a document field, because it
    describes the caller's records and not the physics.
  → HISTORY.md#commands-condensed

  ⚠ 2026-09-15: was a `data/` search, now embedded → HISTORY.md#database-default
- The assembly is named after its namespace, as the root requires; `apthermo` is the
  tool command name of the package (`dotnet pack` produces a tool package whose
  command is `apthermo`), and a direct run is `dotnet APThermo.Cli.dll`.
- CSV output flattens one row per case and station with the station's own
  performance figures (the library reports them at every station); compositions are
  not in CSV.

  ⚠ 2026-09-12: was exit figures in CSV, now per station → HISTORY.md#csv-figures
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

  ⚠ 2026-09-12: was "one batch", now two for a mixed file → HISTORY.md#states-batches
- Timings and the accelerator description are always in the output document's
  `run` section, with the command, the input files, the database files and their
  hashes, the threshold and (2026-09-13) the mass tolerance in force, while every
  case's `mixture` section carries the mass of its element moles (`MixtureMass` of
  the library), so that a raised tolerance never hides the figure.

  ⚠ 2026-09-12: was engine timings, now the tool's phases → HISTORY.md#timings-phases

- **Audit fixes of 2026-09-26 and 2026-09-28** (the hidden-defect audits of those days,
  Data, Problems and Cli). The parts of [Output](Output/BOOT.md),
  [Documents](Documents/BOOT.md) and [Syntax](Syntax/BOOT.md) live in those nodes. Here:
  → HISTORY.md#audit-header-2026-09-26 and #audit-header-2026-09-28
  - **States.** A refusal from the front door names the record's source (file and
    record, or JSON Lines line). This node maps the front door's `StateRecordException.Index`
    within each group back to the record it came from.
    ⚠ 2026-09-26: was only shape/mass refusals mapped, now all → HISTORY.md#audit-states
  - **Mass tolerance.** `--mass-tolerance` applies to documents that carry element
    moles (`states`, `propellant.elementMoles`); with a reactant propellant it is an
    option that does not apply: exit 2, as `API.md` says in the command description and
    in the errors table and as the guide's sentence names. The front door's propellant
    path holds its mixtures to `DefaultMassTolerance`, `run.massTolerance` records that
    value, and a refusal carries the document's path like every other form:
    `<file>: the propellant's mixture (case i): …`. The `species` listing's `run`
    section records only the options the command takes (observation 8).
    → HISTORY.md#audit-mass-tolerance-2026-09-26 and #audit-mass-tolerance-2026-09-28
  - **Transport.** A station's `transport` object is present when transport was
    requested and the station converged; the `API.md` wording follows the front door's
    corrected contract.
  - **Database errors.** They are printed as the Data node gives them, with no second
    prefix.

## Structure

Decided 2026-09-14 (the clean-code pass; the root's code-shape constraint): the node is
split along its commands, its two formats and its two input shapes into types, one per
file and named after the type; everything is internal except `Program` and `ExitCode`.
→ HISTORY.md#structure-children

⚠ 2026-09-15: was "the split is into types, not into sub-nodes", now five child
nodes in this assembly (`## Children`) → HISTORY.md#structure-children

`DocumentWords`, `Names` and the run-bookkeeping types (`RunInfo`, `RunLimits`,
`Timings`) stayed at this level: each is read by two or more children with no dominant
owner, and a move would turn the others into its dependents for one lookup table.
`OutputFormat` and `CaseOutput`/`Combination` moved, each having one majority owner.
`Syntax/` is not named `CommandLine/` (a namespace and a same-named type force a
doubled qualification on every caller). No `## Shape exceptions` row moved: the three
rows name composition roots that stayed here. → HISTORY.md#structure-children

⚠ 2026-09-15: was four table rows drifted from the code, now they follow it
→ HISTORY.md#structure-rows-drift

Types that stayed at this node's own level:

| Type | Responsibility |
|---|---|
| `Program` | the entry point: dispatches through `CommandRegistry`, turns the exceptions it knows into their exit codes through `Failures`, and installs in `Main` the process's unhandled-exception handler that turns every other exception into exit code 3 (2026-09-24, API.md) |
| `Failures` | the exception → exit code rule: `InputException` 2; an accelerator failure, an I/O failure (`IOException`, `UnauthorizedAccessException`) and, through the unhandled-exception handler, every unexpected exception 3 |
| `CommandRegistry` | command name → handler, no logic (it was the class `Commands`); the handlers are this node's own `ProblemCommand`/`StatesCommand`/`SpeciesCommand`/`SchemaCommand` and `Listings.DeviceListing` |
| `DocumentWords` | every word ↔ enum mapping of the documents and the options, both directions (flow, accelerator, role, amount kind, problem kind), with the place (a JSON path or an option) in the message (F-CL-11); read by all four clusters below, no dominant owner (see the warning above) |
| `SolverSession` | the database and the solver of one run, with their timings; disposable |
| `ProblemCommand` | `rocket` and `equilibrium`: read (`Documents`), check the problem type against the command, build the mixtures, expand the sweep, solve (`Cases`), write (`Output`) |
| `StatesCommand` | `states`: the records split by `HasExits` (`Documents`), one call of `SolveStates` and one of `SolveRocketStates` with the run's `StateBatchOptions`, the cases back in input order, written (`Output`) |
| `DatabaseFiles`, `DatabaseInfo` | where the database is found: `--database DIR`, or (2026-09-15) `Data.SpeciesDatabase.LoadBundled()` when no directory is given, reported as the `"embedded:…"` markers; and the record of what was found |
| `RunInfo`, `RunLimits`, `Timings` | a run's own bookkeeping, assembled by `SolverSession.Stop`; read by `Output` and `Listings` through their fields only |
| `Names` | camel case of the library's names and of statuses; read by `Output` and `Listings`, and by `DocumentWords`' own fallback branch (see the warning above) |
| `SpeciesCommand` | the `species` command: the database, the name filter, the rows (`Listings.SpeciesRow`), the run and the delivery (`Listings.SpeciesListing`, `Output.DocumentWriter`) |
| `SchemaCommand` | (2026-09-16) the `schema` command: the rule (a missing or unknown name refuses, naming every name of `SchemaResources.Names`), delivered as the listings are delivered (`Output.DocumentWriter`) |
| `SchemaResources` | (2026-09-17) the catalogue of the embedded schemas: their names, read from the assembly manifest, and their text |
| `InputException`, `InputFile` | the node's own exception, raised throughout; a user-named file read with the missing-file message this node documents |

## Children

Five clusters of `## Structure` passed the child-node test (the root `BOOT.md`,
`## Decomposition`). Each child's own `BOOT.md` names the types it holds and why; the
`AGENTS.md` §1 access rules apply between this node and each child as between
neighbours. → HISTORY.md#children-intro

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
- **No other byte of any output document changes** (the decomposition of 2026-09-14):
  held by the tests node's snapshot of the example outputs, which hashes the bytes the
  command line delivers with `run` cut out by span (`tests/Cli.Tests/BOOT.md`, the Bits
  level); the `run` sections of `species` and `devices` stay as they are.
  ⚠ 2026-09-15: was a hash of a compact re-serialization, now the delivered bytes
  → HISTORY.md#no-other-byte
- **Records and names** (F-CL-09, F-CL-14): records take at most six positional
  parameters (init properties where more); the types are `CommandRegistry` and
  `CommandTable`, with verbs for the case builders. → HISTORY.md#records-and-names
- **The commands are composition roots.** `ProblemCommand`, `StatesCommand` and
  `SpeciesCommand` turn one command into calls of their collaborators: the input read,
  the cases solved through the front door or the database listed, the run written. They
  hold no formula and no rule of a document or a record, so each names every type its
  path passes through, and a coupling above the root's limit is declared in
  `## Shape exceptions` (the root's exception for a composition root). A reader, a
  rendering or a mapper is not one: above the limit it is split along the document's
  sections or the output's parts, never by moving a responsibility to where the count
  fits.
- **Size.** The root's code-shape constraint applies. A composition root or a registry
  that holds no formula and cannot stay under the coupling limit is declared in
  `## Shape exceptions` with its measured figure and its reason; any other type is split.
  ⚠ 2026-09-14: was a copy of the limits, now the root's → HISTORY.md#size-bullet

**Packing (2026-09-15, distribution phase, root `BOOT.md`, `## Delivery`, Packages).**
This node's project packs as `APThermo.Cli`, a .NET tool (`PackAsTool=true`,
`ToolCommandName=apthermo`, `PackageId=APThermo.Cli`); the version and the shared package
metadata come from the root's `Directory.Build.props` and `Directory.Build.targets`, as
for `Problems`. None of its `ProjectReference`s needs `PrivateAssets` or a merge target:
`PackAsTool` packs the published output, which carries every referenced assembly and
`ILGPU.dll` as plain files, so the nuspec holds no `<dependencies>`. `Program.Version`
reads the assembly's informational version, and the tests compare against it, never
against a typed string. → HISTORY.md#packing-proof

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

→ [ACCEPTANCE.md](ACCEPTANCE.md)

## Taboos

- No physics, no unit conversion beyond seconds for specific impulse, no defaults
  for missing physical inputs.
- No output format other than JSON and CSV in version 1; a CEA-like text table is a
  separate decision.
- No network, no telemetry.
- No second list of the result fields: the structs are the list.
- No rule of a state record here (2026-09-14): the front door owns them, and this node
  names the record a refusal is about.
