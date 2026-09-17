# BOOT.md — Samples

## Purpose

The consumer scenarios of the package surface as running programs: one class per
scenario, each solving a fixed case and printing its figures to standard output. The
node is the source of every marked C# block of the guide (root `BOOT.md`, Delivery:
Documentation): each snippet region, delimited by a single `// <!-- snippet: <name> -->`
marker and running from that line to the end of its file, is quoted verbatim in the
guide, so the guide's code always compiles and runs.

## Invariants

- **The package surface only**, as the command line does (root `BOOT.md`, Delivery: Tree
  contracts): no grant from any neighbour, no internal type of another node; what a
  scenario may call is what a consumer of the packed `APThermo` package may call.
- **One class per scenario, one file**, `internal sealed`, named after the scenario. The
  scenarios are the four solve forms of the root `API.md` entry point, in that order:
  `RocketSolve`, `BatchSolve`, `EquilibriumSolve`, `StatesSolve`.
- **The snippet region is the guide's source of truth**: the bytes from the single
  `// <!-- snippet: <name> -->` marker to the end of the file (the whole body of the
  scenario's `Run` method) are what the guide quotes, unmodified. A guide that needs
  different parameters is a different scenario class, never an edited snippet.
- **Standard output is reproducible**: fixed case data (the LOX/LH2 example of the root
  `API.md` entry point), the CPU accelerator pinned (`AcceleratorKind.Cpu`, so a CUDA
  machine approves the same bytes), fixed columns and formatting; nothing time-, path-,
  version- or machine-dependent is printed. The columns are frozen by the approved
  outputs of the docs tests node; changing them is a re-approval decision recorded in
  that node.

## Dependencies

- [Problems](../../src/Problems/API.md) — propellants, problems, the solver and its four solve forms.
- [Data](../../src/Data/API.md) — `SpeciesDatabase.LoadBundled()`, the database embedded in the package.
- [Execution](../../src/Execution/API.md) — `EngineOptions` and `AcceleratorKind` of the solver's creation.
- [Performance](../../src/Performance/API.md) — the performance figures a scenario prints (`FlowModel`, `PerformanceFigures`).
- [Thermo](../../src/Thermo/API.md) — the mixture state a scenario reads off its solution (`MixtureState`).

Outside the tree: none beyond the .NET base class library.

## Constraints

Inherited from the root ([BOOT.md](../../BOOT.md)). In addition:

- The project is `samples/Samples/APThermo.Samples.csproj`, a console app in the
  solution; the assembly and the root namespace are `APThermo.Samples`; it is not
  packable and part of no package.
- It references the library projects directly (`ProjectReference` to `src/Problems`,
  which carries the rest); a package-feed build mode is a post-0.1.0 concern (root
  `BOOT.md`, Delivery: Documentation).
- The tree contract is an internal `Program`: `Main(string[])`,
  `Run(string[] args, TextWriter output, TextWriter error)` with `args` the scenario's
  name (an unknown name prints the usage to `error` and returns 2; a solved scenario
  returns 0), and `IReadOnlyList<string> Scenarios`, the names in order. The assembly
  grants `InternalsVisibleTo` to `APThermo.Docs.Tests` alone, which runs the scenarios
  in-process (root `BOOT.md`, Delivery: Tree contracts: a grant follows the declared
  dependency).
- One scenario class is `internal sealed` with `internal static void Run(TextWriter
  output)`; its snippet region is the whole body of that method.

## Acceptance criteria

- [ ] Every scenario runs against the project references and prints its approved
      output (`tests/Docs.Tests`, L2).
- [ ] ⚠ 2026-09-16: this criterion referred to `-p:APThermoPackageVersion`, a property
      that never existed in `APThermo.Samples.csproj`; the package-feed build mode is a
      post-0.1.0 concern (root `BOOT.md`, Delivery: Documentation). Superseded by the
      first criterion until that mode exists.

## Taboos

- No physics of its own, no formula: every figure is printed as the library returns it.
- No public type; nothing references this assembly except the docs tests node's
  in-process run through the tree contract.
- No file I/O (the database is the embedded one), no network, no environment variable.
- No prose of the guide here: a sentence about a scenario belongs to its guide page,
  and a line of code belongs to exactly one snippet region.
