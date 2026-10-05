# HISTORY.md — Benchmarks

Append-only store of what `BOOT.md` no longer needs to state as current truth
(`AGENTS.md`, §15). Newest entry first. Every entry names the date it was moved, the
`BOOT.md` section it came from, and carries the original text in full, unedited. The
start procedure (`AGENTS.md`, §10) does not read this file; it is reached only by
following a dated pointer left in `BOOT.md` (or `ACCEPTANCE.md`) at the place the text
used to stand.

---

<a id="cuda-tolerance-own-math-2026-10-05"></a>

## 2026-10-05 — from "## Constraints" — the reason for the CUDA tolerance of the before and after comparison

Item 13 of release 0.2.2 replaced `System.Math`'s and libdevice's `exp`, `log` and `pow` by the tree's own correctly rounded ones; CUDA and the CPU accelerator return the same bits since. The comparison class keeps its relative tolerances for the consumer path. The text as it stood:

> NVVM compiles the
> restructured kernels the clean-code pass produced to different last-ULP
> arithmetic than it compiled the kernels before the pass — the same
> libdevice-against-.NET last-ULP effect the root `BOOT.md`'s GPU-equals-CPU
> invariant already documents for a single run.

---

<a id="console-to-library"></a>

## 2026-10-01 — from "## Purpose" — the node stood a console project

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15): a correction is provenance, and the pointer left in its place names both wordings. The text as it stood:

> ⚠ 2026-09-25: stood "a BenchmarkDotNet console project". The root's Diagnostics
> constraint (CA1515) forbids public types in an executable, and BenchmarkDotNet requires
> public benchmark classes (the Constraints ⚠ of the same date). CA1515 does not apply to a
> library, so the owner split the node: the benchmark classes, the job configuration and
> the shared inputs stay here in a library, and the console entry point moves to the child
> node `Runner`.

---

<a id="cuda-hash-tier"></a>

## 2026-10-01 — from "## Invariants" — the CUDA hashes first required equal

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15): a correction is provenance, and the pointer left in its place names both wordings. The text as it stood:

>   ⚠ 2026-09-15: this invariant first required the CUDA hashes equal too, the same as
>   the CPU accelerator's. The first dry run of this node (`## Acceptance criteria`,
>   the before-branch criterion) found `BatchThroughputBenchmarks` (group 1) and
>   `UserStatesBenchmarks` (group 6) bit-for-bit equal between `7661ea9` and this
>   branch on the CPU accelerator and bit-for-bit *different* on CUDA, for identical
>   inputs and identical benchmark logic (`ProblemKindBenchmarks`, group 2, is
>   CPU-only and matched throughout, so the rocket-path adaptation itself was not the
>   cause). Measuring the size of the CUDA difference directly — a raw dump of every
>   result field and every mole, before and after, at every station of groups 1
>   (1 000 and 10 000 cases; 100 000 was not dumped, since a conclusion already four
>   orders of magnitude inside the tolerance would not change) and 6 (all 48 states)
>   — found the statuses equal, the iteration counts equal at every station (0 of
>   4 000, 0 of 40 000 and 0 of 48 differ), and the largest relative difference per
>   field at machine epsilon: 4.863331e-16 on `CvEquilibrium` and 4.160798e-16 on
>   `CpEquilibrium` (group 1, both case counts, the same fixture case replicated),
>   2.184873e-16 on `Entropy` (group 6); every other field and every compared mole
>   fraction exactly 0.0. None of these approach the table's loosest tier (1e-9). The
>   libnvvm options and the libdevice post-link inputs of the two trees are identical:
>   the same single `-arch=<arch>` compiler option (`NvvmOptions` in both trees'
>   `src/Execution/LibDevicePostLink.cs`), the same `arch` derived from the same
>   `^\.target\s+sm_(\d+)` match against the kernel's own PTX, the same module and
>   libdevice bytes handed to `CompileProgram` — the two trees' post-link code differs
>   only in shape (the after-tree decomposes the inline `Link` method of `7661ea9`
>   into `NvvmOptions`, `TargetArch`, `WrapperBody`, `CompileWrappers`,
>   `InsertAfterHeader`, `AssertEveryWrapperDefined` and `TrialLoad`), never in the
>   compiler input. The invariant now reads as above.

---

<a id="deps-expectation"></a>

## 2026-10-01 — from "## Dependencies" — the list was the design's expectation

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15): a correction is provenance, and the pointer left in its place names both wordings. The text as it stood:

> ⚠ 2026-09-15, design: this list is the design's expectation. The coder trims or extends
> it to exactly the nodes the code names, which the dependency check holds. Any difference
> is recorded here with the reason.

---

<a id="deps-equilibrium"></a>

## 2026-10-01 — from "## Dependencies" — Equilibrium joined the list

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15): a correction is provenance, and the pointer left in its place names both wordings. The text as it stood:

> ⚠ 2026-09-15, coding: the code also names `Equilibrium.ProblemKind`, added to the list
> above. `Problems.EquilibriumProblem.Kind` and `Execution.EquilibriumBatch.Kind` are of
> that type, and `SingleCaseBenchmarks` and `OneTimeCostBenchmarks` name it directly to
> build an `EquilibriumProblem` and a trivial `EquilibriumBatch`; the design's list missed
> it the way `Problems`' own first version once did (its `BOOT.md`, the 2026-09-12 note
> on `ProblemKind`). No node of the design's list turned out unused.

---

<a id="deps-transport"></a>

## 2026-10-01 — from "## Dependencies" — Transport joined the list

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15): a correction is provenance, and the pointer left in its place names both wordings. The text as it stood:

> ⚠ 2026-09-15, coding: the dependency check's walk also named `Transport.TransportTable`,
> so [Transport](../../src/Transport/API.md) joins the list. `BatchThroughputBenchmarks`
> and `OneTimeCostBenchmarks` never pass a transport table to `Engine.Upload` — group 1
> and group 4 measure the equilibrium and rocket paths only, transport is out of scope
> for both — but `Upload`'s second parameter is `TransportTable? transport = null`, and
> the walk reads a called member's whole signature, optional parameters included, not
> only the arguments a call site supplies.

---

<a id="ca1515-proof"></a>

## 2026-10-01 — from "## Constraints" — CA1515 proven by a dry run

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15): a correction is provenance, and the pointer left in its place names both wordings. The text as it stood:

>     ⚠ 2026-09-25 (Diagnostics constraint, root BOOT.md): confirmed empirically rather
>     than asserted. Making one benchmark class (`SingleCaseBenchmarks`) `internal` for
>     CA1515 and running `dotnet run -c Release --filter '*SingleCaseBenchmarks*' --job
>     Dry` failed validation with `Benchmarked method 'Solve' is within a non-visible
>     class, all declaring types must be public`, from the `InProcessNoEmitToolchain`
>     this node's job configuration uses (`## Constraints`, Configuration); reverted, and
>     the same dry run then completed. CA1515 on the six benchmark classes
>     (`BatchThroughputBenchmarks`, `OneTimeCostBenchmarks`, `ProblemKindBenchmarks`,
>     `SingleCaseBenchmarks`, `SolverBatchBenchmarks`, `UserStatesBenchmarks`) and the two
>     enums a public benchmark class exposes through a `[Params]`/`[ParamsAllValues]`
>     property (`BenchmarkProblemKind`, `UserStateSelection` — a property's type cannot be
>     less accessible than the property itself) is therefore an unresolvable-in-code
>     conflict, reported rather than suppressed (root BOOT.md, Diagnostics constraint: "a
>     conflict that code cannot resolve goes to the owner").

---

<a id="job-iterations"></a>

## 2026-10-01 — from "## Constraints" — the job's first iteration counts

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15): a correction is provenance, and the pointer left in its place names both wordings. The text as it stood:

>   ⚠ 2026-09-15: the job first read 1 warmup iteration and 3 measured iterations. The
>   first timed comparison run (after, before, after) found BenchmarkDotNet's 99.9 %
>   half-interval as wide as the mean or wider for most benchmarks on three samples
>   (`ProblemKind` hp without transport, 0.358 ± 0.507 ms; tp, 1.321 ± 1.300 ms), and the
>   two after runs drifted apart (100 000 CPU-accelerator cases: 5346.96 ± 154.96 ms,
>   then 3461.43 ± 457.66 ms) — nothing could be compared on that. Found by the first
>   comparison run, 2026-09-15.

---
