# HISTORY.md — Cli

Append-only store of what `BOOT.md` no longer needs to state as current truth
(`AGENTS.md`, §15). Newest entry first. Every entry names the date it was moved, the
`BOOT.md` section it came from, and carries the original text in full, unedited. The
start procedure (`AGENTS.md`, §10) does not read this file; it is reached only by
following a dated pointer left in `BOOT.md` (or `ACCEPTANCE.md`) at the place the text
used to stand.

---

<a id="commands-condensed"></a>

## 2026-10-01 — from "## Constraints" — condensed wording of the commands bullet

Moved because condensed wording: the command and option list is `API.md`'s, the dated asides are history. The text as it stood:

> - Commands: `rocket <input.json>`, `equilibrium <input.json>`, `states <records>...`,
>   `species [--find TEXT]`, `devices`, `schema [name]` (2026-09-16: prints the JSON
>   Schema embedded in the assembly, by name, to standard output or the `--output` file;
>   2026-09-17, the audit's C3/C4/C5: a missing or unknown name is exit code 2 naming
>   every embedded name, read from the assembly manifest, not from a typed list);
>   options `--output PATH`, `--format json|csv`,
>   `--accelerator auto|cpu|cuda`, `--database DIR` (directory with `thermo.inp` and
>   `trans.inp`; without it, the database embedded in `APThermo`,
>   `Data.SpeciesDatabase.LoadBundled()`), `--threshold X` (mole fractions below X are
>   omitted from the composition tables; default 5e-6, the reference's print threshold),
>   `--transport` (states), `--find TEXT` (species), `--help`, `--version` (prints
>   `Program.Version` and exits; applies to no command and may be given alone). Every
>   option applies to the commands `API.md` lists it with; an option that does not
>   apply is an error. 2026-09-13: `--mass-tolerance X` on the solving commands, the
>   mass tolerance declared for every mixture built from element moles (default the
>   library's, 1e-2; a propellant by reactants keeps the default); a run option, not a
>   document field, because it describes the caller's records and not the physics.

---

<a id="children-intro"></a>

## 2026-10-01 — from "## Children" — condensed wording of the Children introduction

Moved because condensed wording. The text as it stood:

> Five clusters of `## Structure` above passed the child-node test (the root `BOOT.md`,
> `## Decomposition`; the warning above records why each was cut where it was, and why
> `DocumentWords`, `Names` and the run-bookkeeping types were not moved). Each child's
> own `BOOT.md` names the types it holds and why; the `AGENTS.md` §1 access rules apply
> between this node and each of them exactly as between neighbours: reading a child's
> code from a sibling child, or from this node past its `API.md`, is not this document's
> business to forbid twice.

---

<a id="records-and-names"></a>

## 2026-10-01 — from "## Children" — the records' parameters and the names of the decomposition

Moved because condensed wording: the listing of what was renamed or reshaped is provenance. The text as it stood:

> - **Records take at most six positional parameters**: `StateDocument` becomes the front
>   door's `StateRecord` with its `RecordSource`; a reactant document carries its custom
>   part as a `CustomReactantDefinition`; `RunInfo` takes `Timings` and `RunLimits`;
>   `CaseOutput` is declared with init properties (F-CL-09).
> - **Names**: `CommandRegistry` and `CommandTable` instead of two `Commands`, and verbs
>   for the case builders (F-CL-14).

---

<a id="audit-header-2026-09-28"></a>

## 2026-10-01 — from "## Constraints" — the header of the audit fixes of 2026-09-28

Moved because its header is merged into the one above the audit fixes (condensed wording). The text as it stood:

>
> - **Audit fixes of 2026-09-28** (the second hidden-defect audit, Data, Problems and
>   Cli, findings F4 to F7 and observation 8). Each child node named holds its part.

---

<a id="audit-mass-tolerance-2026-09-28"></a>

## 2026-10-01 — from "## Constraints" — audit of 2026-09-28, Mass tolerance: the contract says what the code does

Moved because its rule is stated once, with the bullet of 2026-09-26 (condensed wording). The text as it stood:

>   - **Mass tolerance: the contract says what the code does.**
>     - `API.md` states that `--mass-tolerance` on a document whose propellant is given
>       by reactants is exit 2, in the command description and in the errors table.
>     - The guide's sentence says which commands and documents take the option.
>     - The propellant-mixture refusal carries the document's path like every other
>       form: `<file>: the propellant's mixture (case i): …`.
>     - The `species` listing's `run` section records only the options the command
>       takes (observation 8).
>     - ⚠ The fix of 2026-09-26 changed the code and the changelog but left `API.md`
>       saying a reactant propellant "keeps the default". The guide still said every
>       solving command takes the option (finding F7).

---

<a id="audit-header-2026-09-26"></a>

## 2026-10-01 — from "## Constraints" — the header of the audit fixes of 2026-09-26

Moved because condensed wording: one header for both audits, the child nodes' parts linked. The text as it stood:

> - **Audit fixes of 2026-09-26** (the hidden-defect audit of that day, Data, Problems and
>   Cli, findings 1, 2, 3, 7, 8 and 9, and its note on the database error). Each child
>   node named holds its part:

---

<a id="audit-states"></a>

## 2026-10-01 — from "## Constraints" — audit of 2026-09-26, States: the refusal named the batch index

Moved because a ⚠ correction, over the limit (AGENTS.md §15); the pointer names both wordings. The text as it stood:

>     - ⚠ Only the shape and mass refusals were mapped. A zero pressure on line 3 was
>       reported as `equilibrium problem 1`, which reads as line 2.

---

<a id="audit-mass-tolerance-2026-09-26"></a>

## 2026-10-01 — from "## Constraints" — audit of 2026-09-26, Mass tolerance (condensed with the bullet of 2026-09-28)

Moved because condensed wording: the bullet of 2026-09-26 and the one of 2026-09-28 say one rule, stated once. The text as it stood:

>   - **Mass tolerance.** `--mass-tolerance` applies to documents that carry element
>     moles (`states`, `propellant.elementMoles`). With a reactant propellant it is an
>     option that does not apply: exit 2. There the front door's propellant path holds
>     its mixtures to `DefaultMassTolerance`, and `run.massTolerance` records that
>     value. A reactant document's refusal names `the propellant's mixture (case i)`,
>     as the front door's propellant path does.
>     - ⚠ `run.massTolerance` echoed the option while the check in force was 1e-2. The
>       refusal said `mixture i` and cited 1 % under `--mass-tolerance 0.9`.

---

<a id="schemas-readers"></a>

## 2026-10-01 — from "## Invariants" — who reads the schemas, and through what

Moved because a ⚠ correction, over the limit (AGENTS.md §15); the pointer names both wordings. The text as it stood:

>   ⚠ 2026-09-17 (the audit's C6): stood "the tests nodes read the schemas through that
>   command", true of neither test node that reads the schemas at the point this was
>   written. `tests/Cli.Tests` reads them directly through `SchemaResources`, the type
>   under test, so its checks never called `apthermo schema` at all; only
>   `tests/Docs.Tests` reads them through the command, in-process
>   (`Program.Run(["schema", name], …)`), as the mirrored test node has no other way to
>   reach this node's contract. Found by the CLI audit's finding C6, fixed in `68f540a`.

---

<a id="schemas-moved"></a>

## 2026-10-01 — from "## Invariants" — the schema files move from the tests node to this node

Moved because a ⚠ correction, over the limit (AGENTS.md §15); the pointer names both wordings. The text as it stood:

>   ⚠ 2026-09-16: stood "The schema files of the tests node hold the same lists, and a
>   test compares them with the structs." The distribution phase decided at the root
>   (`## Delivery`, Documentation) that the schemas belong to the command line: they move
>   from `tests/Cli.Tests/schemas/` to this node's `Schemas/` directory, are embedded in
>   the assembly and served by the new `schema` command, so that a consumer of the packed
>   tool reads the contract from the tool itself and no second copy can drift.

---

<a id="deps-transport"></a>

## 2026-10-01 — from "## Dependencies" — Transport and PerformanceFigures leave this list for Output

Moved because a ⚠ correction, over the limit (AGENTS.md §15); the pointer names both wordings. The text as it stood:

> ⚠ 2026-09-15: this list carried `Transport` (`TransportFigures`) and read
> `Performance` as `FlowModel, PerformanceFigures`. The clean-code decomposition moved
> `StationFields` and every direct reader of `TransportFigures` and
> `PerformanceFigures` into the child node `Output` (its own `BOOT.md` declares
> `Transport` and `Performance` now); this node's own remaining code reaches
> `Performance` only through `DocumentWords`' flow words. Found by the protocol tests
> node's `DependencyTests` after the move (`src/Cli/BOOT.md declares src/Transport, but
> no type of src/Cli refers to it`); the child-node attribution
> (root `BOOT.md`, Constraints, 2026-09-15) means a dependency used only by a child is
> declared there, not repeated at the parent's own level, unless the parent's own code
> also uses it.

---

<a id="database-default"></a>

## 2026-10-01 — from "## Constraints" — `--database` default: the search for `data/` is gone

Moved because a ⚠ correction, over the limit (AGENTS.md §15); the pointer names both wordings. The text as it stood:

>   ⚠ 2026-09-15 (distribution phase): `--database`'s default stood "`data/` next to
>   the executable, then `data/` under the current directory, then the current
>   directory" (`DatabaseFiles.Resolve`, since removed). A NuGet package and a .NET
>   tool have no `data/` directory beside them (root `BOOT.md`, `## Delivery`, `Data`),
>   so every consumer without `--database` would first have to find NASA files
>   themselves. The search is gone; `DatabaseFiles.Load` now reads
>   `SpeciesDatabase.LoadBundled()` when no directory is given, and `run.database`
>   reports the path-like markers `"embedded:thermo.inp"`/`"embedded:trans.inp"`
>   (`API.md`, Output document) instead of a file path, with the same provenance
>   hashes a caller who pointed `--database` at the committed `data/` would get.

---

<a id="structure-children"></a>

## 2026-10-01 — from "## Structure" — the Structure introduction, the child-node correction and the mechanical move

Moved because condensed wording; the first sentence stood before the split into child nodes, and its ⚠ of 2026-09-15 corrects it. The text as it stood:

> Decided 2026-09-14 (the clean-code pass; the root's code-shape constraint). The node
> had grown four commands, two formats and two input shapes in five files without a
> split: `InputDocuments` at 399 lines with an efferent coupling of 18, `Solving` a hub
> of 27, `CommandLine.Parse` at 117 lines, and the station written once per format (the
> review's F-CL-02 to F-CL-10; the review's measures at `8e36a27`: physical lines and a
> textual count of names; 351 and 101 lines of code by the current rule). One node, one
> directory: the split is into types, not into sub-nodes, which the root's
> one-assembly-per-node rule would turn into several assemblies for one adapter.
> Everything is internal except `Program` and `ExitCode`; one type per file, named after
> the type.
>
> ⚠ 2026-09-15: "the split is into types, not into sub-nodes" no longer holds whole.
> The root's own-assembly-per-node rule was the reason for it, and the root `BOOT.md`
> (Constraints, 2026-09-14 for the decision, dated 2026-09-15 in its own text) now lets a
> child node compile into its nearest ancestor's assembly instead of forcing one of its
> own, precisely so a large node like this one could be cut into sub-nodes without
> widening its public surface. Five clusters of this node passed the child-node test (the
> root `BOOT.md`, `## Decomposition`, the "child nodes phase"): `Syntax/`,
> `Documents/`, `Cases/`, `Output/` and `Listings/`, each with its own `BOOT.md` and
> `API.md` and the namespace of its path
> (`APThermo.Cli.Syntax` and so on), compiled into this
> node's own assembly. `Program`, `CommandRegistry`, `Failures`, `SolverSession`, the
> three command types (`ProblemCommand`, `StatesCommand`, `SpeciesCommand`) and the
> shared vocabulary and run-bookkeeping types with no single owning cluster
> (`DocumentWords`, `Names`, `InputException`, `InputFile`, `DatabaseFiles`,
> `DatabaseInfo`, `RunInfo`, `RunLimits`, `Timings`) stayed at this node's own level.
> `DocumentWords` was weighed for `Syntax/` and for `Documents/` (both read it,
> `CommandTable` for `--accelerator`, the readers for `flow`, `role`, `amountKind` and
> `kind`) and, on inspection, also for `Cases/` (`CaseInputs`'s echo of `kind`) and for
> `Output/` (`RunSection`'s echo of the accelerator): four clusters, no dominant owner,
> so moving it into any one would turn the other three into its dependents for one
> lookup table. It stays here, at this node's own level: every child already names this
> node as its ancestor (`[Cli](../API.md)` in its own `## Dependencies`), so reading
> `DocumentWords` from any of them costs no new edge. `Names` and the run-bookkeeping
> records (`RunInfo` and what it carries) were kept for the same reason, one level less
> sharply split: `Names` reaches `Output/` and `Listings/`, `RunInfo` is `SolverSession`'s
> own return value read by `Output/`, `Listings/` and the command types alike, and moving
> either would still leave at least two of the three as its dependents. `OutputFormat`
> and `CaseOutput`/`Combination` moved instead of staying, because each has a genuine
> majority owner (`Syntax/` and `Cases/` respectively) that the rest of the node
> reaches only through a field already carried by a wider record (`CommandOptions.Format`,
> the case's own shape); their own `BOOT.md` records the reasoning. `Syntax/` is not
> named `CommandLine/`: its own `BOOT.md` records why (a namespace and a same-named type
> inside it force a doubled qualification on every caller in the `Cli` tree).
>
> The move itself was mechanical: `git mv` and a namespace edit per moved file, no logic
> touched, one commit per child (`## Structure` of each child names its own moved
> types). No `## Shape exceptions` row moved, because the three rows below all name a
> composition root that stayed at this node's own level.

---

<a id="structure-rows-drift"></a>

## 2026-10-01 — from "## Structure" — four rows of the table had drifted from the code

Moved because a ⚠ correction, over the limit (AGENTS.md §15); the pointer names both wordings. The text as it stood:

> ⚠ 2026-09-15: four rows of the table below had drifted from the code they describe.
> `StateRecordReader`'s row said the record files' shape was decided "by the first
> non-blank character"; the code decides it by attempting to parse the first JSON value
> and checking what follows it (`JsonText.TryParseWhole`), not by inspecting characters.
> `DocumentWriter`'s row said only "delivery to the output file or the standard output,
> and the non-finite-number rule", omitting that it also renders a case document in the
> requested format and decides its exit code, and that the JSON writer the listings
> (`species`, `devices`) render through is this type's too. `Sweeps`' row named three of
> the sweep's four axes ("ratio-major, then pressure, then temperature"), silently
> dropping chamber pressure, which the code already crossed. `CaseInputs`' row claimed
> the whole `inputs` echo was "written once" in this type, while the code wrote only the
> swept ratio and left `RocketCases` to add `chamberPressure` and `EquilibriumCases` to
> add `kind`, `pressure` and the target (`AddTarget`). Found by the repair review of
> 2026-09-15 reading the code against the table; its R-Cli-7 and R-Cli-3 also moved the
> code of the last two to match the row each already claimed (`Sweeps.Expand` is one
> query over the four axes; `CaseInputs.Rocket` and `CaseInputs.Equilibrium` own the
> whole echo, `AddTarget` moved in from `EquilibriumCases`).

---

<a id="no-other-byte"></a>

## 2026-10-01 — from "## Children" — the decomposition's no-other-byte decision and its correction

Moved because condensed wording; the first sentence named the proof recorded before any code moved. The text as it stood:

> - **No other byte of any output document changes.** The decomposition is proved by the
>   tests node's snapshot of the example outputs, recorded before any code moved; the
>   `run` sections of the `species` and `devices` listings stay as they are (the review's
>   open question 5 is answered by leaving the documents alone).
>
>   ⚠ 2026-09-15: for a JSON document's formatting — indentation, line breaks, the final
>   newline, string escaping — this claim was not held from 2026-09-14, when the
>   decomposition landed, to 2026-09-15: the tests node's snapshot hashed a compact
>   re-serialization of the document, which left that formatting unguarded (the repair
>   review's R-Cli.Tests-2). It holds now: the snapshot hashes the bytes the command line
>   delivers, with `run` cut out by span (`tests/Cli.Tests/BOOT.md`, the Bits level and
>   the criterion of 2026-09-15).

---

<a id="packing-proof"></a>

## 2026-10-01 — from "## Children" — the packing decisions and their one-time proof

Moved because condensed wording; the proof (the packed nuspec and the nineteen files) and the removed hardcoded version are provenance. The text as it stood:

> **Packing (2026-09-15, distribution phase, root `BOOT.md`, `## Delivery`, Packages).**
> This node's project packs as `APThermo.Cli`, a .NET tool (`PackAsTool=true`,
> `ToolCommandName=apthermo`, both already set before this phase): `IsPackable=true`
> already stood, `PackageId=APThermo.Cli` is new, and the version and the shared
> package metadata come from the root's `Directory.Build.targets`, as for `Problems`.
>
> Unlike `Problems`, none of this node's seven `ProjectReference`s need
> `PrivateAssets` or a merge target: `PackAsTool` packs the *published* output
> (`tools/net10.0/any/`), which already carries every referenced assembly (including
> `APThermo.Problems.dll` and, through it, the six it merges) and `ILGPU.dll` as plain
> files, not as nuspec dependencies — a tool has no consumer to declare dependencies
> to. Proved once, read-only: the packed nuspec's `<dependencies>` is absent
> entirely, and `tools/net10.0/any/` holds `APThermo.Cli.dll` plus the seven library
> assemblies and `ILGPU.dll`, nineteen files including the `.deps.json`,
> `.runtimeconfig.json` and `DotnetToolSettings.xml` the SDK's tool packaging adds.
>
> `<Version>1.0.0</Version>`, hardcoded before this phase, is removed: the version is
> now the one place, `Directory.Build.props`' `VersionPrefix` (0.1.0), like every other
> project; `Program.Version` (already reading the assembly's informational version, `##
> Structure` above) needed no change; `ProcessTests` and `CommandLineTests` compare
> against it rather than a typed string, so the version's value never had to be pinned
> in a test.

---

<a id="size-bullet"></a>

## 2026-10-01 — from "## Children" — the Size bullet and its correction

Moved because condensed wording: the numbers are the root's, stated once there. The text as it stood:

> - **Size.** No type over 400 lines, no method over 60, no nesting deeper than 3, no
>   more than 6 parameters, no type with an efferent coupling over 14; a composition root
>   or a registry that holds no formula and cannot stay under the coupling limit is
>   declared in `## Shape exceptions` with its measured figure and its reason, and any
>   other type is split.
>
>   ⚠ 2026-09-14: this bullet stood "over 10" after the root's own limit was recalibrated
>   to 14 the same day (the root `BOOT.md`, Constraints): the root's number moved and this
>   copy of it did not. It also let any type that could not stay under the coupling limit
>   be declared, where the root allows the exception only to a registry or a composition
>   root that holds no formula. The protocol tests node's measurement over the tree with
>   this decomposition merged found `ProblemDocumentReader` at 21 and `SpeciesListing` at
>   17; both were split instead (`PropellantDocumentReader`, `ProblemPartReader` and
>   `SweepDocumentReader` out of the first; `SpeciesCommand` and `SpeciesListing`).

---

<a id="strict-values"></a>

## 2026-10-01 — from "## Invariants" — strictness covered names, not the values of a composition

Moved because a ⚠ correction, over the limit (AGENTS.md §15); the pointer names both wordings. The text as it stood:

>   ⚠ 2026-09-13: the strictness covered the names of fields, not the values of a
>   composition: a state record with every element mole doubled, or given in mol/g,
>   ran through `states` and produced a document with exit code 0, which is exactly
>   the silent unit mistake this invariant promised to catch. The check belongs to the
>   front door, where a mixture meets the database's atomic weights; this node only
>   names the record.

---

<a id="deps-sketch"></a>

## 2026-10-01 — from "## Dependencies" — the sketch's dependency list lacked four nodes

Moved because a ⚠ correction, over the limit (AGENTS.md §15); the pointer names both wordings. The text as it stood:

> ⚠ 2026-09-12: the sketch listed `Problems` and `Data`. The solver is created with the
> execution node's options and reports its accelerator, and the result records carry
> the numerical nodes' structs, which this node reads field by field; the reflection
> dependency check reads types in method bodies, so the links are declared. The root's
> decomposition carries the same.

---

<a id="csv-figures"></a>

## 2026-10-01 — from "## Constraints" — CSV carries each station's own figures

Moved because a ⚠ correction, over the limit (AGENTS.md §15); the pointer names both wordings. The text as it stood:

>   ⚠ 2026-09-12: stood "with the performance figures of the station's exit"; the
>   performance node reports the figures per station.

---

<a id="states-batches"></a>

## 2026-10-01 — from "## Constraints" — a mixed states file is two batches

Moved because a ⚠ correction, over the limit (AGENTS.md §15); the pointer names both wordings. The text as it stood:

>   ⚠ 2026-09-12: stood "one batch": a rocket case and an equilibrium case are
>   different programs of the execution node, so a mixed file is two batches, still one
>   call each.

---

<a id="timings-phases"></a>

## 2026-10-01 — from "## Constraints" — the tool reports its own phases

Moved because a ⚠ correction, over the limit (AGENTS.md §15); the pointer names both wordings. The text as it stood:

>   ⚠ 2026-09-12: the sketch's timings were the engine's (`warmUp`, `upload`, `kernel`,
>   `download`), which the front door does not expose; the tool reports its own phases
>   (`database` load, `solve`).

---
