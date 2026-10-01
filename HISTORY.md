# HISTORY.md — AerospacePropellantThermodynamics (tree root)

Append-only store of what `BOOT.md` no longer needs to state as current truth
(`AGENTS.md`, §15). Newest entry first. Every entry names the date it was moved, the
`BOOT.md` section it came from, and carries the original text in full, unedited. The
start procedure (`AGENTS.md`, §10) does not read this file; it is reached only by
following a dated pointer left in `BOOT.md` at the place the text used to stand.

---

<a id="ci-field-comparison"></a>

## 2026-10-01 — from "## Delivery", "Continuous integration" — the packed tool's example is compared field by field

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the text as it stood
(a correction of 2026-09-30):

> ⚠ 2026-09-30: the step compared byte for byte. The first run on the hosted Linux
> runner after the equilibrium change of 2026-09-28 failed on the last digits
> (about 1e-13 relative), the same platform difference that moved the bit snapshots.
> The comparison lives in the harness, once (`tests/Harness/BOOT.md`).

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

<a id="rehearsal-first-release"></a>

## 2026-10-01 — from "## Delivery", "Rehearsal before the tag" — the first release took four tag pushes

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the text as it stood
(a correction of 2026-09-19):

> ⚠ 2026-09-19: the first release took four tag pushes (`6924aae`, `f603fd4`,
> `fa4d626`, each moved), each failing on a path that had never run before it: a
> context GitHub rejects in a job-level `env`, a script committed without the
> executable bit, a bit snapshot on a hosted CPU, and a `pwsh` shell absent from both
> self-hosted runners. The dispatch trigger that could have rehearsed all of them
> existed since `c3f5b6b` and was never used; the workflows were verified by reading
> and by a linter, which check syntax, not the host. A post-mortem (Fable 5.1, from the
> runners' own `_diag` logs) found the common cause and set this rule. The tag
> `v0.1.0` is moved one last time, after a green rehearsal, since nothing was ever
> published under it.

---

<a id="no-nightly-run"></a>

## 2026-10-01 — from "## Delivery", "Continuous integration" — the nightly run is dropped

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the text as it stood
(a correction of 2026-09-17):

> ⚠ 2026-09-17: stood "A nightly run adds the long-running tests on the CPU
> accelerator". Every long-running test of the tree is a CUDA test. Under
> `APTHERMO_NO_CUDA=1` it only checks the refusal and returns, so a nightly run on
> hosted runners added nothing (the CI audit of 2026-09-17, G1). The long-running CUDA
> tests run at every release on the self-hosted runners. The user decided to drop the
> nightly run rather than add a CPU-only long test.

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
