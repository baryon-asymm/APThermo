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

Outside the tree: .NET SDK 10.0 (C# 14); ILGPU 1.5.3 (NuGet; ILGPU.Algorithms is not
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
  and file names may be platform-specific. Every test runs on both platforms.

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
      (`CudaTests.The_sweep_of_100000_cases_on_cuda_matches_the_cpu_accelerator_and_is_deterministic`
      in the execution tests node, long-running; the table's second tier for mole
      fractions is described under the GPU-equals-CPU invariant above). Re-verified
      2026-09-15 on the decomposed code at `62cd99e`, same test, green on the
      reference machine.
- [x] 2026-09-12 — On the reference machine the CUDA path is at least 5× faster than
      the CPU accelerator path with all cores on the 100 000-state batch; the measured
      figure is recorded in the benchmark's approved file
      (`tests/Execution.Tests/Throughput.approved.txt`: 56.28×, CUDA 0.170 s against
      9.544 s; `CudaTests.Throughput_is_recorded_and_not_below_the_approved_ratio`).
      Re-verified 2026-09-15 on the decomposed code at `62cd99e`, same test,
      `Throughput.approved.txt` unchanged.
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

- [ ] Linux x64 (2026-09-15): the fast suite is green on the CPU accelerator, and
      the execution tests node is green on CUDA, its long-running sweep included, under
      WSL2 on the reference machine. The outcome for the bit snapshots is recorded under
      the platform constraint above.
- [ ] The packages (2026-09-15): packed by the CI from a commit, `APThermo` restores
      from a local feed into every sample, and each sample reproduces its approved
      output on Windows and on Linux. `APThermo.Cli` installs from the same feed as a
      .NET tool and runs an approved example without `--database`. A debugger steps
      from a sample into the library's source through SourceLink, and the step is
      recorded.

      ⚠ 2026-09-17: restored after an unreviewed rewrite of 2026-09-16 that dropped the
      package restore into the samples (the ⚠ of that date under `## Delivery`,
      Documentation).
- [ ] The documentation (2026-09-15), proven by the docs tests node: every code block
      of the guide equals its sample region, and every command-line example's output is
      approved. Every relative link resolves, and every sample document validates
      against its schema.

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
  listing" stood after the public surface review (`SCRATCH/api-review-report.md`, F1)
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
(one CPU host, bit comparison, bit snapshots, fixture families) and names nothing above
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
  - No other project is packable. The version and the package metadata live once, in
    `Directory.Build.props`, and a release tag `v<version>` must equal that version.
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
    `samples/cli/`, and its shown output is approved.
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
    the build, the fast suite with `APTHERMO_NO_CUDA=1`, and packing both packages.
    Then the samples run against the fresh `APThermo` package from a local feed, the
    tool installed from that feed runs an approved example, and the docs tests run (the
    ⚠ of 2026-09-17 under Documentation).
  - A nightly run adds the long-running tests on the CPU accelerator.
- **Release**, on a tag `v*`, in order:
  1. the hosted matrix;
  2. the CUDA tests, the long-running ones included, on two self-hosted runners of the
     reference machine (Windows, and Linux under WSL2);
  3. packing;
  4. a push to nuget.org through Trusted Publishing, behind an environment the owner
     approves;
  5. a GitHub release with the notes of `CHANGELOG.md`.
- **Self-hosted runners** never run a pull request's code. GPU jobs trigger only on tags
  and on manual dispatch, and the runners run under an account without administrator
  rights, started for a release rather than kept as services.
- Nothing is pushed to GitHub or nuget.org without the owner's word.
