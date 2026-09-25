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
  (`Protocol.Tests.InvariantTests.Numerical_nodes_hold_no_single_precision_value_or_operation`).
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
  over every assembly (`Protocol.Tests.InvariantTests.Only_the_execution_node_and_its_tests_name_cuda_types`).
- **GPU equals CPU.** For the same batch, the results on CUDA and on the CPU
  accelerator agree within the tolerance table owned by the execution tests node
  (relative 1e-10 on temperature, relative 1e-10 on mole fractions not below 1e-8).
  Every batch test compares both.

  ⚠ 2026-09-12: those numbers hold at stations where both accelerators stop after
  the same number of Newton steps. The equilibrium solver polishes until its
  corrections fall below 1e-11, the rounding floor of its linear solves, and a
  last-ULP difference between libdevice and .NET flips that threshold decision now
  and then (91 of the 400 000 stations of the 100 000-case sweep): one accelerator
  then takes one polish step more, and the two differ by up to 1.4e-11 on
  temperature and 3.3e-10 on the mole fraction of a minor species. The original
  wording assumed the converged iterate were unique to 1e-10, which the stopping
  rule does not guarantee. The tolerance table of the execution tests node states
  the second tier (1e-9 on mole fractions at such stations, derived from the polish
  threshold) and bounds the share of such stations, so that a systematic divergence
  cannot hide behind it.
- **SI units in every public type**: K, Pa, J/kg, J/(kg·K), kg/kmol, kg/m³, m/s,
  Pa·s, W/(m·K). Specific impulse is the effective exhaust velocity in m/s; the
  conversion to seconds with g0 = 9.80665 m/s² happens only in the command-line front end.
- **No hidden state.** A numerical routine takes every input and every scratch area
  through explicit parameters; numerical nodes have no mutable static fields. Checked
  by reflection (`Protocol.Tests.InvariantTests.Numerical_nodes_have_no_mutable_static_field`).
- **Failures are values.** Numerical code reports a per-case status code and never
  throws; the front door node turns statuses into results or exceptions.

## Dependencies

None.

Outside the tree: .NET SDK 10.0 (C# 14), pinned by `global.json` to 10.0.112 with
`rollForward: latestPatch` (2026-09-17, the CI audit), whose bundled SourceLink replaces
the explicit package the tree briefly referenced; ILGPU 1.5.3 (NuGet; ILGPU.Algorithms is not
used); for the GPU path an NVIDIA driver with CUDA 12.8 or newer, plus libnvvm
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
  §12): the first release run failed on a hosted `windows-latest` runner in Release
  with one rocket case of the front door's snapshot changed in its last bits, every
  CEA tolerance test green, while earlier hosted runs and 35 local runs passed. The
  Windows C runtime picks FMA3 or plain variants of `exp`, `log` and `pow` from the
  CPU, and hosted runners land on different CPUs: disabling the FMA3 variants locally
  (`_set_FMA3_enable(0)`) moved the bits of 20 of 213 front-door cases, 21 of 464
  equilibrium cases and 38 of 99 rocket cases. A bit record therefore belongs to a
  machine. The user chose exact comparison on the reference machine over a
  field-by-field tolerance on hosted runners. What lifts the deviation: a bit-stable
  math path (the CPU dispatch pinned in the execution node) or hosted runners of a
  fixed CPU model.

  ⚠ 2026-09-17: the first wording of that sentence, written the same day, called the bit
  snapshots "the one platform-specific record". The Linux run then also failed the
  throughput tripwire (the ratio under WSL2 is lower and varies between runs, 44× and
  52× measured), and the execution tests node gave it a Linux file too.

  ⚠ 2026-09-17, the first Linux run (WSL2 Ubuntu 24.04, .NET SDK 10.0.112, at
  `f67b1a9`) answered the open question below: the CPU accelerator does not reproduce
  the Windows bits. Every tolerance test against the CEA references, the docs tests and
  the execution tests on CUDA (the 100 000-case sweep included) passed; the bit
  snapshots of `Equilibrium`, `Performance`, `Transport`, `Problems` and `Cli` did not.
  A field-by-field dump of the equilibrium and rocket snapshot sets (58 208 fields per
  platform) found 12 150 fields differing by at most 4.2e-12 relative (temperature
  2.7e-13), with no difference in any iteration count, status or active condensed
  species; `Math.Exp`, `Math.Pow` and `Math.Log` differ by exactly 1 ULP on 0.5 %,
  0.09 % and 0.015 % of sampled arguments. The difference is the C runtimes' last-bit
  rounding carried through the Newton steps, far inside the tolerance tiers of the
  GPU-equals-CPU invariant. The user chose per-platform snapshots over a tolerance
  comparison on Linux or no snapshot on Linux, so that an unintended change stays
  visible to the bit on both platforms.

  ⚠ 2026-09-15 (distribution phase): stood "Windows 11 x64 is the only supported
  platform of version 1. Nothing but the CUDA library discovery paths may be
  Windows-specific." The user decided to ship the library as a NuGet package and the
  command line as a .NET tool for Windows and Linux, with full support on Linux,
  CUDA included. On Linux the GPU path is verified under WSL2 on the reference
  machine. One question stays open until it is measured: whether the CPU accelerator
  reproduces the Windows bit snapshots on Linux. `System.Math` calls the platform's C
  runtime, which may round the last ULP differently. The first Linux run of the suite
  decides it, and that decision is recorded here.
- Language and build: C#, .NET 10, nullable reference types enabled, warnings are
  errors. One assembly per node directory that holds a project, named after its namespace; a
  child node without a project of its own (2026-09-15) compiles into the assembly of its
  nearest ancestor that has one, under its own namespace. One solution
  file at the repository root.

  ⚠ 2026-09-15: stood "One assembly per node directory". A node is a directory
  (AGENTS.md §1). A cluster of a large node with a contract narrower than its code and
  a reason of its own to change earns its own pair of documents, but an assembly of its
  own would widen the public surface and the project graph for types that are internal
  today. A child node therefore compiles into its nearest ancestor's project, and the
  protocol tests node attributes a type to the deepest node whose namespace it carries,
  as AGENTS.md §1 already defines membership by the directory of a file. Decided with
  the user on 2026-09-14 for the phase after the clean-code pass.
- Diagnostics (2026-09-24): the compiler and every analyzer run at their maximum, every
  diagnostic is an error, and nothing in the tree is exempt, the test, sample and
  benchmark nodes included.
  - `Directory.Build.props` sets, for every project:
    - `TreatWarningsAsErrors`;
    - `WarningLevel` 9999, every warning wave present and future;
    - `Features` `strict`;
    - `AnalysisLevel` `latest-all`;
    - `EnforceCodeStyleInBuild`;
    - `GenerateDocumentationFile`, so CS1591 holds for every publicly visible member,
      and IDE0005 needs it in the build.

    `Directory.Build.targets` clears the SDK's default `NoWarn`. No project file sets
    any of these properties itself.
  - The root `.editorconfig` raises every analyzer diagnostic to a warning
    (`dotnet_analyzer_diagnostic.severity = warning`). It fixes the options of the style
    rules to the style the code was written in: `var`, file-scoped namespaces,
    `_camelCase` private instance fields, PascalCase constants and static fields,
    parentheses for clarity in mixed logical and relational expressions and none in
    arithmetic. A style option chooses between two forms; it is never chosen to silence
    a rule.
  - No diagnostic is suppressed anywhere:
    - no `#pragma warning` and no `#nullable disable`;
    - no `[SuppressMessage]` or `[UnconditionalSuppressMessage]`;
    - no `NoWarn` and no `WarningsNotAsErrors`;
    - no severity below `warning` in any `.editorconfig` or `.globalconfig`.
  - A rule that conflicts with a framework is resolved in code:
    - test methods are PascalCase (CA1707) and carry XML documentation like any public
      member (CS1591);
    - an awaited call in a test says `ConfigureAwait(true)`, which satisfies both CA2007
      and xUnit1030;
    - public exceptions carry the standard constructors (CA1032);
    - the result structs `MixtureState`, `PerformanceFigures` and `TransportFigures`
      expose properties and value equality (CA1051, CA1815). This breaks the binary
      surface of 0.1.0, so the next release is 0.2.0 (`CHANGELOG.md`).
  - A conflict that code cannot resolve goes to the owner; no node declares an exception
    of its own.

  Checked by every build and by the protocol tests node (`DiagnosticsTests`). Decided
  with the owner on 2026-09-24, who asked for the maximum and rejected the scoped
  exceptions proposed for the test nodes (CA1707, CS1591 and CA2007 in the tests;
  CA1515 in the benchmarks) and for the public exceptions (CA1032). The measurement
  before the change, at `0899500` with the settings above: 738 diagnostics with the
  proposed exceptions, about 1 500 without them.
- Namespaces mirror the directory path from the tree root (AGENTS.md §1). The root
  namespace is `APThermo`; the grouping directories `src/`, `tests/` and `samples/` are
  transparent: `src/Equilibrium` is `APThermo.Equilibrium`, `tests/Equilibrium.Tests` is
  `APThermo.Equilibrium.Tests`, `samples/Samples` is `APThermo.Samples`. Projects, assemblies
  and the solution (`APThermo.sln`) carry the same names. The product's name in prose stays
  Aerospace Propellant Thermodynamics, and APThermo is its short name and the name of its packages.

  ⚠ 2026-09-16: stood "the grouping directories `src/` and `tests/` are transparent". The
  distribution phase added a third grouping directory, `samples/`, for the consumer-scenario
  node; its assembly and root namespace are `APThermo.Samples` (its `BOOT.md`), not
  `APThermo.samples.Samples`. The protocol tests node's namespace attribution
  (`tests/Protocol.Tests/Node.cs`) now treats `samples/` as transparent with the other two, so
  those types resolve to their own node instead of the root.

  ⚠ 2026-09-15 (distribution phase): the root namespace, the projects, the assemblies
  and the solution were `AerospacePropellantThermodynamics`. The user named the
  packages APThermo (NuGet ID `APThermo`, the tool `APThermo.Cli` with the command
  `apthermo`) and asked that everything be renamed before the first release, 0.1.0.
  With no users yet the rename costs nothing, while after publication it would break
  every consumer; a package ID that differs from the namespaces its users write would
  also be a lasting inconsistency. The rename changes names only: bit snapshots,
  benchmark result hashes and the surface snapshot (up to the name) are unchanged.
  Historical records keep the old name, namely the benchmark results under
  `tests/Benchmarks/results/` and the `bench/before-clean-code` branch.
- Kernel-compatible C# in numerical nodes: static methods, blittable structs,
  `ArrayView` inputs and scratch, no allocation, no exceptions, no virtual calls, no
  LINQ, no strings, no recursion. Per-case scratch lives in batch-sized global buffers;
  the case index is the thread index.
- Math in numerical nodes: only the `double` overloads of `System.Math` from this
  list: `Exp`, `Log`, `Log10`, `Pow`, `Sqrt`, `Abs`, `Min`, `Max`, `Floor`, `Ceiling`,
  plus the constant `Math.PI`, which the compiler inlines and which needs no wrapper
  (the transport node's hard-sphere estimate uses it; recorded 2026-09-14 after the
  architecture review found the eleventh name unlisted). Adding a function is a root
  decision, because the execution node must provide its libdevice wrapper.
- ILGPU 1.5.3 is pinned, and its libdevice support is defective with libnvvm 12.9 and
  13.3: it emits the NVVM version metadata before the target lines, libnvvm rejects the
  module, and ILGPU silently drops the wrappers. The execution node links the libdevice
  wrappers itself; nothing else in the tree may know about the mechanism.
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

  ⚠ 2026-09-15 (distribution phase): stood "they are read at run time from that
  directory or from a path given by the caller". A NuGet package and a .NET tool have
  no `data/` directory beside them, so every consumer would have to find NASA files
  before the first call. Embedding the committed files keeps the data-from-files
  invariant, because the bytes are the committed ones with their hash recorded. The
  caller's path stays for other databases. The command line's search for `data/`
  beside the executable and in the current directory goes with it; its `API.md`
  records the change.
- Repository: git, branch `main`, Conventional Commits, MIT license, English in every
  document, identifier, comment and commit message. No binaries other than the NASA
  text data and text fixtures. Nothing secret exists in this repository.
- Reference machine for measurements: RTX 5070 Ti (SM_120), driver 13.4, CUDA
  Toolkits 12.9 and 13.3, 16 logical CPU cores. Recorded, not required.
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

  ⚠ 2026-09-14: the limit on Ce first read 10. It was calibrated on a textual count of
  the names in the source at `8e36a27`, while the rule is defined by the dependency
  check's walk, which also counts the types of the fields a body reads and of the
  members it calls; on that walk the kernel stages of the decomposed `Performance`,
  which carry their data explicitly as the no-hidden-state invariant requires, measured
  12 to 16. The limit is recalibrated on the walk; the protocol tests node's `BOOT.md`
  records the measurement and the source of the figure.

  ⚠ 2026-09-14, evening: the size limits first counted physical lines, the blank and
  comment lines inside a span included, so the documentation comments of a type's
  members counted toward the type's 400 and an explanatory comment toward a method's
  60. A limit that charges for documentation invites deleting it, and the user asked
  that comments not count. The limits now count the lines that hold code; the figures
  stay 400 and 60, which can only lower a measurement, so no type or method that met
  them stops meeting them. The protocol tests node's `BOOT.md` defines the count.

  ⚠ 2026-09-15: the coupling and stable-type sentences read "A type names at most 14"
  and "A type named by 10 or more", as if they held for every type of the tree. The
  limits were calibrated on the types of the `src` nodes, and the protocol tests node's
  shape check, designed with them on 2026-09-14, applies them to those types only, for
  the reason its `BOOT.md` gives. The wording was found wider than the check at the
  review of `ShapeTests`. It now states the scope the check holds, and the acceptance
  criterion below follows it. A stable type's 100 lines are lines of code, counted as
  the size limits count them.

  ⚠ 2026-09-15 (protocol tests node repair phase, R-Protocol.Tests-9): the previous
  correction above scoped the *named* type of the stable-type sentence to the `src`
  nodes ("a type of the `src` nodes") but left the counting side, "named by 10 or more
  types of the tree", unscoped, so a type used ten times only by test-node code, never
  by another `src` type, still read as stable. The protocol tests node's `ShapeTests`
  measures the naming side the same way the check measures the named side: this
  sentence now reads "types of the `src` nodes" on both sides, and the protocol tests
  node's `BOOT.md` records the re-measurement.

  ⚠ 2026-09-15 (protocol tests node repair phase, R-Protocol.Tests-4): "over their
  project graph" read as if the instability were computed from the nodes' `.csproj`
  `ProjectReference` items. `CouplingMeasures.NodeCoupling` reads the nodes' own
  `## Dependencies` sections instead, which the Dependencies level already holds equal
  to the nodes whose types a node's code actually uses; no `.csproj` is opened by the
  check. The two graphs coincide on this tree, so no instability figure or
  dependency-direction verdict moves; the sentence now names the graph the check
  actually reads, matching the protocol tests node's own Shape-check table.

  ⚠ 2026-09-15 (child-nodes phase): the stable-dependencies sentence read "the
  instability of the `src` nodes", which from the child-nodes decision above on counted
  every child node as a component of its own. Splitting `src/Cli` into five children,
  four of which use `Execution` in their own code, took `Execution`'s afferent count from
  2 to 6 and its instability from 0.667 to 0.400, below `Transport`'s 0.500. That turned
  `ShapeTests.No_src_dependency_points_to_a_less_stable_node` red although no dependency
  between the assemblies changed. Stability is a property of what is built and released
  together, and a child node compiles into its ancestor's assembly (the language-and-build
  constraint above). It is not a component, so the sentence now measures the nodes that
  hold a project. The type-level figures (Ce, Ca, the stable type) do not change.

There is no external ancestor: the tree root is the repository root, and the loader
(`CLAUDE.md`) carries no claims about the system (AGENTS.md §2).

## Acceptance criteria

- [x] 2026-09-12 — For LOX/LH2, LOX/RP-1, N2O4/UDMH and AP/HTPB/Al the chamber,
      throat and exit states and the performance figures, in equilibrium and in frozen
      flow, agree with the NASA CEA reference outputs within the tolerance table of the
      fixtures node. The list of reference files is produced by a directory listing,
      and every file in it is covered:
      `Problems.Tests.RocketTests.The_rocket_case_reproduces_the_reference_end_to_end`
      over every file of `tests/Fixtures/cases/rocket` (89 that day: the four
      propellants with and without transport, and the RP-1311 rocket examples) and
      `EquilibriumTests.Assigned_temperature_cases_reproduce_the_reference`,
      `Assigned_enthalpy_cases_reproduce_the_reference`,
      `Assigned_entropy_cases_reproduce_the_reference` over every tp, hp and sp file
      (106). 2026-09-13: 98 rocket and 115 equilibrium files after the
      melting-plateau cases (example 13 and the plateau band), the same tests green.
      The documented defects of the reference (the fixtures node's BOOT.md: the
      reacting conductivity where a trace component is eliminated, the
      frozen-station cv, the singular derivative matrix of a bound-exact tp) are
      skipped by the rules recorded there, and each skip is guarded: the defect must
      be visible on the reference's own output.
- [x] 2026-09-12 — A batch of 100 000 states on CUDA equals the same batch on the CPU
      accelerator within the tolerance table; the list of compared fields is produced
      by reflection over the result type
      (`CudaTests.TheSweepOf100000CasesOnCudaMatchesTheCpuAcceleratorAndIsDeterministic`
      in the execution tests node, long-running; the table's second tier for mole
      fractions is described under the GPU-equals-CPU invariant above). Re-verified
      2026-09-15 on the decomposed code at `62cd99e`, same test, green on the
      reference machine.
- [x] 2026-09-12 — On the reference machine the CUDA path is at least 5× faster than
      the CPU accelerator path with all cores on the 100 000-state batch; the measured
      figure is recorded in the benchmark's approved file
      (`tests/Execution.Tests/Throughput.approved.txt`: 56.28×, CUDA 0.170 s against
      9.544 s; `CudaTests.ThroughputIsRecordedAndNotBelowTheApprovedRatio`).
      Re-verified 2026-09-15 on the decomposed code at `62cd99e`, same test,
      `Throughput.approved.txt` unchanged. Re-measured 2026-09-19 in Release, the
      configuration the release runs, as the median of three runs of the release job's
      filter: 23.58× on Windows (CUDA 0.151 s against 3.557 s) and 27.48× under WSL2
      (0.204 s against 5.593 s), recorded in `Throughput.approved.txt` and
      `Throughput.linux.approved.txt` with their configuration (merged as `a316ecb`).

      ⚠ 2026-09-19: the figures above of 2026-09-12 (56.28×) and the Linux 52.01× were
      Debug measurements, a fact no record stated. The CPU accelerator runs the kernels
      from the assemblies' IL, so the host build configuration changes its speed about
      2.8× (9.5 s in Debug, 3.4 s in Release), while the CUDA kernel does not depend on
      it. The release rehearsal of 2026-09-19 compared a Release run with the Debug
      record and failed at 29.48×. Found by a Fable 5.1 analysis that ruled out the
      toolkit, the driver, the clocks and the code. The 5× target holds by a wide
      margin in both configurations; the tripwire now records and asserts its
      configuration, and its floors are unchanged.
- [x] 2026-09-12 — The full test suite passes in a process where CUDA is forbidden
      (environment variable `APTHERMO_NO_CUDA=1`, honoured by the execution node):
      `dotnet test AerospacePropellantThermodynamics.sln` with the variable set, 1742
      tests green after the protocol tests node (1733 after the Cli node, 1654 after
      the Problems node, 850 after the Execution node; 1749 on 2026-09-13 after the
      front door's mass check and 1951 after its declared tolerance and mass report,
      the `Problems` BOOT.md; 2143 on 2026-09-14 after the melting-plateau rule, the
      `Equilibrium` BOOT.md; 3037 on 2026-09-15 after the clean-code pass, at
      `62cd99e`), none skipped, the CUDA-category tests verifying the refusal instead.
- [x] 2026-09-12 — The tree passes `protocol_lint` without errors (the lint command
      of `CLAUDE.md`, run after every node and by `Protocol.Tests.LintTests` in
      every test run, last after the protocol tests node: 0 errors,
      0 warnings).
- [x] 2026-09-13 — The reflection checks are written for this stack and each was
      shown red once (AGENTS.md §13): the protocol tests node
      (`tests/Protocol.Tests`: `SurfaceTests`, `CoverageTests`, `DeclarationTests`,
      `DependencyTests`, with `LintTests` running the linter and `InvariantTests`
      holding the three root invariants above), ten mutations applied alone and seen
      red, listed in that node's `BOOT.md`; the surface snapshot is
      `tests/Protocol.Tests/PublicSurface.approved.txt`. The first run over the tree
      found one undocumented public type (`Execution`'s `SpeciesFunctionBatchViews`,
      fixed in its `API.md`).
- [x] 2026-09-15 — The tree meets the code-shape constraint above: no type over 400
      lines of code, no method over 60, no control flow nested deeper than 3, no
      method with more than 6 parameters, no `src` type with Ce over 14 outside the
      registries and composition roots the nodes declare, every stable `src` type in
      shape, no dependency against instability; measured by the protocol tests node's
      `ShapeTests`, all ten facts green at `62cd99e`, over a machine-generated list of
      every type and method of every assembly, the declared exceptions read from the
      nodes' `BOOT.md`. The review of 2026-09-14 (nine read-only reviews over the tree
      at `8e36a27`, one per node group and one across the boundaries, counting
      physical lines) found 5 types over 400 lines (`EquilibriumSolver` 1289,
      `TransportSolver` 794, `Problems.Solver` 663, `Engine` 509,
      `Protocol.Tests.Tree` 428), 31 methods over 60 lines (the longest
      `TransportSolver.Evaluate` 640 and `EquilibriumSolver.Solve` 512) and 10 types
      with Ce over 10 by its textual count; the decompositions are designed in the
      nodes' `BOOT.md` files under `## Structure` and each is accepted only with its
      node's bit-for-bit or field-by-field guard green.

- [x] 2026-09-17 — Linux x64 (2026-09-15): the fast suite is green on the CPU accelerator, and
      the execution tests node is green on CUDA, its long-running sweep included, under
      WSL2 on the reference machine. The outcome for the bit snapshots is recorded under
      the platform constraint above.

      Evidence: WSL2 Ubuntu 24.04, .NET SDK 10.0.112, CUDA 12.9 libnvvm, at `0c3b455`
      (merged as `3bc4039`): `APTHERMO_NO_CUDA=1 dotnet test APThermo.sln --filter
      "Category!=LongRunning"` 3098/3098 against the `Bits.linux.approved.txt` files, and
      `dotnet test tests/Execution.Tests` 55/55 on CUDA, the 100 000-case sweep included,
      with the throughput ratio 52.01× recorded in `Throughput.linux.approved.txt` (the
      execution tests node's `BOOT.md` explains the per-platform file). The first run at
      `f67b1a9` failed only the bit snapshots and two platform assumptions of the tests,
      the discovery test's `.dll` suffix and the Windows throughput figure.
- [x] 2026-09-18 — The packages (2026-09-15): packed by the CI from a commit, `APThermo` restores
      from a local feed into every sample, and each sample reproduces its approved
      output on Windows and on Linux. `APThermo.Cli` installs from the same feed as a
      .NET tool and runs an approved example without `--database`. Every source
      document of every packed assembly resolves through SourceLink to the commit's
      file on the public repository. Before the first release the package READMEs link
      the guide on the public repository; until it exists they carry no guide link.

      Evidence: CI run 35274736153 of `24cd966`, green on `windows-latest` and
      `ubuntu-latest`, whose steps pack both packages, restore `APThermo` from the
      job-local feed into all twelve samples and diff each output with its approved
      file, and install the tool from that feed and run its approved example. The
      symbols: `sourcelink test` passed on all 16 PDBs of `APThermo.snupkg` and
      `APThermo.Cli.snupkg` packed from `8d0b7a1`, and the sampled
      `raw.githubusercontent.com/baryon-asymm/APThermo/<commit>/…` URLs answered 200
      (`SCRATCH/sourcelink-proof.txt`, kept out of the tree with the audit reports).
      The package READMEs link the guide since `20cc262`.

      ⚠ 2026-09-18: the criterion said "a debugger steps from a sample into the
      library's source through SourceLink, and the step is recorded". A step in an IDE
      is a human action that no run can repeat, so the record would age into a claim
      nobody re-checks. The wording now names what a debugger actually needs and what
      a machine can re-prove: every document of every packed PDB resolves to the
      commit's file. The owner may still step through it by hand; nothing in the tree
      depends on that.

      ⚠ 2026-09-17: restored after an unreviewed rewrite of 2026-09-16 that dropped the
      package restore into the samples (the ⚠ of that date under `## Delivery`,
      Documentation).
- [x] 2026-09-17 — The documentation (2026-09-15), proven by the docs tests node, with every check
      shown red once and failing on an empty set:
      - every C# block of the guide equals its snippet (`SnippetTests`, and
        `FenceTagTests` for the fences' tags);
      - every `apthermo` invocation shown is run and its output approved, except the
        declared synopses whose output depends on the machine or the release
        (`CommandLineExampleTests.Every_command_line_invocation_is_a_checked_example_or_a_declared_synopsis`);
      - every sample prints its approved output
        (`SampleOutputTests.The_scenario_prints_its_approved_output`, `ScenarioTableTests`);
      - every link resolves (`LinkTests`);
      - every shown or sample document validates against its schema
        (`SchemaValidationTests`, `CliDocumentTests`);
      - every guide page has the shared shape (`GuideShapeTests`).

      Evidence: `tests/Docs.Tests` 28/28 green at `587f05d` on Windows (27/27 at
      `f67b1a9` under WSL2), the red-once and empty-set records in that node's
      `BOOT.md`, and four read-only documentation reviews on 2026-09-17, the last at
      `48fecae` with no blocker and no major; its minors were closed at `587f05d`.

      Corrected 2026-09-17: the list follows the Documentation bullet of `## Delivery`.
      "Every code block … equals its sample region" predated the snippet markers and
      named only four of the six proofs.
- [ ] Diagnostics (2026-09-24): the tree builds at the maximum of the Diagnostics
      constraint with 0 warnings and 0 errors, and nothing suppresses a diagnostic.
      - `DiagnosticsTests` is green, each of its facts shown red once and failing on an
        empty set.
      - The bit snapshots are unchanged, and the fast suite and the protocol lint are
        green.
      - The execution tests node is green on CUDA on the reference machine, its
        long-running sweep included, because the result structs changed shape.
      - The public surface snapshot moves only by the structs' properties and equality
        and the exceptions' standard constructors.

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
batches on the GPU.

- `src/Data` reads the NASA files into an object model. It is the only node that
  knows the file formats, and it is CPU-only, allocation-heavy code, which is why it
  is not merged with the kernel-capable levels below it.
- `src/Thermo` owns the compact species tables the kernels consume and the species
  functions (Cp°/R, H°/RT, S°/R, G°/RT at a temperature). First kernel-capable level.
- `src/Equilibrium` computes the equilibrium composition and its derivatives for one
  case (tp, hp, sp problems): Gibbs minimization, condensed species logic. It is the
  reusable core and is verified against CEA equilibrium tables on its own.
- `src/Performance` adds the rocket model for one case: chamber, throat search, exit
  stations, frozen and shifting flow, c*, Isp, C_F. Separate from `Equilibrium`
  because it has its own source of truth (CEA rocket tables) and its own iterations.
- `src/Transport` computes viscosity, thermal conductivity and Prandtl number for one
  case, frozen and reacting. Separate because it uses a different data file and is an
  optional stage.
- `src/Execution` owns the accelerators, the libdevice post-link, batch buffers and
  the kernel entry points. It is the only node with CUDA knowledge, so every numerical
  node stays testable on the CPU accelerator without knowing CUDA exists.
- `src/Problems` is the front door: reactants, chemical system assembly (elements,
  species selection, element moles and enthalpy per kilogram of propellant), problem
  and result types, orchestration of `Data`, `Thermo`, `Transport` and `Execution`,
  with the flow model, the exit specification and the result figures of
  `Performance`. Propellant conventions are knowledge about CEA and rockets, not
  about solving.

  ⚠ 2026-09-12: it also names `Equilibrium`'s `ProblemKind` in its equilibrium
  problem type, the kind the execution node's batch takes; the dependency list below
  carries the link, which the first version of this list lacked.
- `src/Cli` is a thin adapter: JSON in, JSON or table out. Kept apart so the library
  never depends on console or serialization concerns.

  ⚠ 2026-09-13: it also uses `Execution` (engine options, the accelerator description,
  the unavailable exception, the CUDA flag, and engine creation of its own for the
  `devices` listing, as its `API.md` records under Side effects; the last two added
  here on 2026-09-14 by the architecture review) and reads the result structs of `Thermo`, `Performance`
  and `Transport` and the problem kind of `Equilibrium`, field by field into the
  documents; the dependency list below carries those links, which its first version
  lacked.

  ⚠ 2026-09-15 (distribution phase): "engine creation of its own for the `devices`
  listing" stood after the public surface review (its finding F1, fixed in `9036c6a`)
  made `Execution`'s `Engine` internal: `Cli` receives no grant (`## Delivery` below,
  "Tree contracts") and now calls the new public `AcceleratorProbe.Describe` instead,
  which binds and releases an engine of its own inside `Execution`. `Cli` still uses
  `Execution` for `EngineOptions`, `AcceleratorInfo`, `AcceleratorUnavailableException`
  and `AcceleratorProbe` itself, all on the package surface; `src/Cli/API.md` records
  the change under Side effects.

Dependencies point downward only: `Cli` → {`Problems`, `Data`, `Execution`, `Thermo`,
`Equilibrium`, `Performance`, `Transport`}; `Problems` → {`Data`,
`Thermo`, `Equilibrium`, `Performance`, `Transport`, `Execution`}; `Execution` → {`Thermo`,
`Equilibrium`, `Performance`, `Transport`};
`Performance` → {`Equilibrium`, `Thermo`}; `Transport` → {`Data`, `Thermo`,
`Equilibrium`}; `Equilibrium` → `Thermo`; `Thermo` → `Data`; `Data` → nothing.

Test nodes mirror the source nodes as `tests/<Node>.Tests`; `tests/Protocol.Tests`
holds the reflection checks of AGENTS.md §13; `tests/Fixtures` holds the reference
outputs generated with NASA's `cea` package, their provenance, the generator scripts
and the tolerance table, and `tests/Fixtures.Tests` proves the form and provenance of
those files; `tests/Harness` (2026-09-14) holds the scaffolding the test nodes share
(one CPU host, bit comparison, bit snapshots, fixture families, and since 2026-09-16 the
JSON Schema subset validator and the run-section cut of the command line's documents; its
`API.md` lists them) and names nothing above
`Data` and `Fixtures`; `tests/Benchmarks` (2026-09-15) measures how fast the library
computes, with BenchmarkDotNet, run by hand outside `dotnet test`, its figures recorded
and never asserted; `samples/Samples` (2026-09-15) shows each consumer scenario as a
running program over the package surface, the source of the guide's code, and
`tests/Docs.Tests` (2026-09-15) holds the approved outputs of the samples and
command-line examples and proves the guide against them (`## Delivery`,
Documentation). The node list with links is in `API.md`.

  ⚠ 2026-09-16: stood "holds the guide to the samples, the approved outputs and the
  schemas". Read literally it placed a copy of the schemas in the docs tests node,
  while the Documentation rule below gives them to the command line
  (`src/Cli/Schemas/`, no copy under `docs/`), and the guide itself lives at the root
  and under `docs/`, not in a test node. The node holds the approved outputs and the
  tests; it reads the schemas through `apthermo schema`.

## Delivery

Decided with the user on 2026-09-15 (distribution phase); 0.1.0 is the first release.

- **Packages.**
  - `APThermo` is packed from `src/Problems`, the front door. It carries every library
    assembly in one package (`Data`, `Thermo`, `Equilibrium`, `Performance`, `Transport`,
    `Execution`, `Problems`), because the nodes are never released apart. Its only
    package dependency is ILGPU.
  - `APThermo.Cli` is packed from `src/Cli` as a .NET tool with the command `apthermo`.
  - No other project is packable. The version lives once, in `Directory.Build.props`,
    and a release tag `v<version>` must equal it. The package metadata and the symbol
    settings apply only to packable projects and so live in `Directory.Build.targets`,
    where `IsPackable` is already known (corrected 2026-09-17, CI audit F5; the wording
    named only the props file).
  - The license expression is `MIT AND Apache-2.0` (the NASA data), with `NOTICE`
    packed.
- **Symbols.**
  - Every packed assembly ships its portable PDB in a `.snupkg`, with SourceLink to the
    GitHub commit, from a deterministic CI build.
  - The library assemblies ship their XML documentation; a public member without a
    documentation comment fails the build.
- **Public surface.** Everything public in a packed assembly is a promise to consumers.
  - Before 0.1.0 the surface is reviewed, and whatever no consumer scenario needs
    becomes internal.
  - Below 1.0.0 a minor version may break the surface; `CHANGELOG.md` names the break.
  - The review of 2026-09-15 (`clean-code-reviewer` over `ed5213b`) found 82 public types.
    38 serve a consumer scenario, and 44 exist only for the composition inside the tree:
    kernel descriptors, views, tables, the engine and its batches.
  - Decided that day: the package surface is the consumer scenarios' types only, and no
    ILGPU type appears on it.
- **Tree contracts.** A type another node uses but no consumer needs is `internal` to its
  assembly (decided 2026-09-15).
  - The assembly grants `InternalsVisibleTo` to exactly the assemblies whose nodes
    declare it in their `## Dependencies`, and to the test and benchmark nodes that use
    it. No grant goes against a declared dependency.
  - An assembly whose internal types reach a kernel as parameters or view elements also
    grants `InternalsVisibleTo("ILGPURuntime")`. ILGPU 1.5.3 emits its kernel wrappers
    into a dynamic assembly of that name. It does not require kernel types to be public,
    as three node documents claimed; the review proved it on the CPU accelerator and on
    CUDA.
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
    consumer's batch entry. How `Solver` compares with the engine at 100 000 states is
    measured by the benchmarks node before 0.1.0. A columnar result on the front door
    would be added only if that figure asks for it, and adding it breaks nobody.
- **Documentation.** Two layers, each with one source of truth.
  - The contracts are the nodes' `API.md` and the XML comments.
  - The guide (`README.md`, `docs/guide/`, the package READMEs under `docs/nuget/`) is
    task-oriented and restates no signature.
  - Every C# block of the guide (`README.md`, `docs/guide/`, the package READMEs)
    equals a snippet of the samples node `samples/Samples`.
    - The samples node is a console project in the solution, with one class per
      consumer scenario. Each class checks the statuses it reads and prints its
      figures.
    - A snippet is delimited by `// snippet-start: <name>` and `// snippet-end`
      comments and holds statements a consumer can paste. The `using` lines a scenario
      needs are a snippet of their own.
    - A snippet may be quoted on several pages. `#region` stays forbidden by the
      code-shape constraint.
  - The samples reference the library projects by default. With
    `-p:APThermoPackageVersion=<version>` they restore the `APThermo` package from a
    feed instead, so one source serves both the build and the check of the packed
    package. They use the package surface only, as the command line does.

  ⚠ 2026-09-17: on 2026-09-16 a local model, working without review, rewrote this
  bullet, the continuous-integration bullet and the packages criterion with no
  correction note. After the rewrite:
  - only *marked* C# blocks were checked;
  - a single marker ran to the end of the file;
  - the package-feed mode was dropped as "a post-0.1.0 concern".

  The audit of 2026-09-17 found three consequences:
  - two C# blocks went unchecked, the README's and the package README's;
  - every guide block carried the samples' class boilerplate;
  - nothing ever restored the `APThermo` package.

  The decisions of 2026-09-15 are restored, with two refinements from the audit: a
  snippet may be quoted on several pages, and the `using` lines are a snippet of their
  own.
  - Every `apthermo` invocation shown in the guide takes its input documents from
    `samples/cli/`, and its shown output is approved. The exceptions are the declared
    synopses whose output depends on the machine (`apthermo devices`) or on the release
    (`apthermo --version`). The docs tests node lists them, and the rest of each such
    line must still parse as a valid invocation.

    ⚠ 2026-09-17: stood without the exceptions. The final documentation review found
    `apthermo devices`, `--help` and `--version` exempted only by the docs tests node,
    a deviation the root did not declare (AGENTS.md §12). `devices` prints what the
    machine has, and `--version` changes with every release, so neither has one
    approved output; `--help` does, and is run.
  - The docs tests node `tests/Docs.Tests` proves each of the following. Each check
    fails when the set it walks is empty, and each was shown red once:
    - every C# block equals its snippet;
    - every sample prints its approved output;
    - every `apthermo` invocation of the guide produces its approved output, with the
      run section cut as the command line's tests cut it;
    - every relative link of `README.md`, `llms.txt`, `docs/` and the package READMEs
      resolves;
    - every document under `samples/cli/` validates against its schema;
    - every guide page has the shared shape.
  - The JSON Schemas of the command line's documents belong to the command line
    (2026-09-15). They move from its tests node to `src/Cli/Schemas/`, are embedded in
    the tool (`apthermo schema <name>` prints one), and are validated there by the
    command line's tests. No copy of them lives under `docs/`.
  - `llms.txt` at the root is the entry for agents: a summary, and links to the guide
    pages, the schemas, the samples and the nodes' `API.md`.
  - Guide pages share one shape (purpose, when to use, steps, errors, see also), so a
    human and an agent navigate them alike.
- **Continuous integration.** GitHub Actions under `.github/workflows`, which holds
  configuration and is not a node.
  - Every push and pull request, on Windows and Linux hosted runners: the protocol lint,
    the build, the fast suite with `APTHERMO_NO_CUDA=1` and without the bit snapshots
    (`Category!=BitSnapshot`, the ⚠ of 2026-09-18 under the platform constraint), and
    packing both packages. The release's self-hosted jobs on the reference machine run
    the bit snapshots with the CUDA tests.
    Then the samples run against the fresh `APThermo` package from a local feed, the
    tool installed from that feed runs an approved example, and the docs tests run (the
    ⚠ of 2026-09-17 under Documentation).
  - There is no nightly run (2026-09-17).

  ⚠ 2026-09-17: stood "A nightly run adds the long-running tests on the CPU
  accelerator". Every long-running test of the tree is a CUDA test. Under
  `APTHERMO_NO_CUDA=1` it only checks the refusal and returns, so a nightly run on
  hosted runners added nothing (the CI audit of 2026-09-17, G1). The long-running CUDA
  tests run at every release on the self-hosted runners. The user decided to drop the
  nightly run rather than add a CPU-only long test.
- **Release**, on a tag `v*`, in order:
  1. the hosted matrix;
  2. the CUDA tests, the long-running ones included, and the bit snapshots, on two
     self-hosted runners of the reference machine (Windows, and Linux under WSL2), one
     after the other, since they share one CPU and one GPU and the throughput tripwire
     measures both;
  3. packing;
  4. a push to nuget.org through Trusted Publishing, behind an environment the owner
     approves;
  5. a GitHub release with the notes of `CHANGELOG.md`.
- **Rehearsal before the tag** (2026-09-19). A manual dispatch of the release workflow
  runs steps 1 to 3 on the commit to be released, and never 4 or 5. A tag `v<version>`
  is pushed only on a commit whose dispatch run is green through packing, and the tag
  message names that run. A tag is not moved once pushed; a failure after the tag is
  fixed on a new commit, rehearsed, and released under the next patch version.

  ⚠ 2026-09-19: the first release took four tag pushes (`6924aae`, `f603fd4`,
  `fa4d626`, each moved), each failing on a path that had never run before it: a
  context GitHub rejects in a job-level `env`, a script committed without the
  executable bit, a bit snapshot on a hosted CPU, and a `pwsh` shell absent from both
  self-hosted runners. The dispatch trigger that could have rehearsed all of them
  existed since `c3f5b6b` and was never used; the workflows were verified by reading
  and by a linter, which check syntax, not the host. A post-mortem (Fable 5.1, from the
  runners' own `_diag` logs) found the common cause and set this rule. The tag
  `v0.1.0` is moved one last time, after a green rehearsal, since nothing was ever
  published under it.
- **Self-hosted runners** never run a pull request's code. GPU jobs trigger only on tags
  and on manual dispatch, and the runners run under an account without administrator
  rights, started for a release rather than kept as services.
  - What a runner must provide, checked by a preflight step that names the missing
    item: git, the .NET SDK of `global.json`, an NVIDIA driver (`nvidia-smi`), libnvvm
    and `libdevice.10.bc` where the execution node's discovery looks, and no
    `APTHERMO_NO_CUDA`. Nothing else is assumed: no PowerShell 7, no Python, no Git
    Bash. A step of a self-hosted job names its shell explicitly, `powershell` on
    Windows and `bash` on Linux.
- **Evidence for workflow changes.** A change under `.github/` runs only on GitHub, so it
  is accepted on a run of the path it changes, on the runner class it targets: a CI
  run for `ci.yml`, a dispatch run for `release.yml`. A review or a linter is not
  enough.
- Nothing is pushed to GitHub or nuget.org without the owner's word.
