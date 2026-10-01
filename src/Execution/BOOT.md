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

  ⚠ 2026-09-15: was a second sentence naming ILGPU only through this node's own types
  and `ArrayView`, now dropped (the package surface names no ILGPU type) →
  HISTORY.md#no-cuda-type-ilgpu-naming-2026-09-15
- **The same kernels everywhere.** A kernel is one static entry point per program; it
  is loaded on the CPU accelerator and on CUDA from the same method; there is no
  accelerator-specific numerical code.
- **Every CUDA kernel goes through the post-link, which completes it.** ILGPU 1.5.3
  defines the libdevice wrappers itself for the targets `compute_75` to `compute_90`
  and silently drops them for `compute_100` and newer (the root's ILGPU constraint).
  The post-link reads which wrappers the kernel calls and which it already defines. It
  compiles and inserts only the missing ones, and inserts nothing when none is missing.
  Every kernel that calls a wrapper is loaded once as a trial on either path. A kernel
  that still calls an undefined wrapper is refused at load, and the error names the
  wrapper. Either path yields the same program: the kernels ILGPU completes equal the
  kernels the post-link completes, as PTX text, up to ILGPU's generated names and the
  `.target` line (the architecture fact, Acceptance criteria).

  ⚠ 2026-09-26: was "ILGPU 1.5.3's own libdevice wrapper generation is never relied on",
  now the post-link completes the wrappers ILGPU dropped and inserts none it already
  defined → HISTORY.md#post-link-completes-not-replaces-2026-09-26
- **CUDA is bound only when a kernel runs on it** (2026-09-26). The choice accepts a
  CUDA session only after the math probe kernel, which calls every wrapper of the math
  list, has been compiled, post-linked and loaded on its device. `Engine.Create` and
  `AcceleratorProbe.Describe` therefore never report a CUDA device on which no kernel
  can load.

  ⚠ 2026-09-26: was a CUDA session accepted once its context existed, now only after the
  math probe kernel has loaded on the device →
  HISTORY.md#cuda-bound-only-when-a-kernel-runs-2026-09-26
- **No libnvvm or driver result is ignored** (2026-09-26). The post-link checks the
  result of every call it makes into libnvvm (`GetIRVersion`, `CreateProgram`,
  `AddModuleToProgram`, `LazyAddModuleToProgram`, `CompileProgram`, `GetProgramLog`,
  `GetCompiledResult`, `DestroyProgram`) and into the CUDA driver (`LoadModule`,
  `DestroyModule`). A result other than success is an `InvalidOperationException`
  that names the post-link, the target `compute_XX`, which library failed (libnvvm or
  the CUDA driver) and its call, and the result code, carrying the compiler's or the
  driver's log where one exists (`API.md`, Errors).
  - The log of a failed compilation is read after the failure. If reading the log
    fails too, the compilation's exception still propagates and says the log could
    not be read, naming that result.
  - Releasing a program or a module (`DestroyProgram`, `DestroyModule`) is checked only
    when the path before it succeeded. When an earlier call has already failed, the
    earlier exception propagates unchanged and the release is best-effort, so that a
    cleanup failure never hides the cause.
  - One internal method turns a result into the exception, so the message has one
    shape. It is unit-tested on the CPU with every non-success value of `NvvmResult`
    and a failing `CudaError`. The success path is proven by the CUDA tests of this
    node, which must stay green with no bit or throughput record moving.

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
  context and accelerator can be created, and the math probe kernel post-links and
  loads on the device (2026-09-26, the invariant "CUDA is bound only when a kernel runs
  on it"); otherwise the CPU accelerator with all cores. `AcceleratorKind.Cuda` fails
  instead of falling back and names what was missing, with every path tried.
  `AcceleratorKind.Cpu` never looks for CUDA.

  When the probe fails:
  - with `Auto`, its failure is the fallback reason in `CudaSkippedBecause`, with the
    post-link's message;
  - with `Cuda`, it is an `AcceleratorUnavailableException` carrying the post-link's
    exception as its inner exception;
  - in both cases the session is disposed before the choice returns.

  The probe kernel loaded for the check is released at once. It costs 0.05 to 0.2 s per
  CUDA engine on the reference machine (measured 2026-09-26).
- **libdevice discovery order**: an explicit path pair in the options is tried first,
  on every platform. Unless `LibDeviceDiscovery` is off, the platform is then chosen
  with `OperatingSystem.IsWindows()` / `IsLinux()`; any other OS does no discovery (the
  explicit pair is still tried, and the CPU accelerator is used when it is absent
  too).

  On **Windows**: the roots are the `CUDA_PATH` directory, then
  `%ProgramFiles%\NVIDIA GPU Computing Toolkit\CUDA\v*` from the newest version down;
  in each root both `nvvm\bin\nvvm64_40_0.dll` (12.x layout) and
  `nvvm\bin\x64\nvvm64_40_0.dll` (13.x layout) are tried, with
  `nvvm\libdevice\libdevice.10.bc`.

  On **Linux**: the roots are `CUDA_PATH`, then `CUDA_HOME`, then `/usr/local/cuda`,
  then `/usr/local/cuda-*` from the newest version down; in each root
  `nvvm/lib64/libnvvm.so` is tried, with `nvvm/libdevice/libdevice.10.bc`.

  On both platforms a root already tried (`CUDA_PATH` repeated among the versioned
  roots, or equal to `CUDA_HOME` on Linux) is skipped, and a root whose library exists
  but whose bitcode does not is passed over rather than accepted.

  The context is created with `LibDevice(dllPath, bitcodePath)` so that ILGPU emits
  the intrinsic calls.

  ⚠ 2026-09-15: was the Windows roots and `nvvm64_40_0.dll` only, now a platform branch
  with the Linux roots and `libnvvm.so` →
  HISTORY.md#libdevice-discovery-linux-2026-09-15
- **The post-link**, the one place in the tree that knows ILGPU internals. Its stages,
  in order:
  1. Compile the entry point with the CUDA accelerator's backend.
  2. Take the wrapper inventory of the kernel PTX (2026-09-26):
     - the wrappers *called* are the `__ilgpu__nv_*` names at `call` instructions
       only, never parameter names or `ld.param` operands;
     - the wrappers *defined* are the names of the kernel's own `.func` headers.

     Both sets drop the `__ilgpu` prefix, as the fragment keys do.
  3. The *missing* wrappers are those called and not defined.
     - When no wrapper is called, the kernel is returned untouched, without a trial
       load (as before).
     - When none is missing, nothing is compiled or inserted: ILGPU defined them all.
  4. Otherwise build an NVVM module from ILGPU's own fragments of the missing wrappers
     only. The fragments are the private static `fragments` dictionary of
     `ILGPU.Backends.PTX.PTXLibDeviceNvvm`, read by reflection. The header goes in the
     order libnvvm accepts: `target triple`, `target datalayout`, then
     `!nvvmir.version`.
  5. Compile that module with ILGPU's `NvvmAPI` for the `compute_XX` of the kernel's
     `.target sm_XX` line.
  6. Strip `.version`, `.target` and `.address_size` from the result.
  7. Insert it right after the kernel's `.address_size` line.
  8. Check that every missing wrapper now has a definition in the inserted text.
  9. Bind the accelerator's context to the calling thread and load the PTX once through
     the CUDA driver API as a trial, so that a refusal carries the driver's log. This
     happens on both paths of stage 3.
  10. When anything was inserted, set the private backing field of
      `PTXCompiledKernel.PTXAssembly` by reflection.
  11. Load with `LoadAutoGroupedKernel`.

  The reason the completion needs no branch for a kernel with some wrappers defined and
  some missing (ILGPU defines all of a kernel's fragments or none) →
  HISTORY.md#post-link-mixed-definitions-rationale-2026-09-26

  `Link` reports what it did as a value (the wrappers ILGPU defined, the wrappers it
  compiled), so that the tests can see which path a kernel took. It stays internal.

  The CPU accelerator loads the same method through `LoadAutoGroupedKernel(MethodInfo)`
  without any of this. The ILGPU assembly version and the presence and types of every
  reflected member are asserted once per process, at the first `Engine.Create`, and a
  mismatch is an error that names the ILGPU version.

  ⚠ 2026-09-26: was "collect the distinct `__ilgpu__nv_*` names" and compile and insert
  unconditionally, now the inventory of wrappers called against defined and only the
  missing ones inserted → HISTORY.md#post-link-wrapper-inventory-2026-09-26

  ⚠ 2026-09-12: was the wrappers compiled for a fixed `compute_80` with no trial load,
  now the target read from the kernel's PTX and a trial load on the bound thread →
  HISTORY.md#post-link-target-and-trial-load-2026-09-12
- **Batch layout**: structure of arrays for inputs and outputs; the case index is the
  thread index; per-case scratch is a slice of a batch-sized buffer laid out by the
  numerical nodes' `ScratchLayout` and `TransportLayout`; batches are processed in
  chunks of at most `ChunkSize` cases (default 16 384), and fewer when a chunk's
  scratch would exceed `ScratchBytes` (default 256 MB), so that memory stays bounded;
  the device buffers of a chunk are allocated once per run and reused; results are
  copied back per chunk.

  ⚠ 2026-09-12: was a chunk bounded by the case count only, now also by `ScratchBytes`,
  the rocket chunk's moles included → HISTORY.md#batch-layout-scratch-bound-2026-09-12
- **Kernels**: one entry point per program (`Equilibrium`, `Rocket`, `Transport`,
  and `Functions` for the species functions of `Thermo` at given temperatures) and
  the `Probe` of the root's math list; each entry point does nothing but slice the
  views for its case and call the numerical node. The transport kernel takes a plain
  batch of stations (a temperature and a composition each); the batch is built from a
  finished rocket or equilibrium result by factories of the batch type, not by the
  engine, which does not know where a composition came from.

  ⚠ 2026-09-12: was no species-function batch, now the `Functions` entry point for the
  front door's reactant enthalpies →
  HISTORY.md#kernels-species-function-batch-2026-09-12
- **Warm-up**: kernel compilation and post-link happen on the first run of a program
  per engine and are cached for the engine's lifetime; the time is reported as the
  run's `WarmUp`, separately from the upload, kernel and download times.
- **Host-side errors are exceptions** (missing libdevice, ILGPU version mismatch,
  inconsistent batches, a refused PTX, out-of-memory); per-case failures are statuses
  in the output arrays.
- Reference figures of 2026-09-12 (the probe within 4 ULP of the CPU accelerator over 26
  decades; the 100 000-case rocket sweep in 0.15 to 0.17 s on CUDA against 9.5 s on the
  CPU accelerator, 56 to 65 times; bounds on expectation, not requirements) →
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
    - ⚠ 2026-09-26: was the CUDA context created before libnvvm was loaded (a bad
      library threw a raw `BadImageFormatException` and leaked about 190 MiB per
      attempt), now the library first →
      HISTORY.md#bad-library-leaked-the-context-2026-09-26
  - **All cores (F3).** The CPU accelerator runs `Environment.ProcessorCount` threads,
    through a `CPUDevice` sized for it rather than ILGPU's predefined 16-thread device.
    The layout rounds down to the nearest multiple of 4 not above the count, and the
    choice is documented where it is made. On the reference
    machine the count is 16, the layout is today's, and no throughput record moves.
    Results do not depend on the thread count (Invariants: deterministic batches).

    ⚠ 2026-09-28: was the layout rounded for "a count ILGPU's warp layout cannot express
    exactly", now rounded to keep the (4, 4, 1) shape of every record →
    HISTORY.md#all-cores-layout-reason-2026-09-28
    - ⚠ 2026-09-26: was "all cores" true on the 16-thread reference machine only
      (`CPUDevice.Default`), now the device sized from `Environment.ProcessorCount` →
      HISTORY.md#all-cores-cpudevice-default-2026-09-26
  - **Observations.**
    - A driver or libnvvm log is trimmed of NUL padding as well as white space (the
      trial load's message carried 45 NULs).
    - `Engine.Upload` disposes the buffers it already uploaded when a later upload
      fails.
    - A half-given explicit path pair (`LibNvvmPath` without `LibDevicePath`, or the
      reverse) is an `ArgumentException` at `Create` naming the missing option. It was
      tried as `("", path)` and then replaced by discovery without a word.

- **Every CUDA context of a process binds under WSL** (2026-09-27). ILGPU 1.5.3's
  `builder.Cuda()` calls `NativeLibrary.SetDllImportResolver` on its own assembly
  whenever the process runs under WSL (`CudaContextExtensions.CudaInternal`, source tag
  `v1.5.3`), to load `libcuda` from the WSL driver directory. .NET allows one resolver
  per assembly, so the second CUDA context of a process throws
  `InvalidOperationException` ("A resolver is already set for the assembly") before any
  device is registered.
  - Rule: the context build calls `builder.Cuda()` as today. When that call throws
    this exception under WSL, the resolver ILGPU needs is already in place, and the
    build registers the CUDA devices itself through ILGPU's internal
    `CudaDevice.GetDevices(configure, predicate, builder.DeviceRegistry)`, the call
    `CudaInternal` makes after the resolver. The internal members are reached by
    reflection, which the pinned version makes stable. A missing member is an
    `AcceleratorUnavailableException` naming it, so that a changed ILGPU fails loudly
    at the first bind.
  - No static state: the build tries the public call first every time, so nothing
    records that a resolver was set. Outside WSL the public call never throws this
    exception, and the path is never taken.
  - ⚠ 2026-09-27: was one CUDA context per process under WSL, now the second and later
    contexts bind too → HISTORY.md#wsl-second-cuda-context-fails-2026-09-27
  - The exception is recognised by where it was thrown, not by its message
    (2026-09-28): an `InvalidOperationException` whose `TargetSite` is
    `NativeLibrary.SetDllImportResolver`. An application trimmed with
    `UseSystemResourceKeys` gets the resource key in place of the English text. The
    message test would then miss it, and every later engine would fall back to the
    CPU (the second audit's observation 5, by reading).

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
    - The Chunks node holds an internal `LaunchBudget`, built at bind time from the
      device's kernel run-time-limit attribute. A device without the limit, and the
      CPU accelerator, have no budget. A device with it gets a quarter of the 2 s
      default.
    - `ChunkPlan` takes the budget as a fourth bound, beside the count, the bytes and
      the offsets. The first chunk of a run is one wave of the device (its
      multiprocessors times the threads each holds at once). Each later chunk is sized
      from the previous chunk's measured time per case.
    - Results do not depend on the chunking. The audit's `E13` found the same bits at
      chunk sizes 16 384, 1, 7 and 64, so no result bit moves.
    - A launch the driver kills for its run time (`CUDA_ERROR_LAUNCH_TIMEOUT`) is
      translated by `BatchRun` into `AcceleratorUnavailableException`. The message names
      the run-time limit, the chunk's case count and the CPU accelerator as the remedy.
      No ILGPU type reaches a consumer, and `API.md`'s errors table gains the row.
    - `API.md` and the guide state the limit no chunking can lift. One case of a
      system of about 16 or more elements takes longer than the default limit on the
      reference GPU, and such systems belong on the CPU accelerator or on a device
      without the limit (TCC mode, headless).
    - ⚠ 2026-09-28: was no bound on a launch's duration, now a time budget of a quarter
      of the driver's 2 s limit → HISTORY.md#launch-duration-unbounded-2026-09-28
  - The documentation and small items (observations 1 to 8) and the guards of this node
    (F7, F8, O2) of the second audit, 2026-09-28 →
    HISTORY.md#audit-fixes-small-items-and-guards-2026-09-28

## Structure

Decided 2026-09-14 (the clean-code pass; the root's code-shape constraint). The engine
is a composition root over internal types, one class per file in this directory and
namespace. The kernel entry points and the calls into the numerical nodes are untouched
by the split, so the emitted PTX, the post-link and the kernel time cannot move.

⚠ 2026-09-15: was `Engine` and `MathProbe` the node's public composition types, now
internal, `AcceleratorProbe` replacing them on the package surface →
HISTORY.md#engine-and-mathprobe-internal-2026-09-15

| Type | Responsibility | Visibility |
|---|---|---|
| `Engine` | the composition root: `Create` delegating to the choice, `Upload`, the four `Run` overloads delegating to their pipelines, `ProbeMath` (the one run without a pipeline: allocates, launches and reads back the probe over the session's accelerator), `Budget` and `RunBatchLoop` (2026-09-28, F2: the session's time budget and the chunk loop, exposed for the tests node's own chunk-plan facts), `Launchers` (2026-09-30: the kernel cache, exposed for the tests node's facts on release at dispose), `Dispose` (which empties the kernel cache before it disposes the session); no loop, no arithmetic, no ILGPU call except through the session. Named here as the composition root the root's Ce rule allows above its limit: four typed `Run` overloads name twelve types by themselves (Ce 30 by the dependency check's walk on 2026-09-28, 25 on 2026-09-15) | internal (2026-09-15, distribution phase; F1, `API.md`'s ⚠), contract as `API.md`'s tree-contract section says |
| `AcceleratorSession` | owns one ILGPU context, one accelerator, the optional NvvmAPI and the `AcceleratorInfo`; disposes them in order, once, and disposes what was built when the build fails | internal |
| `AcceleratorChoice` | turns `EngineOptions` into an `AcceleratorDecision` by the rules under Constraints: the session, the reason CUDA was skipped when it was, the paths tried | internal |
| `KernelCache` | typed kernel launchers, compiled and post-linked on first use, one per entry-point name; reports the warm-up time; `Count` and `Clear` (2026-09-30): `Clear` drops every launcher and clears the ILGPU context's caches, which is how a disposed engine releases the compiled programs it kept | internal |
| `RunTimer` | the four phases of one run as named scopes; produces `RunTimings` | internal |
| `Chunks/` (child node, `APThermo.Execution.Chunks`) | the chunking policy and one program's chunk device buffers: `Chunk`, `ChunkPlan`, `ChunkBuffer<T>`, `ChunkBuffers`, `ChunkTransfer`, `IChunkBuffer`; its own `BOOT.md`/`API.md` hold the contract | internal |
| `BatchRun` | the loop and nothing else: per chunk, upload, launch and synchronise, download, each in its timer scope; since 2026-09-29 also owns disposing the chunk buffers it was given, in its own `finally`, through the private `DisposeChunkBuffers` — the one place of this node whose `catch` drops a lost session's own sticky `CudaException` (the third audit pass's finding 2; `AcceleratorSession.DropsAfterLoss` holds the decision, `Engine.DisposeAfterLoss` and `AcceleratorSession.Dispose` read the same decision for the pieces CA2000 does not force into this node's own method) | internal |
| `EquilibriumPipeline`, `RocketPipeline`, `TransportPipeline`, `SpeciesFunctionPipeline` | one per program: declare its host arrays, device buffers and views struct, assemble its result; no formula. Named here as the composition roots of their programs' runs, which the root's Ce rule allows above its limit: each names its program's batch, result and views types and the tables' buffers and views besides the run's machinery (the session, the plan, the chunk buffers, the loop, the timer, the kernel cache, and since 2026-09-28 `LaunchBudget`, threaded from `session.Budget` into `ChunkPlan.For`, F2). Three of the four also gained an internal `DeclareBuffers` test-support method for the F8 wiring fact, naming no new type; since 2026-09-29 (the third audit pass's observation) `DeclareBuffers` and `Run` both call one private `Declare` method instead of restating the buffer declarations, so the two cannot drift; `Run`'s own `using var buffers` disposes nothing for real once `BatchRun.Execute` has already run (ILGPU's own dispose is idempotent), and exists only because CA2000 needs a literal dispose beside the allocation. By the dependency check's walk on 2026-09-28 (2026-09-14 in parentheses): `RocketPipeline` 25 (23), `TransportPipeline` 23 (22), `EquilibriumPipeline` 22 (21), `SpeciesFunctionPipeline` 18 (17); unchanged by the 2026-09-29 refactor (`Declare` and `DisposeChunkBuffers` name no type these pipelines did not already name) | internal |
| `Kernels` | the registry of entry points: each slices the views of its case and calls the numerical node; no formula. Named here as the registry the root's Ce rule allows above its limit (Ce 26 by the dependency check's walk on 2026-09-27, 25 on 2026-09-14, 22 by the review's textual count the same day: one views struct, one layout class and one solver per program, which no split removes) | internal |
| `MathProbe` | the probe of the root's math list, in a file of its own; `StrideCount` is the internal constant the kernel strides by, tied to `FunctionCount` by a test, and the function list is asserted to have that length | internal (2026-09-15, distribution phase), contract unchanged |
| `LibDevicePostLink` | the post-link as the sequence of its stages, each a method or a small internal type: the wrapper inventory of the kernel PTX (called at `call` sites, defined by `.func` headers; 2026-09-26), the NVVM module from the fragments of the missing wrappers, the compilation, the insertion after the header, the definition check as a set comparison over the wrapper text, the trial load; `Link` returns what it did | internal |
| `CudaWslDevices` | the WSL workaround (2026-09-27, Constraints, "Every CUDA context of a process binds under WSL"): tries `builder.Cuda()` first, every call, and only on the resolver-already-set exception registers the devices itself by reflecting ILGPU's own internal `CudaDevice.GetDevices` | internal |

⚠ 2026-09-14: was `FunctionCount` the constant the kernel strides by, now it stays a
public property and an internal const `StrideCount` strides →
HISTORY.md#probe-stride-count-2026-09-14

Decision of 2026-09-15 (the child-nodes phase, root `BOOT.md`, 0aa7e60): a cluster earns
a child directory when the rest of the node reaches it through a contract narrower than
its code, it has a reason of its own to change and it holds about five types or more.
`Chunks/` passed (six internal types, its row above); `LibDevice/` failed (three types)
and `ExpectedIlgpuVersion` stays on `LibDevicePostLink`: `LibDeviceLocator` and
`LibDevicePostLink` stay two files of this node →
HISTORY.md#child-nodes-decision-2026-09-15

Decisions taken with the review of 2026-09-14:

- The fallback says why (`CudaSkippedBecause`, a contract change recorded in `API.md`).
  ⚠ 2026-09-15: was `CudaSkippedBecause` "null when CUDA was not tried or was bound",
  now null when CUDA was bound or the options asked for the CPU →
  HISTORY.md#fallback-says-why-2026-09-15
- Decisions of the review of 2026-09-14: the missing-definition guard names the wrapper,
  and the chunk bound counts every buffer →
  HISTORY.md#review-decisions-guard-and-chunk-bound-2026-09-14
- **The views structs keep their constructors.** `RocketBatchViews` (17 parameters)
  and `EquilibriumBatchViews` (12) are kernel parameter descriptors; grouping their
  views would re-emit the kernels and move the contract. They are this node's
  declared exception to the parameter rule, and so are the constructors of
  `RocketBatchResult` (10) and `EquilibriumBatchResult` (7), which mirror the batch
  results `API.md` publishes, one argument per property. The pipelines are the only
  callers of the four, and every call names its arguments, as the root requires of a
  mirrored shape. The other two views structs take six parameters and are within the
  rule.

  ⚠ 2026-09-15: was the views structs "kernel parameter descriptors ILGPU requires to be
  public", now internal with `InternalsVisibleTo("ILGPURuntime")` →
  HISTORY.md#views-structs-need-not-be-public-2026-09-15

  ⚠ 2026-09-14: was four views structs declared as the parameter-count exception, now
  two (the other two take six parameters) and the result constructors declared too →
  HISTORY.md#views-structs-exception-claim-2026-09-14
- Decision of the review of 2026-09-14: the unreachable batch-length checks went →
  HISTORY.md#review-decision-unreachable-checks-2026-09-14
- **One thread at a time.** An engine is used from one thread at a time; the kernel
  cache is the only synchronised piece. Said in `API.md`.
- **The probe's stride** is `MathProbe.StrideCount` (internal; see the ⚠ above); the two
  literals of the species-function chunk are the strides the pipeline declares.
- **Size.** No method over 60 lines, no control flow nested deeper than 3, no more than
  6 parameters (the views structs aside).

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

  ⚠ 2026-09-26: was "No reliance on `Context.Builder.LibDevice()` to produce wrappers:
  it does not", now it does for `compute_75` to `compute_90` and the taboo is on kernels
  whose wrappers go unchecked → HISTORY.md#taboo-libdevice-reliance-2026-09-26
- No fallback from an explicitly requested CUDA accelerator to the CPU: silent
  fallbacks hide the very failures this node exists to surface.
- No reduction, atomic or shared-memory construct in a kernel: determinism first.
