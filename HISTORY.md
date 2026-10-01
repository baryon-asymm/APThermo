# HISTORY.md — AerospacePropellantThermodynamics (tree root)

Append-only store of what `BOOT.md` no longer needs to state as current truth
(`AGENTS.md`, §15). Newest entry first. Every entry names the date it was moved, the
`BOOT.md` section it came from, and carries the original text in full, unedited. The
start procedure (`AGENTS.md`, §10) does not read this file; it is reached only by
following a dated pointer left in `BOOT.md` at the place the text used to stand.

---

<a id="platform-deviation-condensed"></a>

## 2026-10-01 — from "## Constraints" — condensed wording

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the text as it stood
before its wording was condensed:

>   ⚠ 2026-09-18, declared deviation from "every test runs on both platforms" (AGENTS.md
>   §12): the first release run failed on a hosted `windows-latest` runner in Release
>   with one rocket case of the front door's snapshot changed in its last bits, every
>   CEA tolerance test green, while earlier hosted runs and 35 local runs passed. The
>   Windows C runtime picks FMA3 or plain variants of `exp`, `log` and `pow` from the
>   CPU, and hosted runners land on different CPUs: disabling the FMA3 variants locally
>   (`_set_FMA3_enable(0)`) moved the bits of 20 of 213 front-door cases, 21 of 464
>   equilibrium cases and 38 of 99 rocket cases. A bit record therefore belongs to a
>   machine. The user chose exact comparison on the reference machine over a
>   field-by-field tolerance on hosted runners. What lifts the deviation: a bit-stable
>   math path (the CPU dispatch pinned in the execution node) or hosted runners of a
>   fixed CPU model.

---

<a id="diagnostics-condensed"></a>

## 2026-10-01 — from "## Constraints" — condensed wording

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the text as it stood
before its wording was condensed:

> - Diagnostics (2026-09-24): the compiler and every analyzer run at their maximum, every
>   diagnostic is an error, and nothing in the tree is exempt, the test, sample and
>   benchmark nodes included.
>   - `Directory.Build.props` sets, for every project:
>     - `TreatWarningsAsErrors`;
>     - `WarningLevel` 9999, every warning wave present and future;
>     - `Features` `strict`;
>     - `AnalysisLevel` `latest-all`;
>     - `EnforceCodeStyleInBuild`;
>     - `GenerateDocumentationFile`, so CS1591 holds for every publicly visible member,
>       and IDE0005 needs it in the build.
>
>     `Directory.Build.targets` clears the SDK's default `NoWarn`. No project file sets
>     any of these properties itself.
>   - The root `.editorconfig` raises every analyzer diagnostic to a warning
>     (`dotnet_analyzer_diagnostic.severity = warning`). It fixes the options of the style
>     rules to the style the code was written in: `var`, file-scoped namespaces,
>     `_camelCase` private instance fields, PascalCase constants and static fields,
>     parentheses for clarity in mixed logical and relational expressions and none in
>     arithmetic. A style option chooses between two forms; it is never chosen to silence
>     a rule.
>   - No diagnostic is suppressed anywhere:
>     - no `#pragma warning` and no `#nullable disable`;
>     - no `[SuppressMessage]` or `[UnconditionalSuppressMessage]`;
>     - no `NoWarn` and no `WarningsNotAsErrors`;
>     - no severity below `warning` in any `.editorconfig` or `.globalconfig`;
>     - no rule set: no `CodeAnalysisRuleSet` property and no `*.ruleset` file (2026-09-28,
>       the third audit pass: a rule set setting CA1822 and CA1812 to `None` built with
>       both violations and passed every check).
>   - A rule that conflicts with a framework is resolved in code:
>     - test methods are PascalCase (CA1707) and carry XML documentation like any public
>       member (CS1591);
>     - an awaited call in a test says `ConfigureAwait(true)`, which satisfies both CA2007
>       and xUnit1030;
>     - public exceptions carry the standard constructors (CA1032);
>     - the result structs `MixtureState`, `PerformanceFigures` and `TransportFigures`
>       expose properties and value equality (CA1051, CA1815). This breaks the binary
>       surface of 0.1.0, so the next release is 0.2.0 (`CHANGELOG.md`).
>   - A conflict that code cannot resolve goes to the owner; no node declares an exception
>     of its own.
>
>   Checked by every build and by the protocol tests node (`DiagnosticsTests`). Decided
>   with the owner on 2026-09-24, who asked for the maximum and rejected the scoped
>   exceptions proposed for the test nodes (CA1707, CS1591 and CA2007 in the tests;
>   CA1515 in the benchmarks) and for the public exceptions (CA1032). The measurement
>   before the change, at `0899500` with the settings above: 738 diagnostics with the
>   proposed exceptions, about 1 500 without them.

---

<a id="math-list-condensed"></a>

## 2026-10-01 — from "## Constraints" — condensed wording

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the text as it stood
before its wording was condensed:

> - Math in numerical nodes: only the `double` overloads of `System.Math` from this
>   list: `Exp`, `Log`, `Log10`, `Pow`, `Sqrt`, `Abs`, `Floor`, `Ceiling`, plus the
>   constant `Math.PI`, which the compiler inlines and which needs no wrapper (the
>   transport node's hard-sphere estimate uses it; recorded 2026-09-14 after the
>   architecture review found the eleventh name unlisted). The minimum and the maximum
>   come from the thermo node's `KernelMath.Min` and `KernelMath.Max`, never from
>   `Math.Min` or `Math.Max` (2026-09-27), nor from `double.Min`, `double.Max` or any
>   other member of `System.Math` or `System.Double` outside this list: `double.Max` is
>   `Math.Max` in CoreLib's IL and compiles to the same `max.f64` (2026-09-28). The
>   protocol tests node checks the list itself, as an allow-list of the calls a numerical
>   node makes into `System.Math` and `System.Double`, `double.IsNaN` and
>   `double.IsNegative` allowed inside `KernelMath` only. Adding a function is a root
>   decision, because the execution node must provide its libdevice wrapper.

---

<a id="ilgpu-hazards-condensed"></a>

## 2026-10-01 — from "## Constraints" — condensed wording

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the text as it stood
before its wording was condensed:

> - ILGPU 1.5.3 is pinned, and its libdevice support is defective for the targets
>   `compute_100` and newer (Blackwell): it emits the NVVM version metadata before the
>   target lines, libnvvm rejects that module for those targets, and ILGPU silently drops
>   the wrappers. For `compute_75` to `compute_90` libnvvm accepts the same module and
>   ILGPU defines the wrappers itself. The execution node checks every kernel and
>   completes the wrappers ILGPU dropped; nothing else in the tree may know about the
>   mechanism.
>
>   A second defect of the same version (2026-09-27): under WSL, ILGPU installs a
>   `DllImport` resolver on every CUDA context it creates, which .NET allows once per
>   process, so the second CUDA engine of a process failed to bind. The execution node
>   registers the devices of every later context itself. Its `BOOT.md` records the rule.
>
>   A third defect of the same version (2026-09-28): ILGPU moves a constant left operand
>   of a floating-point comparison to the right and inverts its NaN ordering while doing
>   so (`IR/Construction/Compare.cs:67-85`, `UpdateFlags` in `IR/Values/Compare.cs:128-140`),
>   so `1.0 < v` compiles to `setp.gtu.f64` and is true for a NaN `v` on CUDA and false
>   on the CPU. The rule for the numerical nodes: an ordered floating-point comparison
>   (`<`, `<=`, `>`, `>=`) either has no literal or constant on its left in the source,
>   or runs after a NaN test of its operands. The protocol tests node checks the source
>   half; the half that appears only after inlining (a local assigned a constant, a
>   constant argument of an inlined method) is the reason `KernelMath` tests both
>   operands for NaN first, and the execution node's probe runs `KernelMath` with the
>   constant in either position. Found by the second hidden-defect audit of 2026-09-28,
>   on the reference device and in ILGPU's source.
>
>   A fourth hazard of the same version (2026-10-01): its transfer overloads that take a
>   `ref T` into host memory do not pin it, and a garbage collection between the pointer
>   and the copy moves the array under the copy. A lost download handed a consumer an `Ok`
>   case with zero figures (`CaseStatus.Ok` is 0), against the failures-are-values
>   invariant, from the first commit to 0.2.0's first tag. The rule: host memory crosses
>   into ILGPU only through an overload that pins it, and a download that wrote nothing is
>   refused. The execution node's `Chunks` child holds both (its `BOOT.md`); the protocol
>   tests node checks that no `src` method calls a by-reference transfer.

---

<a id="decomposition-condensed"></a>

## 2026-10-01 — from "## Decomposition" — condensed wording

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the text as it stood
before its wording was condensed:

> The tree is cut along the data flow, one abstraction level per node, and every
> numerical level is written once in kernel-compatible C# (first invariant): a case is
> one thread's sequential program, so the same code serves single calls on the CPU and
> batches on the GPU.
>
> - `src/Data` reads the NASA files into an object model. It is the only node that
>   knows the file formats, and it is CPU-only, allocation-heavy code, which is why it
>   is not merged with the kernel-capable levels below it.
> - `src/Thermo` owns the compact species tables the kernels consume and the species
>   functions (Cp°/R, H°/RT, S°/R, G°/RT at a temperature). First kernel-capable level.
> - `src/Equilibrium` computes the equilibrium composition and its derivatives for one
>   case (tp, hp, sp problems): Gibbs minimization, condensed species logic. It is the
>   reusable core and is verified against CEA equilibrium tables on its own.
> - `src/Performance` adds the rocket model for one case: chamber, throat search, exit
>   stations, frozen and shifting flow, c*, Isp, C_F. Separate from `Equilibrium`
>   because it has its own source of truth (CEA rocket tables) and its own iterations.
> - `src/Transport` computes viscosity, thermal conductivity and Prandtl number for one
>   case, frozen and reacting. Separate because it uses a different data file and is an
>   optional stage.
> - `src/Execution` owns the accelerators, the libdevice post-link, batch buffers and
>   the kernel entry points. It is the only node with CUDA knowledge, so every numerical
>   node stays testable on the CPU accelerator without knowing CUDA exists.
> - `src/Problems` is the front door: reactants, chemical system assembly (elements,
>   species selection, element moles and enthalpy per kilogram of propellant), problem
>   and result types, orchestration of `Data`, `Thermo`, `Transport` and `Execution`,
>   with the flow model, the exit specification and the result figures of
>   `Performance`. Propellant conventions are knowledge about CEA and rockets, not
>   about solving.
>
> - `src/Cli` is a thin adapter: JSON in, JSON or table out. Kept apart so the library
>   never depends on console or serialization concerns.
>
> Dependencies point downward only: `Cli` → {`Problems`, `Data`, `Execution`, `Thermo`,
> `Equilibrium`, `Performance`, `Transport`}; `Problems` → {`Data`,
> `Thermo`, `Equilibrium`, `Performance`, `Transport`, `Execution`}; `Execution` → {`Thermo`,
> `Equilibrium`, `Performance`, `Transport`};
> `Performance` → {`Equilibrium`, `Thermo`}; `Transport` → {`Data`, `Thermo`,
> `Equilibrium`}; `Equilibrium` → `Thermo`; `Thermo` → `Data`; `Data` → nothing.
>
> Test nodes mirror the source nodes as `tests/<Node>.Tests`; `tests/Protocol.Tests`
> holds the reflection checks of AGENTS.md §13; `tests/Fixtures` holds the reference
> outputs generated with NASA's `cea` package, their provenance, the generator scripts
> and the tolerance table, and `tests/Fixtures.Tests` proves the form and provenance of
> those files; `tests/Harness` (2026-09-14) holds the scaffolding the test nodes share
> (one CPU host, bit comparison, bit snapshots, fixture families, and since 2026-09-16 the
> JSON Schema subset validator and the run-section cut of the command line's documents; its
> `API.md` lists them) and names nothing above
> `Data` and `Fixtures`; `tests/Benchmarks` (2026-09-15) measures how fast the library
> computes, with BenchmarkDotNet, run by hand outside `dotnet test`, its figures recorded
> and never asserted (since 2026-09-25 a library, run through its child node
> `tests/Benchmarks/Runner`, the Diagnostics constraint's CA1515 forbidding public types in
> an executable); `samples/Samples` (2026-09-15) shows each consumer scenario as a
> running program over the package surface, the source of the guide's code, and
> `tests/Docs.Tests` (2026-09-15) holds the approved outputs of the samples and
> command-line examples and proves the guide against them (`## Delivery`,
> Documentation). The node list with links is in `API.md`.

---

<a id="delivery-packages-condensed"></a>

## 2026-10-01 — from "## Delivery, "Packages"" — condensed wording

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the text as it stood
before its wording was condensed:

> - **Packages.**
>   - `APThermo` is packed from `src/Problems`, the front door. It carries every library
>     assembly in one package (`Data`, `Thermo`, `Equilibrium`, `Performance`, `Transport`,
>     `Execution`, `Problems`), because the nodes are never released apart. Its only
>     package dependency is ILGPU.
>   - `APThermo.Cli` is packed from `src/Cli` as a .NET tool with the command `apthermo`.
>   - No other project is packable. The version lives once, in `Directory.Build.props`,
>     and a release tag `v<version>` must equal it. The package metadata and the symbol
>     settings apply only to packable projects and so live in `Directory.Build.targets`,
>     where `IsPackable` is already known (corrected 2026-09-17, CI audit F5; the wording
>     named only the props file).
>   - The license expression is `MIT AND Apache-2.0` (the NASA data), with `NOTICE`
>     packed.

---

<a id="delivery-public-surface-condensed"></a>

## 2026-10-01 — from "## Delivery, "Public surface"" — condensed wording

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the text as it stood
before its wording was condensed:

> - **Public surface.** Everything public in a packed assembly is a promise to consumers.
>   - Before 0.1.0 the surface is reviewed, and whatever no consumer scenario needs
>     becomes internal.
>   - Below 1.0.0 a minor version may break the surface; `CHANGELOG.md` names the break.
>   - The review of 2026-09-15 (`clean-code-reviewer` over `ed5213b`) found 82 public types.
>     38 serve a consumer scenario, and 44 exist only for the composition inside the tree:
>     kernel descriptors, views, tables, the engine and its batches.
>   - Decided that day: the package surface is the consumer scenarios' types only, and no
>     ILGPU type appears on it.

---

<a id="delivery-tree-contracts-condensed"></a>

## 2026-10-01 — from "## Delivery, "Tree contracts"" — condensed wording

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the text as it stood
before its wording was condensed:

> - **Tree contracts.** A type another node uses but no consumer needs is `internal` to its
>   assembly (decided 2026-09-15).
>   - The assembly grants `InternalsVisibleTo` to exactly the assemblies whose nodes
>     declare it in their `## Dependencies`, and to the test and benchmark nodes that use
>     it. No grant goes against a declared dependency.
>   - An assembly whose internal types reach a kernel as parameters or view elements also
>     grants `InternalsVisibleTo("ILGPURuntime")`. ILGPU 1.5.3 emits its kernel wrappers
>     into a dynamic assembly of that name. It does not require kernel types to be public,
>     as three node documents claimed; the review proved it on the CPU accelerator and on
>     CUDA.
>   - A node's `API.md` keeps two parts, marked in the section headings: the package
>     surface, and the tree contract.
>   - A friend assembly may name an internal type of another node only when that node's
>     tree contract declares it; the protocol tests node checks this. A grant exposes
>     every internal, and the check holds the grant to the contract.
>   - The command line is a consumer like any other: it receives no grant and uses the
>     package surface only.
>   - Records the library creates for consumers (results, database records, the
>     accelerator description) have internal constructors. A field added in 0.x then
>     breaks no consumer.
>   - The batch path of the execution node (engine, batches, uploaded tables) leaves the
>     package surface together with the rest of the tree contract. `Solver` stays the
>     consumer's batch entry. How `Solver` compares with the engine at 100 000 states is
>     measured by the benchmarks node before 0.1.0. A columnar result on the front door
>     would be added only if that figure asks for it, and adding it breaks nobody.

---

<a id="delivery-documentation-condensed"></a>

## 2026-10-01 — from "## Delivery, "Documentation"" — condensed wording

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the text as it stood
before its wording was condensed:

> - **Documentation.** Two layers, each with one source of truth.
>   - The contracts are the nodes' `API.md` and the XML comments.
>   - The guide (`README.md`, `docs/guide/`, the package READMEs under `docs/nuget/`) is
>     task-oriented and restates no signature.
>   - Every C# block of the guide (`README.md`, `docs/guide/`, the package READMEs)
>     equals a snippet of the samples node `samples/Samples`.
>     - The samples node is a console project in the solution, with one class per
>       consumer scenario. Each class checks the statuses it reads and prints its
>       figures.
>     - A snippet is delimited by `// snippet-start: <name>` and `// snippet-end`
>       comments and holds statements a consumer can paste. The `using` lines a scenario
>       needs are a snippet of their own.
>     - A snippet may be quoted on several pages. `#region` stays forbidden by the
>       code-shape constraint.
>   - The samples reference the library projects by default. With
>     `-p:APThermoPackageVersion=<version>` they restore the `APThermo` package from a
>     feed instead, so one source serves both the build and the check of the packed
>     package. They use the package surface only, as the command line does.

---

<a id="delivery-docs-proofs-condensed"></a>

## 2026-10-01 — from "## Delivery, "Documentation"" — condensed wording

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the text as it stood
before its wording was condensed:

>   - The docs tests node `tests/Docs.Tests` proves each of the following. Each check
>     fails when the set it walks is empty, and each was shown red once:
>     - every C# block equals its snippet;
>     - every sample prints its approved output;
>     - every `apthermo` invocation of the guide produces its approved output, with the
>       run section cut as the command line's tests cut it. The approved output is a
>       record of the reference machine like the bit snapshots (2026-09-29): a Windows
>       and a Linux file, compared exactly under `Category=BitSnapshot`; on every runner,
>       the hosted ones included, the same document is compared field by field, its
>       numbers within 1e-9 relative;
>
>
>     - every relative link of `README.md`, `llms.txt`, `docs/` and the package READMEs
>       resolves;
>     - every document under `samples/cli/` validates against its schema;
>     - every guide page has the shared shape.

---

<a id="compile-size-measurement"></a>

## 2026-10-01 — from "## Constraints", "Compile size" — the measurement behind the compile size rule

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the text as it stood
(a correction of 2026-09-30):

> ⚠ 2026-09-30: nothing bounded it, and the rocket kernel grew from 3 to 7 call sites of
> `StationSolve.At` in the audit fixes of 2026-09-27 and 2026-09-28. Measured by the
> memory investigation of 2026-09-29 on the CPU accelerator, one `Solver`'s first rocket
> call: 5.7 s and 1.16 GB committed at 0.1.0 (3 sites), 49.7 s and 11.2 GB at `d270bf1`
> (7 sites), 2.6 s and 0.43 GB with the attribute on `StationSolve.At`. The test suite
> followed: 25 GB and 35 minutes for `Cli.Tests`, over 27 GB for `Docs.Tests` and
> `Execution.Tests`, against 3 GB and 4 minutes at 0.1.0, and a WSL instance died of
> the machine's memory. No result bit moves. The owner decided on 2026-09-30 to fix it
> before 0.2.0.

---

<a id="docs-platform-rule"></a>

## 2026-10-01 — from "## Delivery", "Documentation" — the approved command-line output is a record of the reference machine

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the text as it stood
(a correction of 2026-09-29):

> ⚠ 2026-09-29: stood without the platform rule, one approved file compared exactly
> everywhere. It matched Linux and the hosted runners by chance: after the
> equilibrium change of 2026-09-28 the README's rocket example differs under WSL in
> its last digits (c* 2304.5776171446328 against 2304.577617144234, about 1e-13
> relative). The owner chose exact records per platform on the reference machine
> and a field tolerance everywhere, over exact records only (no check on hosted
> runners) or a tolerance only;

---

<a id="math-allow-list"></a>

## 2026-10-01 — from "## Constraints", "Math in numerical nodes" — the check matched only Min and Max

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the text as it stood
(a correction of 2026-09-28):

> ⚠ 2026-09-28: the check of 2026-09-27 matched the declaring type `System.Math` and
> the names `Min` and `Max`. `double.Max` in place of `KernelMath.Max` at the
> convergence test and the station velocity, and `Math.Tanh`, `Math.Cbrt` and
> `Math.Clamp` in the thermo node, built and passed the whole hosted suite; the
> offline PTX of `double.Max` is `max.f64`. Nothing checked the rest of this list.
> Found by the second hidden-defect audit of 2026-09-28 (guards, finding F1).

---

<a id="math-min-max"></a>

## 2026-10-01 — from "## Constraints", "Math in numerical nodes" — Min and Max leave the math list

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the text as it stood
(a correction of 2026-09-27):

> ⚠ 2026-09-27: `Min` and `Max` stood in the list. ILGPU compiles `Math.Min` and
> `Math.Max` to PTX `min.f64` and `max.f64`, which return the other operand when one is
> NaN, while .NET returns NaN. The guards audit's probe inputs (F11) measured it on the
> reference device: `Min(NaN, 1)` and `Max(NaN, 1)` are NaN on the CPU accelerator and
> 1 on CUDA, the only divergence over the whole probed domain. The numerical nodes call
> them in the convergence tests, the damped step, the row scaling of the linear solve
> and the station velocity. There a NaN fails a case on the CPU and passes on the GPU:
> a false `Ok`, or a velocity of 0 in place of NaN. The owner chose functions of the
> tree's own, written once in comparisons and selections, so that both accelerators run
> the same instructions and propagate NaN as .NET does. Documenting the divergence, or
> proving that no NaN reaches any call, were the alternatives.

---

<a id="ilgpu-defect-by-target"></a>

## 2026-10-01 — from "## Constraints", "ILGPU 1.5.3" — the libdevice defect depends on the architecture, not on the libnvvm version

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the text as it stood
(a correction of 2026-09-26):

> ⚠ 2026-09-26: stood "defective with libnvvm 12.9 and 13.3 … The execution node links
> the libdevice wrappers itself". The defect depends on the target architecture, not on
> the libnvvm version. It was measured only on the reference machine's SM_120, where
> ILGPU always drops the wrappers. On every older GPU ILGPU defined them itself, the
> post-link then inserted a second copy, and every CUDA run of 0.1.0 threw on SM_75 to
> SM_90 (the hidden-defect audit of 2026-09-26, `Execution` finding F1). Measured the
> same day with libnvvm 12.9, 13.3 and 13.4 by compiling for every architecture on the
> reference device:
> - ILGPU's order compiles for `compute_75` to `compute_90` with all three, and
>   `compute_60`/`compute_70` with 12.9 only;
> - it fails for `compute_100` and newer with all three;
> - the kernels ILGPU completes itself equal, as PTX text, the kernels the post-link
>   completes, up to ILGPU's generated names and the `.target` line;
> - their probe outputs are the same bits.
>
> The execution node's `BOOT.md` records the design.

---

<a id="declared-synopses"></a>

## 2026-10-01 — from "## Delivery", "Documentation" — the declared synopses of the command line

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the text as it stood
(a correction of 2026-09-17):

> ⚠ 2026-09-17: stood without the exceptions. The final documentation review found
> `apthermo devices`, `--help` and `--version` exempted only by the docs tests node,
> a deviation the root did not declare (AGENTS.md §12). `devices` prints what the
> machine has, and `--version` changes with every release, so neither has one
> approved output; `--help` does, and is run.

---

<a id="documentation-restored"></a>

## 2026-10-01 — from "## Delivery", "Documentation" — the unreviewed rewrite of the Documentation bullet, restored

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the text as it stood
(a correction of 2026-09-17):

> ⚠ 2026-09-17: on 2026-09-16 a local model, working without review, rewrote this
> bullet, the continuous-integration bullet and the packages criterion with no
> correction note. After the rewrite:
> - only *marked* C# blocks were checked;
> - a single marker ran to the end of the file;
> - the package-feed mode was dropped as "a post-0.1.0 concern".
>
> The audit of 2026-09-17 found three consequences:
> - two C# blocks went unchecked, the README's and the package README's;
> - every guide block carried the samples' class boilerplate;
> - nothing ever restored the `APThermo` package.
>
> The decisions of 2026-09-15 are restored, with two refinements from the audit: a
> snippet may be quoted on several pages, and the `using` lines are a snippet of their
> own.

---

<a id="platform-first-linux-run"></a>

## 2026-10-01 — from "## Constraints", "Platform" — the first Linux run answers the open question

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the text as it stood
(a correction of 2026-09-17):

> ⚠ 2026-09-17, the first Linux run (WSL2 Ubuntu 24.04, .NET SDK 10.0.112, at
> `f67b1a9`) answered the open question below: the CPU accelerator does not reproduce
> the Windows bits. Every tolerance test against the CEA references, the docs tests and
> the execution tests on CUDA (the 100 000-case sweep included) passed; the bit
> snapshots of `Equilibrium`, `Performance`, `Transport`, `Problems` and `Cli` did not.
> A field-by-field dump of the equilibrium and rocket snapshot sets (58 208 fields per
> platform) found 12 150 fields differing by at most 4.2e-12 relative (temperature
> 2.7e-13), with no difference in any iteration count, status or active condensed
> species; `Math.Exp`, `Math.Pow` and `Math.Log` differ by exactly 1 ULP on 0.5 %,
> 0.09 % and 0.015 % of sampled arguments. The difference is the C runtimes' last-bit
> rounding carried through the Newton steps, far inside the tolerance tiers of the
> GPU-equals-CPU invariant. The user chose per-platform snapshots over a tolerance
> comparison on Linux or no snapshot on Linux, so that an unintended change stays
> visible to the bit on both platforms.

---

<a id="platform-throughput-record"></a>

## 2026-10-01 — from "## Constraints", "Platform" — the bit snapshots were not the one platform-specific record

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the text as it stood
(a correction of 2026-09-17):

> ⚠ 2026-09-17: the first wording of that sentence, written the same day, called the bit
> snapshots "the one platform-specific record". The Linux run then also failed the
> throughput tripwire (the ratio under WSL2 is lower and varies between runs, 44× and
> 52× measured), and the execution tests node gave it a Linux file too.

---

<a id="docs-tests-schemas"></a>

## 2026-10-01 — from "## Decomposition", "test nodes" — the docs tests node does not hold the schemas

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the text as it stood
(a correction of 2026-09-16):

> ⚠ 2026-09-16: stood "holds the guide to the samples, the approved outputs and the
> schemas". Read literally it placed a copy of the schemas in the docs tests node,
> while the Documentation rule below gives them to the command line
> (`src/Cli/Schemas/`, no copy under `docs/`), and the guide itself lives at the root
> and under `docs/`, not in a test node. The node holds the approved outputs and the
> tests; it reads the schemas through `apthermo schema`.

---

<a id="namespaces-samples"></a>

## 2026-10-01 — from "## Constraints", "Namespaces" — the grouping directory samples is transparent

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the text as it stood
(a correction of 2026-09-16):

> ⚠ 2026-09-16: stood "the grouping directories `src/` and `tests/` are transparent". The
> distribution phase added a third grouping directory, `samples/`, for the consumer-scenario
> node; its assembly and root namespace are `APThermo.Samples` (its `BOOT.md`), not
> `APThermo.samples.Samples`. The protocol tests node's namespace attribution
> (`tests/Protocol.Tests/Node.cs`) now treats `samples/` as transparent with the other two, so
> those types resolve to their own node instead of the root.

---

<a id="cli-accelerator-probe"></a>

## 2026-10-01 — from "## Decomposition", "src/Cli" — Cli calls AcceleratorProbe in place of an engine of its own

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the text as it stood
(a correction of 2026-09-15):

> ⚠ 2026-09-15 (distribution phase): "engine creation of its own for the `devices`
> listing" stood after the public surface review (its finding F1, fixed in `9036c6a`)
> made `Execution`'s `Engine` internal: `Cli` receives no grant (`## Delivery` below,
> "Tree contracts") and now calls the new public `AcceleratorProbe.Describe` instead,
> which binds and releases an engine of its own inside `Execution`. `Cli` still uses
> `Execution` for `EngineOptions`, `AcceleratorInfo`, `AcceleratorUnavailableException`
> and `AcceleratorProbe` itself, all on the package surface; `src/Cli/API.md` records
> the change under Side effects.

---

<a id="code-shape-instability"></a>

## 2026-10-01 — from "## Constraints", "Code shape" — the instability is measured on the nodes that hold a project

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the text as it stood
(a correction of 2026-09-15):

> ⚠ 2026-09-15 (child-nodes phase): the stable-dependencies sentence read "the
> instability of the `src` nodes", which from the child-nodes decision above on counted
> every child node as a component of its own. Splitting `src/Cli` into five children,
> four of which use `Execution` in their own code, took `Execution`'s afferent count from
> 2 to 6 and its instability from 0.667 to 0.400, below `Transport`'s 0.500. That turned
> `ShapeTests.NoSrcDependencyPointsToALessStableNode` red although no dependency
> between the assemblies changed. Stability is a property of what is built and released
> together, and a child node compiles into its ancestor's assembly (the language-and-build
> constraint above). It is not a component, so the sentence now measures the nodes that
> hold a project. The type-level figures (Ce, Ca, the stable type) do not change.

---

<a id="code-shape-dependency-graph"></a>

## 2026-10-01 — from "## Constraints", "Code shape" — the instability is read from the Dependencies sections

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the text as it stood
(a correction of 2026-09-15):

> ⚠ 2026-09-15 (protocol tests node repair phase, R-Protocol.Tests-4): "over their
> project graph" read as if the instability were computed from the nodes' `.csproj`
> `ProjectReference` items. `CouplingMeasures.NodeCoupling` reads the nodes' own
> `## Dependencies` sections instead, which the Dependencies level already holds equal
> to the nodes whose types a node's code actually uses; no `.csproj` is opened by the
> check. The two graphs coincide on this tree, so no instability figure or
> dependency-direction verdict moves; the sentence now names the graph the check
> actually reads, matching the protocol tests node's own Shape-check table.

---

<a id="code-shape-src-both-sides"></a>

## 2026-10-01 — from "## Constraints", "Code shape" — the counting side of the stable type is scoped too

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the text as it stood
(a correction of 2026-09-15):

> ⚠ 2026-09-15 (protocol tests node repair phase, R-Protocol.Tests-9): the previous
> correction above scoped the *named* type of the stable-type sentence to the `src`
> nodes ("a type of the `src` nodes") but left the counting side, "named by 10 or more
> types of the tree", unscoped, so a type used ten times only by test-node code, never
> by another `src` type, still read as stable. The protocol tests node's `ShapeTests`
> measures the naming side the same way the check measures the named side: this
> sentence now reads "types of the `src` nodes" on both sides, and the protocol tests
> node's `BOOT.md` records the re-measurement.

---

<a id="code-shape-src-scope"></a>

## 2026-10-01 — from "## Constraints", "Code shape" — the coupling and stable-type sentences are scoped to the src nodes

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the text as it stood
(a correction of 2026-09-15):

> ⚠ 2026-09-15: the coupling and stable-type sentences read "A type names at most 14"
> and "A type named by 10 or more", as if they held for every type of the tree. The
> limits were calibrated on the types of the `src` nodes, and the protocol tests node's
> shape check, designed with them on 2026-09-14, applies them to those types only, for
> the reason its `BOOT.md` gives. The wording was found wider than the check at the
> review of `ShapeTests`. It now states the scope the check holds, and the acceptance
> criterion below follows it. A stable type's 100 lines are lines of code, counted as
> the size limits count them.

---

<a id="data-embedded"></a>

## 2026-10-01 — from "## Constraints", "Data" — the NASA files are embedded in the data assembly

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the text as it stood
(a correction of 2026-09-15):

> ⚠ 2026-09-15 (distribution phase): stood "they are read at run time from that
> directory or from a path given by the caller". A NuGet package and a .NET tool have
> no `data/` directory beside them, so every consumer would have to find NASA files
> before the first call. Embedding the committed files keeps the data-from-files
> invariant, because the bytes are the committed ones with their hash recorded. The
> caller's path stays for other databases. The command line's search for `data/`
> beside the executable and in the current directory goes with it; its `API.md`
> records the change.

---

<a id="namespaces-rename"></a>

## 2026-10-01 — from "## Constraints", "Namespaces" — the rename to APThermo

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the text as it stood
(a correction of 2026-09-15):

> ⚠ 2026-09-15 (distribution phase): the root namespace, the projects, the assemblies
> and the solution were `AerospacePropellantThermodynamics`. The user named the
> packages APThermo (NuGet ID `APThermo`, the tool `APThermo.Cli` with the command
> `apthermo`) and asked that everything be renamed before the first release, 0.1.0.
> With no users yet the rename costs nothing, while after publication it would break
> every consumer; a package ID that differs from the namespaces its users write would
> also be a lasting inconsistency. The rename changes names only: bit snapshots,
> benchmark result hashes and the surface snapshot (up to the name) are unchanged.
> Historical records keep the old name, namely the benchmark results under
> `tests/Benchmarks/results/` and the `bench/before-clean-code` branch.

---

<a id="assembly-per-node"></a>

## 2026-10-01 — from "## Constraints", "Language and build" — one assembly per node directory

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the text as it stood
(a correction of 2026-09-15):

> ⚠ 2026-09-15: stood "One assembly per node directory". A node is a directory
> (AGENTS.md §1). A cluster of a large node with a contract narrower than its code and
> a reason of its own to change earns its own pair of documents, but an assembly of its
> own would widen the public surface and the project graph for types that are internal
> today. A child node therefore compiles into its nearest ancestor's project, and the
> protocol tests node attributes a type to the deepest node whose namespace it carries,
> as AGENTS.md §1 already defines membership by the directory of a file. Decided with
> the user on 2026-09-14 for the phase after the clean-code pass.

---

<a id="platform-windows-only"></a>

## 2026-10-01 — from "## Constraints", "Platform" — Windows was the only supported platform of version 1

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the text as it stood
(a correction of 2026-09-15):

> ⚠ 2026-09-15 (distribution phase): stood "Windows 11 x64 is the only supported
> platform of version 1. Nothing but the CUDA library discovery paths may be
> Windows-specific." The user decided to ship the library as a NuGet package and the
> command line as a .NET tool for Windows and Linux, with full support on Linux,
> CUDA included. On Linux the GPU path is verified under WSL2 on the reference
> machine. One question stays open until it is measured: whether the CPU accelerator
> reproduces the Windows bit snapshots on Linux. `System.Math` calls the platform's C
> runtime, which may round the last ULP differently. The first Linux run of the suite
> decides it, and that decision is recorded here.

---

<a id="code-shape-lines-of-code"></a>

## 2026-10-01 — from "## Constraints", "Code shape" — the size limits count lines of code

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the text as it stood
(a correction of 2026-09-14):

> ⚠ 2026-09-14, evening: the size limits first counted physical lines, the blank and
> comment lines inside a span included, so the documentation comments of a type's
> members counted toward the type's 400 and an explanatory comment toward a method's
> 60. A limit that charges for documentation invites deleting it, and the user asked
> that comments not count. The limits now count the lines that hold code; the figures
> stay 400 and 60, which can only lower a measurement, so no type or method that met
> them stops meeting them. The protocol tests node's `BOOT.md` defines the count.

---

<a id="code-shape-ce-limit"></a>

## 2026-10-01 — from "## Constraints", "Code shape" — the limit on Ce, 10 recalibrated to 14

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the text as it stood
(a correction of 2026-09-14):

> ⚠ 2026-09-14: the limit on Ce first read 10. It was calibrated on a textual count of
> the names in the source at `8e36a27`, while the rule is defined by the dependency
> check's walk, which also counts the types of the fields a body reads and of the
> members it calls; on that walk the kernel stages of the decomposed `Performance`,
> which carry their data explicitly as the no-hidden-state invariant requires, measured
> 12 to 16. The limit is recalibrated on the walk; the protocol tests node's `BOOT.md`
> records the measurement and the source of the figure.

---

<a id="cli-dependencies"></a>

## 2026-10-01 — from "## Decomposition", "src/Cli" — Cli uses Execution and reads the result structs

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the text as it stood
(a correction of 2026-09-13):

> ⚠ 2026-09-13: it also uses `Execution` (engine options, the accelerator description,
> the unavailable exception, the CUDA flag, and engine creation of its own for the
> `devices` listing, as its `API.md` records under Side effects; the last two added
> here on 2026-09-14 by the architecture review) and reads the result structs of `Thermo`, `Performance`
> and `Transport` and the problem kind of `Equilibrium`, field by field into the
> documents; the dependency list below carries those links, which its first version
> lacked.

---

<a id="problems-problemkind"></a>

## 2026-10-01 — from "## Decomposition", "src/Problems" — Problems names the ProblemKind of Equilibrium

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the text as it stood
(a correction of 2026-09-12):

> ⚠ 2026-09-12: it also names `Equilibrium`'s `ProblemKind` in its equilibrium
> problem type, the kind the execution node's batch takes; the dependency list below
> carries the link, which the first version of this list lacked.

---

<a id="gpu-equals-cpu-second-tier"></a>

## 2026-10-01 — from "## Invariants", "GPU equals CPU" — the second tolerance tier of GPU equals CPU

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the text as it stood
(a correction of 2026-09-12):

> ⚠ 2026-09-12: those numbers hold at stations where both accelerators stop after
> the same number of Newton steps. The equilibrium solver polishes until its
> corrections fall below 1e-11, the rounding floor of its linear solves, and a
> last-ULP difference between libdevice and .NET flips that threshold decision now
> and then (91 of the 400 000 stations of the 100 000-case sweep): one accelerator
> then takes one polish step more, and the two differ by up to 1.4e-11 on
> temperature and 3.3e-10 on the mole fraction of a minor species. The original
> wording assumed the converged iterate were unique to 1e-10, which the stopping
> rule does not guarantee. The tolerance table of the execution tests node states
> the second tier (1e-9 on mole fractions at such stations, derived from the polish
> threshold) and bounds the share of such stations, so that a systematic divergence
> cannot hide behind it.

---
