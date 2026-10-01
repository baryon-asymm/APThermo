# HISTORY.md — Execution

Append-only store of what `BOOT.md` no longer needs to state as current truth
(`AGENTS.md`, §15). Newest entry first. Every entry names the date it was moved, the
`BOOT.md` section it came from, and carries the original text in full, unedited. The
start procedure (`AGENTS.md`, §10) does not read this file; it is reached only by
following a dated pointer left in `BOOT.md` (or `ACCEPTANCE.md`) at the place the text
used to stand.

---

<a id="structure-intro-guarantee-2026-10-01"></a>

## 2026-10-01 — from "## Structure" — the Structure introduction: the no-move guarantee of the split

Moved because the last sentence is the guarantee of the 2026-09-14 split, a measurement of that step rather than a rule (CONDENSE.md, rule 2). The text as it stood:

> Decided 2026-09-14 (the clean-code pass; the root's code-shape constraint). The engine
> is a composition root over internal types, one class per file in this directory and
> namespace. The kernel entry points and the calls into the numerical nodes are untouched
> by the split, so the emitted PTX, the post-link and the kernel time cannot move.

---

<a id="audit-observations-2026-10-01"></a>

## 2026-10-01 — from "## Constraints" — the audit's observations, as one paragraph

Moved because `BOOT.md` is over the §15 line limit for its kind of node; the rule stays in `BOOT.md` in shorter wording or at the pointer (CONDENSE.md, 2026-10-01). The text as it stood:

>   - **Observations.**
>     - A driver or libnvvm log is trimmed of NUL padding as well as white space (the
>       trial load's message carried 45 NULs).
>     - `Engine.Upload` disposes the buffers it already uploaded when a later upload
>       fails.
>     - A half-given explicit path pair (`LibNvvmPath` without `LibDevicePath`, or the
>       reverse) is an `ArgumentException` at `Create` naming the missing option. It was
>       tried as `("", path)` and then replaced by discovery without a word.

---

<a id="batch-layout-chunking-moved-2026-10-01"></a>

## 2026-10-01 — from "## Constraints" — batch layout: the chunking moves to Chunks

Moved because the chunk bound and the chunk buffers bind only the `Chunks` child node; the four lines and the pointer that follows them went down verbatim into its `BOOT.md` (CONDENSE.md, rule 3). The text as it stood:

> - **Batch layout**: structure of arrays for inputs and outputs; the case index is the
>   thread index; per-case scratch is a slice of a batch-sized buffer laid out by the
>   numerical nodes' `ScratchLayout` and `TransportLayout`; batches are processed in
>   chunks of at most `ChunkSize` cases (default 16 384), and fewer when a chunk's
>   scratch would exceed `ScratchBytes` (default 256 MB), so that memory stays bounded;
>   the device buffers of a chunk are allocated once per run and reused; results are
>   copied back per chunk.
>
>   ⚠ 2026-09-12: was a chunk bounded by the case count only, now also by `ScratchBytes`,
>   the rocket chunk's moles included → HISTORY.md#batch-layout-scratch-bound-2026-09-12

---

<a id="inv-no-result-ignored-2026-10-01"></a>

## 2026-10-01 — from "## Invariants" — invariant: no libnvvm or driver result is ignored

Moved because `BOOT.md` is over the §15 line limit for its kind of node; the rule stays in `BOOT.md` in shorter wording or at the pointer (CONDENSE.md, 2026-10-01). The text as it stood:

> - **No libnvvm or driver result is ignored** (2026-09-26). The post-link checks the
>   result of every call it makes into libnvvm (`GetIRVersion`, `CreateProgram`,
>   `AddModuleToProgram`, `LazyAddModuleToProgram`, `CompileProgram`, `GetProgramLog`,
>   `GetCompiledResult`, `DestroyProgram`) and into the CUDA driver (`LoadModule`,
>   `DestroyModule`). A result other than success is an `InvalidOperationException`
>   that names the post-link, the target `compute_XX`, which library failed (libnvvm or
>   the CUDA driver) and its call, and the result code, carrying the compiler's or the
>   driver's log where one exists (`API.md`, Errors).
>   - The log of a failed compilation is read after the failure. If reading the log
>     fails too, the compilation's exception still propagates and says the log could
>     not be read, naming that result.
>   - Releasing a program or a module (`DestroyProgram`, `DestroyModule`) is checked only
>     when the path before it succeeded. When an earlier call has already failed, the
>     earlier exception propagates unchanged and the release is best-effort, so that a
>     cleanup failure never hides the cause.
>   - One internal method turns a result into the exception, so the message has one
>     shape. It is unit-tested on the CPU with every non-success value of `NvvmResult`
>     and a failing `CudaError`. The success path is proven by the CUDA tests of this
>     node, which must stay green with no bit or throughput record moving.

---

<a id="inv-cuda-bound-2026-10-01"></a>

## 2026-10-01 — from "## Invariants" — invariant: CUDA is bound only when a kernel runs on it

Moved because `BOOT.md` is over the §15 line limit for its kind of node; the rule stays in `BOOT.md` in shorter wording or at the pointer (CONDENSE.md, 2026-10-01). The text as it stood:

> - **CUDA is bound only when a kernel runs on it** (2026-09-26). The choice accepts a
>   CUDA session only after the math probe kernel, which calls every wrapper of the math
>   list, has been compiled, post-linked and loaded on its device. `Engine.Create` and
>   `AcceleratorProbe.Describe` therefore never report a CUDA device on which no kernel
>   can load.

---

<a id="inv-post-link-2026-10-01"></a>

## 2026-10-01 — from "## Invariants" — invariant: every CUDA kernel goes through the post-link

Moved because `BOOT.md` is over the §15 line limit for its kind of node; the rule stays in `BOOT.md` in shorter wording or at the pointer (CONDENSE.md, 2026-10-01). The text as it stood:

> - **Every CUDA kernel goes through the post-link, which completes it.** ILGPU 1.5.3
>   defines the libdevice wrappers itself for the targets `compute_75` to `compute_90`
>   and silently drops them for `compute_100` and newer (the root's ILGPU constraint).
>   The post-link reads which wrappers the kernel calls and which it already defines. It
>   compiles and inserts only the missing ones, and inserts nothing when none is missing.
>   Every kernel that calls a wrapper is loaded once as a trial on either path. A kernel
>   that still calls an undefined wrapper is refused at load, and the error names the
>   wrapper. Either path yields the same program: the kernels ILGPU completes equal the
>   kernels the post-link completes, as PTX text, up to ILGPU's generated names and the
>   `.target` line (the architecture fact, Acceptance criteria).

---

<a id="structure-size-retelling-2026-10-01"></a>

## 2026-10-01 — from "## Structure" — the size bullet restating the root's code-shape constraint

Moved because it restates the root's code-shape constraint, which binds this node unchanged (CONDENSE.md, rule 4); the views structs it set aside are the rows of `## Shape exceptions`. The text as it stood:

> - **Size.** No method over 60 lines, no control flow nested deeper than 3, no more than
>   6 parameters (the views structs aside).

---

<a id="structure-rows-condensed-2026-10-01"></a>

## 2026-10-01 — from "## Structure" — the rows of Engine, KernelCache, MathProbe, the four pipelines and Kernels, merged

Moved because the rows are merged into two (`API.md` holds the members of `Engine`, `KernelCache` and `MathProbe`, and `## Shape exceptions` holds the coupling figures and the reasons) and their measurements and dated notes leave the current wording (CONDENSE.md, rules 2, 4 and 5). The rows as they stood:

> | `Engine` | the composition root: `Create` delegating to the choice, `Upload`, the four `Run` overloads delegating to their pipelines, `ProbeMath` (the one run without a pipeline: allocates, launches and reads back the probe over the session's accelerator), `Budget` and `RunBatchLoop` (2026-09-28, F2: the session's time budget and the chunk loop, exposed for the tests node's own chunk-plan facts), `Launchers` (2026-09-30: the kernel cache, exposed for the tests node's facts on release at dispose), `Dispose` (which empties the kernel cache before it disposes the session); no loop, no arithmetic, no ILGPU call except through the session. Named here as the composition root the root's Ce rule allows above its limit: four typed `Run` overloads name twelve types by themselves (Ce 30 by the dependency check's walk on 2026-09-28, 25 on 2026-09-15) | internal (2026-09-15, distribution phase; F1, `API.md`'s ⚠), contract as `API.md`'s tree-contract section says |
> | `KernelCache` | typed kernel launchers, compiled and post-linked on first use, one per entry-point name; reports the warm-up time; `Count` and `Clear` (2026-09-30): `Clear` drops every launcher and clears the ILGPU context's caches, which is how a disposed engine releases the compiled programs it kept | internal |
> | `MathProbe` | the probe of the root's math list, in a file of its own; `StrideCount` is the internal constant the kernel strides by, tied to `FunctionCount` by a test, and the function list is asserted to have that length | internal (2026-09-15, distribution phase), contract unchanged |
>
> | `EquilibriumPipeline`, `RocketPipeline`, `TransportPipeline`, `SpeciesFunctionPipeline` | one per program: declare its host arrays, device buffers and views struct, assemble its result; no formula. Named here as the composition roots of their programs' runs, which the root's Ce rule allows above its limit: each names its program's batch, result and views types and the tables' buffers and views besides the run's machinery (the session, the plan, the chunk buffers, the loop, the timer, the kernel cache, and since 2026-09-28 `LaunchBudget`, threaded from `session.Budget` into `ChunkPlan.For`, F2). Three of the four also gained an internal `DeclareBuffers` test-support method for the F8 wiring fact, naming no new type; since 2026-09-29 (the third audit pass's observation) `DeclareBuffers` and `Run` both call one private `Declare` method instead of restating the buffer declarations, so the two cannot drift; `Run`'s own `using var buffers` disposes nothing for real once `BatchRun.Execute` has already run (ILGPU's own dispose is idempotent), and exists only because CA2000 needs a literal dispose beside the allocation. By the dependency check's walk on 2026-09-28 (2026-09-14 in parentheses): `RocketPipeline` 25 (23), `TransportPipeline` 23 (22), `EquilibriumPipeline` 22 (21), `SpeciesFunctionPipeline` 18 (17); unchanged by the 2026-09-29 refactor (`Declare` and `DisposeChunkBuffers` name no type these pipelines did not already name) | internal |
> | `Kernels` | the registry of entry points: each slices the views of its case and calls the numerical node; no formula. Named here as the registry the root's Ce rule allows above its limit (Ce 26 by the dependency check's walk on 2026-09-27, 25 on 2026-09-14, 22 by the review's textual count the same day: one views struct, one layout class and one solver per program, which no split removes) | internal |

---

<a id="audit-f2-budget-condensed-2026-10-01"></a>

## 2026-10-01 — from "## Constraints" — the launch time budget: its mechanics move to Chunks, the limit's statement is API.md's

Moved because the mechanics bind only the `Chunks` child node and went down verbatim into its `BOOT.md` (CONDENSE.md, rule 3), and the statement of the limit that no chunking can lift is `API.md`'s (rule 4). The text as it stood:

>   - **A launch has a time budget (F2).** A GPU that drives a display runs every
>     kernel under the driver's run-time limit, 2 s by Windows' default, also under WSL2,
>     whose GPU access goes through the same driver. One case is one thread's sequential
>     program, and its time grows with the system.
>     - The Chunks node holds an internal `LaunchBudget`, built at bind time from the
>       device's kernel run-time-limit attribute. A device without the limit, and the
>       CPU accelerator, have no budget. A device with it gets a quarter of the 2 s
>       default.
>     - `ChunkPlan` takes the budget as a fourth bound, beside the count, the bytes and
>       the offsets. The first chunk of a run is one wave of the device (its
>       multiprocessors times the threads each holds at once). Each later chunk is sized
>       from the previous chunk's measured time per case.
>     - Results do not depend on the chunking. The audit's `E13` found the same bits at
>       chunk sizes 16 384, 1, 7 and 64, so no result bit moves.
>     - A launch the driver kills for its run time (`CUDA_ERROR_LAUNCH_TIMEOUT`) is
>       translated by `BatchRun` into `AcceleratorUnavailableException`. The message names
>       the run-time limit, the chunk's case count and the CPU accelerator as the remedy.
>       No ILGPU type reaches a consumer, and `API.md`'s errors table gains the row.
>     - `API.md` and the guide state the limit no chunking can lift. One case of a
>       system of about 16 or more elements takes longer than the default limit on the
>       reference GPU, and such systems belong on the CPU accelerator or on a device
>       without the limit (TCC mode, headless).

---

<a id="kernels-transport-batch-retelling-2026-10-01"></a>

## 2026-10-01 — from "## Constraints" — the Kernels bullet: how the transport batch is built is API.md's

Moved because how the transport batch is built from a finished result is the batch type's contract in `API.md`, Batches (CONDENSE.md, rule 4); the bullet keeps the entry points and the pointer of 2026-09-12 follows it unchanged in meaning. The text as it stood:

> - **Kernels**: one entry point per program (`Equilibrium`, `Rocket`, `Transport`,
>   and `Functions` for the species functions of `Thermo` at given temperatures) and
>   the `Probe` of the root's math list; each entry point does nothing but slice the
>   views for its case and call the numerical node. The transport kernel takes a plain
>   batch of stations (a temperature and a composition each); the batch is built from a
>   finished rocket or equilibrium result by factories of the batch type, not by the
>   engine, which does not know where a composition came from.
>
>   ⚠ 2026-09-12: was no species-function batch, now the `Functions` entry point for the
>   front door's reactant enthalpies →
>   HISTORY.md#kernels-species-function-batch-2026-09-12

---

<a id="choice-probe-cost-2026-10-01"></a>

## 2026-10-01 — from "## Constraints" — the accelerator choice: the probe's cost and the sub-bullets as one sentence

Moved because `BOOT.md` is over the §15 line limit for its kind of node; the rule stays in `BOOT.md` in shorter wording or at the pointer (CONDENSE.md, 2026-10-01). The text as it stood:

> - **Accelerator choice** (`AcceleratorKind.Auto`): CUDA if `APTHERMO_NO_CUDA` is not
>   `1`, libnvvm and libdevice are found, the device at the requested index exists, the
>   context and accelerator can be created, and the math probe kernel post-links and
>   loads on the device (2026-09-26, the invariant "CUDA is bound only when a kernel runs
>   on it"); otherwise the CPU accelerator with all cores. `AcceleratorKind.Cuda` fails
>   instead of falling back and names what was missing, with every path tried.
>   `AcceleratorKind.Cpu` never looks for CUDA.
>
>   When the probe fails:
>   - with `Auto`, its failure is the fallback reason in `CudaSkippedBecause`, with the
>     post-link's message;
>   - with `Cuda`, it is an `AcceleratorUnavailableException` carrying the post-link's
>     exception as its inner exception;
>   - in both cases the session is disposed before the choice returns.
>
>   The probe kernel loaded for the check is released at once. It costs 0.05 to 0.2 s per
>   CUDA engine on the reference machine (measured 2026-09-26).

---

<a id="pointer-wordings-condensed-2026-10-01"></a>

## 2026-10-01 — from "## Constraints" — the pointer paragraphs, at their earlier and longer wordings

Moved because the pointer paragraphs of `BOOT.md` are cut to two lines each (CONDENSE.md, rule 1); each keeps its date, its old and new wording and its anchor. Each paragraph as it stood is quoted below, headed by its section in parentheses:

> (from "## Invariants")
>   ⚠ 2026-09-15: was a second sentence naming ILGPU only through this node's own types
>   and `ArrayView`, now dropped (the package surface names no ILGPU type) →
>   HISTORY.md#no-cuda-type-ilgpu-naming-2026-09-15
>
> (from "## Invariants")
>   ⚠ 2026-09-26: was "ILGPU 1.5.3's own libdevice wrapper generation is never relied on",
>   now the post-link completes the wrappers ILGPU dropped and inserts none it already
>   defined → HISTORY.md#post-link-completes-not-replaces-2026-09-26
>
> (from "## Invariants")
>   ⚠ 2026-09-26: was a CUDA session accepted once its context existed, now only after the
>   math probe kernel has loaded on the device →
>   HISTORY.md#cuda-bound-only-when-a-kernel-runs-2026-09-26
>
> (from "## Constraints")
> - Reference figures of 2026-09-12 (the probe within 4 ULP of the CPU accelerator over 26
>   decades; the 100 000-case rocket sweep in 0.15 to 0.17 s on CUDA against 9.5 s on the
>   CPU accelerator, 56 to 65 times; bounds on expectation, not requirements) →
>   HISTORY.md#reference-figures-2026-09-12
>
> (from "## Constraints")
>     - ⚠ 2026-09-26: was the CUDA context created before libnvvm was loaded (a bad
>       library threw a raw `BadImageFormatException` and leaked about 190 MiB per
>       attempt), now the library first →
>       HISTORY.md#bad-library-leaked-the-context-2026-09-26
>
> (from "## Constraints")
>     ⚠ 2026-09-28: was the layout rounded for "a count ILGPU's warp layout cannot express
>     exactly", now rounded to keep the (4, 4, 1) shape of every record →
>     HISTORY.md#all-cores-layout-reason-2026-09-28
>
> (from "## Constraints")
>     - ⚠ 2026-09-26: was "all cores" true on the 16-thread reference machine only
>       (`CPUDevice.Default`), now the device sized from `Environment.ProcessorCount` →
>       HISTORY.md#all-cores-cpudevice-default-2026-09-26
>
> (from "## Structure")
> ⚠ 2026-09-15: was `Engine` and `MathProbe` the node's public composition types, now
> internal, `AcceleratorProbe` replacing them on the package surface →
> HISTORY.md#engine-and-mathprobe-internal-2026-09-15
>
> (from "## Structure")
> ⚠ 2026-09-14: was `FunctionCount` the constant the kernel strides by, now it stays a
> public property and an internal const `StrideCount` strides →
> HISTORY.md#probe-stride-count-2026-09-14
>
> (from "## Structure")
> ⚠ 2026-10-01: was `LibDevice/` failed (three types) and stayed two files of this
> node, now a child: about 90 lines of rules bind only its files →
> HISTORY.md#libdevice-child-node-2026-10-01
>
> (from "## Structure")
>   ⚠ 2026-09-15: was `CudaSkippedBecause` "null when CUDA was not tried or was bound",
>   now null when CUDA was bound or the options asked for the CPU →
>   HISTORY.md#fallback-says-why-2026-09-15
>
> (from "## Structure")
> - Decisions of the review of 2026-09-14: the missing-definition guard names the wrapper,
>   and the chunk bound counts every buffer →
>   HISTORY.md#review-decisions-guard-and-chunk-bound-2026-09-14
>
> (from "## Structure")
>   ⚠ 2026-09-15: was the views structs "kernel parameter descriptors ILGPU requires to be
>   public", now internal with `InternalsVisibleTo("ILGPURuntime")` →
>   HISTORY.md#views-structs-need-not-be-public-2026-09-15
>
> (from "## Structure")
>   ⚠ 2026-09-14: was four views structs declared as the parameter-count exception, now
>   two (the other two take six parameters) and the result constructors declared too →
>   HISTORY.md#views-structs-exception-claim-2026-09-14
>
> (from "## Taboos")
>   ⚠ 2026-09-26: was "No reliance on `Context.Builder.LibDevice()` to produce wrappers:
>   it does not", now it does for `compute_75` to `compute_90` and the taboo is on kernels
>   whose wrappers go unchecked → HISTORY.md#taboo-libdevice-reliance-2026-09-26

>>
> (from "pointers of the second pass")
>   - The documentation and small items (observations 1 to 8) and the guards of this node
>     (F7, F8, O2) of the second audit, 2026-09-28 →
>     HISTORY.md#audit-fixes-small-items-and-guards-2026-09-28
>
> (from "pointers of the second pass")
> Decision of 2026-09-15 (the child-nodes phase, root `BOOT.md`, 0aa7e60): a cluster earns
> a child directory when the rest of the node reaches it through a contract narrower than
> its code, it has a reason of its own to change and it holds about five types or more.
> `Chunks/` passed (six internal types, its row above) →
> HISTORY.md#child-nodes-decision-2026-09-15
---

<a id="libdevice-child-node-2026-10-01"></a>

## 2026-10-01 — from "## Structure" — LibDevice becomes a child node: the Structure rows of its files and the decision's last lines

Moved because the node `src/Execution/LibDevice` now exists (the owner's decision of 2026-10-01) and the two rows and the decision's last lines are replaced by one row and a pointer. The text as it stood:

> | `LibDevicePostLink` | the post-link as the sequence of its stages, each a method or a small internal type: the wrapper inventory of the kernel PTX (called at `call` sites, defined by `.func` headers; 2026-09-26), the NVVM module from the fragments of the missing wrappers, the compilation, the insertion after the header, the definition check as a set comparison over the wrapper text, the trial load; `Link` returns what it did | internal |
> | `CudaWslDevices` | the WSL workaround (2026-09-27, Constraints, "Every CUDA context of a process binds under WSL"): tries `builder.Cuda()` first, every call, and only on the resolver-already-set exception registers the devices itself by reflecting ILGPU's own internal `CudaDevice.GetDevices` | internal |
>
> `Chunks/` passed (six internal types, its row above); `LibDevice/` failed (three types)
> and `ExpectedIlgpuVersion` stays on `LibDevicePostLink`: `LibDeviceLocator` and
> `LibDevicePostLink` stay two files of this node →
> HISTORY.md#child-nodes-decision-2026-09-15

---

<a id="compile-bound-evidence-table-2026-09-30"></a>

## 2026-10-01 — from "## Acceptance criteria" — criterion: the compile bound, the evidence run and the per-project peaks

Moved because `ACCEPTANCE.md` is held to 400 non-blank lines (`AGENTS.md`, §15); the criterion keeps its tick, date and evidence place there. The text as it stood:

>       Evidence at `ee3c598` and the documentation commit that follows it, on this machine (60 GB, shared), Windows: `dotnet build
>       APThermo.sln` 0 warnings, 0 errors; the protocol lint 0 errors, 0 warnings;
>       `APTHERMO_NO_CUDA=1 dotnet test tests/<project> --no-build --filter
>       "Category!=LongRunning"`, one project at a time, Debug, process tree sampled every
>       250 ms (the largest single process is the test host; `Execution.Tests` and `Cli.Tests` also start child processes):
>
>       | Project | Tests | Wall time | Peak private, process tree (GiB) | Largest process (GiB) |
>       |---|---|---|---|---|
>       | `Performance.Tests` | 1429 | 34.6 s | 0.75 | 0.65 |
>       | `Problems.Tests` | 1252 | 13.2 s | 0.95 | 0.84 |
>       | `Execution.Tests` | 168 | 16.4 s | 1.52 | 0.85 |
>       | `Cli.Tests` | 142 | 118.7 s | 1.05 | 0.91 |
>       | `Docs.Tests` | 30 | 41.9 s | 1.61 | 1.55 |
>
>       No `Bits*.approved.txt`, `Throughput*.approved.txt` or `PublicSurface.approved.txt`
>       moved (`git status` after every run). Nothing ran on CUDA: the CUDA proof is the
>       root's, after the merge, where a call the attribute leaves in the PTX would show.

---

<a id="probe-allocation-replaced-2026-09-30"></a>

## 2026-10-01 — from "## Acceptance criteria" — criterion: no test allocates what it measures, the second allocation

Moved because `ACCEPTANCE.md` is held to 400 non-blank lines (`AGENTS.md`, §15); the criterion keeps its tick, date and evidence place there. The text as it stood:

>         ⚠ 2026-09-30: the criterion named that one allocation. Measuring the project's peak
>         found a second of the same kind: `ProbeMathRefusesAnInputCountWhoseOutputOverflowsA32BitOffset`
>         built a `double[153 391 690]` (1.2 GB) to make `Engine.ProbeMath` refuse it, which put
>         the test host of the whole project at 1.81 to 2.11 GiB, on both sides of the tests
>         node's 2 GB bound. `MathProbe.OutputLength(int inputCount)` now holds the bound
>         `ProbeMath` called inline (the same message, the same `ArgumentException`, its parameter
>         name now `inputCount`), and `TheProbeOutputLengthBoundIsInclusiveOfTheLargestOffset`
>         asks it of the count just inside and just over the limit (red with the bound moved 14
>         counts either way, seen). What the old fact proved and the new one does not: that
>         `ProbeMath` itself refuses. The refusal needs an input array of the size it refuses,
>         so it cannot be proved without the allocation. The tests node's criterion names the
>         trade for the owner. The host's peak after the change: 0.81 to 0.93 GiB.

---

<a id="compile-guard-metric-and-runs-2026-09-30"></a>

## 2026-10-01 — from "## Acceptance criteria" — criterion: the compile guard, the metric and the five-run table

Moved because `ACCEPTANCE.md` is held to 400 non-blank lines (`AGENTS.md`, §15); the criterion keeps its tick, date and evidence place there. The text as it stood:

>         **The metric.** ILGPU offers no public size of the compiled program
>         (`Context.IRContext` is internal in 1.5.3). Read by reflection, the IR's block count
>         is deterministic (Debug 36 321 with the attribute, 232 041 without; Release 35 573
>         and 228 085), but the two are 6.4 times apart, so the bound the criterion asks for
>         (at least 3 times above green, at most half of red) would sit in a window of 6.5 %,
>         and reading it needs a reflection over ILGPU's internals; rejected. The bytes
>         allocated on the calling thread are as stable (within 1 % over five
>         processes) and about 24 to 26 times apart. Seconds are the other candidate and not used:
>         a loaded machine moves them.
>
>         **Five fresh-process runs each**, bytes allocated on the calling thread / the run's
>         warm-up in seconds / peak private memory of the process tree in GiB (`dotnet test`,
>         its host included), run 1 to 5:
>         - Debug, attribute on: 338 600 984 / 1.99 / 0.64; 338 600 280 / 1.99 / 0.65;
>           338 614 424 / 2.05 / 0.63; 338 566 440 / 2.11 / 0.63; 338 599 544 / 2.05 / 0.63.
>         - Debug, attribute removed: 8 663 162 104 / 44.88 / 13.64; 8 663 407 664 / 45.71 /
>           13.78; 8 663 191 440 / 46.43 / 13.73; 8 663 194 792 / 46.08 / 13.71;
>           8 663 231 224 / 48.74 / 13.62.
>         - Release, attribute on: 304 713 600 / 1.82 / 0.59; 303 625 648 / 1.81 / 0.56;
>           301 729 608 / 1.98 / 0.63; 304 710 888 / 1.96 / 0.60; 304 710 008 / 1.96 / 0.56.
>         - Release, attribute removed: 7 210 688 104 / 40.00 / 12.01; 7 200 512 896 / 39.26 /
>           12.21; 7 210 360 992 / 38.29 / 12.14; 7 210 335 176 / 37.77 / 12.27;
>           7 186 564 984 / 38.22 / 12.25.
>
>         Kept managed heap after the compile, one process each (GC forced): Debug 143 MiB with
>         the attribute, 3 965 MiB without; Release 111 and 2 992 MiB. The first measurement
>         pass (Debug, the full kernel sequence's rocket step) agrees: 2.10 to 2.45 s and
>         625 to 637 MB peak commit with the attribute over six runs, 49.7 to 59.1 s and
>         14 236 to 14 359 MB without over five.

---

<a id="clear-cache-after-each-load-not-adopted-2026-09-30"></a>

## 2026-10-01 — from "## Acceptance criteria" — criterion: Context.ClearCache after each kernel load, not adopted

Moved because `ACCEPTANCE.md` is held to 400 non-blank lines (`AGENTS.md`, §15); the criterion keeps its tick, date and evidence place there. The text as it stood:

>         **Decision on `Context.ClearCache` after each kernel load: not adopted.** Debug,
>         the attribute on, three fresh processes each, one engine loading the rocket, transport,
>         equilibrium and species-function kernels in turn, with (a) no call and (b) the call
>         after every run. Later loads' warm-up in seconds, transport / equilibrium: (a)
>         0.39 / 0.50, 0.31 / 0.41, 0.38 / 0.40; (b) 0.31 / 1.23, 0.27 / 1.02, 0.26 / 1.08.
>         Peak commit of the process in MB: (a) 663, 726, 661; (b) 765, 754, 739. The call
>         drops the kept 143 MiB of a live engine, but the equilibrium kernel's load is 2.5
>         times slower after it, the peak is not lower, and a live engine's 143 MiB is not the
>         cost that took the suite down. The call runs at `Dispose` only. The result bits of
>         the cleared sequence were not compared, since it is not adopted.

---

<a id="release-at-dispose-context-cache-2026-09-30"></a>

## 2026-10-01 — from "## Acceptance criteria" — criterion: release at dispose, the launchers alone free nothing

Moved because `ACCEPTANCE.md` is held to 400 non-blank lines (`AGENTS.md`, §15); the criterion keeps its tick, date and evidence place there. The text as it stood:

>         ⚠ 2026-09-30: the design stood "`KernelCache` gains a `Clear`, called before the
>         session is disposed" as the whole release, and expected the measured 3 GB of ILGPU IR
>         to go with the launchers. Measured, it does not: dropping the launchers frees none of
>         it (143 MiB kept by one rocket kernel with the attribute on, 143 MiB still kept after
>         `Dispose` with the engine referenced, with and without the launcher dictionary
>         cleared). The IR sits in the context's caches, which the disposed session keeps
>         reachable; `Context.ClearCache(Everything)` before the dispose took it from 143 MiB
>         to 0. The launcher fact alone would have passed a release that released nothing, so
>         `Clear` also clears the context and the second fact watches the heap. Found while
>         measuring the criterion's own claim, before ticking it.

---

<a id="lost-context-evidence-run-2026-09-29"></a>

## 2026-10-01 — from "## Acceptance criteria" — criterion: a lost context, the evidence run

Moved because `ACCEPTANCE.md` is held to 400 non-blank lines (`AGENTS.md`, §15); the criterion keeps its tick, date and evidence place there. The text as it stood:

>       Evidence, on the reference machine (Windows), from a tree with every `bin` and
>       `obj` removed:
>       - `dotnet build APThermo.sln`: 0 warnings, 0 errors;
>       - `APTHERMO_NO_CUDA=1 dotnet test APThermo.sln --filter "Category!=LongRunning"`:
>         5429 of 5429, none skipped, `Execution.Tests` 161 of 161 among them (see
>         above);
>       - `git status --short -- '**/Bits*.approved.txt' '**/Throughput*.approved.txt'
>         '**/PublicSurface.approved.txt'` empty: no snapshot moved (no public or
>         internal-tree-contract shape changed; every new member is `internal`);
>       - the protocol lint: 0 errors, 0 warnings.
>
>       This CPU-side evidence is complete; the `Category=Cuda` run and the red-once
>       mutation on real hardware stay the orchestrator's, per this task's own
>       instruction not to run CUDA tests from this worktree.

---

<a id="lost-context-second-correction-2026-09-29"></a>

## 2026-10-01 — from "## Acceptance criteria" — criterion: a lost context, the second correction

Moved because `ACCEPTANCE.md` is held to 400 non-blank lines (`AGENTS.md`, §15); the criterion keeps its tick, date and evidence place there. The text as it stood:

>         ⚠ 2026-09-29 (second correction, this task): that fix was itself wrong on two
>         counts, found on a second review. First, removing the gate made both facts
>         construct a real `CudaException`, which loads `nvcuda` into the shared test
>         process even on the CPU accelerator; to keep the fast suite green, the first fix
>         narrowed `AcceleratorChoiceTests.NoCudaDriverIsLoadedInAProcessThatForbidsCuda`
>         from a whole-process check to a before/after diff around `Engine.Create` alone —
>         weakening a check written to guard the root's "CPU path needs no NVIDIA
>         software" invariant process-wide, to make a test pass, exactly the taboo this
>         root forbids. Second, an un-gated fact that constructs a `CudaException` is
>         itself a hosted-CI risk: a runner with no NVIDIA driver at all may fail to build
>         one, not merely fail to use one. The reviewer's fix (adopted here): restore
>         `AcceleratorChoiceTests.cs` exactly as committed on `main`
>         (`git checkout main -- tests/Execution.Tests/AcceleratorChoiceTests.cs`), keep
>         `ATimedOutEngineRefusesANewCall` and `ATimedOutEnginesDisposalDropsTheStickyFailure`
>         `Cuda`-tagged and gated exactly as first written, and add the decision itself
>         (`AcceleratorSession.DropsAfterLoss(CudaError)`, a thin `DropsAfterLoss(CudaException)`
>         extraction onto it) plus two new, un-gated CPU facts
>         (`AnEngineMarkedLostRefusesANewCall`,
>         `DropsAfterLossMatchesOnlyTheStickyLaunchTimeoutOfALostSession`) that drive
>         `Engine.MarkLost`/`ProbeMath`/`DropsAfterLoss(CudaError)` directly, without any
>         `CudaException`. `LaunchBudgetTests`' own class doc now says four `Cuda`-tagged
>         facts, not two, since the count was already stale before this task touched it.
>
>         Both new CPU facts shown red once on the reference machine, reverted, green
>         again: `AnEngineMarkedLostRefusesANewCall` with `AcceleratorSession.ThrowIfLost`'s
>         `if` condition changed to `_lostBy is { } timeout && false` (`Assert.Throws`
>         failed, "No exception was thrown");
>         `DropsAfterLossMatchesOnlyTheStickyLaunchTimeoutOfALostSession` with
>         `AcceleratorSession.DropsAfterLoss(CudaError)` changed to match
>         `CUDA_ERROR_OUT_OF_MEMORY` instead of the sticky timeout (`Assert.True` failed,
>         expected true, actual false). The two `Cuda`-tagged facts and
>         `NoCudaDriverIsLoadedInAProcessThatForbidsCuda` are unchanged from `main` and
>         need no fresh red-once record here.
>
>         Built and passing on the reference machine's CPU accelerator, in one process,
>         the restored check and the two new facts together: `dotnet test
>         tests/Execution.Tests --filter "Category!=LongRunning"` 163 of 163 under
>         `APTHERMO_NO_CUDA=1` (159 before this task, 161 after the first, wrong fix, 163
>         after the second). `dotnet test tests/Protocol.Tests --filter
>         "Category!=LongRunning"` 35 of 35 (`TreeContractSnapshotTests` re-approved for
>         `Engine.MarkLost`/`DropsAfterLoss(CudaError)`, both added to `API.md`;
>         `ShapeTests` re-measured `Engine`'s efferent coupling at 31, one over its
>         previous row, and the row above is updated with the reason). The protocol lint:
>         0 errors, 0 warnings. The `Category=Cuda` run itself — the two facts that need a
>         real CUDA driver to construct their own injected exception at all — is the
>         orchestrator's to run on the reference machine, per this task's own instruction
>         not to run CUDA tests from this worktree; not run here.

---

<a id="lost-context-first-correction-2026-09-29"></a>

## 2026-10-01 — from "## Acceptance criteria" — criterion: a lost context, the first correction

Moved because `ACCEPTANCE.md` is held to 400 non-blank lines (`AGENTS.md`, §15); the criterion keeps its tick, date and evidence place there. The text as it stood:

>         ⚠ 2026-09-29 (first correction, this task): this evidence first read that
>         `ATimedOutEngineRefusesANewCall` and `ATimedOutEnginesDisposalDropsTheStickyFailure`
>         carried `[Trait("Category","Cuda")]` and returned at once under
>         `Engine.CudaForbidden` — so under `APTHERMO_NO_CUDA=1`, the fast suite and every
>         hosted runner, neither fact executed a single assertion, and the section's own
>         claim was never actually proven by a run recorded here. Found on review. The
>         fix removed the gate from both facts directly.

---

<a id="lost-context-evidence-seams-2026-09-29"></a>

## 2026-10-01 — from "## Acceptance criteria" — criterion: a lost context, the evidence seams

Moved because `ACCEPTANCE.md` is held to 400 non-blank lines (`AGENTS.md`, §15); the criterion keeps its tick, date and evidence place there. The text as it stood:

>       - **Evidence.** The timeout cannot be provoked on the reference machine (it resets
>         the display driver), so every fact below injects its failure through a seam
>         that needs no real device. Split across two kinds of fact, decided by whether
>         the fact needs an actual `CudaException` (which loads the CUDA driver, `nvcuda`,
>         into the process to build even on the CPU accelerator — see the second ⚠ below):
>         - **`Cuda`-tagged, `Engine.CudaForbidden`-gated, on the reference machine only**
>           (with `ALaunchTimeoutBecomesAnAcceleratorUnavailableExceptionNamingTheLimitAndTheRemedy`
>           above): `ATimedOutEngineRefusesANewCall` runs the real translation
>           (`Engine.RunBatchLoop` with a launch delegate that throws
>           `CudaException(CUDA_ERROR_LAUNCH_TIMEOUT)`) and then calls `cpu.ProbeMath`,
>           asserting `AcceleratorUnavailableException` naming "earlier launch timeout";
>           `ATimedOutEnginesDisposalDropsTheStickyFailure` hands `Engine.DisposeAfterLoss`
>           an injected `IDisposable` (`StickyDisposable`) whose first `Dispose` always
>           throws `CudaException(CUDA_ERROR_LAUNCH_TIMEOUT)` and asserts no exception
>           escapes. Both prove the real, driver-touching path end to end; the
>           orchestrator runs them on the reference machine after the merge.
>         - **No `Cuda` trait, no gate, run under `APTHERMO_NO_CUDA=1` on every runner
>           hosted CI included**: `AnEngineMarkedLostRefusesANewCall` calls the new
>           `Engine.MarkLost` with an injected `AcceleratorUnavailableException` — never a
>           `CudaException` — then asserts `cpu.ProbeMath` refuses the same way, naming the
>           same timeout as its inner exception;
>           `DropsAfterLossMatchesOnlyTheStickyLaunchTimeoutOfALostSession` proves
>           `AcceleratorSession.DropsAfterLoss(CudaError)`'s decision on the bare enum value
>           alone (never lost, the sticky error → false; lost, the sticky error → true;
>           lost, the wrong error → false), through the new `Engine.DropsAfterLoss(CudaError)`.

---

<a id="lost-context-catch-place-2026-09-29"></a>

## 2026-10-01 — from "## Acceptance criteria" — criterion: a lost context, where the catch stands

Moved because `ACCEPTANCE.md` is held to 400 non-blank lines (`AGENTS.md`, §15); the criterion keeps its tick, date and evidence place there. The text as it stood:

>         ⚠ 2026-09-29: the sketch said the drop happens "in one named place of this
>         node, and nowhere else". The decision does (`DropsAfterLoss`, above), but the
>         `catch` itself could not be that one place: CA2000 (this node's Diagnostics
>         constraint) refuses a `new ChunkBuffers(session.Accelerator)` whose disposal is
>         routed through a called method rather than a literal `Dispose()` call in the
>         same method — confirmed by trying exactly that first and reading CA2000's own
>         refusal. `BatchRun.Execute` therefore owns disposing the buffers it was given,
>         in its own `finally`, through the new private `DisposeChunkBuffers` (the pass
>         every pipeline's `Run` actually depends on for the drop); each pipeline's
>         `using var buffers` still exists only to satisfy CA2000 at its own allocation
>         site, and its own dispose call, reached after `BatchRun.Execute` already
>         disposed the same buffers, finds every device buffer already disposed and does
>         nothing (ILGPU's own dispose is idempotent, confirmed by reading
>         `DisposeBase.DisposeDriver`'s `Interlocked.CompareExchange` guard). `Engine.DisposeAfterLoss`
>         and `AcceleratorSession.Dispose`'s own three `catch` blocks read the same
>         decision for the pieces CA2000 does not flag (an existing field's disposal, not
>         a freshly allocated local).

---

<a id="audit-guards-race-and-evidence-2026-09-28"></a>

## 2026-10-01 — from "## Acceptance criteria" — criterion: the audit fixes of 2026-09-28, guards, the WSL race and the evidence

Moved because `ACCEPTANCE.md` is held to 400 non-blank lines (`AGENTS.md`, §15); the criterion keeps its tick, date and evidence place there. The text as it stood:

>       - **Guards (F7, F8, O2).** `AcceleratorChoice.Decide`/`Cuda` gained internal
>         overloads taking an explicit `cudaForbidden` flag; `AnAutoFallbackSaysWhyCudaWasSkippedAndWhichPathsWereTried`,
>         `AnExplicitCudaRequestWithPathsNowhereNamesEveryPathTried` and
>         `CudaForbiddenRefusesBeforeDiscoveryEverRuns` call it directly and so run
>         regardless of `APTHERMO_NO_CUDA`; red once by emptying the refusal's `tried` list
>         unconditionally, which the first two facts had asserted a real list from.
>         `ChunkPlanWiringTests.EachPipelinesChosenPlanRespectsItsOwnOffsetCap` (F8) drives
>         three of the four pipelines' own buffer declarations (a new internal
>         `DeclareBuffers` each, `SpeciesFunctionPipeline` excepted: every one of its
>         strides is 1) through `ChunkPlan.For` and compares against an independently
>         computed expected stride, never `buffers.MaxElementsPerCase` read back; red once
>         with `MaxElementsPerCase => _buffers.Count` (still instance data, so the mutation
>         compiles), all three theory rows failing. `SpeciesFunctionTests.CompareFunctions`
>         (O2) is NaN-aware; `TheComparisonIsNaNAwareAndCatchesAMismatchOnlyOneSideMakesNaN`
>         is red once by removing the two NaN branches, which then missed a value NaN on
>         one accelerator and not the other.
>       - `ArchitectureTests` gives each backend its own `NvvmAPI` (observation 4): a
>         shared instance, freed once per backend's `Dispose`, only worked because the
>         fixture's own CUDA engine kept the same libnvvm loaded; not independently
>         reproduced with a different libnvvm (would need a second engine construction
>         path this task did not build), accepted by inspection against ILGPU's
>         `PTXBackend.Dispose` (`PTXBackend.cs:149-158`, cited by the audit).
>
>       A genuine WSL race, found only on a full `-c Release` run of `tests/Execution.Tests`
>       on real CUDA hardware (never on an isolated fact): `LaunchBudgetTests` had no
>       xUnit `[Collection]`, so its two `Cuda`-tagged facts (a real `CudaException`
>       construction, which touches the driver) could run on a separate thread
>       concurrently with `EngineFixture`'s own lazy CUDA engine creation, and the two
>       raced during the process's first real CUDA use: "CUDA device 0 was requested, but
>       0 device(s) exist", 22 facts failing together, on about a third of full-suite runs.
>       Fixed by joining `LaunchBudgetTests` to `EngineFixture.CollectionName`, serializing
>       it against every other CUDA-touching class; not independently red-onced against
>       the race itself, since the race was not reliably reproducible on demand, only
>       observed and then absent over two full re-runs after the fix.
>
>       Evidence, on the reference machine, from a tree with every `bin` and `obj`
>       removed:
>       - `dotnet build APThermo.sln`: 0 warnings, 0 errors;
>       - `APTHERMO_NO_CUDA=1 dotnet test APThermo.sln --no-build --filter
>         "Category!=LongRunning"`: every project green, `Execution.Tests` 159/159,
>         `Protocol.Tests` 32/32 (with the `## Shape exceptions` table's `Engine` and the
>         four pipelines' Ce re-measured for `LaunchBudget` and the new members);
>       - `dotnet test tests/Execution.Tests -c Release` (no filter), on Windows:
>         162/162, the 100 000-case sweep, the architecture fact and the throughput
>         tripwire included, twice (once before and once after the collection fix, both
>         green — the race was never observed on Windows);
>       - the same command under WSL2 (Ubuntu 24.04, libnvvm 12.9, a throwaway scratch
>         clone of this branch, never `~/apthermo`): 162/162 on the commit with the
>         collection fix (a prior run of the commit before it hit the race above, 22
>         failures, all resolved by the fix); `Throughput.linux.approved.txt` unchanged
>         (27.48×, the 2026-09-19 figure — this task changed no numerical code path the
>         throughput measures);
>       - no `Bits*.approved.txt`, `Throughput*.approved.txt` or
>         `Protocol.Tests/PublicSurface.approved.txt` differs from before this task's
>         first commit, in this node's own subtree;
>       - the protocol lint: 0 errors, 0 warnings.
>
>       Under WSL the fast suite's Linux bit and approved-output comparisons of other
>       nodes did not match at this task's base (`2744915`). That is expected: the
>       numerical change of 2026-09-28 in `Equilibrium` moved the Windows records, and
>       the root's platform constraint has the orchestrator record the Linux files
>       under WSL after the merges. This node's own facts were green under WSL
>       throughout. Reworded by the orchestrator at the merge: the coder's note
>       retold other nodes' state, which AGENTS.md §8 keeps out of a node's document.

---

<a id="audit-small-items-facts-2026-09-28"></a>

## 2026-10-01 — from "## Acceptance criteria" — criterion: the audit fixes of 2026-09-28, the small items' facts

Moved because `ACCEPTANCE.md` is held to 400 non-blank lines (`AGENTS.md`, §15); the criterion keeps its tick, date and evidence place there. The text as it stood:

>       - **Small items.** `BatchConstructorsRefuseACountWhoseArrayOverflowsA32BitLength`
>         (`EquilibriumBatch`, `RocketBatch`, `TransportBatch`, and a count just inside the
>         bound still allocates); `AChunkBufferRefusesAHostArrayShorterThanTheChunkNeeds`
>         (both directions); `ProbeMathRefusesAnInputCountWhoseOutputOverflowsA32BitOffset`;
>         `ALogThatTrimsToNothingLeavesNoTrailingColon`, red once by narrowing the
>         post-link's emptiness check from "trims to nothing" to "is null", which then left
>         a trailing ": " with nothing after it.

---

<a id="architecture-fact-duration-2026-09-28"></a>

## 2026-10-01 — from "## Acceptance criteria" — criterion: every architecture, the duration claim

Moved because `ACCEPTANCE.md` is held to 400 non-blank lines (`AGENTS.md`, §15); the criterion keeps its tick, date and evidence place there. The text as it stood:

>         ⚠ 2026-09-28: this record said "about three minutes" for the fact and "first
>         (cold JIT cache) run 3 m 41 s, subsequent runs about 14 s once the CUDA driver's
>         own compute cache is warm". The second audit's warm-up measurement (finding,
>         "The first audit's fixes", observation 3) found the driver's compute cache is
>         keyed by the PTX text and ILGPU's generated names come from process-wide
>         counters, so every engine after the first in a process misses it regardless of
>         an earlier run; the fact creates several engines and never warms a shared
>         cache. Measured again: 8 m 2 s on Windows, 11 m 3 s under WSL2, both green. The
>         assertions and the architecture and entry-point coverage are unchanged; only
>         the duration claim was wrong.

---

<a id="audit-fixes-small-items-and-guards-2026-09-28"></a>

## 2026-10-01 — from "## Constraints" — audit fixes of 2026-09-28: documentation, small items and the guards of this node

Moved because `BOOT.md` is over the §15 line limit for its kind of node; what it said stays at the pointer. The text as it stood:

>   - **Documentation and small items (observations 1 to 8).**
>     - `MathProbe` and the tests node say that `Floor` and `Ceiling` go through libdevice
>       (`__ilgpu__nv_floor`, `__ilgpu__nv_ceil`): the post-link completes seven wrappers
>       for the probe. Only `Abs` is emitted directly.
>     - The PTX fixtures are regenerated from the current probe. The regeneration rule
>       names a change to `Kernels.Probe` beside an ILGPU upgrade.
>     - `API.md` states the warm-up an engine costs: the rocket kernel's compile, 13 to
>       22 s, and the driver's JIT. The JIT runs again for every engine after the first
>       in a process, 55 to 59 s, because ILGPU's generated names come from process-wide
>       counters and the driver's cache is keyed by the PTX text. The advice: one `Solver`
>       per process. The durations this document and the tests node give for the
>       architecture fact are the measured ones, 8 m on Windows and 11 m under WSL, not
>       "about three minutes".
>     - Batch constructors compute sizes in `long` and refuse a count whose buffers exceed
>       the 32-bit offsets. `ChunkBuffer` checks the host array's length before a copy.
>       `ProbeMath` caps its input count the same way.
>     - An empty libnvvm log leaves no trailing ": " in the post-link's message.
>     - The all-cores layout's reason is corrected. Any warp size from 2 constructs, as
>       `AcceleratorChoice` already records. The layout rounds down to a multiple of 4 to
>       keep the (4, 4, 1) shape at 16 threads, the reference machine's record, and up to
>       3 threads idle is its cost.
>   - **Guards of this node (the guards part's F7, F8, O2).**
>     - The two library-discovery "not found" facts run their assertions in the hosted
>       matrix: the forbidden flag is injected into `AcceleratorChoice`, so the not-found
>       branch runs without CUDA. Until then they returned early in every job.
>     - The 32-bit chunk cap is asserted as wiring: for each of the four pipelines,
>       driven with a huge `ChunkSize` and `ScratchBytes`, the plan it chose satisfies
>       `Size × MaxElementsPerCase ≤ int.MaxValue`. Until then
>       `MaxElementsPerCase => 0` passed every test.
>     - `SpeciesFunctionTests`' comparison is NaN-aware, as the probe's and the sweep's
>       are.
>     - `ArchitectureTests` gives each backend its own `NvvmAPI`. `PTXBackend.Dispose`
>       frees the one it was given, and a shared instance survived only because the
>       fixture's engine kept the same libnvvm loaded (observation 4: a variant with
>       libnvvm 12.9 crashed with an access violation).

---

<a id="launch-duration-unbounded-2026-09-28"></a>

## 2026-10-01 — from "## Constraints" — launch budget: nothing bounded a launch's duration

Moved because `BOOT.md` is over the §15 line limit for its kind of node; what it said stays at the pointer. The text as it stood:

>     - ⚠ Nothing bounded a launch's duration, and neither this document nor `API.md`
>       named the limit. The audit measured launch times up to 1.15 s, and projected
>       about 2 s for a default chunk at 14 elements and past 2 s for one case from 17.
>       Under `Auto` the kill comes after CUDA was bound, so there is no fallback. It was
>       not provoked, because it resets the display driver the desktop and the release
>       runners share.

---

<a id="probe-constant-first-order-2026-09-28"></a>

## 2026-10-01 — from "## Constraints" — the probe called Min and Max with the variable first only

Moved because `BOOT.md` is over the §15 line limit for its kind of node; what it said stays at the pointer. The text as it stood:

>     - ⚠ The probe called `Min(v, 1.0)` and `Max(v, 1.0)` only, the order ILGPU does
>       not swap. The tests node's record "0 ULP, NaN included" held for that order.
>       On the reference device `KernelMath.Min(1.0, NaN)` was 1.0 on CUDA and NaN on the
>       CPU (the audit's `E01`: 18 mismatches over 12 outputs and 10 inputs, all at NaN).

---

<a id="all-cores-layout-reason-2026-09-28"></a>

## 2026-10-01 — from "## Constraints" — all cores: the reason of the multiple-of-4 layout corrected

Moved because `BOOT.md` is over the §15 line limit for its kind of node; what it said stays at the pointer. The text as it stood:

>     ⚠ 2026-09-28 (the second hidden-defect audit, guards observation O8): this bullet
>     said the layout is rounded because "a count ILGPU's warp layout cannot express
>     exactly". `AcceleratorChoice.CpuDeviceFor`'s own summary already said otherwise: any
>     warp size from 2 constructs (ILGPU's `CPUDevice` constructor refuses only 1). The
>     real reason the layout is fixed at a multiple of 4 rather than reaching every count
>     exactly is to keep the (4, 4, 1) shape at 16 threads that every bit and throughput
>     record was measured against; a processor count that is not a multiple of 4 then
>     leaves up to 3 threads idle, the cost of that choice, not a limit ILGPU imposes.
>     Found by the guards audit reading `AcceleratorChoice.cs` against this document.

---

<a id="wsl-evidence-2026-09-27"></a>

## 2026-10-01 — from "## Acceptance criteria" — criterion: every CUDA context binds under WSL, evidence

Moved because `ACCEPTANCE.md` is held to 400 non-blank lines (`AGENTS.md`, §15); the criterion keeps its tick, date and evidence place there. The text as it stood:

>       Evidence, on the reference machine:
>       - **Red once, under WSL** (WSL2 Ubuntu 24.04, .NET SDK 10.0.112, CUDA 12.9
>         libnvvm, a clone at `~/apthermo` whose `origin` is this repository, git
>         checkout `89bb619` detached): the existing
>         `AcceleratorChoiceTests.AnExplicitCudaRequestFailsWithThePostLinksOwnExceptionWhenTheProbeKernelCannotBind`
>         (`dotnet test tests/Execution.Tests -c Release --filter
>         "FullyQualifiedName~ProbeKernelCannotBind"`) failed on its second CUDA context
>         (the `Auto` fact right before it in `AcceleratorChoiceTests` already having
>         created the first) — "Assert.Contains() Failure … Not found: \"the math probe
>         kernel could not be loaded\"", the message instead "the CUDA context could not
>         be created (driver or device problem): A resolver is already set for the
>         assembly", exactly the defect. A throwaway three-engine fact (this criterion's
>         own, without `CudaWslDevices` yet — not committed) failed the same way on its
>         second engine: `AcceleratorUnavailableException`, inner
>         `InvalidOperationException` "A resolver is already set for the assembly.",
>         through `CudaContextExtensions.CudaInternal` → `NativeLibrary.SetDllImportResolver`.
>       - **Green after the fix, under WSL**, at `bfab662`: the new
>         `CudaWslDevicesTests.EveryCudaEngineOfTheProcessBindsAndProbes` (three fresh
>         CUDA engines, each binding and probing) and
>         `AcceleratorChoiceTests.AnAutoFallbackNamesThePostLinkWhenTheProbeKernelCannotBind`
>         together with `...AnExplicitCudaRequestFailsWithThePostLinksOwnExceptionWhenTheProbeKernelCannotBind`
>         (the two bind-time probe facts, in one run) both green;
>         `dotnet test tests/Execution.Tests -c Release`, no filter: 143 of 143, the
>         100 000-case sweep and the throughput tripwire included, `git status --short`
>         against `Bits*.approved.txt`, `Throughput*.approved.txt` and the protocol tests
>         node's `PublicSurface.approved.txt` empty; the throughput ratio 27.48× (the
>         actual run measured 34.16×, comfortably above 80 % of it and the root's 5×
>         floor, so `Throughput.linux.approved.txt` was not re-approved).
>       - **Green on Windows, before and after**: `dotnet test tests/Execution.Tests -c
>         Release`, no filter, on a clean `bin`/`obj`: 143 of 143 both at `89bb619` (where
>         the resolver defect does not exist, since `IsRunningOnWSL()` is false) and at
>         `bfab662`; the throughput ratio at `bfab662` 28.38× (against the approved
>         23.58×), no `Bits*.approved.txt`, `Throughput*.approved.txt` or
>         `PublicSurface.approved.txt` changed; `protocol_lint` 0 errors, 0 warnings.
>       - **The missing-member path**: `CudaWslDevicesTests.ARenamedIlgpuMemberNamesItself`
>         calls the internal `CudaWslDevices.Reflect(registryPropertyName,
>         getDevicesMethodName)` seam directly with a wrong name for each of the two
>         members in turn (no WSL needed to reach it this way) and asserts the exception
>         names it; the same call with the real names still resolves, proving the fact
>         exercises a wrong name, not a broken reflection call.

---

<a id="kernels-ce-26-2026-09-27"></a>

## 2026-10-01 — from "## Shape exceptions" — Shape exceptions: the Kernels row

Moved because `BOOT.md` is over the §15 line limit for its kind of node; what it said stays at the pointer. The text as it stood:

> ⚠ 2026-09-27: `Kernels`' row read 25. `Kernels.Probe` now calls the thermo node's
> `KernelMath.Min` and `KernelMath.Max` in place of `System.Math.Min`/`Max` (the root's
> math constraint), which names one more type of the tree. Measured 26 by the
> dependency check's walk on this commit; the protocol tests node's `ShapeTests`
> confirms it.

---

<a id="wsl-second-cuda-context-fails-2026-09-27"></a>

## 2026-10-01 — from "## Constraints" — WSL: every engine after the first CUDA context failed

Moved because `BOOT.md` is over the §15 line limit for its kind of node; what it said stays at the pointer. The text as it stood:

>   - ⚠ Under WSL every `Engine.Create` after the first CUDA context of a process failed.
>     With `Auto` it bound the CPU with the reason "the CUDA context could not be created
>     (driver or device problem): A resolver is already set for the assembly". With
>     `Cuda` it threw. The tests had always met one CUDA context per process, until
>     the bind-time facts of 2026-09-26 created two:
>     `AnExplicitCudaRequestFailsWithThePostLinksOwnExceptionWhenTheProbeKernelCannotBind`
>     failed under WSL at `89bb619` right after the `Auto` fact, and passed alone. Found
>     by the orchestrator's WSL run of 2026-09-27. The native Linux path does not take
>     ILGPU's branch, and was not run.

---

<a id="taboo-libdevice-reliance-2026-09-26"></a>

## 2026-10-01 — from "## Taboos" — taboos: no reliance on Context.Builder.LibDevice()

Moved because `BOOT.md` is over the §15 line limit for its kind of node; what it said stays at the pointer. The text as it stood:

>   ⚠ 2026-09-26: stood "No reliance on `Context.Builder.LibDevice()` to produce
>   wrappers: it does not." It does, for `compute_75` to `compute_90`; the taboo was
>   written on SM_120 alone (the ⚠ of the invariant "Every CUDA kernel goes through the post-link").

---

<a id="audit-f2-f3-observations-and-evidence-2026-09-26"></a>

## 2026-10-01 — from "## Acceptance criteria" — criterion F2/F3: observations and the evidence runs

Moved because `ACCEPTANCE.md` is held to 400 non-blank lines (`AGENTS.md`, §15); the criterion keeps its tick, date and evidence place there. The text as it stood:

>       - **Observations.** `LibDevicePostLink.FailureMessage` now trims a log of NUL
>         (`\0`) alongside whitespace (`TrimLog`), so a NUL-padded driver or libnvvm log
>         carries no NUL in the exception message
>         (`PostLinkTests.ALogWithNulPaddingIsTrimmedOfIt`, shown red once against a plain
>         `.Trim()`: the message still held the NUL). `Engine.Create` refuses one of
>         `LibNvvmPath`/`LibDevicePath` given without the other with an `ArgumentException`
>         naming the missing option, rather than silently falling through to discovery as
>         `("", path)` used to (`AcceleratorChoiceTests.AHalfGivenExplicitLibraryPairIsRefused`,
>         shown red once: no exception thrown against the code before this change; the row
>         `API.md` already carried from the design). `Engine.Upload` disposes the species
>         buffers it already uploaded when the transport table's own upload then fails —
>         correct by inspection of the try/catch/dispose pattern, with no dedicated
>         reproduction: the tree's only path to a `TransportTable` is `TransportTable.Build`,
>         which always produces an internally consistent shape, so no legitimate call
>         makes `TransportTableBuffers.Upload` fail short of exhausting device memory.
>       - **Nothing else moves.** No `Bits*.approved.txt`, `Throughput*.approved.txt` or
>         the protocol tests node's `PublicSurface.approved.txt` changed (`git status
>         --short -- '**/Bits*.approved.txt' '**/Throughput*.approved.txt'
>         '**/PublicSurface.approved.txt'` empty); `Throughput.approved.txt` still reads
>         "cpu: CPUAccelerator with 16 threads" on the reference machine, unchanged
>         (16 processors reduces to the same (4, 4, 1) layout as before). The node's CUDA
>         tests are green in Release. `API.md`'s Errors table already stated the
>         half-pair refusal from the design; `Options.cs` and every "all cores" sentence
>         of this node are now literally true, not only on the reference machine.
>
>       Evidence, on the reference machine (RTX 5070 Ti, driver 13.4, CUDA toolkits
>       12.9/13.3/13.4), from a tree with every `bin` and `obj` removed:
>       - `dotnet build APThermo.sln`: 0 warnings, 0 errors;
>       - `APTHERMO_NO_CUDA=1 dotnet test APThermo.sln --no-build --filter
>         "Category!=LongRunning"`: 3191 total, 3190 passed, 0 skipped; the one failure,
>         `Protocol.Tests.DeclarationTests.EveryDeclarationUnderATickExists` on
>         `src/Performance/API.md`'s `RocketSolver.MaxThroatBisections`, predates this
>         change (present on an untouched checkout of the same commit, `src/Performance`
>         and `tests/Protocol.Tests` outside this coding task's subtree) and is the
>         coding half of the Performance/Transport hidden-defect audit's own design
>         commit (`9a6888f`), not yet landed;
>       - `dotnet test tests/Execution.Tests -c Release` (no filter): 140 of 140 (134
>         before this change plus six new facts: `ChunksStayWithinInt32OffsetsAtTableLimits`,
>         `AllCoresLayoutWorker`, `TheCpuEngineReportsTheDocumentedLayoutAtEveryProcessorCountAndResultsDoNotMove`,
>         `BadLibraryTests.ABadLibraryNamesBothPathsAndNeverReachesTheDevice`,
>         `PostLinkTests.ALogWithNulPaddingIsTrimmedOfIt`,
>         `AcceleratorChoiceTests.AHalfGivenExplicitLibraryPairIsRefused`), the
>         100 000-case sweep and the throughput tripwire included;
>       - `git status --short -- '**/Bits*.approved.txt' '**/Throughput*.approved.txt'
>         '**/PublicSurface.approved.txt'` empty: no snapshot moved;
>       - the protocol lint: 0 errors, 0 warnings.
>
>       Evidence for the "All cores" correction above (2026-09-26, on the same reference
>       machine), added to the evidence already recorded, not replacing it:
>       - `dotnet build APThermo.sln -c Release`: 0 warnings, 0 errors;
>       - `dotnet test tests/Execution.Tests -c Release` (no filter): 141 of 141 (the
>         140 already recorded plus
>         `TheAllCoresLayoutMatchesEveryProcessorCountOrTheDocumentedFallback`), the
>         100 000-case sweep and the throughput tripwire included;
>       - `APTHERMO_NO_CUDA=1 dotnet test APThermo.sln --no-build --filter
>         "Category!=LongRunning"`: 3192 total, 3191 passed, 0 skipped; the one failure is
>         the same pre-existing `DeclarationTests` fact named above, now fixed on `main`
>         at `a6bc55d` (outside this coding task's subtree, not rebased onto here on the
>         coordinator's own instruction);
>       - `git status --short -- '**/Bits*.approved.txt' '**/Throughput*.approved.txt'
>         '**/PublicSurface.approved.txt'` empty: no snapshot moved;
>       - the protocol lint: 0 errors, 0 warnings.

---

<a id="all-cores-one-multiprocessor-2026-09-26"></a>

## 2026-10-01 — from "## Acceptance criteria" — criterion F3: the first construction lost threads

Moved because `ACCEPTANCE.md` is held to 400 non-blank lines (`AGENTS.md`, §15); the criterion keeps its tick, date and evidence place there. The text as it stood:

>         ⚠ 2026-09-26: this criterion first described the construction above as "one
>         multiprocessor throughout", the warps count alone reaching the largest power of
>         two not over the count. The coordinator's review found that this loses threads
>         at every count that needs more than one multiprocessor to reach exactly: 12 → 8,
>         24 → 16, 48 → 32, 20 → 16. The construction now chosen keeps the warp size fixed
>         at 4 from 4 processors up (so 16 still reduces to (4, 4, 1)) and uses the
>         multiprocessor count, not just the warps count, to reach every multiple of 4
>         exactly, as ILGPU's own unconstrained multiprocessor argument allows. Shown red
>         once against the superseded rule: the new host-only fact failed at 12 —
>         `Assert.Equal() Failure: Values differ Expected: 12 Actual: 8` — matching the
>         coordinator's own example; reverted before committing. The superseded rule's own
>         red-once record (against `builder.CPU()`, ILGPU's fixed 16-thread
>         `CPUDevice.Default`, failing the 4-processor child process with
>         `Assert.Equal() Failure: Values differ Expected: 4 Actual: 16`) still holds for
>         the corrected rule, unchanged by this correction.

---

<a id="all-cores-ilgpu-measurements-2026-09-26"></a>

## 2026-10-01 — from "## Acceptance criteria" — criterion F3: ILGPU's CPUDevice constructor measured and the proofs

Moved because `ACCEPTANCE.md` is held to 400 non-blank lines (`AGENTS.md`, §15); the criterion keeps its tick, date and evidence place there. The text as it stood:

>         ILGPU 1.5.3's `CPUDevice` constructor was measured directly by reflection
>         (its summary records the figures): the warp size needs no upper bound and only
>         refuses 1; the warps per multiprocessor must be a power of two, and every
>         non-power-of-two value tried (3, 5, 6, 7, 9, 10, 12, 24, 48) throws
>         `ArgumentOutOfRangeException`, misnaming `numThreadsPerWarp` although the warps
>         argument is the one at fault; the multiprocessor count carries no constraint
>         ILGPU checks at all, from 2 to 1 000 000. The corrected construction uses only
>         layouts this probe confirmed the constructor accepts.
>
>         Proven under `DOTNET_PROCESSOR_COUNT` 4, 12, 16 and 64, each its own
>         `dotnet test` child process — `Environment.ProcessorCount` is read once, at
>         process start — spawned by the same test class acting as its own worker
>         (`AllCoresLayoutTests.TheCpuEngineReportsTheDocumentedLayoutAtEveryProcessorCountAndResultsDoNotMove`,
>         `AllCoresLayoutWorker`, `tests/Execution.Tests`): the reported thread count is
>         4, 12, 16 and 64 respectively, and a rocket batch's result hash (specific
>         impulse, c*, thrust coefficient over every station) is identical at every
>         count. A second, host-only fact
>         (`TheAllCoresLayoutMatchesEveryProcessorCountOrTheDocumentedFallback`) asserts
>         the layout's thread total, with no child process, at every count of 1, 2, 3, 4,
>         6, 8, 12, 16, 20, 24, 32, 48, 64 and 128: exact at every one of them except 1
>         (the ILGPU floor of 2 threads exceeds it) and 6 (not a multiple of 4, so the
>         layout falls back to 4), each asserted against its documented fallback instead
>         of equality.

---

<a id="bad-library-red-once-2026-09-26"></a>

## 2026-10-01 — from "## Acceptance criteria" — criterion F2/F3: the bad library, red once

Moved because `ACCEPTANCE.md` is held to 400 non-blank lines (`AGENTS.md`, §15); the criterion keeps its tick, date and evidence place there. The text as it stood:

>         Shown red once against the pre-fix order (device before the library,
>         unwrapped): the same fact failed —
>         `Assert.Throws() Failure: Exception type was not an exact match Expected:
>         typeof(APThermo.Execution.AcceleratorUnavailableException) Actual:
>         typeof(System.BadImageFormatException)`, its inner exception "An attempt was
>         made to load a program with an incorrect format. (0x8007000B)" — exactly the
>         audit's own finding; reverted before committing.

---

<a id="architecture-nothing-else-moves-2026-09-26"></a>

## 2026-10-01 — from "## Acceptance criteria" — criterion: every architecture, the records and the evidence run

Moved because `ACCEPTANCE.md` is held to 400 non-blank lines (`AGENTS.md`, §15); the criterion keeps its tick, date and evidence place there. The text as it stood:

>       - **Nothing else moves.** No `Bits*.approved.txt`, `Throughput*.approved.txt` or
>         `PublicSurface.approved.txt` changed
>         (`git diff 8dfe20f --stat -- '**/Bits*.approved.txt' '**/Throughput*.approved.txt'
>         '**/PublicSurface.approved.txt'` empty). The node's CUDA tests are green in
>         Release (`dotnet test tests/Execution.Tests -c Release`, no filter, from a clean
>         `bin`/`obj`): 134 of 134, the sweep and the throughput tripwire included. The
>         fast suite (`APTHERMO_NO_CUDA=1 dotnet test APThermo.sln --filter
>         "Category!=LongRunning"`, from a clean `bin`/`obj`): 3185 of 3185, none skipped
>         (3178 before this change plus the 5 inventory facts and the 2 bind-time facts).
>         The protocol lint: 0 errors, 0 warnings.
>       - **The records.** `API.md` already stated the bind-time probe (under `Engine`,
>         and in the Errors table) from the design; `CHANGELOG.md` names the fix under
>         `[Unreleased]`'s "Fixed" (the release after 0.1.0 is 0.2.0, per the Diagnostics
>         phase's binary break already recorded there).

---

<a id="bind-time-probe-implementation-2026-09-26"></a>

## 2026-10-01 — from "## Acceptance criteria" — criterion: the bind-time probe, implementation

Moved because `ACCEPTANCE.md` is held to 400 non-blank lines (`AGENTS.md`, §15); the criterion keeps its tick, date and evidence place there. The text as it stood:

>         ILGPU's own accelerator constructor does not refuse such a file at context
>         creation (it validates libdevice's content only lazily, when the post-link
>         actually compiles against it), so no substitute input was needed: a file that
>         exists but holds arbitrary text reaches the post-link at bind and fails there
>         with `NVVM_ERROR_COMPILATION`, wrapped as designed. Implemented as
>         `AcceleratorChoiceTests.AnAutoFallbackNamesThePostLinkWhenTheProbeKernelCannotBind`
>         and `...AnExplicitCudaRequestFailsWithThePostLinksOwnExceptionWhenTheProbeKernelCannotBind`,
>         green on the reference machine.

---

<a id="wrapper-inventory-implementation-2026-09-26"></a>

## 2026-10-01 — from "## Acceptance criteria" — criterion: the wrapper inventory, implementation and red-once record

Moved because `ACCEPTANCE.md` is held to 400 non-blank lines (`AGENTS.md`, §15); the criterion keeps its tick, date and evidence place there. The text as it stood:

>         Implemented as `WrapperInventoryTests` (`tests/Execution.Tests/WrapperInventoryTests.cs`,
>         5 facts, no GPU needed), over `tests/Execution.Tests/Ptx/probe.sm_89.ptx` and
>         `probe.sm_120.ptx`, generated on the reference machine with libnvvm 13.4 the same
>         day (a throwaway generator, not committed). Shown red once against the old
>         `WrapperCall` regex (`__ilgpu__nv_[A-Za-z0-9_]+`, no `call`-site or comma
>         requirement, `WrappersCalled` reading `m.Value` instead of a capture group): 3
>         of the 5 facts failed —
>         `OnSm89EveryCalledWrapperIsAlreadyDefined` and
>         `BothArchitecturesCallTheSameWrappers`, "Assert.Equal() Failure: HashSets differ
>         … Expected: [\"__nv_exp\", \"__nv_exp_param_0\", \"__nv_log\",
>         \"__nv_log_param_0\", \"__nv_log10\", ···] … Actual: [\"__nv_exp\", \"__nv_log\",
>         \"__nv_log10\", \"__nv_pow\", \"__nv_sqrt\", ···]", and
>         `NoParameterNameIsReadAsACall`, "Assert.DoesNotContain() Failure: Filter matched
>         in collection … Collection: [\"__nv_exp\", \"__nv_exp_param_0\", \"__nv_log\",
>         \"__nv_log_param_0\", \"__nv_log10\", ···]" — the parameter names read as calls,
>         exactly the defect. Reverted; a clean rebuild confirmed all 5 green again.

---

<a id="architecture-fact-implementation-2026-09-26"></a>

## 2026-10-01 — from "## Acceptance criteria" — criterion: every architecture, the fact's implementation and red-once record

Moved because `ACCEPTANCE.md` is held to 400 non-blank lines (`AGENTS.md`, §15); the criterion keeps its tick, date and evidence place there. The text as it stood:

>         Implemented as `ArchitectureTests.EveryArchitectureFromSm75UpPassesThePostLinkAndMatchesTheDevice`
>         (`tests/Execution.Tests/ArchitectureTests.cs`), reflecting over the 11 `CudaArchitecture`
>         fields from `SM_75` to `SM_121` and the 5 entry points of `Kernels`
>         (`Equilibrium`, `Rocket`, `Transport`, `Functions`, `Probe`); green on the
>         reference machine (RTX 5070 Ti, SM_120, libnvvm 13.4). `SM_75`..`SM_90` measured
>         `DefinedByIlgpu.Count > 0` and `Compiled.Count == 0` (ILGPU defined every
>         wrapper); `SM_100`..`SM_121` measured the reverse (the post-link compiled every
>         wrapper), so both paths are exercised. Every architecture's normalized PTX
>         equalled the device's own (`SM_120`'s), and the probe matched the engine's own
>         CUDA probe bit for bit and the CPU accelerator within `GpuCpuTolerances.MathUlp`
>         on every architecture.
>
>         Shown red once, reproduced directly against `LibDevicePostLink` as it stands at
>         `9c33398` (a throwaway repro compiling `Kernels.Probe` for `SM_75`, `SM_80`,
>         `SM_86`, `SM_89`, `SM_90` and calling the old `Link`, not committed): every one
>         threw `InvalidOperationException`, "the kernel calls the libdevice wrapper
>         __nv_exp_param_0, for which ILGPU 1.5.3.0 has no fragment.", exactly the message
>         this criterion predicted.

---

<a id="no-result-ignored-message-review-2026-09-26"></a>

## 2026-10-01 — from "## Acceptance criteria" — criterion: no result ignored, the first cut's message

Moved because `ACCEPTANCE.md` is held to 400 non-blank lines (`AGENTS.md`, §15); the criterion keeps its tick, date and evidence place there. The text as it stood:

>       ⚠ 2026-09-26, review: the first cut named only the call and the result
>       ("CompileProgram failed for compute_120 (…)"), losing what actually failed — that
>       it was the libdevice post-link, and which library. The message now leads with
>       "the libdevice post-link for {arch}" and names the library ("libnvvm" or "the CUDA
>       driver's") beside the call, and `ReleaseProgram`'s best-effort branch dropped the
>       throw-then-catch-the-exact-type pattern (an empty catch block) for the simpler
>       shape above: the release's own result is stored but checked only on the success
>       path, so nothing is ever thrown and swallowed.

---

<a id="criterion-no-result-ignored-implementation-2026-09-26"></a>

## 2026-10-01 — from "## Acceptance criteria" — criterion: no libnvvm or driver result is ignored, implementation and evidence

Moved because `ACCEPTANCE.md` is held to 400 non-blank lines (`AGENTS.md`, §15); the criterion keeps its tick, date and evidence place there. The text as it stood:

>       Implemented as two `ThrowIfFailed` overloads (`NvvmResult`, `CudaError`), the one
>       shape every checked call throws in: `"the libdevice post-link for {arch}: libnvvm
>       {call} returned {result}"` or `"…: the CUDA driver's {call} returned {result}"`,
>       followed by `": {log}"` where one exists (or the "log could not be read (…)" text
>       below, in its place). Every checked call of the invariant's list (`GetIRVersion`,
>       `CreateProgram`, `AddModuleToProgram`, `LazyAddModuleToProgram`, `CompileProgram`,
>       `GetProgramLog`, `GetCompiledResult`, `DestroyProgram`, `LoadModule`,
>       `DestroyModule`) now goes through one of the two overloads; the compile log is
>       read only after a failed `CompileProgram` (`ThrowCompileFailure`), and a
>       `GetProgramLog` failure of its own is folded into the compile exception's message
>       ("the log could not be read (…)") rather than raising a second exception.
>       `DestroyProgram`'s release in `CompileWrappers`'s `finally` (`ReleaseProgram`) is
>       attempted either way, but its own result is checked (and can throw) only when the
>       path before it succeeded; when an earlier call's exception is already propagating
>       through the same `finally`, the release's result is not checked at all, so it
>       cannot throw a second exception that would replace the one already in flight — no
>       catch of any kind is needed for this. `DestroyModule` in `TrialLoad` is reached
>       only after `LoadModule` succeeded, so it is always checked in the ordinary way.
>
>       Evidence, on this worktree (branch `claude/nvvm-result-codes`, on top of
>       `6d5d57e`):
>       - a repository-wide search for the old silent-discard pattern (an underscore
>         assigned to the return value of an `nvvm.` or `CudaAPI.` call) over `src` finds
>         none; this file's own history note above, which once quoted that pattern in
>         prose, was reworded to drop it, without changing its meaning — quoting the
>         pattern verbatim here would trip the same search against this paragraph;
>       - `dotnet build APThermo.sln`: 0 warnings, 0 errors;
>       - `PostLinkTests` (`tests/Execution.Tests/PostLinkTests.cs`):
>         `TheSuccessResultOfEitherKindThrowsNothing`,
>         `EveryNonSuccessNvvmResultNamesTheLibraryTheCallTheResultAndTheTarget` (a theory
>         over every value of `NvvmResult` but `NVVM_SUCCESS`, read from the enum: 9
>         cases, each asserting the message names "the libdevice post-link", "libnvvm",
>         the call, the result and the target),
>         `EveryNonSuccessCudaErrorNamesTheLibraryTheCallTheResultAndTheTarget` (the same
>         over every value of `CudaError` but `CUDA_SUCCESS`: 58 cases, asserting "the
>         libdevice post-link", "the CUDA driver", the call, the result and the target),
>         `ALogWhereOneExistsIsCarriedInTheMessage`,
>         `WithoutALogTheMessageStillNamesTheLibraryTheCallTheResultAndTheTarget`, and the
>         four pre-existing `AssertEveryWrapperDefined` facts, all green;
>       - `APTHERMO_NO_CUDA=1 dotnet test APThermo.sln --no-build --filter
>         "Category!=LongRunning"`: 3178 of 3178, none skipped (3108 before this change
>         plus the 70 new theory cases and facts above);
>       - `dotnet test tests/Execution.Tests -c Release` on the reference machine (RTX
>         5070 Ti), no filter: 126 of 126, the long-running 100 000-case sweep and the
>         throughput tripwire included (124 of the fast set plus these 2);
>       - no `Bits*.approved.txt` or `Throughput*.approved.txt` differs from `main`;
>       - the protocol lint: 0 errors, 0 warnings.

---

<a id="all-cores-cpudevice-default-2026-09-26"></a>

## 2026-10-01 — from "## Constraints" — all cores: ILGPU's fixed 16-thread CPU device

Moved because `BOOT.md` is over the §15 line limit for its kind of node; what it said stays at the pointer. The text as it stood:

>     - ⚠ `CPUDevice.Default` is one multiprocessor of four warps of four threads,
>       whatever the machine. Every "all cores" of the tree (`Options.cs`, this
>       document, the root) was true only on the 16-thread reference machine. A
>       64-thread workstation ran 16 threads, and a 4-vCPU runner oversubscribed four
>       times. The owner chose all cores over documenting 16.

---

<a id="bad-library-leaked-the-context-2026-09-26"></a>

## 2026-10-01 — from "## Constraints" — the library before the device: what the old order did

Moved because `BOOT.md` is over the §15 line limit for its kind of node; what it said stays at the pointer. The text as it stood:

>     - ⚠ ILGPU's accelerator constructor creates the CUDA context first and loads
>       libnvvm after. A libnvvm that exists but does not load threw a raw
>       `BadImageFormatException` from an explicit `Cuda` request. With `Auto` the
>       fallback reason held no path. Each attempt leaked the context already created,
>       about 190 MiB of device memory: 20 `Auto` creations lost 3 800 MiB. A failure
>       inside ILGPU's constructor after the context, for a cause this check cannot
>       foresee, still leaks: ILGPU gives no handle to release. The known cause, a bad
>       library, no longer reaches the device.

---

<a id="post-link-wrapper-inventory-2026-09-26"></a>

## 2026-10-01 — from "## Constraints" — the post-link's first stages: collecting every name, compiling unconditionally

Moved because `BOOT.md` is over the §15 line limit for its kind of node; what it said stays at the pointer. The text as it stood:

>   ⚠ 2026-09-26: stood as one sentence whose second step was "collect the distinct
>   `__ilgpu__nv_*` names from the PTX", followed by unconditional compilation and
>   insertion. That read the parameter names of ILGPU's own definitions as calls. It was
>   also a replacement, not a completion, and doubled every definition ILGPU had already
>   made (the ⚠ of the invariant "Every CUDA kernel goes through the post-link"). Rejected alternatives, measured the same day:
>   - Compile with a backend that has no `NvvmAPI`, so that ILGPU never defines the
>     wrappers: ILGPU 1.5.3 then refuses every kernel that uses the math list with an
>     `InternalCompilerException`.
>   - Cut ILGPU's definitions out of the text and insert the post-link's own: that
>     rewrites the same text into the same text, since the two are equal.

---

<a id="post-link-mixed-definitions-rationale-2026-09-26"></a>

## 2026-10-01 — from "## Constraints" — the post-link: why a kernel with some wrappers defined needs no branch of its own

Moved because `BOOT.md` is over the §15 line limit for its kind of node; what it said stays at the pointer. The text as it stood:

>   A kernel with some wrappers defined and some missing is not refused by a branch of
>   its own. ILGPU 1.5.3 compiles all of a kernel's fragments in one libnvvm program, so
>   it defines all of them or none. If a mix ever arrived, the completion would insert
>   only the missing ones. A definition both sides emitted, such as libdevice's
>   `__internal_accurate_pow`, would then be refused by the trial load with the driver's
>   "Duplicate definition" log.

---

<a id="no-result-ignored-first-cut-2026-09-26"></a>

## 2026-10-01 — from "## Invariants" — no libnvvm or driver result is ignored: what was checked before

Moved because `BOOT.md` is over the §15 line limit for its kind of node; what it said stays at the pointer. The text as it stood:

>   ⚠ 2026-09-26: until then only `CompileProgram` and `LoadModule` were checked; the
>   other calls' results were dropped, a fact the Diagnostics pass made visible when
>   IDE0058 turned the silent drops into explicit discards (an underscore assigned in
>   front of the call). A failure of, say, `AddModuleToProgram` surfaced later as a
>   compilation error with a misleading log, or not at all.

---

<a id="cuda-bound-only-when-a-kernel-runs-2026-09-26"></a>

## 2026-10-01 — from "## Invariants" — CUDA is bound only when a kernel runs on it

Moved because `BOOT.md` is over the §15 line limit for its kind of node; what it said stays at the pointer. The text as it stood:

>   ⚠ 2026-09-26: before, a CUDA session was accepted once its context existed. The
>   first failure then surfaced at the first `Run`, where `Auto` has no fallback left. A
>   post-link defect or a device whose target libnvvm refuses turned every run into an
>   exception, while `apthermo devices` showed CUDA as usable. Found by the same audit.

---

<a id="post-link-completes-not-replaces-2026-09-26"></a>

## 2026-10-01 — from "## Invariants" — every CUDA kernel goes through the post-link: completes, never replaces

Moved because `BOOT.md` is over the §15 line limit for its kind of node; what it said stays at the pointer. The text as it stood:

>   ⚠ 2026-09-26: stood "ILGPU 1.5.3's own libdevice wrapper generation is never relied
>   on (it is defective with libnvvm 12.9 and 13.3 …)". It was measured on the reference
>   machine's SM_120 only, where ILGPU always drops the wrappers. On SM_75 to SM_90 ILGPU
>   defined them itself, and the post-link had two faults:
>   - it read the definitions' parameter names (`__ilgpu__nv_exp_param_0`) as wrappers
>     called, and threw for want of a fragment;
>   - read correctly, it would have inserted a second copy, which the driver refuses
>     ("Duplicate definition").
>
>   So every CUDA run of 0.1.0 threw on every GPU older than Blackwell. Found by the
>   hidden-defect audit of 2026-09-26 (finding F1), on kernels compiled for those
>   architectures and run on the reference device. The measurements behind the new
>   wording are in the root's ⚠ of the same date.

---

<a id="criterion-linux-libdevice-discovery-2026-09-15"></a>

## 2026-10-01 — from "## Acceptance criteria" — criterion: Linux libdevice discovery

Moved because `ACCEPTANCE.md` is held to 400 non-blank lines (`AGENTS.md`, §15); the criterion keeps its tick, date and evidence place there. The text as it stood:

> - [x] 2026-09-15 (distribution phase) — Linux libdevice discovery, added for the root's
>       Platform constraint (`cf87211`): `LibDeviceLocator` branches on
>       `OperatingSystem.IsWindows()` / `IsLinux()` and, on Linux, tries `CUDA_PATH`, then
>       `CUDA_HOME`, then `/usr/local/cuda`, then `/usr/local/cuda-*` newest first, each
>       root's `nvvm/lib64/libnvvm.so` paired with `nvvm/libdevice/libdevice.10.bc`; on
>       any other OS no discovery runs and the CPU accelerator is used. Covered by
>       `tests/Execution.Tests/LibDeviceDiscoveryTests.cs`, driven through the internal
>       seam (`LibDeviceLocator.Locate(EngineOptions, LocatorPlatform, Func<string,
>       string?>, string)`) so both platforms and both Windows dll layouts (12.x
>       `nvvm\bin`, 13.x `nvvm\bin\x64`) are exercised from one host OS, over fake
>       toolkit trees under a temp directory: explicit paths win and are tried first;
>       an unsupported platform does no discovery; `CUDA_PATH` before the toolkit
>       directories on Windows and before `CUDA_HOME`, the fixed root and the versions
>       on Linux; both platforms order their versioned toolkit directories by parsed
>       `Version`, newest first (proven against a case where numeric and alphabetical
>       order disagree, `v13.3`/`v9.0` and `cuda-13.3`/`cuda-9.0`); a library present
>       without its bitcode is passed over for the next root; a root named twice (by
>       `CUDA_PATH` or `CUDA_HOME` repeating an already-tried directory) is tried once —
>       12 facts, `dotnet test tests/Execution.Tests --filter
>       "FullyQualifiedName~LibDeviceDiscoveryTests"`, all green. The ordering fact was
>       shown red once and reverted (AGENTS.md §13): `VersionedDirectories`'s
>       `OrderByDescending` flipped to `OrderBy` reddened both
>       `WindowsOrdersTheToolkitDirectoriesNewestVersionFirst` and
>       `LinuxOrdersTheVersionedDirectoriesNewestFirst`, reverted before
>       committing. The unavailable-accelerator message and the `EngineOptions` doc
>       comments now name the platform's library instead of `nvvm64_40_0.dll`
>       unconditionally (`LibDeviceLocator.LibraryFileName`); `AcceleratorChoiceTests`
>       unchanged in behaviour, its one hard-coded `nvvm64_40_0.dll` assertion now reads
>       the same property. `LibDevicePostLink` and every other file of this node were
>       checked for Windows-only assumptions (path separators, `.dll` literals,
>       case-insensitive comparisons) and none were found outside `LibDeviceLocator`,
>       `AcceleratorChoice`'s message and `Options.cs`'s doc comment, all covered above.
>       Verified on the reference machine, CUDA present: build clean, 0 warnings;
>       `dotnet test tests/Execution.Tests --filter "Category!=LongRunning"`, 53 of 53
>       green (41 pre-existing plus these 12), including the CUDA-category tests, so
>       real discovery still finds the installed toolkit through the unchanged default
>       `Locate(EngineOptions)` entry point; the whole solution's fast set green (3047
>       tests, 0 failed, `Category!=LongRunning`, CUDA-category tests exercised since
>       the machine has a device); `protocol_lint` 0 errors, 0 warnings; every
>       `Bits.approved.txt` and the surface snapshot unchanged (the seam is internal, no
>       public type added).

---

<a id="criterion-compile-against-libdevice-merged-2026-09-15"></a>

## 2026-10-01 — from "## Acceptance criteria" — criterion: CompileAgainstLibdevice merged back

Moved because `ACCEPTANCE.md` is held to 400 non-blank lines (`AGENTS.md`, §15); the criterion keeps its tick, date and evidence place there. The text as it stood:

> - [x] 2026-09-15 — `LibDevicePostLink.CompileAgainstLibdevice`, extracted from
>       `CompileWrappers` in `23ccc1d` "bringing its nesting back to 3", took every
>       parameter and local of its caller (six, the root's limit) and existed only to
>       hold the `unsafe`/`fixed` block: the nesting measure counts only
>       `if`/`for`/`foreach`/`while`/`do`/`switch`/`try`, so the extraction bought
>       nothing the measure itself cares about. Merged back into one `CompileWrappers`
>       (create the program, build the options, add both modules under one `fixed`,
>       compile, log and throw, read the compiled result, destroy the program in
>       `finally`); `NvvmOptions`, which owns the unmanaged allocations, is unchanged.
>       The merged method now satisfies both
>       `ShapeTests.NoControlFlowNestsDeeperThan3` and
>       `ShapeTests.NoMethodSpansMoreThan60Lines`. Found by the repair review
>       (R-Execution-2). Verified on the reference machine, `APTHERMO_NO_CUDA` unset:
>       `tests/Execution.Tests/ProbeKernelTests.CudaMatchesTheCpuAcceleratorWithinTheUlpBoundForEveryFunction`
>       green, exercising this exact method on real hardware (the probe kernel's
>       wrappers compiled by it, linked, run, and matching the CPU accelerator within
>       the ULP bound).

---

<a id="criterion-dead-run-timer-2026-09-15"></a>

## 2026-10-01 — from "## Acceptance criteria" — criterion: Engine.ProbeMath's dead RunTimer

Moved because `ACCEPTANCE.md` is held to 400 non-blank lines (`AGENTS.md`, §15); the criterion keeps its tick, date and evidence place there. The text as it stood:

> - [x] 2026-09-15 — `Engine.ProbeMath`'s dead `RunTimer` (a `KernelCache.Get` overload
>       once needed it; `a2a1d6e`'s `out warmUp` overload made it unreachable, and the
>       two lines allocating and discarding one stayed) is gone: `out var warmUp` is
>       `out _`. `RunTimer` was the method's only use of that type, so `Engine`'s
>       efferent coupling fell from 26 to 25, the figure the Shape exceptions row above
>       now carries; the Structure row's own wording is unchanged, since it already
>       described `ProbeMath` correctly. Found by the repair review (R-Execution-1).
>       Verified: the CPU-accelerator fast suite green (`APTHERMO_NO_CUDA=1`), the
>       re-measured Ce confirmed by
>       `ShapeTests.EveryShapeExceptionIsMeasuredAndStillNeeded`, which holds
>       the `Engine` row at 25 on the merged tree.

---

<a id="decomposition-shape-rows-2026-09-15"></a>

## 2026-10-01 — from "## Acceptance criteria" — criterion of the decomposition: the figures described 42efbe7

Moved because `ACCEPTANCE.md` is held to 400 non-blank lines (`AGENTS.md`, §15); the criterion keeps its tick, date and evidence place there. The text as it stood:

>       ⚠ 2026-09-15: these figures described the code at `42efbe7`, before `e453063`
>       reformatted `RocketPipeline.Run`'s two wide constructor calls onto named
>       arguments; `## Shape exceptions` now holds ten rows: six efferent-coupling
>       rows, the four pipelines among them, and the constructors of
>       `RocketBatchViews`, `EquilibriumBatchViews`, `RocketBatchResult` and
>       `EquilibriumBatchResult`. `RocketPipeline.Run`'s size afterward is
>       `ShapeTests.NoMethodSpansMoreThan60Lines`'s to state; this node records
>       no line figure of its own. Found by the repair review (R-Execution-5).

---

<a id="views-structs-need-not-be-public-2026-09-15"></a>

## 2026-10-01 — from "## Structure" — the views structs keep their constructors: the public claim

Moved because `BOOT.md` is over the §15 line limit for its kind of node; what it said stays at the pointer. The text as it stood:

>   ⚠ 2026-09-15 (distribution phase): this bullet said the two views structs "are
>   kernel parameter descriptors ILGPU requires to be public". Wrong: ILGPU 1.5.3 needs
>   only `[assembly: InternalsVisibleTo("ILGPURuntime")]` on the declaring assembly, not
>   a public type (the API review of that day, section 3, fixed in `9036c6a`,
>   ran the failure and the fix on the CPU accelerator and on CUDA; the claim entered
>   with `f2e5de7` and `53ec9fb` on 2026-09-12 from an observed failure that never tried
>   the grant). All four views structs are internal now, with that grant on
>   `APThermo.Execution.csproj`; the reason for the declared parameter-count exception
>   is unchanged.

---

<a id="fallback-says-why-2026-09-15"></a>

## 2026-10-01 — from "## Structure" — the fallback says why, and when the reason is null

Moved because `BOOT.md` is over the §15 line limit for its kind of node; what it said stays at the pointer. The text as it stood:

> - **The fallback says why.** `Auto` keeps falling back to the CPU accelerator, and the
>   reason no longer dies in a discarded exception: `AcceleratorInfo` gains
>   `CudaSkippedBecause` (null when CUDA was bound or the options asked for the CPU), the
>   message of the failure that turned the choice, the forbidding variable included, with
>   the paths tried where they apply. A contract change, recorded in `API.md` with its ⚠,
>   the snapshot moving in the same commit; the command line prints it in the `devices`
>   listing and in every document's `run.accelerator` (a later change of that node).
>
>   ⚠ 2026-09-15: this bullet, `API.md` and `Options.cs` read "null when CUDA was not
>   tried or was bound" / "null when CUDA was bound or never tried". With
>   `APTHERMO_NO_CUDA=1` and `Auto`, CUDA is not skipped upfront: `AcceleratorChoice.Decide`
>   still calls into `Cuda`, which throws immediately without touching any CUDA API, and
>   the caught failure becomes a non-null reason (`AcceleratorChoiceTests`, the variable's
>   own case). "No CUDA API is touched" (this document's invariants) is true of the driver,
>   not of whether a reason is recorded; the only case with no reason at all is
>   `AcceleratorKind.Cpu`, where `Cuda` is never called because CUDA was never asked for.
>   Found by the repair review (R-Execution-6); the three places now read "null when CUDA
>   was bound or the options asked for the CPU".

---

<a id="child-nodes-decision-2026-09-15"></a>

## 2026-10-01 — from "## Structure" — the child-nodes decision: Chunks passes, LibDevice stays, the ILGPU version constant stays

Moved because `BOOT.md` is over the §15 line limit for its kind of node; what it said stays at the pointer. The text as it stood:

> Decided 2026-09-15 (the child-nodes phase; root `BOOT.md`, 0aa7e60): a cluster of this
> node earns its own child directory, `BOOT.md` and `API.md` when the rest of the node
> reaches it through a contract narrower than its code, it has a reason of its own to
> change, and it holds about five types or more (root `BOOT.md`, the child-nodes
> decision), no public type moving into the child namespace.
>
> - **`Chunks/` passes.** `Chunk`, `ChunkPlan`, `ChunkBuffer<T>`, `ChunkBuffers`,
>   `ChunkTransfer` and `IChunkBuffer` — six internal types — become
>   `APThermo.Execution.Chunks`. The rest of this node reaches
>   them through `ChunkPlan.For`/`.Chunks()`, `ChunkBuffers`'s declaration methods and
>   `ChunkBuffer<T>.View`; `IChunkBuffer`, `ChunkTransfer` and the `Chunk` record are
>   never named outside the cluster. Its reason to change — the chunking and transfer
>   policy — is its own, distinct from the kernel loop (`BatchRun`, staying here) and
>   the accelerator session it runs on. Its own `BOOT.md` and `API.md` hold the
>   contract; this row of the table above points to them instead of repeating them.
> - **`LibDevice/` fails, and stays here.** `LibDeviceLocator` and `LibDevicePostLink`
>   hold three types between them (the two named classes and `LibDevicePostLink`'s
>   private nested `NvvmOptions`), short of "about five types or more"; splitting three
>   types into a child for a contract of one method each (`Locate`, `Link`) would add a
>   directory and a document pair without narrowing anything. They stay as two files of
>   this node's own directory, unchanged by this phase.
> - **The ILGPU version constant stays on `LibDevicePostLink`.** `ExpectedIlgpuVersion`
>   names the ILGPU release `LibDevicePostLink`'s own reflection (`AssertIlgpu`) was
>   written against; every reader of it — the lazy member lookup inside the same type,
>   `AcceleratorChoice`'s `AcceleratorInfo.IlgpuVersion` field, and the tests that prove
>   the assertion fails loudly on a mismatch — is either inside the post-link mechanism
>   or reads it as a diagnostic string, not as a general engine constant a second type
>   would need to own. Nothing moves.

---

<a id="engine-and-mathprobe-internal-2026-09-15"></a>

## 2026-10-01 — from "## Structure" — structure: Engine and MathProbe public composition types

Moved because `BOOT.md` is over the §15 line limit for its kind of node; what it said stays at the pointer. The text as it stood:

> ⚠ 2026-09-15 (distribution phase): `Engine` and `MathProbe` were the node's only
> public composition types, per the table below; the API review of that day
> (its section 4, findings D1 and F1, fixed in `9036c6a`) found no consumer scenario for
> either. `Engine` became internal and `AcceleratorProbe` (`AcceleratorProbe.cs`)
> replaces it on the package surface for the two questions a consumer actually asked of
> it: what a set of `EngineOptions` binds to (`Describe`), and whether the environment
> forbids CUDA (`CudaForbidden`); `Solver.Create` (`Problems`) keeps creating its own
> `Engine` internally. `MathProbe`, the eight batch and batch-result types, `UploadedTables`
> and `RunTimings` became internal with it. `APThermo.Execution.csproj` grants
> `InternalsVisibleTo` to `Problems` (the only `src` node whose `## Dependencies` names
> this one; `Cli` goes through `AcceleratorProbe` and receives no grant), to
> `Execution.Tests` and `Problems.Tests`, to `Benchmarks`, and to `ILGPURuntime` for the
> four kernel-parameter views structs (`Kernels.cs`'s own ⚠ below).

---

<a id="libdevice-discovery-linux-2026-09-15"></a>

## 2026-10-01 — from "## Constraints" — libdevice discovery order: Windows only

Moved because `BOOT.md` is over the §15 line limit for its kind of node; what it said stays at the pointer. The text as it stood:

>   ⚠ 2026-09-15 (distribution phase): this bullet named only the Windows roots and
>   `nvvm64_40_0.dll`, matching the root's Platform constraint before `cf87211` made
>   Linux x64 a supported platform, CUDA included, and fixed the discovery order for it.
>   `LibDeviceLocator` now branches on the platform; every other stage of discovery and
>   of the post-link is unchanged, since the root constraint restricts the platform
>   split to library discovery paths and file names.

---

<a id="dependencies-libnvvm-on-linux-2026-09-15"></a>

## 2026-10-01 — from "## Dependencies" — dependencies: libnvvm named for Windows only

Moved because `BOOT.md` is over the §15 line limit for its kind of node; what it said stays at the pointer. The text as it stood:

> ⚠ 2026-09-15 (distribution phase): stood "libnvvm (`nvvm64_40_0.dll`)", naming the
> Windows file only, before the root's Platform constraint (`cf87211`) added Linux as a
> supported platform, CUDA included. Linux ships the same library as `libnvvm.so`; the
> line now names both.

---

<a id="no-cuda-type-ilgpu-naming-2026-09-15"></a>

## 2026-10-01 — from "## Invariants" — no CUDA type leaves this node: the ILGPU-naming sentence dropped

Moved because `BOOT.md` is over the §15 line limit for its kind of node; what it said stays at the pointer. The text as it stood:

>   ⚠ 2026-09-15 (distribution phase): this invariant went on to say the public surface
>   "names ILGPU only through this node's own types and, in the kernel parameter
>   structs, ILGPU's `ArrayView`". The API review of that day
>   (fixed in `9036c6a`) demoted `Engine`, the batch types and the four
>   views structs into the tree contract; the package surface (`AcceleratorKind`,
>   `EngineOptions`, `AcceleratorInfo`, `AcceleratorUnavailableException`,
>   `AcceleratorProbe`) now names no ILGPU type at all, so the second sentence no longer
>   describes anything and is dropped rather than corrected in place.

---

<a id="criterion-wide-constructors-named-2026-09-14"></a>

## 2026-10-01 — from "## Acceptance criteria" — criterion: the wide constructors are created with named arguments

Moved because `ACCEPTANCE.md` is held to 400 non-blank lines (`AGENTS.md`, §15); the criterion keeps its tick, date and evidence place there. The text as it stood:

> - [x] 2026-09-14 — Every creation of the node's four wide constructors names its
>       arguments (`RocketBatchViews`, `EquilibriumBatchViews`, `RocketBatchResult`,
>       `EquilibriumBatchResult`; the decision "The views structs keep their
>       constructors"), the protocol tests node's named-construction fact green once it
>       exists; the emitted kernels unchanged, the tests node's fast set green on the CPU
>       accelerator and on CUDA. A scan of every `new T(…)` and `T x = new(…)` in `src/`
>       and `tests/` (a script outside the tree) finds the four sites of the node's types,
>       in `RocketPipeline` and `EquilibriumPipeline`, every argument named; the build of
>       `Execution` after the change carries the IL of the build before it, method by
>       method, kernels included, so no argument binds to another parameter; the fast set
>       green with `APTHERMO_NO_CUDA=1` (41 tests on the CPU accelerator) and without it
>       (the same 41 on CUDA). The fact,
>       `ShapeTests.EveryWideConstructorIsCalledWithNamedArguments`, is designed
>       and not yet written; it takes over as the evidence when it is.

---

<a id="decomposition-nesting-not-measured-2026-09-14"></a>

## 2026-10-01 — from "## Acceptance criteria" — criterion of the decomposition: nesting was not measured

Moved because `ACCEPTANCE.md` is held to 400 non-blank lines (`AGENTS.md`, §15); the criterion keeps its tick, date and evidence place there. The text as it stood:

>       ⚠ 2026-09-14: this criterion's "no type or method of the node above the root's
>       code-shape limits" was evidenced only by `python inventory.py .`'s line counts
>       (type and method lines, plus the two declared Ce exceptions), not by nesting.
>       The protocol tests node's own measurement the same day found
>       `LibDeviceLocator.Locate` nesting 4 deep: the `if (File.Exists(bitcode))` inside
>       the `if (File.Exists(dll))` inside two `foreach` loops, over the root's limit of
>       3. `5e3a24b` turns the inner check into a guard clause
>       (`if (!File.Exists(dll)) continue;`) and brings `Locate` to depth 3, examining
>       the same paths in the same order.

---

<a id="criterion-decomposition-2026-09-14"></a>

## 2026-10-01 — from "## Acceptance criteria" — criterion: the decomposition of 2026-09-14

Moved because `ACCEPTANCE.md` is held to 400 non-blank lines (`AGENTS.md`, §15); the criterion keeps its tick, date and evidence place there. The text as it stood:

> - [x] 2026-09-14 — The decomposition of 2026-09-14 (`## Structure`): no type or method
>       of the node above the root's code-shape limits, the declared exceptions being
>       the four views structs' constructors and `Engine`'s and `Kernels`' Ce, both
>       named in `## Structure`; covered by the protocol tests node's `ShapeTests`, all
>       ten facts green at `62cd99e` — the public surface changed only by
>       `AcceleratorInfo.CudaSkippedBecause`, in `c10ab0e` alone
>       (`git diff 6af23b1..HEAD -- tests/Protocol.Tests/PublicSurface.approved.txt`:
>       one line added, that property; `Protocol.Tests.SurfaceTests` green against it
>       unchanged since); `BatchTests.ChunkingAndRepetitionDoNotChangeABit`, the
>       probe, species-function and accelerator-choice tests green
>       (`APThermo.Execution.Tests.dll`: 41 passed); the fast
>       suite of the whole solution green (`dotnet test
>       APThermo.sln --filter "Category!=LongRunning"` with
>       `APTHERMO_NO_CUDA=1`: 2147 passed, 0 failed, 0 skipped). The CUDA sweep and the
>       throughput benchmark are the orchestrator's to run once at the end, after the
>       merge, on the reference machine (not run from this worktree).

---

<a id="review-decision-unreachable-checks-2026-09-14"></a>

## 2026-10-01 — from "## Structure" — decision of the review of 2026-09-14: the unreachable checks go

Moved because `BOOT.md` is over the §15 line limit for its kind of node; what it said stays at the pointer. The text as it stood:

> - **The unreachable checks go.** Every array of a batch is assigned once in its
>   constructor from one count, so "arrays of inconsistent lengths" cannot happen; the
>   four branches and the row of `API.md` go, replaced by the sentence that the
>   constructor guarantees it. The element- and species-count checks stay.

---

<a id="views-structs-exception-claim-2026-09-14"></a>

## 2026-10-01 — from "## Structure" — the views structs keep their constructors: the first claim

Moved because `BOOT.md` is over the §15 line limit for its kind of node; what it said stays at the pointer. The text as it stood:

>   ⚠ 2026-09-14: this bullet stood "`RocketBatchViews` (17 parameters),
>   `EquilibriumBatchViews` (12) and the other two are the kernel parameter descriptors
>   ILGPU requires to be public; … They are this node's declared exception to the
>   parameter rule; the pipelines are their only callers and fill them by name". Two
>   claims were wrong, found by a scan of every construction site after the coupling
>   measurement: `SpeciesFunctionBatchViews` and `TransportBatchViews` take six
>   parameters and need no exception, and the pipelines passed the views and the
>   results by position, not by name. The result constructors, over the rule as well,
>   were not declared.

---

<a id="review-decisions-guard-and-chunk-bound-2026-09-14"></a>

## 2026-10-01 — from "## Structure" — decisions of the review of 2026-09-14: the missing-definition guard, the chunk bound

Moved because `BOOT.md` is over the §15 line limit for its kind of node; what it said stays at the pointer. The text as it stood:

> - **The missing-definition guard names the wrapper.** The check parses the wrapper text
>   libnvvm returned for its `.func` definitions and compares the set with the names the
>   kernel calls; it is testable without a GPU by handing it a wrapper body with one
>   definition removed, and the tests node does exactly that.
> - **The chunk bound counts every buffer.** `ChunkBuffers` sums the per-case strides it
>   declares, so `ScratchBytes` bounds the device bytes of a chunk by construction (until
>   now only the scratch and the moles were counted); results do not depend on chunking
>   (Invariants), so no result moves. `ScratchBytes` must be positive, like `ChunkSize`.

---

<a id="probe-stride-count-2026-09-14"></a>

## 2026-10-01 — from "## Structure" — the MathProbe row: FunctionCount as a const

Moved because `BOOT.md` is over the §15 line limit for its kind of node; what it said stays at the pointer. The text as it stood:

> ⚠ 2026-09-14: this row first read "`FunctionCount` is the constant the kernel strides by" (F-EX-07's own
> wording: `public const int FunctionCount = 10;`), which would have turned `FunctionCount` from a property
> into a `const` field — a second public-surface change beyond `AcceleratorInfo.CudaSkippedBecause`, which
> the coding task reserves that change for alone. Confirmed red-handed by running
> `Protocol.Tests.SurfaceTests` against the literal change: it failed, naming exactly this member
> (`approved 'static Int32 FunctionCount { get; }', actual 'const Int32 FunctionCount = 10'`). `FunctionCount`
> stays the public property (contract truly unchanged); an `internal const int StrideCount = 10` was added
> beside it for the kernel to stride by (a `const` inlines into kernel-compatible code, a property touching
> the managed string array `Functions` does not), and
> `ProbeKernelTests.TheKernelsStrideConstantMatchesTheFunctionList` asserts `StrideCount ==
> FunctionCount` so the two cannot drift silently.

---

<a id="reference-figures-2026-09-12"></a>

## 2026-10-01 — from "## Constraints" — reference figures of the first measurements

Moved because `BOOT.md` is over the §15 line limit for its kind of node; what it said stays at the pointer. The text as it stood:

> - Reference figures on the reference machine, measured 2026-09-12: the probe of the
>   math list within 4 ULP of the CPU accelerator over 26 decades (3 measured in the
>   first spike); the 100 000-case rocket sweep (4 stations, 11 species, shifting
>   equilibrium) in 0.15–0.17 s on CUDA (0.10 s of it in the kernel) against 9.5 s on
>   the CPU accelerator with 16 threads, 56–65×. These bound expectations; they are
>   not requirements.

---

<a id="kernels-species-function-batch-2026-09-12"></a>

## 2026-10-01 — from "## Constraints" — kernels: the species-function batch added

Moved because `BOOT.md` is over the §15 line limit for its kind of node; what it said stays at the pointer. The text as it stood:

>   ⚠ 2026-09-12: the species-function batch was not in the sketch; the front door needs
>   the reactant enthalpies at their temperatures from the tree's one implementation of
>   the polynomials, and that implementation runs only over accelerator memory.

---

<a id="batch-layout-scratch-bound-2026-09-12"></a>

## 2026-10-01 — from "## Constraints" — batch layout: a chunk bounded by the case count only

Moved because `BOOT.md` is over the §15 line limit for its kind of node; what it said stays at the pointer. The text as it stood:

>   ⚠ 2026-09-12: the sketch bounded a chunk by the case count only. The transport
>   scratch is 4·M² + E·M + 8·M doubles per station with M = 40, about 43 KB, so a
>   chunk of 16 384 stations would take 700 MB; the memory bound was added, and the
>   rocket chunk counts its per-station moles in the same bound.

---

<a id="post-link-target-and-trial-load-2026-09-12"></a>

## 2026-10-01 — from "## Constraints" — the post-link's sketch: a fixed target and no trial load

Moved because `BOOT.md` is over the §15 line limit for its kind of node; what it said stays at the pointer. The text as it stood:

>   ⚠ 2026-09-12: the sketch compiled the wrappers for a fixed `compute_80`, "the PTX
>   target ILGPU 1.5.3 emits". That is what ILGPU emits for the reference machine's
>   SM_120 device, but it is ILGPU's choice per device, so the post-link reads the
>   target from the PTX instead; a mismatch between the wrappers and the kernel would
>   otherwise be silent until the driver refuses the module. The sketch also had no
>   trial load: the first attempt loaded the linked PTX only inside ILGPU, and a
>   refusal surfaced as a bare `CUDA_ERROR_INVALID_PTX` without the driver's log; the
>   trial load failed with `CUDA_ERROR_INVALID_CONTEXT` from xunit's worker threads
>   until the accelerator was bound to the calling thread first.

---
