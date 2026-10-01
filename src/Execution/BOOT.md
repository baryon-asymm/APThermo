# BOOT.md — Execution

## Purpose

Runs the per-case numerical programs of `Equilibrium`, `Performance` and `Transport`
over batches of cases on an accelerator: the ILGPU context and accelerator choice
(CUDA when available and allowed, otherwise the CPU accelerator), the upload of the
tables, the batch buffers in structure-of-arrays layout, the kernel entry points, the
chunking of large batches, and the loading of kernels on CUDA with the libdevice
wrappers linked by this node. It is the only node that knows CUDA exists, so that every
numerical node stays testable without it.

## Invariants

- **No CUDA type leaves this node.** `ILGPU.Runtime.Cuda` appears in no signature.

  ⚠ 2026-09-15: was a second sentence on ILGPU types named, now dropped (none on the
  surface) → HISTORY.md#no-cuda-type-ilgpu-naming-2026-09-15
- **The same kernels everywhere.** A kernel is one static entry point per program; it
  is loaded on the CPU accelerator and on CUDA from the same method; there is no
  accelerator-specific numerical code.
- **Every CUDA kernel goes through the post-link, which completes it.** ILGPU 1.5.3
  defines the libdevice wrappers itself for `compute_75` to `compute_90` and silently
  drops them for `compute_100` and newer (the root's ILGPU constraint). The post-link
  compiles and inserts only the wrappers a kernel calls and does not define, and none
  when none is missing. Every kernel that calls a wrapper is loaded once as a trial on
  either path; one that still calls an undefined wrapper is refused at load, the error
  naming it. Either path yields the same program: the kernels ILGPU completes equal
  those the post-link completes, as PTX text, up to ILGPU's generated names and the
  `.target` line (the architecture fact, Acceptance criteria).
  ⚠ 2026-09-26: was "ILGPU's wrapper generation is never relied on", now the post-link
  completes the dropped ones → HISTORY.md#post-link-completes-not-replaces-2026-09-26
- **CUDA is bound only when a kernel runs on it** (2026-09-26). The choice accepts a
  CUDA session only after the math probe kernel, which calls every wrapper of the math
  list, has been compiled, post-linked and loaded on its device: `Engine.Create` and
  `AcceleratorProbe.Describe` never report a CUDA device on which no kernel can load.
  ⚠ 2026-09-26: was a CUDA session accepted once its context existed, now only after
  the probe kernel loaded → HISTORY.md#cuda-bound-only-when-a-kernel-runs-2026-09-26
- **No libnvvm or driver result is ignored** (2026-09-26). The post-link checks the
  result of every call it makes into libnvvm (`GetIRVersion`, `CreateProgram`,
  `AddModuleToProgram`, `LazyAddModuleToProgram`, `CompileProgram`, `GetProgramLog`,
  `GetCompiledResult`, `DestroyProgram`) and into the CUDA driver (`LoadModule`,
  `DestroyModule`). A result other than success is an `InvalidOperationException` naming
  the post-link, the target `compute_XX`, the failing library (libnvvm or the CUDA
  driver), its call and the result code, with the compiler's or the driver's log where
  one exists (`API.md`, Errors).
  - If reading the log of a failed compilation fails too, the compilation's exception
    still propagates and says the log could not be read, naming that result.
  - `DestroyProgram` and `DestroyModule` are checked only when the path before them
    succeeded; after an earlier failure the earlier exception propagates unchanged and
    the release is best-effort, so a cleanup failure never hides the cause.
  - One internal method turns a result into the exception, so the message has one shape.
    It is unit-tested on the CPU with every non-success value of `NvvmResult` and a
    failing `CudaError`; the success path is proven by the CUDA tests of this node,
    which must stay green with no bit or throughput record moving.
  ⚠ 2026-09-26: was only `CompileProgram` and `LoadModule` checked, now the result of
  every libnvvm and driver call → HISTORY.md#no-result-ignored-first-cut-2026-09-26
- **The wrapper list equals the root's math list.** The post-link provides wrappers
  for exactly the `System.Math` functions the root allows; a probe kernel using each
  of them loads and matches the CPU accelerator within the tolerance table.
- **The accelerator is explicit in the result**: every batch result names the
  accelerator that produced it (kind, device name, ILGPU version, libnvvm and
  libdevice paths or none) and carries the timings of the run.
- **Deterministic batches.** No atomics, no reductions, no shared memory: each case
  writes only its own slots, so a batch result is bit-identical between two runs on
  the same accelerator and does not depend on the chunking.
- **CUDA can be forbidden.** With the environment variable `APTHERMO_NO_CUDA=1` or the
  option `AcceleratorKind.Cpu`, no CUDA API is touched at all.

## Dependencies

- [Thermo](../Thermo/API.md) — species table arrays and view, `MixtureState`, `CaseStatus`.
- [Equilibrium](../Equilibrium/API.md) — the equilibrium solver, its scratch layout and result views.
- [Performance](../Performance/API.md) — the rocket solver and its descriptors.
- [Transport](../Transport/API.md) — the transport table and evaluation.

Outside the tree: ILGPU 1.5.3 (NuGet); for CUDA an NVIDIA GPU of compute capability 7.5
or newer (2026-09-26, the root's range), an NVIDIA driver with CUDA 12.8 or
newer, libnvvm (`nvvm64_40_0.dll` on Windows, `libnvvm.so` on Linux) and
`libdevice.10.bc` from a CUDA Toolkit 12.8 or newer.

⚠ 2026-09-15: was libnvvm named as `nvvm64_40_0.dll` only, now also `libnvvm.so` on
Linux → HISTORY.md#dependencies-libnvvm-on-linux-2026-09-15

## Constraints

Inherited from the root ([BOOT.md](../../BOOT.md)). In addition:

- **Accelerator choice** (`AcceleratorKind.Auto`): CUDA if `APTHERMO_NO_CUDA` is not
  `1`, libnvvm and libdevice are found, the device at the requested index exists, the
  context and accelerator can be created, and the math probe kernel post-links and loads
  on the device (the invariant "CUDA is bound only when a kernel runs on it"); otherwise
  the CPU accelerator with all cores. `AcceleratorKind.Cuda` fails instead of falling
  back and names what was missing, with every path tried; `AcceleratorKind.Cpu` never
  looks for CUDA. When the probe fails, with `Auto` its failure is the fallback reason
  in `CudaSkippedBecause`, with the post-link's message; with `Cuda` it is an
  `AcceleratorUnavailableException` carrying the post-link's exception as its inner
  exception; in both cases the session is disposed before the choice returns. The probe
  kernel is released at once (its cost: HISTORY.md#choice-probe-cost-2026-10-01).
- **libdevice discovery order**: the explicit pair, then the platform's toolkit roots
  → [LibDevice/BOOT.md](LibDevice/BOOT.md)
- **The post-link**, the one place in the tree that knows ILGPU internals, and its
  stages → [LibDevice/BOOT.md](LibDevice/BOOT.md)
- **Batch layout**: structure of arrays for inputs and outputs; the case index is the
  thread index; per-case scratch is a slice of a batch-sized buffer laid out by the
  numerical nodes' `ScratchLayout` and `TransportLayout`; the chunking of a batch, its
  bytes bound and its device buffers: [Chunks/BOOT.md](Chunks/BOOT.md)
- **Kernels**: one entry point per program (`Equilibrium`, `Rocket`, `Transport`,
  and `Functions` for the species functions of `Thermo` at given temperatures) and
  the `Probe` of the root's math list; each entry point does nothing but slice the
  views for its case and call the numerical node. The transport kernel takes a plain
  batch of stations, built by the batch type's factories (`API.md`, Batches) →
  HISTORY.md#kernels-transport-batch-retelling-2026-10-01
  ⚠ 2026-09-12: was no species-function batch, now the `Functions` entry point →
  HISTORY.md#kernels-species-function-batch-2026-09-12
- **Warm-up**: kernel compilation and post-link happen on the first run of a program
  per engine and are cached for the engine's lifetime; the time is reported as the
  run's `WarmUp`, separately from the upload, kernel and download times.
- **Host-side errors are exceptions** (missing libdevice, ILGPU version mismatch,
  inconsistent batches, a refused PTX, out-of-memory); per-case failures are statuses
  in the output arrays.
- Reference figures of 2026-09-12 (the probe within 4 ULP of the CPU accelerator; the
  100 000-case sweep 56 to 65 times faster on CUDA; expectations, not requirements) →
  HISTORY.md#reference-figures-2026-09-12

- **The audit's findings F2 and F3 and its observations** (the hidden-defect audit of
  2026-09-26; decided that day, F3 by the owner):
  - **The library before the device (F2).** The choice loads libnvvm and asks its IR
    version (`NvvmAPI.Create`, `GetIRVersion`), and reads the bitcode, before it
    creates any CUDA context. The session keeps that binding, instead of a second
    `NvvmAPI.Create` after the accelerator. A failure there is an
    `AcceleratorUnavailableException` naming `[dll, bitcode]`, as `CudaContext` already
    does for the context. `CreateCudaAccelerator` is wrapped the same way, so every
    bind failure has the documented exception type. With `Auto` it becomes the
    fallback reason, with its paths.
    - ⚠ 2026-09-26: was the context created before libnvvm was loaded, now the library
      first → HISTORY.md#bad-library-leaked-the-context-2026-09-26
  - **All cores (F3).** The CPU accelerator runs `Environment.ProcessorCount` threads,
    through a `CPUDevice` sized for it rather than ILGPU's predefined 16-thread device.
    The layout rounds down to the nearest multiple of 4 not above the count, and the
    choice is documented where it is made. On the reference
    machine the count is 16, the layout is today's, and no throughput record moves.
    Results do not depend on the thread count (Invariants: deterministic batches).

    ⚠ 2026-09-28: was the layout rounded for the warp layout, now to keep (4, 4, 1)
    → HISTORY.md#all-cores-layout-reason-2026-09-28
    - ⚠ 2026-09-26: was `CPUDevice.Default` (all cores on a 16-thread machine only), now
      sized from `ProcessorCount` → HISTORY.md#all-cores-cpudevice-default-2026-09-26
  - **Observations.** `Engine.Upload` disposes the buffers it already uploaded when a
    later upload fails. A half-given explicit path pair (`LibNvvmPath` without
    `LibDevicePath`, or the reverse) is an `ArgumentException` at `Create` naming the
    missing option. The trimming of a driver or libnvvm log is
    [LibDevice/BOOT.md](LibDevice/BOOT.md)'s → HISTORY.md#audit-observations-2026-10-01

- **Every CUDA context of a process binds under WSL** (2026-09-27): the workaround
  and its rule → [LibDevice/BOOT.md](LibDevice/BOOT.md)

- **Audit fixes of 2026-09-28** (the second hidden-defect audit, Execution findings F1
  and F2 and observations 1 to 8; the guards part's F7, F8 and O2).
  - **The probe runs `KernelMath` with the constant first (F1).** `Kernels.Probe`
    gains `KernelMath.Min(1.0, v)` and `KernelMath.Max(1.0, v)`, and `MathProbe`
    names the two functions. The root records the ILGPU defect this covers (the third
    ILGPU bullet), and the thermo node's `KernelMath` tests both operands for NaN first.
    - ⚠ 2026-09-28: was the probe calling `Min(v, 1.0)` and `Max(v, 1.0)` only, now both
      operand orders → HISTORY.md#probe-constant-first-order-2026-09-28
  - **A launch has a time budget (F2).** A GPU that drives a display runs every
    kernel under the driver's run-time limit, 2 s by Windows' default, also under WSL2,
    whose GPU access goes through the same driver. One case is one thread's sequential
    program, and its time grows with the system.
    - The budget (`LaunchBudget`, built at bind time from the device's run-time-limit
      attribute) and the chunk bound it adds → [Chunks/BOOT.md](Chunks/BOOT.md)
    - A launch the driver kills for its run time (`CUDA_ERROR_LAUNCH_TIMEOUT`) is
      translated by `BatchRun` into `AcceleratorUnavailableException` (`API.md`,
      Errors); no ILGPU type reaches a consumer.
    - The limit no chunking can lift (about 16 or more elements on the reference GPU) is
      in `API.md` and the guide; the remedy is the CPU accelerator or a device
      without the limit (TCC mode, headless) →
      HISTORY.md#audit-f2-budget-condensed-2026-10-01
    - ⚠ 2026-09-28: was no bound on a launch's duration, now a time budget of a quarter
      of the driver's 2 s limit → HISTORY.md#launch-duration-unbounded-2026-09-28
  - The small items (observations 1 to 8) and the guards (F7, F8, O2) of the second
    audit, 2026-09-28 → HISTORY.md#audit-fixes-small-items-and-guards-2026-09-28

## Structure

Decided 2026-09-14 (the clean-code pass; the root's code-shape constraint). The engine
is a composition root over internal types, one class per file in this directory and
namespace → HISTORY.md#structure-intro-guarantee-2026-10-01

⚠ 2026-09-15: was `Engine` and `MathProbe` public, now internal, `AcceleratorProbe` on
the package surface → HISTORY.md#engine-and-mathprobe-internal-2026-09-15

| Type | Responsibility | Visibility |
|---|---|---|
| `Engine`, `KernelCache`, `MathProbe` | the composition root: `Create` delegating to the choice, `Upload`, the four `Run` overloads delegating to their pipelines, `ProbeMath` (the one run without a pipeline), `Budget`, `RunBatchLoop` and `Launchers` (exposed for the tests node's facts) and `Dispose`, which empties the kernel cache before it disposes the session; no loop, no arithmetic, no ILGPU call except through the session. `KernelCache`: typed kernel launchers, compiled and post-linked on first use, one per entry-point name, with the warm-up time, `Count` and `Clear`. `MathProbe`: the probe of the root's math list, whose `StrideCount` the kernel strides by, tied to `FunctionCount` by a test. The root's Ce rule allows `Engine` above its limit as a composition root (`## Shape exceptions`); the contracts are `API.md`'s; condensed → HISTORY.md#structure-rows-condensed-2026-10-01 | internal |
| `AcceleratorSession` | owns one ILGPU context, one accelerator, the optional NvvmAPI and the `AcceleratorInfo`; disposes them in order, once, and disposes what was built when the build fails | internal |
| `AcceleratorChoice` | turns `EngineOptions` into an `AcceleratorDecision` by the rules under Constraints: the session, the reason CUDA was skipped when it was, the paths tried | internal |
| `RunTimer` | the four phases of one run as named scopes; produces `RunTimings` | internal |
| `Chunks/` (child node, `APThermo.Execution.Chunks`) | the chunking policy and one program's chunk device buffers: `Chunk`, `ChunkPlan`, `ChunkBuffer<T>`, `ChunkBuffers`, `ChunkTransfer`, `IChunkBuffer`; its own `BOOT.md`/`API.md` hold the contract | internal |
| `BatchRun` | the loop and nothing else: per chunk, upload, launch and synchronise, download, each in its timer scope; since 2026-09-29 also owns disposing the chunk buffers it was given, in its own `finally`, through the private `DisposeChunkBuffers` — the one place of this node whose `catch` drops a lost session's own sticky `CudaException` (the third audit pass's finding 2; `AcceleratorSession.DropsAfterLoss` holds the decision, `Engine.DisposeAfterLoss` and `AcceleratorSession.Dispose` read the same decision for the pieces CA2000 does not force into this node's own method) | internal |
| `EquilibriumPipeline`, `RocketPipeline`, `TransportPipeline`, `SpeciesFunctionPipeline`, `Kernels` | one pipeline per program declares its host arrays, device buffers and views struct and assembles its result; `Kernels` is the registry of entry points, each slicing the views of its case and calling the numerical node; no formula in either. The root's Ce rule allows them above its limit as composition roots and a registry (`## Shape exceptions`); `LaunchBudget` is threaded from `session.Budget` into `ChunkPlan.For`. Three pipelines also have an internal `DeclareBuffers` for the F8 wiring fact; it and `Run` call one private `Declare`, so the two cannot drift, and `Run`'s `using var buffers` exists only because CA2000 needs a literal dispose beside the allocation (`BatchRun.Execute` has already disposed) | internal |
| `LibDevice/` (child node, `APThermo.Execution.LibDevice`) | libdevice discovery (`LibDeviceLocator`), the post-link (`LibDevicePostLink`, whose `Link` returns what it did) and the WSL workaround (`CudaWslDevices`); its own `BOOT.md`/`API.md` hold the contract | internal |

⚠ 2026-09-14: was `FunctionCount` the kernel's stride, now the const `StrideCount` →
HISTORY.md#probe-stride-count-2026-09-14

Decision of 2026-09-15 (the child-nodes phase, root `BOOT.md`, 0aa7e60): a cluster earns
a child directory when the rest of the node reaches it through a contract narrower than
its code, it has a reason of its own to change and it holds about five types or more →
HISTORY.md#child-nodes-decision-2026-09-15

⚠ 2026-10-01: was `LibDevice/` failed (three types), now a child: about 90 lines of
rules bind only its files → HISTORY.md#libdevice-child-node-2026-10-01

Decisions taken with the review of 2026-09-14:

- The fallback says why (`CudaSkippedBecause`, a contract change recorded in `API.md`).
  ⚠ 2026-09-15: was `CudaSkippedBecause` null when CUDA was not tried, now null when
  CUDA was bound or the CPU was asked for → HISTORY.md#fallback-says-why-2026-09-15
- Review of 2026-09-14: the missing-definition guard names the wrapper, the chunk bound
  counts every buffer → HISTORY.md#review-decisions-guard-and-chunk-bound-2026-09-14
- **The views structs keep their constructors.** `RocketBatchViews` (17 parameters)
  and `EquilibriumBatchViews` (12) are kernel parameter descriptors; grouping their
  views would re-emit the kernels and move the contract. They are this node's
  declared exception to the parameter rule, and so are the constructors of
  `RocketBatchResult` (10) and `EquilibriumBatchResult` (7), which mirror the batch
  results `API.md` publishes, one argument per property. The pipelines are the only
  callers of the four, and every call names its arguments, as the root requires of a
  mirrored shape. The other two views structs take six parameters and are within the
  rule.

  ⚠ 2026-09-15: was the views structs public (ILGPU "requires" it), now internal →
  HISTORY.md#views-structs-need-not-be-public-2026-09-15

  ⚠ 2026-09-14: was four views structs declared as the exception, now two, plus the
  result constructors → HISTORY.md#views-structs-exception-claim-2026-09-14
- Decision of the review of 2026-09-14: the unreachable batch-length checks went →
  HISTORY.md#review-decision-unreachable-checks-2026-09-14
- **One thread at a time.** An engine is used from one thread at a time; the kernel
  cache is the only synchronised piece. Said in `API.md`.
- **The probe's stride** is `MathProbe.StrideCount` (internal; see the ⚠ above); the two
  literals of the species-function chunk are the strides the pipeline declares.

## Shape exceptions

The rows below are this node's declared exceptions to the root's code-shape constraint,
in the form the protocol tests node reads; their reasons are decisions of `## Structure`.

| Where | Rule | Measured | Reason |
|---|---|---|---|
| `Engine` | efferent coupling | 31 | the composition root: `Create` delegating to the choice, `Upload`, the four `Run` overloads delegating to their pipelines, `ProbeMath` (the one run without a pipeline: allocates, launches and reads back the probe over the session's accelerator), `Budget` and `RunBatchLoop` (2026-09-28, F2), `MarkLost` and `DropsAfterLoss(CudaError)` (2026-09-29, review: test-only seams that let the tests node drive the third audit pass's finding 2 without a driver-touching `CudaException`), `Dispose`; no loop, no arithmetic, no ILGPU call except through the session |
| `Kernels` | efferent coupling | 26 | the registry of entry points: each slices the views of its case and calls the numerical node; no formula |
| `RocketPipeline` | efferent coupling | 25 | the composition root of its program's run: declares its host arrays, device buffers and views struct, assembles its result; no formula |
| `TransportPipeline` | efferent coupling | 23 | the same case as `RocketPipeline` above |
| `EquilibriumPipeline` | efferent coupling | 22 | the same case as `RocketPipeline` above |
| `SpeciesFunctionPipeline` | efferent coupling | 18 | the same case as `RocketPipeline` above |
| `RocketBatchViews.RocketBatchViews` | parameters | 17 | a kernel parameter descriptor (internal since 2026-09-15, the ⚠ under "The views structs keep their constructors"); grouping its views would re-emit the kernels and move the contract; every creation names its arguments |
| `EquilibriumBatchViews.EquilibriumBatchViews` | parameters | 12 | the same case as `RocketBatchViews` above |
| `RocketBatchResult.RocketBatchResult` | parameters | 10 | mirrors the batch result `API.md` publishes, one argument per property, as `RocketBatchViews` above |
| `EquilibriumBatchResult.EquilibriumBatchResult` | parameters | 7 | the same case as `RocketBatchResult` above |

`SpeciesFunctionBatchViews` and `TransportBatchViews`, the other two views structs
`## Structure` names, take six parameters each and need no row, as it already says.

⚠ 2026-09-27: was the `Kernels` row at 25, now 26 (`KernelMath.Min` and `Max` named) →
HISTORY.md#kernels-ce-26-2026-09-27

## Acceptance criteria

→ [ACCEPTANCE.md](ACCEPTANCE.md)

## Taboos

- No numerical formula in this node: kernels only slice and call.
- No ILGPU.Algorithms, no `XMath`, no `LibDevice.*` calls: the wrappers are provided by
  the post-link and the numerical nodes call `System.Math`.
- No kernel whose wrappers go unchecked: `Context.Builder.LibDevice()` defines them for
  some targets and silently drops them for others (the post-link constraint).

  ⚠ 2026-09-26: was "No reliance on `Context.Builder.LibDevice()` for wrappers", now it
  does for `compute_75` to `compute_90` → HISTORY.md#taboo-libdevice-reliance-2026-09-26
- No fallback from an explicitly requested CUDA accelerator to the CPU: silent
  fallbacks hide the very failures this node exists to surface.
- No reduction, atomic or shared-memory construct in a kernel: determinism first.
