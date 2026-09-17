# BOOT.md — Samples

## Purpose

The consumer scenarios of the package surface as running programs: one class per
scenario, each solving a fixed case, checking the statuses it reads and printing its
figures to standard output. The node is the source of every C# block of the
guide (root `BOOT.md`, Delivery: Documentation): each snippet region, delimited by a
`// snippet-start: <name>` / `// snippet-end` pair, is quoted verbatim in the guide, so
the guide's code always compiles and runs. A scenario's `using` lines are a snippet
region of their own (`<ScenarioName>Usings`), separate from the body of `Run`
(`<ScenarioName>`), so a guide page can show a self-contained, pasteable block.

## Invariants

- **The package surface only**, as the command line does (root `BOOT.md`, Delivery: Tree
  contracts): no grant from any neighbour, no internal type of another node; what a
  scenario may call is what a consumer of the packed `APThermo` package may call.
- **One class per scenario, one file**, `internal sealed`, named after the scenario.
  Every scenario checks the `Status` of every result and station it reads before
  reading a figure off it (root `BOOT.md`, Invariants: "Failures are values").
- **A snippet region is the guide's source of truth**: the bytes between its
  `// snippet-start: <name>` and `// snippet-end` markers are what the guide quotes,
  unmodified, after the common leading indentation is stripped. A region may be quoted
  by several guide pages; a guide page that needs different parameters is a different
  scenario class, never an edited snippet.
- **Standard output is reproducible**: fixed case data, the CPU accelerator pinned
  (`AcceleratorKind.Cpu`, so a CUDA machine approves the same bytes; `AcceleratorChoice`
  is the one scenario that also touches `Auto`, and it prints only facts that hold
  regardless of which accelerator `Auto` chose), fixed columns and formatting; nothing
  time-, path-, version- or machine-dependent is printed. The columns are frozen by the
  approved outputs of the docs tests node; changing them is a re-approval decision
  recorded in that node.

  ⚠ 2026-09-17: this bullet, "one class per scenario" above, and the Constraints and
  Acceptance criteria below were rewritten together with the code, after the audit of
  that day (fixed in `900e5a4`) found the node covered four of about
  fourteen consumer questions (S2), printed no status check (S1), and misdescribed
  itself in three places (S3, S9, W2). The scenario set, the marker syntax and the
  package-feed mode are the root `BOOT.md`'s restored Delivery/Documentation bullet of
  the same date; this node's own text now matches it and the code. One of the three
  misdescriptions: this document claimed a region was "the whole body of the
  scenario's `Run` method" while the code's single marker sat above the class
  declaration, before `Run` even began — a straightforward contradiction the audit
  caught by reading both (W2). The rewrite makes the claim true instead of removing
  it: a scenario's body region now starts inside `Run`, after its own
  `// snippet-start` line, and ends at `// snippet-end` before `Run`'s closing brace,
  so "the body of that method" (Constraints, below) is again what the code does.

## Dependencies

- [Problems](../../src/Problems/API.md) — propellants, problems, elemental mixtures, state records, the solver and its solve forms.
- [Data](../../src/Data/API.md) — `SpeciesDatabase.LoadBundled()` and `Load()`, the database embedded in the package or read from files.
- [Execution](../../src/Execution/API.md) — `EngineOptions`, `AcceleratorKind` and `AcceleratorProbe` of the solver's creation and accelerator choice.
- [Performance](../../src/Performance/API.md) — the performance figures a scenario prints (`FlowModel`, `PerformanceFigures`).
- [Thermo](../../src/Thermo/API.md) — the mixture state and `CaseStatus` a scenario reads off its solution.
- [Transport](../../src/Transport/API.md) — `TransportFigures`, read off a station when transport was requested.
- [Equilibrium](../../src/Equilibrium/API.md) — `ProblemKind`, named on an `EquilibriumProblem`.

Outside the tree: none beyond the .NET base class library.

## Constraints

Inherited from the root ([BOOT.md](../../BOOT.md)). In addition:

- The project is `samples/Samples/APThermo.Samples.csproj`, a console app in the
  solution; the assembly and the root namespace are `APThermo.Samples`; it is not
  packable and part of no package.
- **Two build modes** (root `BOOT.md`, Delivery: Documentation). By default the
  project references the seven library projects directly (`Problems`, `Data`,
  `Execution`, `Performance`, `Thermo`, `Transport`, `Equilibrium`), so a change in
  `src/` is visible immediately. With `-p:APThermoPackageVersion=<version>` it instead
  takes one `PackageReference` to the packed `APThermo` package at that version
  (`VersionOverride`, since the tree's package versions are centrally managed), from a
  feed given with `--source` or a scratch `nuget.config` outside the tree; nothing
  under source control names a feed path. Every scenario uses the package surface only
  (the first invariant above), so it compiles unchanged in both modes; the docs tests
  node runs the default (project-reference) build only, since it needs the assembly's
  internal tree contract.
- The tree contract is an internal `Program`: `Main(string[])`,
  `Run(string[] args, TextWriter output, TextWriter error)` with `args` the scenario's
  name (an unknown name, or none, prints the usage to `error` and returns 2; a solved
  scenario returns 0), `IReadOnlyList<string> Scenarios`, the names in order, and
  `ClassNameOf(string scenario)`, the source file's class name of a scenario — the one
  place that maps a name to its class (finding S11, fixed in `900e5a4`), read by
  reflecting on the scenario table's own delegates rather than a second, hand-written
  list. The assembly grants `InternalsVisibleTo` to `APThermo.Docs.Tests` alone, which
  runs the scenarios in-process (root `BOOT.md`, Delivery: Tree contracts: a grant
  follows the declared dependency).
- One scenario class is `internal sealed` with `internal static void Run(TextWriter
  output)`; its snippet region is the body of that method, and its `using` lines are a
  second, separate region.
- `DatabaseFromFiles` is the one scenario allowed file I/O (its own taboo below is
  narrowed for it alone): it reads the committed `data/thermo.inp` and `data/trans.inp`
  through `SpeciesDatabase.Load`, locating the repository root from its own source file
  (`[CallerFilePath]`, the same technique the fixtures node's `RepositoryPaths` uses;
  AGENTS.md §13), never from the current directory or the executable's location.

## Acceptance criteria

- [ ] Every scenario runs against the project references and prints its approved
      output (`tests/Docs.Tests`, L2, `approved/samples/`).
- [ ] Every C# block of the guide equals its snippet region byte for byte
      (`tests/Docs.Tests`, L1).
- [x] 2026-09-17 — The package-feed build mode restores `APThermo` from a local feed
      and every scenario reproduces its approved output against it (root `BOOT.md`,
      Delivery: Documentation and the packages acceptance criterion). Not proven by
      `dotnet test`, since it needs a packed feed outside the tree; proven instead by:
      CI's "Samples run against the packaged library" step
      (`.github/workflows/ci.yml`), which packs `src/Problems`, sets a job-local
      `NUGET_PACKAGES`, loops over every scenario named in this node's `API.md`
      `## Scenarios` table (not typed into the workflow) and diffs each output with
      `tests/Docs.Tests/approved/samples/`; and one recorded local run the same day —
      `dotnet pack src/Problems/APThermo.Problems.csproj --configuration Release
      --output <feed>`, `NUGET_PACKAGES=<scratch dir>`, the same loop against
      `-p:APThermoPackageVersion=0.1.0 -p:RestoreAdditionalProjectSources=<feed>` — all
      twelve scenarios reproduced their approved bytes.

## Taboos

- No physics of its own, no formula: every figure is printed as the library returns it.
- No public type; nothing references this assembly except the docs tests node's
  in-process run through the tree contract.
- No file I/O beyond `DatabaseFromFiles`' committed `data/` files (no other scenario
  reads a file), no network, no environment variable.
- No prose of the guide here: a sentence about a scenario belongs to its guide page,
  and a line of code belongs to exactly one snippet region (though a region may be
  quoted by more than one page).
- No scenario written without reading and checking a `Status` before it reads a figure.
