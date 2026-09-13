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
(`nvvm64_40_0.dll`) and `libdevice.10.bc` from a CUDA Toolkit 12.8 or newer (13.x
keeps the DLL under `nvvm/bin/x64`); NASA CEA data `thermo.inp` and `trans.inp` from
github.com/nasa/cea (Apache-2.0); the `cea` Python package 3.3.4 (NASA CEA,
Apache-2.0) as the generator of the reference outputs; Python 3.8+ for the protocol
linter; xunit for tests.

## Constraints

- Platform: Windows 11 x64 is the only supported platform of version 1. Nothing but
  the CUDA library discovery paths may be Windows-specific.
- Language and build: C#, .NET 10, nullable reference types enabled, warnings are
  errors. One assembly per node directory, named after its namespace. One solution
  file at the repository root.
- Namespaces mirror the directory path from the tree root (AGENTS.md §1). The root
  namespace is `AerospacePropellantThermodynamics`; the grouping directories `src/`
  and `tests/` are transparent: `src/Equilibrium` is
  `AerospacePropellantThermodynamics.Equilibrium`, `tests/Equilibrium.Tests` is
  `AerospacePropellantThermodynamics.Equilibrium.Tests`.
- Kernel-compatible C# in numerical nodes: static methods, blittable structs,
  `ArrayView` inputs and scratch, no allocation, no exceptions, no virtual calls, no
  LINQ, no strings, no recursion. Per-case scratch lives in batch-sized global buffers;
  the case index is the thread index.
- Math in numerical nodes: only the `double` overloads of `System.Math` from this
  list: `Exp`, `Log`, `Log10`, `Pow`, `Sqrt`, `Abs`, `Min`, `Max`, `Floor`, `Ceiling`.
  Adding a function is a root decision, because the execution node must provide its
  libdevice wrapper.
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
  (Apache-2.0) and the upstream commit hash; they are read at run time from that
  directory or from a path given by the caller.
- Repository: git, branch `main`, Conventional Commits, MIT license, English in every
  document, identifier, comment and commit message. No binaries other than the NASA
  text data and text fixtures. Nothing secret exists in this repository.
- Reference machine for measurements: RTX 5070 Ti (SM_120), driver 13.4, CUDA
  Toolkits 12.9 and 13.3, 16 logical CPU cores. Recorded, not required.

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
      (106). The two documented defects of the reference (the fixtures node's BOOT.md:
      the reacting conductivity where a trace component is eliminated, the
      frozen-station cv) are skipped by the rule recorded there, and the skip is
      guarded: the defect must be visible on the reference's own composition.
- [x] 2026-09-12 — A batch of 100 000 states on CUDA equals the same batch on the CPU
      accelerator within the tolerance table; the list of compared fields is produced
      by reflection over the result type
      (`CudaTests.The_sweep_of_100000_cases_on_cuda_matches_the_cpu_accelerator_and_is_deterministic`
      in the execution tests node, long-running; the table's second tier for mole
      fractions is described under the GPU-equals-CPU invariant above).
- [x] 2026-09-12 — On the reference machine the CUDA path is at least 5× faster than
      the CPU accelerator path with all cores on the 100 000-state batch; the measured
      figure is recorded in the benchmark's approved file
      (`tests/Execution.Tests/Throughput.approved.txt`: 56.28×, CUDA 0.170 s against
      9.544 s; `CudaTests.Throughput_is_recorded_and_not_below_the_approved_ratio`).
- [x] 2026-09-12 — The full test suite passes in a process where CUDA is forbidden
      (environment variable `APTHERMO_NO_CUDA=1`, honoured by the execution node):
      `dotnet test AerospacePropellantThermodynamics.sln` with the variable set, 1742
      tests green after the protocol tests node (1733 after the Cli node, 1654 after
      the Problems node, 850 after the Execution node), none skipped, the
      CUDA-category tests verifying the refusal instead.
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
  with the result types of `Performance`. Propellant conventions are knowledge about
  CEA and rockets, not about solving.

  ⚠ 2026-09-12: it also names `Equilibrium`'s `ProblemKind` in its equilibrium
  problem type, the kind the execution node's batch takes; the dependency list below
  carries the link, which the first version of this list lacked.
- `src/Cli` is a thin adapter: JSON in, JSON or table out. Kept apart so the library
  never depends on console or serialization concerns.

  ⚠ 2026-09-13: it also uses `Execution` (engine options, the accelerator description,
  the unavailable exception) and reads the result structs of `Thermo`, `Performance`
  and `Transport` and the problem kind of `Equilibrium`, field by field into the
  documents; the dependency list below carries those links, which its first version
  lacked.

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
those files. The node list with links is in `API.md`.
