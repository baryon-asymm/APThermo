# BOOT.md — AerospacePropellantThermodynamics (tree root)

<!-- Every agent reads this file in every session (AGENTS.md §10), so it stays short:
     goals, invariants, constraints, taboos. Details live in the child nodes. -->

## Purpose

A .NET library, with a thin command-line front end, that computes the chemical
equilibrium composition and the thermodynamic and transport properties of rocket
propellant combustion products, and from them the performance of a rocket engine:
chamber, throat and nozzle exit, in shifting-equilibrium and in frozen flow. It is the
class of tool NASA CEA belongs to, rebuilt so that one numerical program runs both on
the CPU and, for large batches of states, on an NVIDIA GPU in double precision.

Input: a propellant (reactants from the NASA database with mass fractions or an
oxidizer-to-fuel ratio, reactant temperatures or enthalpies) or a mixture given
directly by its element moles per kilogram and its enthalpy, as another simulation
hands it over; a chamber pressure, nozzle expansion ratios or exit pressures, and the
flow model. Output: the state at
the chamber, the throat and every exit station (temperature, pressure, composition,
molar mass, heat capacities, isentropic exponent, sound speed, density, enthalpy,
entropy, viscosity, thermal conductivity, Prandtl number) and the performance figures
(characteristic velocity, specific impulse, thrust coefficient). Users are propulsion
engineers running parametric studies from .NET code or from the command line.

Not goals of version 1: detonation and shock problems, ionized species, finite-area
chamber, constant-volume problems (uv, tv, sv), compatibility with CEA input files,
graphical interfaces, thermodynamic databases in formats other than the NASA one.

## Invariants

- **One numerical program.** Every formula of the computation (species functions,
  equilibrium, performance, transport) exists exactly once in the tree, written in
  kernel-compatible C#, and runs unchanged on the CPU accelerator and on CUDA. There
  is no second, scalar implementation of any formula. Checked by the batch tests that
  run the same kernels on both accelerators and compare.
- **Double precision only.** Numerical nodes contain no `float` or `Half` value or
  operation. Checked by reflection over the numerical assemblies
  (`Protocol.Tests.InvariantTests.NumericalNodesHoldNoSinglePrecisionValueOrOperation`).
- **Data come from files.** No thermodynamic or transport coefficient and no atomic
  weight is typed into code: every number comes from the committed NASA files, whose
  upstream commit hash is recorded next to them. Atomic weights are taken from the
  monatomic gaseous species of the same database.
- **Gibbs minimization is the only equilibrium method.** Equilibrium composition is
  the minimum of the Gibbs energy under element conservation (the Gordon–McBride
  formulation). No reaction sets and no equilibrium constants anywhere in the tree.
- **A batch has one species set.** All cases of a batch share the element list, the
  candidate species list (gaseous and condensed) and the thermodynamic tables.
  Condensed species enter and leave a case's solution, but the candidate list does not
  change. Cases with different species sets belong to different batches.
- **The CPU path needs no NVIDIA software.** No numerical node references the
  `ILGPU.Runtime.Cuda` namespace; only the execution node does, and it works with the
  CPU accelerator when there is no CUDA device or no libdevice. Checked by reflection
  over every assembly (`Protocol.Tests.InvariantTests.OnlyTheExecutionNodeAndItsTestsNameCudaTypes`).
- **GPU equals CPU.** For the same batch, the results on CUDA and on the CPU
  accelerator agree within the tolerance table owned by the execution tests node
  (relative 1e-10 on temperature, relative 1e-10 on mole fractions not below 1e-8).
  Every batch test compares both.

  ⚠ 2026-09-12: was 1e-10 on mole fractions at every station, now a second tier, 1e-9,
  where the Newton step counts differ → HISTORY.md#gpu-equals-cpu-second-tier
- **SI units in every public type**: K, Pa, J/kg, J/(kg·K), kg/kmol, kg/m³, m/s,
  Pa·s, W/(m·K). Specific impulse is the effective exhaust velocity in m/s; the
  conversion to seconds with g0 = 9.80665 m/s² happens only in the command-line front end.
- **No hidden state.** A numerical routine takes every input and every scratch area
  through explicit parameters; numerical nodes have no mutable static fields. Checked
  by reflection (`Protocol.Tests.InvariantTests.NumericalNodesHaveNoMutableStaticField`).
- **Failures are values.** Numerical code reports a per-case status code and never
  throws; the front door node turns statuses into results or exceptions.

## Dependencies

None.

Outside the tree: .NET SDK 10.0 (C# 14), pinned by `global.json` to 10.0.112 with
`rollForward: latestPatch` (2026-09-17, the CI audit), whose bundled SourceLink replaces
the explicit package the tree briefly referenced; ILGPU 1.5.3 (NuGet; ILGPU.Algorithms is not
used); for the GPU path an NVIDIA GPU of compute capability 7.5 or newer (2026-09-26,
the range the execution node proves; an older device may work with a 12.x toolkit and is
not verified, and a 13.x libnvvm refuses its target when the engine binds, which the
`Auto` choice turns into the CPU accelerator with the reason), an NVIDIA driver with CUDA 12.8 or newer, plus libnvvm
(`nvvm64_40_0.dll` on Windows, `libnvvm.so` on Linux, 2026-09-15) and
`libdevice.10.bc` from a CUDA Toolkit 12.8 or newer (13.x keeps the DLL under
`nvvm/bin/x64`; on Linux, and under WSL2, the toolkit's `nvvm/lib64`); NASA CEA data `thermo.inp` and `trans.inp` from
github.com/nasa/cea (Apache-2.0); the `cea` Python package 3.3.4 (NASA CEA,
Apache-2.0) as the generator of the reference outputs; Python 3.8+ for the protocol
linter; xunit for tests; Microsoft.CodeAnalysis.CSharp (Roslyn) for the protocol tests
node's shape check (2026-09-14); BenchmarkDotNet 0.15.8 for the benchmarks node
(2026-09-15, the newest stable release on NuGet supporting net10.0 through
`RuntimeMoniker.Net10`, since 0.15.0; pinned in `Directory.Packages.props`); GitHub Actions and nuget.org for
delivery (2026-09-15, `## Delivery` below).

## Constraints

- Platform: Windows x64 and Linux x64 are the supported platforms, both on the CPU
  accelerator and on CUDA (2026-09-15). Nothing but the CUDA library discovery paths
  and file names may be platform-specific. Every test runs on both platforms. The
  approved records are platform-specific (2026-09-17): a node's `Bits.approved.txt`
  holds the Windows bits and its `Bits.linux.approved.txt` the Linux bits, the execution
  tests node's throughput figures follow the same rule, the harness picks the file of
  the running platform, and an intended numerical change re-approves both in the same
  commit. The bits are a record of the reference machine, not of the platform alone
  (2026-09-18): they are compared exactly on the reference machine, in local runs and
  on the self-hosted release runners (Windows and WSL2), and not on the hosted CI
  runners, where the facts carrying the trait `Category=BitSnapshot` are filtered out
  and the CEA tolerance tests hold correctness.

  ⚠ 2026-09-18, declared deviation from "every test runs on both platforms" (AGENTS.md
  §12): a hosted `windows-latest` runner changed one rocket case of the front door's
  snapshot in its last bits, every CEA tolerance test green. The Windows C runtime picks
  FMA3 or plain variants of `exp`, `log` and `pow` from the CPU and hosted runners land
  on different CPUs (disabling the FMA3 variants locally moved 38 of 99 rocket cases),
  so a bit record belongs to a machine. The user chose exact comparison on the
  reference machine over a field-by-field tolerance on hosted runners. What lifts the
  deviation: a bit-stable math path (the CPU dispatch pinned in the execution node) or
  hosted runners of a fixed CPU model. → HISTORY.md#platform-deviation-condensed

  ⚠ 2026-09-17: was the bit snapshots "the one platform-specific record", now the
  throughput figures too → HISTORY.md#platform-throughput-record

  ⚠ 2026-09-17: was the Linux bit question open, now answered: Linux differs by up to
  4.2e-12 relative, records per platform → HISTORY.md#platform-first-linux-run

  ⚠ 2026-09-15: was "Windows 11 x64 is the only supported platform", now Windows and
  Linux x64, CUDA included → HISTORY.md#platform-windows-only
- Language and build: C#, .NET 10, nullable reference types enabled, warnings are
  errors. One assembly per node directory that holds a project, named after its namespace; a
  child node without a project of its own (2026-09-15) compiles into the assembly of its
  nearest ancestor that has one, under its own namespace. One solution
  file at the repository root.

  ⚠ 2026-09-15: was "One assembly per node directory", now per node that holds a project
  (children join their ancestor's) → HISTORY.md#assembly-per-node
- Diagnostics (2026-09-24): the compiler and every analyzer run at their maximum, every
  diagnostic is an error, and nothing in the tree is exempt, the test, sample and
  benchmark nodes included.
  - `Directory.Build.props` sets, for every project: `TreatWarningsAsErrors`;
    `WarningLevel` 9999, every warning wave present and future; `Features` `strict`;
    `AnalysisLevel` `latest-all`; `EnforceCodeStyleInBuild`; `GenerateDocumentationFile`,
    so CS1591 holds for every publicly visible member, and IDE0005 needs it in the
    build. `Directory.Build.targets` clears the SDK's default `NoWarn`. No project file
    sets any of these properties itself.
  - The root `.editorconfig` raises every analyzer diagnostic to a warning
    (`dotnet_analyzer_diagnostic.severity = warning`). It fixes the options of the style
    rules to the style the code was written in: `var`, file-scoped namespaces,
    `_camelCase` private instance fields, PascalCase constants and static fields,
    parentheses for clarity in mixed logical and relational expressions and none in
    arithmetic. A style option chooses between two forms; it is never chosen to silence
    a rule.
  - No diagnostic is suppressed anywhere: no `#pragma warning` and no
    `#nullable disable`; no `[SuppressMessage]` or `[UnconditionalSuppressMessage]`; no
    `NoWarn` and no `WarningsNotAsErrors`; no severity below `warning` in any
    `.editorconfig` or `.globalconfig`; no rule set, that is no `CodeAnalysisRuleSet`
    property and no `*.ruleset` file.
  - A rule that conflicts with a framework is resolved in code: test methods are
    PascalCase (CA1707) and carry XML documentation like any public member (CS1591); an
    awaited call in a test says `ConfigureAwait(true)`, which satisfies both CA2007 and
    xUnit1030; public exceptions carry the standard constructors (CA1032); the result
    structs `MixtureState`, `PerformanceFigures` and `TransportFigures` expose
    properties and value equality (CA1051, CA1815). This breaks the binary surface of
    0.1.0, so the next release is 0.2.0 (`CHANGELOG.md`).
  - A conflict that code cannot resolve goes to the owner; no node declares an exception
    of its own.
  - Checked by every build and by the protocol tests node (`DiagnosticsTests`). Decided
    with the owner on 2026-09-24, who asked for the maximum and rejected the scoped
    exceptions proposed for the test nodes, the benchmarks and the public exceptions.
    → HISTORY.md#diagnostics-condensed
- Namespaces mirror the directory path from the tree root (AGENTS.md §1). The root
  namespace is `APThermo`; the grouping directories `src/`, `tests/` and `samples/` are
  transparent: `src/Equilibrium` is `APThermo.Equilibrium`, `tests/Equilibrium.Tests` is
  `APThermo.Equilibrium.Tests`, `samples/Samples` is `APThermo.Samples`. Projects, assemblies
  and the solution (`APThermo.sln`) carry the same names. The product's name in prose stays
  Aerospace Propellant Thermodynamics, and APThermo is its short name and the name of its packages.

  ⚠ 2026-09-16: was `src/` and `tests/` the transparent directories, now `samples/` too
  → HISTORY.md#namespaces-samples

  ⚠ 2026-09-15: was the root namespace `AerospacePropellantThermodynamics`, now
  `APThermo` → HISTORY.md#namespaces-rename
- Kernel-compatible C# in numerical nodes: static methods, blittable structs,
  `ArrayView` inputs and scratch, no allocation, no exceptions, no virtual calls, no
  LINQ, no strings, no recursion. Per-case scratch lives in batch-sized global buffers;
  the case index is the thread index.
- Math in numerical nodes: only the `double` overloads of `System.Math` from this
  list: `Exp`, `Log`, `Log10`, `Pow`, `Sqrt`, `Abs`, `Floor`, `Ceiling`, plus the
  constant `Math.PI`, which the compiler inlines and which needs no wrapper (the
  transport node's hard-sphere estimate uses it). The minimum and the maximum come from
  the thermo node's `KernelMath.Min` and `KernelMath.Max`, never from `Math.Min` or
  `Math.Max`, nor from `double.Min`, `double.Max` or any other member of `System.Math`
  or `System.Double` outside this list: `double.Max` is `Math.Max` in CoreLib's IL and
  compiles to the same `max.f64`. The protocol tests node checks the list itself, as an
  allow-list of the calls a numerical node makes into `System.Math` and `System.Double`,
  `double.IsNaN` and `double.IsNegative` allowed inside `KernelMath` only. Adding a
  function is a root decision, because the execution node must provide its libdevice
  wrapper. → HISTORY.md#math-list-condensed

  ⚠ 2026-09-28: was a check on `Math.Min` and `Math.Max` only, now an allow-list of
  every `System.Math` and `System.Double` call → HISTORY.md#math-allow-list

  ⚠ 2026-09-27: was `Min` and `Max` in the math list, now `KernelMath.Min` and
  `KernelMath.Max` (NaN differs on CUDA) → HISTORY.md#math-min-max
- ILGPU 1.5.3 is pinned, and its libdevice support is defective for the targets
  `compute_100` and newer (Blackwell): libnvvm rejects the module ILGPU emits, and
  ILGPU silently drops the wrappers. For `compute_75` to `compute_90` libnvvm accepts
  it and ILGPU defines the wrappers itself. The execution node checks every kernel and
  completes the wrappers ILGPU dropped; nothing else in the tree may know about the
  mechanism.
  - A second defect of the version (2026-09-27): under WSL, ILGPU installs a `DllImport`
    resolver on every CUDA context it creates, which .NET allows once per process, so
    the second CUDA engine of a process failed to bind. The execution node registers
    the devices of every later context itself (its `BOOT.md` records the rule).
  - A third (2026-09-28): ILGPU moves a constant left operand of a floating-point
    comparison to the right and inverts its NaN ordering while doing so, so `1.0 < v`
    compiles to `setp.gtu.f64` and is true for a NaN `v` on CUDA and false on the CPU.
    The rule for the numerical nodes: an ordered floating-point comparison (`<`, `<=`,
    `>`, `>=`) either has no literal or constant on its left in the source, or runs
    after a NaN test of its operands. The protocol tests node checks the source half;
    the half that appears only after inlining (a local assigned a constant, a constant
    argument of an inlined method) is the reason `KernelMath` tests both operands for
    NaN first, and the execution node's probe runs `KernelMath` with the constant in
    either position.
  - A fourth (2026-10-01): its transfer overloads that take a `ref T` into host memory
    do not pin it, and a garbage collection between the pointer and the copy moves the
    array under the copy. A lost download handed a consumer an `Ok` case with zero
    figures (`CaseStatus.Ok` is 0), against the failures-are-values invariant. The rule:
    host memory crosses into ILGPU only through an overload that pins it, and a download
    that wrote nothing is refused. The execution node's `Chunks` child holds both (its
    `BOOT.md`); the protocol tests node checks that no `src` method calls a
    by-reference transfer. → HISTORY.md#ilgpu-hazards-condensed

  ⚠ 2026-09-26: was the defect tied to libnvvm 12.9 and 13.3, now to the target,
  `compute_100` and newer → HISTORY.md#ilgpu-defect-by-target
- Compile size (2026-09-30): ILGPU 1.5.3 inlines every function by default
  (`InliningMode.Default`), so each call site of a method that holds a whole solve is a
  full copy of it in the compiled program. A stage that holds or reaches a whole solve
  (`Equilibrium`'s `Solve` and `SolveFrozen`, through `StationSolve` in `Performance`) is
  reached through one method marked `[MethodImpl(MethodImplOptions.NoInlining)]`, or has
  one call site. The rocket kernel's compile on the CPU accelerator stays inside the
  bounds of the execution node's guard (its `BOOT.md`).

  ⚠ 2026-09-30: was no bound on the compile (7 sites: 49.7 s, 11.2 GB), now the
  attribute rule (2.6 s, 0.43 GB) → HISTORY.md#compile-size-measurement
- Batches: structure-of-arrays layout, one case per GPU thread, no dynamic allocation
  during a solve.
- Performance target: on a batch of 100 000 states the CUDA path is at least 5× faster
  than the CPU accelerator path using all cores. There is no single-case latency target
  in version 1.
- Data: the NASA files are committed verbatim under `data/` with a `NOTICE`
  (Apache-2.0) and the upstream commit hash. The data node embeds those same files in
  its assembly (2026-09-15): they are linked from `data/`, never copied in the tree, and a
  test proves by SHA-256 that the embedded bytes equal the files. At run time a database
  is read from the embedded copy or from a path given by the caller.

  ⚠ 2026-09-15: was data "read at run time from that directory", now embedded in the
  data assembly → HISTORY.md#data-embedded
- Repository: git, branch `main`, Conventional Commits, MIT license, English in every
  document, identifier, comment and commit message. No binaries other than the NASA
  text data and text fixtures. Nothing secret exists in this repository.
- Reference machine for measurements: RTX 5070 Ti (SM_120), driver 13.4, CUDA
  Toolkits 12.9, 13.3 and 13.4 (13.4 recorded 2026-09-26; discovery binds the newest),
  16 logical CPU cores. Recorded, not required. It is the only GPU the tree is run on:
  older architectures are proven by compiling for them and running the result on this
  device (the execution node's architecture fact), not on their own hardware.
- Code shape (2026-09-14, the clean-code pass): a type spans at most 400 lines of
  code from its declaration to its closing brace, a method at most 60 (a line of code
  holds more than white space and comments), control flow
  nests at most 3 deep, a method takes at most 6 parameters (kernels aggregate through
  their `in` view and scratch structs; a constructor is a method for this count, a
  record's primary constructor included, and a type that mirrors an external format or
  a published shape field for field may exceed it as a declared exception, constructed
  at its sites with named arguments). A type of the `src` nodes names at most 14
  distinct types of the tree in its signatures and bodies, as the dependency check's
  walk reads them (its efferent coupling, Ce), unless it is a registry or a
  composition root that holds no formula and is named as such in its node's
  `BOOT.md`. A type of the `src` nodes named by 10 or more types of the `src` nodes
  (its afferent coupling, Ca) is a stable type: at most 100 lines of code and no
  behaviour beyond construction and validation, or a contract in its node's `API.md`.
  The instability `I = Ce / (Ca + Ce)` of the `src` nodes that hold a project, over the
  dependency graph their `## Dependencies` declare, never rises along a dependency; a
  child node without a project counts as part of its nearest ancestor with one, its
  declared dependencies joined to that ancestor's and its dependencies inside that
  ancestor's subtree dropped. Every exception is
  declared in the node's `BOOT.md`, as a
  row of its `## Shape exceptions` table with the measured figure and the reason.
  Decomposition goes along the domain's axes (stages of an
  algorithm, entities, phases of a pipeline), never through `partial` (the
  `[GeneratedRegex]` requirement excepted), `#region` or a Helpers/Utils class.
  Everything else in this constraint holds for every type of the tree, the test
  nodes' included. Checked by the protocol tests node (`ShapeTests`), whose `BOOT.md`
  records why the numbers are what they are.

  ⚠ 2026-09-14: was a Ce limit of 10 (a textual count), now 14 (the check's walk) →
  HISTORY.md#code-shape-ce-limit

  ⚠ 2026-09-14, evening: was physical lines counted, now lines of code, 400 and 60
  unchanged → HISTORY.md#code-shape-lines-of-code

  ⚠ 2026-09-15: was the coupling sentences for "every type of the tree", now the `src`
  types → HISTORY.md#code-shape-src-scope

  ⚠ 2026-09-15: was "named by 10 or more types of the tree", now types of the `src`
  nodes → HISTORY.md#code-shape-src-both-sides

  ⚠ 2026-09-15: was "over their project graph", now the graph of the `## Dependencies`
  sections → HISTORY.md#code-shape-dependency-graph

  ⚠ 2026-09-15: was the instability of every `src` node, now of those that hold a
  project → HISTORY.md#code-shape-instability

There is no external ancestor: the tree root is the repository root, and the loader
(`CLAUDE.md`) carries no claims about the system (AGENTS.md §2).

## Acceptance criteria

→ [ACCEPTANCE.md](ACCEPTANCE.md)

## Taboos

- No second implementation of a formula "for convenience on the CPU": it drifts.
- No `float`, `Half` or mixed precision in numerical nodes: concentrations span thirty
  orders of magnitude.
- No coefficient, atomic weight or table typed into code: it cannot be audited against
  the source file.
- No equilibrium constants, reaction sets or hidden species lists: they bind results to
  choices nobody can review.
- No `LibDevice.*`, `XMath` or ILGPU.Algorithms in numerical nodes: Algorithms replaces
  double math with CORDIC, and direct bindings bypass the wrapper list.
- No exceptions, allocations or virtual calls in numerical nodes: kernels cannot run them.
- No unit conversion inside numerical nodes: SI in, SI out.
- No CUDA type outside the execution node: it would silently remove the CPU path.
- No text in a language other than English anywhere in the tree.
- No loosening of a tolerance to turn a test green, and no expected value typed into a
  test when it exists in a fixture file.
- No public type outside its node's `API.md`: undocumented surface is a contract nobody agreed to.
- No suppressed diagnostic, in any form or scope: a warning is fixed in the code, and a
  conflict the code cannot resolve goes to the owner (the Diagnostics constraint).

## Decomposition

The tree is cut along the data flow, one abstraction level per node, and every
numerical level is written once in kernel-compatible C# (first invariant): a case is
one thread's sequential program, so the same code serves single calls on the CPU and
batches on the GPU. What each node owns is its own `## Purpose`, the nodes are listed
with links in [API.md](API.md), and the arrows between them are the nodes'
`## Dependencies`, which point downward only and which `DependencyTests` holds.
- The kernel-capable numerical levels are `src/Thermo`, `src/Equilibrium`,
  `src/Performance` and `src/Transport`. `src/Data` is CPU-only, allocation-heavy code
  and the only node that knows the file formats, which is why it is not merged with
  them.
- `src/Performance` is separate from `src/Equilibrium` because it has its own source of
  truth (CEA rocket tables) and its own iterations, `src/Transport` because it uses a
  different data file and is an optional stage.
- `src/Execution` owns the accelerators and is the only node with CUDA knowledge, so
  every numerical node stays testable on the CPU accelerator without knowing CUDA
  exists.
- `src/Problems` is the front door: propellant conventions are knowledge about CEA and
  rockets, not about solving. `src/Cli` is a thin adapter, kept apart so the library
  never depends on console or serialization concerns.
→ HISTORY.md#decomposition-condensed

  ⚠ 2026-09-12: was `Problems` without a link to `Equilibrium`, now with it
  (`ProblemKind`) → HISTORY.md#problems-problemkind

  ⚠ 2026-09-13: was `Cli` without links to `Execution` and the result structs, now with
  them → HISTORY.md#cli-dependencies

  ⚠ 2026-09-15: was `Cli` creating an engine for `devices`, now
  `AcceleratorProbe.Describe` → HISTORY.md#cli-accelerator-probe

Test nodes mirror the source nodes as `tests/<Node>.Tests`. `tests/Protocol.Tests` holds
the reflection checks of AGENTS.md §13; `tests/Fixtures` the reference outputs
generated with NASA's `cea` package, with their provenance, the generator scripts and
the tolerance table; `tests/Harness` the scaffolding the test nodes share, naming
nothing above `Data` and `Fixtures`; `tests/Benchmarks` the BenchmarkDotNet
measurements, run by hand outside `dotnet test`, its figures recorded and never
asserted (a library, run through its child node `tests/Benchmarks/Runner`);
`samples/Samples` each consumer scenario as a running program over the package surface,
the source of the guide's code; `tests/Docs.Tests` the approved outputs of the samples
and command-line examples and the proof of the guide against them (`## Delivery`,
Documentation). → HISTORY.md#decomposition-condensed

  ⚠ 2026-09-16: was the docs tests node holding "the schemas", now only approved outputs
  and tests → HISTORY.md#docs-tests-schemas

## Delivery

Decided with the user on 2026-09-15 (distribution phase); 0.1.0 is the first release.

- **Packages.** `APThermo` is packed from `src/Problems`, the front door, and carries
  every library assembly (`Data`, `Thermo`, `Equilibrium`, `Performance`, `Transport`,
  `Execution`, `Problems`) in one package, because the nodes are never released apart;
  its only package dependency is ILGPU. `APThermo.Cli` is packed from `src/Cli` as a
  .NET tool with the command `apthermo`. No other project is packable. The version lives
  once, in `Directory.Build.props`, and a release tag `v<version>` must equal it. The
  package metadata and the symbol settings apply only to packable projects and so live
  in `Directory.Build.targets`, where `IsPackable` is already known. The license
  expression is `MIT AND Apache-2.0` (the NASA data), with `NOTICE` packed.
  → HISTORY.md#delivery-packages-condensed
- **Symbols.**
  - Every packed assembly ships its portable PDB in a `.snupkg`, with SourceLink to the
    GitHub commit, from a deterministic CI build.
  - The library assemblies ship their XML documentation; a public member without a
    documentation comment fails the build.
- **Public surface.** Everything public in a packed assembly is a promise to consumers:
  the package surface is the consumer scenarios' types only (38 of the 82 public types
  the review found), whatever no consumer scenario needs is internal, and no ILGPU type
  appears on it. Below 1.0.0 a minor version may break the surface; `CHANGELOG.md`
  names the break. → HISTORY.md#delivery-public-surface-condensed
- **Tree contracts.** A type another node uses but no consumer needs is `internal` to its
  assembly.
  - The assembly grants `InternalsVisibleTo` to exactly the assemblies whose nodes
    declare it in their `## Dependencies`, and to the test and benchmark nodes that use
    it. No grant goes against a declared dependency.
  - An assembly whose internal types reach a kernel as parameters or view elements also
    grants `InternalsVisibleTo("ILGPURuntime")`, the dynamic assembly into which ILGPU
    1.5.3 emits its kernel wrappers. ILGPU does not require kernel types to be public.
  - A node's `API.md` keeps two parts, marked in the section headings: the package
    surface, and the tree contract.
  - A friend assembly may name an internal type of another node only when that node's
    tree contract declares it; the protocol tests node checks this. A grant exposes
    every internal, and the check holds the grant to the contract.
  - The command line is a consumer like any other: it receives no grant and uses the
    package surface only.
  - Records the library creates for consumers (results, database records, the
    accelerator description) have internal constructors. A field added in 0.x then
    breaks no consumer.
  - The batch path of the execution node (engine, batches, uploaded tables) leaves the
    package surface together with the rest of the tree contract. `Solver` stays the
    consumer's batch entry. → HISTORY.md#delivery-tree-contracts-condensed
- **Documentation.** Two layers, each with one source of truth: the contracts are the
  nodes' `API.md` and the XML comments; the guide (`README.md`, `docs/guide/`, the
  package READMEs under `docs/nuget/`) is task-oriented and restates no signature.
  - Every C# block of the guide equals a snippet of the samples node `samples/Samples`,
    a console project in the solution with one class per consumer scenario; each class
    checks the statuses it reads and prints its figures. A snippet is delimited by
    `// snippet-start: <name>` and `// snippet-end` comments, holds statements a
    consumer can paste and may be quoted on several pages; the `using` lines a scenario
    needs are a snippet of their own. `#region` stays forbidden by the code-shape
    constraint.
  - The samples reference the library projects by default. With
    `-p:APThermoPackageVersion=<version>` they restore the `APThermo` package from a
    feed instead, so one source serves both the build and the check of the packed
    package. They use the package surface only, as the command line does.
    → HISTORY.md#delivery-documentation-condensed

  ⚠ 2026-09-17: was an unreviewed rewrite (marked blocks only, no package feed), now
  every block checked, feed restored → HISTORY.md#documentation-restored
  - Every `apthermo` invocation shown in the guide takes its input documents from
    `samples/cli/`, and its shown output is approved. The exceptions are the declared
    synopses whose output depends on the machine (`apthermo devices`) or on the release
    (`apthermo --version`). The docs tests node lists them, and the rest of each such
    line must still parse as a valid invocation.

    ⚠ 2026-09-17: was every invocation approved, now `devices` and `--version` declared
    synopses → HISTORY.md#declared-synopses
  - The docs tests node `tests/Docs.Tests` proves each of the following, each check
    failing when the set it walks is empty and each shown red once: every C# block
    equals its snippet; every sample prints its approved output; every `apthermo`
    invocation of the guide produces its approved output, with the run section cut as
    the command line's tests cut it; every relative link of `README.md`, `llms.txt`,
    `docs/` and the package READMEs resolves; every document under `samples/cli/`
    validates against its schema; every guide page has the shared shape. The approved
    output of an invocation is a record of the reference machine like the bit snapshots
    (2026-09-29): a Windows and a Linux file, compared exactly under
    `Category=BitSnapshot`; on every runner, the hosted ones included, the same document
    is compared field by field, its numbers within 1e-9 relative.
    → HISTORY.md#delivery-docs-proofs-condensed

      ⚠ 2026-09-29: was one approved file compared exactly everywhere, now per-platform
      records plus a 1e-9 field tolerance → HISTORY.md#docs-platform-rule
  - The JSON Schemas of the command line's documents belong to the command line
    (2026-09-15). They move from its tests node to `src/Cli/Schemas/`, are embedded in
    the tool (`apthermo schema <name>` prints one), and are validated there by the
    command line's tests. No copy of them lives under `docs/`.
  - `llms.txt` at the root is the entry for agents: a summary, and links to the guide
    pages, the schemas, the samples and the nodes' `API.md`.
  - Guide pages share one shape (purpose, when to use, steps, errors, see also), so a
    human and an agent navigate them alike.
- **Continuous integration**: [.github/BOOT.md](.github/BOOT.md).
- **Release**, on a tag `v*`: [.github/BOOT.md](.github/BOOT.md).
- **Rehearsal before the tag**: [.github/BOOT.md](.github/BOOT.md).
- **Self-hosted runners**: [.github/BOOT.md](.github/BOOT.md).
- **Evidence for workflow changes**: [.github/BOOT.md](.github/BOOT.md).

  ⚠ 2026-10-01: was `.github/diagnostics/IsaProbe` a declared deviation, now a node
  → .github/HISTORY.md#isaprobe-deviation
- Nothing is pushed to GitHub or nuget.org without the owner's word.
