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

  ⚠ 2026-09-15 (distribution phase): this invariant went on to say the public surface
  "names ILGPU only through this node's own types and, in the kernel parameter
  structs, ILGPU's `ArrayView`". The API review of that day
  (fixed in `9036c6a`) demoted `Engine`, the batch types and the four
  views structs into the tree contract; the package surface (`AcceleratorKind`,
  `EngineOptions`, `AcceleratorInfo`, `AcceleratorUnavailableException`,
  `AcceleratorProbe`) now names no ILGPU type at all, so the second sentence no longer
  describes anything and is dropped rather than corrected in place.
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

  ⚠ 2026-09-26: stood "ILGPU 1.5.3's own libdevice wrapper generation is never relied
  on (it is defective with libnvvm 12.9 and 13.3 …)". It was measured on the reference
  machine's SM_120 only, where ILGPU always drops the wrappers. On SM_75 to SM_90 ILGPU
  defined them itself, and the post-link had two faults:
  - it read the definitions' parameter names (`__ilgpu__nv_exp_param_0`) as wrappers
    called, and threw for want of a fragment;
  - read correctly, it would have inserted a second copy, which the driver refuses
    ("Duplicate definition").

  So every CUDA run of 0.1.0 threw on every GPU older than Blackwell. Found by the
  hidden-defect audit of 2026-09-26 (finding F1), on kernels compiled for those
  architectures and run on the reference device. The measurements behind the new
  wording are in the root's ⚠ of the same date.
- **CUDA is bound only when a kernel runs on it** (2026-09-26). The choice accepts a
  CUDA session only after the math probe kernel, which calls every wrapper of the math
  list, has been compiled, post-linked and loaded on its device. `Engine.Create` and
  `AcceleratorProbe.Describe` therefore never report a CUDA device on which no kernel
  can load.

  ⚠ 2026-09-26: before, a CUDA session was accepted once its context existed. The
  first failure then surfaced at the first `Run`, where `Auto` has no fallback left. A
  post-link defect or a device whose target libnvvm refuses turned every run into an
  exception, while `apthermo devices` showed CUDA as usable. Found by the same audit.
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

  ⚠ 2026-09-26: until then only `CompileProgram` and `LoadModule` were checked; the
  other calls' results were dropped, a fact the Diagnostics pass made visible when
  IDE0058 turned the silent drops into explicit discards (an underscore assigned in
  front of the call). A failure of, say, `AddModuleToProgram` surfaced later as a
  compilation error with a misleading log, or not at all.
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

⚠ 2026-09-15 (distribution phase): stood "libnvvm (`nvvm64_40_0.dll`)", naming the
Windows file only, before the root's Platform constraint (`cf87211`) added Linux as a
supported platform, CUDA included. Linux ships the same library as `libnvvm.so`; the
line now names both.

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

  ⚠ 2026-09-15 (distribution phase): this bullet named only the Windows roots and
  `nvvm64_40_0.dll`, matching the root's Platform constraint before `cf87211` made
  Linux x64 a supported platform, CUDA included, and fixed the discovery order for it.
  `LibDeviceLocator` now branches on the platform; every other stage of discovery and
  of the post-link is unchanged, since the root constraint restricts the platform
  split to library discovery paths and file names.
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

  A kernel with some wrappers defined and some missing is not refused by a branch of
  its own. ILGPU 1.5.3 compiles all of a kernel's fragments in one libnvvm program, so
  it defines all of them or none. If a mix ever arrived, the completion would insert
  only the missing ones. A definition both sides emitted, such as libdevice's
  `__internal_accurate_pow`, would then be refused by the trial load with the driver's
  "Duplicate definition" log.

  `Link` reports what it did as a value (the wrappers ILGPU defined, the wrappers it
  compiled), so that the tests can see which path a kernel took. It stays internal.

  The CPU accelerator loads the same method through `LoadAutoGroupedKernel(MethodInfo)`
  without any of this. The ILGPU assembly version and the presence and types of every
  reflected member are asserted once per process, at the first `Engine.Create`, and a
  mismatch is an error that names the ILGPU version.

  ⚠ 2026-09-26: stood as one sentence whose second step was "collect the distinct
  `__ilgpu__nv_*` names from the PTX", followed by unconditional compilation and
  insertion. That read the parameter names of ILGPU's own definitions as calls. It was
  also a replacement, not a completion, and doubled every definition ILGPU had already
  made (the ⚠ of the invariant "Every CUDA kernel goes through the post-link"). Rejected alternatives, measured the same day:
  - Compile with a backend that has no `NvvmAPI`, so that ILGPU never defines the
    wrappers: ILGPU 1.5.3 then refuses every kernel that uses the math list with an
    `InternalCompilerException`.
  - Cut ILGPU's definitions out of the text and insert the post-link's own: that
    rewrites the same text into the same text, since the two are equal.

  ⚠ 2026-09-12: the sketch compiled the wrappers for a fixed `compute_80`, "the PTX
  target ILGPU 1.5.3 emits". That is what ILGPU emits for the reference machine's
  SM_120 device, but it is ILGPU's choice per device, so the post-link reads the
  target from the PTX instead; a mismatch between the wrappers and the kernel would
  otherwise be silent until the driver refuses the module. The sketch also had no
  trial load: the first attempt loaded the linked PTX only inside ILGPU, and a
  refusal surfaced as a bare `CUDA_ERROR_INVALID_PTX` without the driver's log; the
  trial load failed with `CUDA_ERROR_INVALID_CONTEXT` from xunit's worker threads
  until the accelerator was bound to the calling thread first.
- **Batch layout**: structure of arrays for inputs and outputs; the case index is the
  thread index; per-case scratch is a slice of a batch-sized buffer laid out by the
  numerical nodes' `ScratchLayout` and `TransportLayout`; batches are processed in
  chunks of at most `ChunkSize` cases (default 16 384), and fewer when a chunk's
  scratch would exceed `ScratchBytes` (default 256 MB), so that memory stays bounded;
  the device buffers of a chunk are allocated once per run and reused; results are
  copied back per chunk.

  ⚠ 2026-09-12: the sketch bounded a chunk by the case count only. The transport
  scratch is 4·M² + E·M + 8·M doubles per station with M = 40, about 43 KB, so a
  chunk of 16 384 stations would take 700 MB; the memory bound was added, and the
  rocket chunk counts its per-station moles in the same bound.
- **Kernels**: one entry point per program (`Equilibrium`, `Rocket`, `Transport`,
  and `Functions` for the species functions of `Thermo` at given temperatures) and
  the `Probe` of the root's math list; each entry point does nothing but slice the
  views for its case and call the numerical node. The transport kernel takes a plain
  batch of stations (a temperature and a composition each); the batch is built from a
  finished rocket or equilibrium result by factories of the batch type, not by the
  engine, which does not know where a composition came from.

  ⚠ 2026-09-12: the species-function batch was not in the sketch; the front door needs
  the reactant enthalpies at their temperatures from the tree's one implementation of
  the polynomials, and that implementation runs only over accelerator memory.
- **Warm-up**: kernel compilation and post-link happen on the first run of a program
  per engine and are cached for the engine's lifetime; the time is reported as the
  run's `WarmUp`, separately from the upload, kernel and download times.
- **Host-side errors are exceptions** (missing libdevice, ILGPU version mismatch,
  inconsistent batches, a refused PTX, out-of-memory); per-case failures are statuses
  in the output arrays.
- Reference figures on the reference machine, measured 2026-09-12: the probe of the
  math list within 4 ULP of the CPU accelerator over 26 decades (3 measured in the
  first spike); the 100 000-case rocket sweep (4 stations, 11 species, shifting
  equilibrium) in 0.15–0.17 s on CUDA (0.10 s of it in the kernel) against 9.5 s on
  the CPU accelerator with 16 threads, 56–65×. These bound expectations; they are
  not requirements.

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
    - ⚠ ILGPU's accelerator constructor creates the CUDA context first and loads
      libnvvm after. A libnvvm that exists but does not load threw a raw
      `BadImageFormatException` from an explicit `Cuda` request. With `Auto` the
      fallback reason held no path. Each attempt leaked the context already created,
      about 190 MiB of device memory: 20 `Auto` creations lost 3 800 MiB. A failure
      inside ILGPU's constructor after the context, for a cause this check cannot
      foresee, still leaks: ILGPU gives no handle to release. The known cause, a bad
      library, no longer reaches the device.
  - **All cores (F3).** The CPU accelerator runs `Environment.ProcessorCount` threads,
    through a `CPUDevice` sized for it rather than ILGPU's predefined 16-thread device.
    The layout rounds down to the nearest multiple of 4 not above the count, and the
    choice is documented where it is made. On the reference
    machine the count is 16, the layout is today's, and no throughput record moves.
    Results do not depend on the thread count (Invariants: deterministic batches).

    ⚠ 2026-09-28 (the second hidden-defect audit, guards observation O8): this bullet
    said the layout is rounded because "a count ILGPU's warp layout cannot express
    exactly". `AcceleratorChoice.CpuDeviceFor`'s own summary already said otherwise: any
    warp size from 2 constructs (ILGPU's `CPUDevice` constructor refuses only 1). The
    real reason the layout is fixed at a multiple of 4 rather than reaching every count
    exactly is to keep the (4, 4, 1) shape at 16 threads that every bit and throughput
    record was measured against; a processor count that is not a multiple of 4 then
    leaves up to 3 threads idle, the cost of that choice, not a limit ILGPU imposes.
    Found by the guards audit reading `AcceleratorChoice.cs` against this document.
    - ⚠ `CPUDevice.Default` is one multiprocessor of four warps of four threads,
      whatever the machine. Every "all cores" of the tree (`Options.cs`, this
      document, the root) was true only on the 16-thread reference machine. A
      64-thread workstation ran 16 threads, and a 4-vCPU runner oversubscribed four
      times. The owner chose all cores over documenting 16.
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
  - ⚠ Under WSL every `Engine.Create` after the first CUDA context of a process failed.
    With `Auto` it bound the CPU with the reason "the CUDA context could not be created
    (driver or device problem): A resolver is already set for the assembly". With
    `Cuda` it threw. The tests had always met one CUDA context per process, until
    the bind-time facts of 2026-09-26 created two:
    `AnExplicitCudaRequestFailsWithThePostLinksOwnExceptionWhenTheProbeKernelCannotBind`
    failed under WSL at `89bb619` right after the `Auto` fact, and passed alone. Found
    by the orchestrator's WSL run of 2026-09-27. The native Linux path does not take
    ILGPU's branch, and was not run.
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
    - ⚠ The probe called `Min(v, 1.0)` and `Max(v, 1.0)` only, the order ILGPU does
      not swap. The tests node's record "0 ULP, NaN included" held for that order.
      On the reference device `KernelMath.Min(1.0, NaN)` was 1.0 on CUDA and NaN on the
      CPU (the audit's `E01`: 18 mismatches over 12 outputs and 10 inputs, all at NaN).
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
    - ⚠ Nothing bounded a launch's duration, and neither this document nor `API.md`
      named the limit. The audit measured launch times up to 1.15 s, and projected
      about 2 s for a default chunk at 14 elements and past 2 s for one case from 17.
      Under `Auto` the kill comes after CUDA was bound, so there is no fallback. It was
      not provoked, because it resets the display driver the desktop and the release
      runners share.
  - **Documentation and small items (observations 1 to 8).**
    - `MathProbe` and the tests node say that `Floor` and `Ceiling` go through libdevice
      (`__ilgpu__nv_floor`, `__ilgpu__nv_ceil`): the post-link completes seven wrappers
      for the probe. Only `Abs` is emitted directly.
    - The PTX fixtures are regenerated from the current probe. The regeneration rule
      names a change to `Kernels.Probe` beside an ILGPU upgrade.
    - `API.md` states the warm-up an engine costs: the rocket kernel's compile, 13 to
      22 s, and the driver's JIT. The JIT runs again for every engine after the first
      in a process, 55 to 59 s, because ILGPU's generated names come from process-wide
      counters and the driver's cache is keyed by the PTX text. The advice: one `Solver`
      per process. The durations this document and the tests node give for the
      architecture fact are the measured ones, 8 m on Windows and 11 m under WSL, not
      "about three minutes".
    - Batch constructors compute sizes in `long` and refuse a count whose buffers exceed
      the 32-bit offsets. `ChunkBuffer` checks the host array's length before a copy.
      `ProbeMath` caps its input count the same way.
    - An empty libnvvm log leaves no trailing ": " in the post-link's message.
    - The all-cores layout's reason is corrected. Any warp size from 2 constructs, as
      `AcceleratorChoice` already records. The layout rounds down to a multiple of 4 to
      keep the (4, 4, 1) shape at 16 threads, the reference machine's record, and up to
      3 threads idle is its cost.
  - **Guards of this node (the guards part's F7, F8, O2).**
    - The two library-discovery "not found" facts run their assertions in the hosted
      matrix: the forbidden flag is injected into `AcceleratorChoice`, so the not-found
      branch runs without CUDA. Until then they returned early in every job.
    - The 32-bit chunk cap is asserted as wiring: for each of the four pipelines,
      driven with a huge `ChunkSize` and `ScratchBytes`, the plan it chose satisfies
      `Size × MaxElementsPerCase ≤ int.MaxValue`. Until then
      `MaxElementsPerCase => 0` passed every test.
    - `SpeciesFunctionTests`' comparison is NaN-aware, as the probe's and the sweep's
      are.
    - `ArchitectureTests` gives each backend its own `NvvmAPI`. `PTXBackend.Dispose`
      frees the one it was given, and a shared instance survived only because the
      fixture's engine kept the same libnvvm loaded (observation 4: a variant with
      libnvvm 12.9 crashed with an access violation).

## Structure

Decided 2026-09-14 (the clean-code pass; the root's code-shape constraint). The engine
is a composition root over internal types, one class per file in this directory and
namespace. The kernel entry points and the calls into the numerical nodes are untouched
by the split, so the emitted PTX, the post-link and the kernel time cannot move.

⚠ 2026-09-15 (distribution phase): `Engine` and `MathProbe` were the node's only
public composition types, per the table below; the API review of that day
(its section 4, findings D1 and F1, fixed in `9036c6a`) found no consumer scenario for
either. `Engine` became internal and `AcceleratorProbe` (`AcceleratorProbe.cs`)
replaces it on the package surface for the two questions a consumer actually asked of
it: what a set of `EngineOptions` binds to (`Describe`), and whether the environment
forbids CUDA (`CudaForbidden`); `Solver.Create` (`Problems`) keeps creating its own
`Engine` internally. `MathProbe`, the eight batch and batch-result types, `UploadedTables`
and `RunTimings` became internal with it. `APThermo.Execution.csproj` grants
`InternalsVisibleTo` to `Problems` (the only `src` node whose `## Dependencies` names
this one; `Cli` goes through `AcceleratorProbe` and receives no grant), to
`Execution.Tests` and `Problems.Tests`, to `Benchmarks`, and to `ILGPURuntime` for the
four kernel-parameter views structs (`Kernels.cs`'s own ⚠ below).

| Type | Responsibility | Visibility |
|---|---|---|
| `Engine` | the composition root: `Create` delegating to the choice, `Upload`, the four `Run` overloads delegating to their pipelines, `ProbeMath` (the one run without a pipeline: allocates, launches and reads back the probe over the session's accelerator), `Budget` and `RunBatchLoop` (2026-09-28, F2: the session's time budget and the chunk loop, exposed for the tests node's own chunk-plan facts), `Dispose`; no loop, no arithmetic, no ILGPU call except through the session. Named here as the composition root the root's Ce rule allows above its limit: four typed `Run` overloads name twelve types by themselves (Ce 30 by the dependency check's walk on 2026-09-28, 25 on 2026-09-15) | internal (2026-09-15, distribution phase; F1, `API.md`'s ⚠), contract as `API.md`'s tree-contract section says |
| `AcceleratorSession` | owns one ILGPU context, one accelerator, the optional NvvmAPI and the `AcceleratorInfo`; disposes them in order, once, and disposes what was built when the build fails | internal |
| `AcceleratorChoice` | turns `EngineOptions` into an `AcceleratorDecision` by the rules under Constraints: the session, the reason CUDA was skipped when it was, the paths tried | internal |
| `KernelCache` | typed kernel launchers, compiled and post-linked on first use, one per entry-point name; reports the warm-up time | internal |
| `RunTimer` | the four phases of one run as named scopes; produces `RunTimings` | internal |
| `Chunks/` (child node, `APThermo.Execution.Chunks`) | the chunking policy and one program's chunk device buffers: `Chunk`, `ChunkPlan`, `ChunkBuffer<T>`, `ChunkBuffers`, `ChunkTransfer`, `IChunkBuffer`; its own `BOOT.md`/`API.md` hold the contract | internal |
| `BatchRun` | the loop and nothing else: per chunk, upload, launch and synchronise, download, each in its timer scope; since 2026-09-29 also owns disposing the chunk buffers it was given, in its own `finally`, through the private `DisposeChunkBuffers` — the one place of this node whose `catch` drops a lost session's own sticky `CudaException` (the third audit pass's finding 2; `AcceleratorSession.DropsAfterLoss` holds the decision, `Engine.DisposeAfterLoss` and `AcceleratorSession.Dispose` read the same decision for the pieces CA2000 does not force into this node's own method) | internal |
| `EquilibriumPipeline`, `RocketPipeline`, `TransportPipeline`, `SpeciesFunctionPipeline` | one per program: declare its host arrays, device buffers and views struct, assemble its result; no formula. Named here as the composition roots of their programs' runs, which the root's Ce rule allows above its limit: each names its program's batch, result and views types and the tables' buffers and views besides the run's machinery (the session, the plan, the chunk buffers, the loop, the timer, the kernel cache, and since 2026-09-28 `LaunchBudget`, threaded from `session.Budget` into `ChunkPlan.For`, F2). Three of the four also gained an internal `DeclareBuffers` test-support method for the F8 wiring fact, naming no new type; since 2026-09-29 (the third audit pass's observation) `DeclareBuffers` and `Run` both call one private `Declare` method instead of restating the buffer declarations, so the two cannot drift; `Run`'s own `using var buffers` disposes nothing for real once `BatchRun.Execute` has already run (ILGPU's own dispose is idempotent), and exists only because CA2000 needs a literal dispose beside the allocation. By the dependency check's walk on 2026-09-28 (2026-09-14 in parentheses): `RocketPipeline` 25 (23), `TransportPipeline` 23 (22), `EquilibriumPipeline` 22 (21), `SpeciesFunctionPipeline` 18 (17); unchanged by the 2026-09-29 refactor (`Declare` and `DisposeChunkBuffers` name no type these pipelines did not already name) | internal |
| `Kernels` | the registry of entry points: each slices the views of its case and calls the numerical node; no formula. Named here as the registry the root's Ce rule allows above its limit (Ce 26 by the dependency check's walk on 2026-09-27, 25 on 2026-09-14, 22 by the review's textual count the same day: one views struct, one layout class and one solver per program, which no split removes) | internal |
| `MathProbe` | the probe of the root's math list, in a file of its own; `StrideCount` is the internal constant the kernel strides by, tied to `FunctionCount` by a test, and the function list is asserted to have that length | internal (2026-09-15, distribution phase), contract unchanged |
| `LibDevicePostLink` | the post-link as the sequence of its stages, each a method or a small internal type: the wrapper inventory of the kernel PTX (called at `call` sites, defined by `.func` headers; 2026-09-26), the NVVM module from the fragments of the missing wrappers, the compilation, the insertion after the header, the definition check as a set comparison over the wrapper text, the trial load; `Link` returns what it did | internal |
| `CudaWslDevices` | the WSL workaround (2026-09-27, Constraints, "Every CUDA context of a process binds under WSL"): tries `builder.Cuda()` first, every call, and only on the resolver-already-set exception registers the devices itself by reflecting ILGPU's own internal `CudaDevice.GetDevices` | internal |

⚠ 2026-09-14: this row first read "`FunctionCount` is the constant the kernel strides by" (F-EX-07's own
wording: `public const int FunctionCount = 10;`), which would have turned `FunctionCount` from a property
into a `const` field — a second public-surface change beyond `AcceleratorInfo.CudaSkippedBecause`, which
the coding task reserves that change for alone. Confirmed red-handed by running
`Protocol.Tests.SurfaceTests` against the literal change: it failed, naming exactly this member
(`approved 'static Int32 FunctionCount { get; }', actual 'const Int32 FunctionCount = 10'`). `FunctionCount`
stays the public property (contract truly unchanged); an `internal const int StrideCount = 10` was added
beside it for the kernel to stride by (a `const` inlines into kernel-compatible code, a property touching
the managed string array `Functions` does not), and
`ProbeKernelTests.TheKernelsStrideConstantMatchesTheFunctionList` asserts `StrideCount ==
FunctionCount` so the two cannot drift silently.

Decided 2026-09-15 (the child-nodes phase; root `BOOT.md`, 0aa7e60): a cluster of this
node earns its own child directory, `BOOT.md` and `API.md` when the rest of the node
reaches it through a contract narrower than its code, it has a reason of its own to
change, and it holds about five types or more (root `BOOT.md`, the child-nodes
decision), no public type moving into the child namespace.

- **`Chunks/` passes.** `Chunk`, `ChunkPlan`, `ChunkBuffer<T>`, `ChunkBuffers`,
  `ChunkTransfer` and `IChunkBuffer` — six internal types — become
  `APThermo.Execution.Chunks`. The rest of this node reaches
  them through `ChunkPlan.For`/`.Chunks()`, `ChunkBuffers`'s declaration methods and
  `ChunkBuffer<T>.View`; `IChunkBuffer`, `ChunkTransfer` and the `Chunk` record are
  never named outside the cluster. Its reason to change — the chunking and transfer
  policy — is its own, distinct from the kernel loop (`BatchRun`, staying here) and
  the accelerator session it runs on. Its own `BOOT.md` and `API.md` hold the
  contract; this row of the table above points to them instead of repeating them.
- **`LibDevice/` fails, and stays here.** `LibDeviceLocator` and `LibDevicePostLink`
  hold three types between them (the two named classes and `LibDevicePostLink`'s
  private nested `NvvmOptions`), short of "about five types or more"; splitting three
  types into a child for a contract of one method each (`Locate`, `Link`) would add a
  directory and a document pair without narrowing anything. They stay as two files of
  this node's own directory, unchanged by this phase.
- **The ILGPU version constant stays on `LibDevicePostLink`.** `ExpectedIlgpuVersion`
  names the ILGPU release `LibDevicePostLink`'s own reflection (`AssertIlgpu`) was
  written against; every reader of it — the lazy member lookup inside the same type,
  `AcceleratorChoice`'s `AcceleratorInfo.IlgpuVersion` field, and the tests that prove
  the assertion fails loudly on a mismatch — is either inside the post-link mechanism
  or reads it as a diagnostic string, not as a general engine constant a second type
  would need to own. Nothing moves.

Decisions taken with the review of 2026-09-14:

- **The fallback says why.** `Auto` keeps falling back to the CPU accelerator, and the
  reason no longer dies in a discarded exception: `AcceleratorInfo` gains
  `CudaSkippedBecause` (null when CUDA was bound or the options asked for the CPU), the
  message of the failure that turned the choice, the forbidding variable included, with
  the paths tried where they apply. A contract change, recorded in `API.md` with its ⚠,
  the snapshot moving in the same commit; the command line prints it in the `devices`
  listing and in every document's `run.accelerator` (a later change of that node).

  ⚠ 2026-09-15: this bullet, `API.md` and `Options.cs` read "null when CUDA was not
  tried or was bound" / "null when CUDA was bound or never tried". With
  `APTHERMO_NO_CUDA=1` and `Auto`, CUDA is not skipped upfront: `AcceleratorChoice.Decide`
  still calls into `Cuda`, which throws immediately without touching any CUDA API, and
  the caught failure becomes a non-null reason (`AcceleratorChoiceTests`, the variable's
  own case). "No CUDA API is touched" (this document's invariants) is true of the driver,
  not of whether a reason is recorded; the only case with no reason at all is
  `AcceleratorKind.Cpu`, where `Cuda` is never called because CUDA was never asked for.
  Found by the repair review (R-Execution-6); the three places now read "null when CUDA
  was bound or the options asked for the CPU".
- **The missing-definition guard names the wrapper.** The check parses the wrapper text
  libnvvm returned for its `.func` definitions and compares the set with the names the
  kernel calls; it is testable without a GPU by handing it a wrapper body with one
  definition removed, and the tests node does exactly that.
- **The chunk bound counts every buffer.** `ChunkBuffers` sums the per-case strides it
  declares, so `ScratchBytes` bounds the device bytes of a chunk by construction (until
  now only the scratch and the moles were counted); results do not depend on chunking
  (Invariants), so no result moves. `ScratchBytes` must be positive, like `ChunkSize`.
- **The views structs keep their constructors.** `RocketBatchViews` (17 parameters)
  and `EquilibriumBatchViews` (12) are kernel parameter descriptors; grouping their
  views would re-emit the kernels and move the contract. They are this node's
  declared exception to the parameter rule, and so are the constructors of
  `RocketBatchResult` (10) and `EquilibriumBatchResult` (7), which mirror the batch
  results `API.md` publishes, one argument per property. The pipelines are the only
  callers of the four, and every call names its arguments, as the root requires of a
  mirrored shape. The other two views structs take six parameters and are within the
  rule.

  ⚠ 2026-09-15 (distribution phase): this bullet said the two views structs "are
  kernel parameter descriptors ILGPU requires to be public". Wrong: ILGPU 1.5.3 needs
  only `[assembly: InternalsVisibleTo("ILGPURuntime")]` on the declaring assembly, not
  a public type (the API review of that day, section 3, fixed in `9036c6a`,
  ran the failure and the fix on the CPU accelerator and on CUDA; the claim entered
  with `f2e5de7` and `53ec9fb` on 2026-09-12 from an observed failure that never tried
  the grant). All four views structs are internal now, with that grant on
  `APThermo.Execution.csproj`; the reason for the declared parameter-count exception
  is unchanged.

  ⚠ 2026-09-14: this bullet stood "`RocketBatchViews` (17 parameters),
  `EquilibriumBatchViews` (12) and the other two are the kernel parameter descriptors
  ILGPU requires to be public; … They are this node's declared exception to the
  parameter rule; the pipelines are their only callers and fill them by name". Two
  claims were wrong, found by a scan of every construction site after the coupling
  measurement: `SpeciesFunctionBatchViews` and `TransportBatchViews` take six
  parameters and need no exception, and the pipelines passed the views and the
  results by position, not by name. The result constructors, over the rule as well,
  were not declared.
- **The unreachable checks go.** Every array of a batch is assigned once in its
  constructor from one count, so "arrays of inconsistent lengths" cannot happen; the
  four branches and the row of `API.md` go, replaced by the sentence that the
  constructor guarantees it. The element- and species-count checks stay.
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

⚠ 2026-09-27: `Kernels`' row read 25. `Kernels.Probe` now calls the thermo node's
`KernelMath.Min` and `KernelMath.Max` in place of `System.Math.Min`/`Max` (the root's
math constraint), which names one more type of the tree. Measured 26 by the
dependency check's walk on this commit; the protocol tests node's `ShapeTests`
confirms it.

## Acceptance criteria

- [x] 2026-09-12 — Probe kernel with every function of the root's math list: loads on
      CUDA through the post-link, and its results equal the CPU accelerator within the
      tolerance table (execution tests node; `ProbeKernelTests`:
      `TheCpuAcceleratorReproducesDotnetMathExactly`,
      `CudaMatchesTheCpuAcceleratorWithinTheUlpBoundForEveryFunction`).
- [x] 2026-09-12 — The rocket batch of 100 000 cases on CUDA equals the same batch on
      the CPU accelerator within the tolerance table; the compared fields are
      enumerated by reflection over `MixtureState` and `PerformanceFigures`
      (`CudaTests.TheSweepOf100000CasesOnCudaMatchesTheCpuAcceleratorAndIsDeterministic`,
      long-running). The table has two tiers for mole fractions, by whether both
      accelerators stopped after the same number of Newton steps at the station: see
      the tests node's invariants for the finding behind it.
- [x] 2026-09-12 — Throughput: the 100 000-case rocket batch on CUDA is at least 5×
      faster than on the CPU accelerator with all cores on the reference machine; the
      measured figures are written to the approved benchmark file (long-running test
      `CudaTests.ThroughputIsRecordedAndNotBelowTheApprovedRatio`;
      `tests/Execution.Tests/Throughput.approved.txt`: 56.28×).
- [x] 2026-09-12 — With `APTHERMO_NO_CUDA=1` every test of this node passes on the CPU
      accelerator and no CUDA API is called (verified by the absence of `nvcuda` and
      `nvvm` in the loaded modules of the test process:
      `AcceleratorChoiceTests.NoCudaDriverIsLoadedInAProcessThatForbidsCuda`;
      the whole solution's suite run with the variable set, see the root's criteria).
- [x] 2026-09-12 — With `AcceleratorKind.Cuda` and libdevice paths pointing nowhere,
      the error names every path that was tried
      (`AcceleratorChoiceTests.AnExplicitCudaRequestWithPathsNowhereNamesEveryPathTried`,
      `DiscoveryReportsTheToolkitPathsItExamined`).
- [x] 2026-09-12 — The ILGPU version and reflected members are asserted at startup; a
      mutation test proves the assertion fails loudly
      (`AcceleratorChoiceTests.TheIlgpuAssertionFailsLoudlyForAnotherVersion`
      asserts against a wrong version; the mutation of `ExpectedIlgpuVersion` in the
      tests node's evidence list makes every test of the node red).
- [x] 2026-09-12 — Two runs of the same batch on the same accelerator are bit-identical
      (`BatchTests.ChunkingAndRepetitionDoNotChangeABit` on the CPU accelerator,
      the sweep test above on CUDA).
- [x] 2026-09-12 — The species-function batch equals the host calls of `Thermo`'s
      functions bit for bit on the CPU accelerator and matches CUDA within the tolerance
      table, inside and outside the records' ranges (`SpeciesFunctionTests`:
      `TheCpuAcceleratorEqualsTheHostFunctionsBitForBit`,
      `CudaMatchesTheCpuAcceleratorWithinTheTable`,
      `ASpeciesIndexOutsideTheTableIsRefusedBeforeAnyKernelRuns`).
- [x] 2026-09-14 — The decomposition of 2026-09-14 (`## Structure`): no type or method
      of the node above the root's code-shape limits, the declared exceptions being
      the four views structs' constructors and `Engine`'s and `Kernels`' Ce, both
      named in `## Structure`; covered by the protocol tests node's `ShapeTests`, all
      ten facts green at `62cd99e` — the public surface changed only by
      `AcceleratorInfo.CudaSkippedBecause`, in `c10ab0e` alone
      (`git diff 6af23b1..HEAD -- tests/Protocol.Tests/PublicSurface.approved.txt`:
      one line added, that property; `Protocol.Tests.SurfaceTests` green against it
      unchanged since); `BatchTests.ChunkingAndRepetitionDoNotChangeABit`, the
      probe, species-function and accelerator-choice tests green
      (`APThermo.Execution.Tests.dll`: 41 passed); the fast
      suite of the whole solution green (`dotnet test
      APThermo.sln --filter "Category!=LongRunning"` with
      `APTHERMO_NO_CUDA=1`: 2147 passed, 0 failed, 0 skipped). The CUDA sweep and the
      throughput benchmark are the orchestrator's to run once at the end, after the
      merge, on the reference machine (not run from this worktree).

      ⚠ 2026-09-14: this criterion's "no type or method of the node above the root's
      code-shape limits" was evidenced only by `python inventory.py .`'s line counts
      (type and method lines, plus the two declared Ce exceptions), not by nesting.
      The protocol tests node's own measurement the same day found
      `LibDeviceLocator.Locate` nesting 4 deep: the `if (File.Exists(bitcode))` inside
      the `if (File.Exists(dll))` inside two `foreach` loops, over the root's limit of
      3. `5e3a24b` turns the inner check into a guard clause
      (`if (!File.Exists(dll)) continue;`) and brings `Locate` to depth 3, examining
      the same paths in the same order.

      ⚠ 2026-09-15: these figures described the code at `42efbe7`, before `e453063`
      reformatted `RocketPipeline.Run`'s two wide constructor calls onto named
      arguments; `## Shape exceptions` now holds ten rows: six efferent-coupling
      rows, the four pipelines among them, and the constructors of
      `RocketBatchViews`, `EquilibriumBatchViews`, `RocketBatchResult` and
      `EquilibriumBatchResult`. `RocketPipeline.Run`'s size afterward is
      `ShapeTests.NoMethodSpansMoreThan60Lines`'s to state; this node records
      no line figure of its own. Found by the repair review (R-Execution-5).
- [x] 2026-09-14 — The fallback names its reason: with `Auto`, `LibDeviceDiscovery`
      off and the explicit paths pointing nowhere, the engine is the CPU one and
      `CudaSkippedBecause` names what was missing and the paths tried
      (`AcceleratorChoiceTests.AnAutoFallbackSaysWhyCudaWasSkippedAndWhichPathsWereTried`,
      the mirror of `AnExplicitCudaRequestWithPathsNowhereNamesEveryPathTried`);
      committed in `c10ab0e`, where the equivalent test against the code of `8e36a27`
      (where `AcceleratorInfo` said nothing) would have been red.
- [x] 2026-09-14 — The missing-definition guard of the post-link is proven
      non-degenerate: a wrapper body with one definition removed makes the check name
      that wrapper, without a GPU
      (`PostLinkTests.AWrapperBodyWithOneDefinitionRemovedNamesThatWrapper`,
      `..._with_every_definition_removed_names_every_wrapper`, and
      `ACallSiteIsNotMistakenForADefinition` against the two-substring-search
      shape the guard had before `23ccc1d`, which read the whole linked text instead
      of the wrapper body alone).
- [x] 2026-09-14 — `ScratchBytes` of zero or less is refused at `Create` naming the
      option, like `ChunkSize`
      (`AcceleratorChoiceTests.ChunksAreBoundedByTheChunkSizeAndTheScratchMemory`,
      committed in `fcb1128` with the chunk-plan extraction); the "inconsistent lengths" row was
      never a separate row of `API.md`'s error table by 2026-09-14
      (already merged into the one row above it), and the four branches that could
      not fire are gone from `Batches.cs` (`23ccc1d`).
- [x] 2026-09-14 — Every creation of the node's four wide constructors names its
      arguments (`RocketBatchViews`, `EquilibriumBatchViews`, `RocketBatchResult`,
      `EquilibriumBatchResult`; the decision "The views structs keep their
      constructors"), the protocol tests node's named-construction fact green once it
      exists; the emitted kernels unchanged, the tests node's fast set green on the CPU
      accelerator and on CUDA. A scan of every `new T(…)` and `T x = new(…)` in `src/`
      and `tests/` (a script outside the tree) finds the four sites of the node's types,
      in `RocketPipeline` and `EquilibriumPipeline`, every argument named; the build of
      `Execution` after the change carries the IL of the build before it, method by
      method, kernels included, so no argument binds to another parameter; the fast set
      green with `APTHERMO_NO_CUDA=1` (41 tests on the CPU accelerator) and without it
      (the same 41 on CUDA). The fact,
      `ShapeTests.EveryWideConstructorIsCalledWithNamedArguments`, is designed
      and not yet written; it takes over as the evidence when it is.
- [x] 2026-09-15 — `Engine.ProbeMath`'s dead `RunTimer` (a `KernelCache.Get` overload
      once needed it; `a2a1d6e`'s `out warmUp` overload made it unreachable, and the
      two lines allocating and discarding one stayed) is gone: `out var warmUp` is
      `out _`. `RunTimer` was the method's only use of that type, so `Engine`'s
      efferent coupling fell from 26 to 25, the figure the Shape exceptions row above
      now carries; the Structure row's own wording is unchanged, since it already
      described `ProbeMath` correctly. Found by the repair review (R-Execution-1).
      Verified: the CPU-accelerator fast suite green (`APTHERMO_NO_CUDA=1`), the
      re-measured Ce confirmed by
      `ShapeTests.EveryShapeExceptionIsMeasuredAndStillNeeded`, which holds
      the `Engine` row at 25 on the merged tree.
- [x] 2026-09-15 — `LibDevicePostLink.CompileAgainstLibdevice`, extracted from
      `CompileWrappers` in `23ccc1d` "bringing its nesting back to 3", took every
      parameter and local of its caller (six, the root's limit) and existed only to
      hold the `unsafe`/`fixed` block: the nesting measure counts only
      `if`/`for`/`foreach`/`while`/`do`/`switch`/`try`, so the extraction bought
      nothing the measure itself cares about. Merged back into one `CompileWrappers`
      (create the program, build the options, add both modules under one `fixed`,
      compile, log and throw, read the compiled result, destroy the program in
      `finally`); `NvvmOptions`, which owns the unmanaged allocations, is unchanged.
      The merged method now satisfies both
      `ShapeTests.NoControlFlowNestsDeeperThan3` and
      `ShapeTests.NoMethodSpansMoreThan60Lines`. Found by the repair review
      (R-Execution-2). Verified on the reference machine, `APTHERMO_NO_CUDA` unset:
      `tests/Execution.Tests/ProbeKernelTests.CudaMatchesTheCpuAcceleratorWithinTheUlpBoundForEveryFunction`
      green, exercising this exact method on real hardware (the probe kernel's
      wrappers compiled by it, linked, run, and matching the CPU accelerator within
      the ULP bound).
- [x] 2026-09-15 — Every ticked criterion above re-verified on the decomposed and
      repaired code at `62cd99e`: its tests green in the full suite
      (`APTHERMO_NO_CUDA=1`, every category, 3037 tests, none skipped), and
      CUDA-category evidence on the reference machine (`tests/Execution.Tests`, 41,
      and the long-running sweep and throughput tests).
- [x] 2026-09-15 (distribution phase) — Linux libdevice discovery, added for the root's
      Platform constraint (`cf87211`): `LibDeviceLocator` branches on
      `OperatingSystem.IsWindows()` / `IsLinux()` and, on Linux, tries `CUDA_PATH`, then
      `CUDA_HOME`, then `/usr/local/cuda`, then `/usr/local/cuda-*` newest first, each
      root's `nvvm/lib64/libnvvm.so` paired with `nvvm/libdevice/libdevice.10.bc`; on
      any other OS no discovery runs and the CPU accelerator is used. Covered by
      `tests/Execution.Tests/LibDeviceDiscoveryTests.cs`, driven through the internal
      seam (`LibDeviceLocator.Locate(EngineOptions, LocatorPlatform, Func<string,
      string?>, string)`) so both platforms and both Windows dll layouts (12.x
      `nvvm\bin`, 13.x `nvvm\bin\x64`) are exercised from one host OS, over fake
      toolkit trees under a temp directory: explicit paths win and are tried first;
      an unsupported platform does no discovery; `CUDA_PATH` before the toolkit
      directories on Windows and before `CUDA_HOME`, the fixed root and the versions
      on Linux; both platforms order their versioned toolkit directories by parsed
      `Version`, newest first (proven against a case where numeric and alphabetical
      order disagree, `v13.3`/`v9.0` and `cuda-13.3`/`cuda-9.0`); a library present
      without its bitcode is passed over for the next root; a root named twice (by
      `CUDA_PATH` or `CUDA_HOME` repeating an already-tried directory) is tried once —
      12 facts, `dotnet test tests/Execution.Tests --filter
      "FullyQualifiedName~LibDeviceDiscoveryTests"`, all green. The ordering fact was
      shown red once and reverted (AGENTS.md §13): `VersionedDirectories`'s
      `OrderByDescending` flipped to `OrderBy` reddened both
      `WindowsOrdersTheToolkitDirectoriesNewestVersionFirst` and
      `LinuxOrdersTheVersionedDirectoriesNewestFirst`, reverted before
      committing. The unavailable-accelerator message and the `EngineOptions` doc
      comments now name the platform's library instead of `nvvm64_40_0.dll`
      unconditionally (`LibDeviceLocator.LibraryFileName`); `AcceleratorChoiceTests`
      unchanged in behaviour, its one hard-coded `nvvm64_40_0.dll` assertion now reads
      the same property. `LibDevicePostLink` and every other file of this node were
      checked for Windows-only assumptions (path separators, `.dll` literals,
      case-insensitive comparisons) and none were found outside `LibDeviceLocator`,
      `AcceleratorChoice`'s message and `Options.cs`'s doc comment, all covered above.
      Verified on the reference machine, CUDA present: build clean, 0 warnings;
      `dotnet test tests/Execution.Tests --filter "Category!=LongRunning"`, 53 of 53
      green (41 pre-existing plus these 12), including the CUDA-category tests, so
      real discovery still finds the installed toolkit through the unchanged default
      `Locate(EngineOptions)` entry point; the whole solution's fast set green (3047
      tests, 0 failed, `Category!=LongRunning`, CUDA-category tests exercised since
      the machine has a device); `protocol_lint` 0 errors, 0 warnings; every
      `Bits.approved.txt` and the surface snapshot unchanged (the seam is internal, no
      public type added).

- [x] 2026-09-26 — No libnvvm or driver result is ignored: no `NvvmResult` or
      `CudaError` returned by a call of the post-link is discarded (no `_ =` on such a
      call remains in `LibDevicePostLink.cs`); the result-to-exception method is tested
      with every non-success `NvvmResult` and a failing `CudaError`; the CUDA tests of
      this node are green in Release, the long-running sweep and the throughput tripwire
      included, with no `Bits*` or `Throughput*` record moving; the fast suite and the
      lint are green.

      Implemented as two `ThrowIfFailed` overloads (`NvvmResult`, `CudaError`), the one
      shape every checked call throws in: `"the libdevice post-link for {arch}: libnvvm
      {call} returned {result}"` or `"…: the CUDA driver's {call} returned {result}"`,
      followed by `": {log}"` where one exists (or the "log could not be read (…)" text
      below, in its place). Every checked call of the invariant's list (`GetIRVersion`,
      `CreateProgram`, `AddModuleToProgram`, `LazyAddModuleToProgram`, `CompileProgram`,
      `GetProgramLog`, `GetCompiledResult`, `DestroyProgram`, `LoadModule`,
      `DestroyModule`) now goes through one of the two overloads; the compile log is
      read only after a failed `CompileProgram` (`ThrowCompileFailure`), and a
      `GetProgramLog` failure of its own is folded into the compile exception's message
      ("the log could not be read (…)") rather than raising a second exception.
      `DestroyProgram`'s release in `CompileWrappers`'s `finally` (`ReleaseProgram`) is
      attempted either way, but its own result is checked (and can throw) only when the
      path before it succeeded; when an earlier call's exception is already propagating
      through the same `finally`, the release's result is not checked at all, so it
      cannot throw a second exception that would replace the one already in flight — no
      catch of any kind is needed for this. `DestroyModule` in `TrialLoad` is reached
      only after `LoadModule` succeeded, so it is always checked in the ordinary way.

      Evidence, on this worktree (branch `claude/nvvm-result-codes`, on top of
      `6d5d57e`):
      - a repository-wide search for the old silent-discard pattern (an underscore
        assigned to the return value of an `nvvm.` or `CudaAPI.` call) over `src` finds
        none; this file's own history note above, which once quoted that pattern in
        prose, was reworded to drop it, without changing its meaning — quoting the
        pattern verbatim here would trip the same search against this paragraph;
      - `dotnet build APThermo.sln`: 0 warnings, 0 errors;
      - `PostLinkTests` (`tests/Execution.Tests/PostLinkTests.cs`):
        `TheSuccessResultOfEitherKindThrowsNothing`,
        `EveryNonSuccessNvvmResultNamesTheLibraryTheCallTheResultAndTheTarget` (a theory
        over every value of `NvvmResult` but `NVVM_SUCCESS`, read from the enum: 9
        cases, each asserting the message names "the libdevice post-link", "libnvvm",
        the call, the result and the target),
        `EveryNonSuccessCudaErrorNamesTheLibraryTheCallTheResultAndTheTarget` (the same
        over every value of `CudaError` but `CUDA_SUCCESS`: 58 cases, asserting "the
        libdevice post-link", "the CUDA driver", the call, the result and the target),
        `ALogWhereOneExistsIsCarriedInTheMessage`,
        `WithoutALogTheMessageStillNamesTheLibraryTheCallTheResultAndTheTarget`, and the
        four pre-existing `AssertEveryWrapperDefined` facts, all green;
      - `APTHERMO_NO_CUDA=1 dotnet test APThermo.sln --no-build --filter
        "Category!=LongRunning"`: 3178 of 3178, none skipped (3108 before this change
        plus the 70 new theory cases and facts above);
      - `dotnet test tests/Execution.Tests -c Release` on the reference machine (RTX
        5070 Ti), no filter: 126 of 126, the long-running 100 000-case sweep and the
        throughput tripwire included (124 of the fast set plus these 2);
      - no `Bits*.approved.txt` or `Throughput*.approved.txt` differs from `main`;
      - the protocol lint: 0 errors, 0 warnings.

      ⚠ 2026-09-26, review: the first cut named only the call and the result
      ("CompileProgram failed for compute_120 (…)"), losing what actually failed — that
      it was the libdevice post-link, and which library. The message now leads with
      "the libdevice post-link for {arch}" and names the library ("libnvvm" or "the CUDA
      driver's") beside the call, and `ReleaseProgram`'s best-effort branch dropped the
      throw-then-catch-the-exact-type pattern (an empty catch block) for the simpler
      shape above: the release's own result is stored but checked only on the success
      path, so nothing is ever thrown and swallowed.

- [x] 2026-09-26 — Every GPU architecture (the root's criterion of the same date; audit
      finding F1).
      - **The architecture fact** (`Category=Cuda`, `Category=LongRunning`). The architectures are every
        `CudaArchitecture` ILGPU 1.5.3 declares from SM_75 up, and the entry points are
        every entry point of `Kernels`; both lists come from reflection. For each
        pair, the fact compiles the entry point with a `PTXBackend` for that
        architecture and the session's libnvvm, passes it through `Link`, and loads it
        on the reference device. It then asserts four things:
        - both paths of the post-link occur: at least one architecture where ILGPU
          defined every wrapper and `Link` compiled none, and one where `Link` compiled
          them all;
        - every kernel's PTX equals the same entry point's PTX for the device's own
          architecture, once ILGPU's generated numeric suffixes, comment lines, blank
          lines and the `.target` line are set aside;
        - the probe launched from each architecture's kernel returns the engine's own
          CUDA probe bit for bit;
        - it stays within `GpuCpuTolerances.MathUlp` of the CPU accelerator.

        Implemented as `ArchitectureTests.EveryArchitectureFromSm75UpPassesThePostLinkAndMatchesTheDevice`
        (`tests/Execution.Tests/ArchitectureTests.cs`), reflecting over the 11 `CudaArchitecture`
        fields from `SM_75` to `SM_121` and the 5 entry points of `Kernels`
        (`Equilibrium`, `Rocket`, `Transport`, `Functions`, `Probe`); green on the
        reference machine (RTX 5070 Ti, SM_120, libnvvm 13.4). `SM_75`..`SM_90` measured
        `DefinedByIlgpu.Count > 0` and `Compiled.Count == 0` (ILGPU defined every
        wrapper); `SM_100`..`SM_121` measured the reverse (the post-link compiled every
        wrapper), so both paths are exercised. Every architecture's normalized PTX
        equalled the device's own (`SM_120`'s), and the probe matched the engine's own
        CUDA probe bit for bit and the CPU accelerator within `GpuCpuTolerances.MathUlp`
        on every architecture.

        Shown red once, reproduced directly against `LibDevicePostLink` as it stands at
        `9c33398` (a throwaway repro compiling `Kernels.Probe` for `SM_75`, `SM_80`,
        `SM_86`, `SM_89`, `SM_90` and calling the old `Link`, not committed): every one
        threw `InvalidOperationException`, "the kernel calls the libdevice wrapper
        __nv_exp_param_0, for which ILGPU 1.5.3.0 has no fragment.", exactly the message
        this criterion predicted.

        ⚠ 2026-09-28: this record said "about three minutes" for the fact and "first
        (cold JIT cache) run 3 m 41 s, subsequent runs about 14 s once the CUDA driver's
        own compute cache is warm". The second audit's warm-up measurement (finding,
        "The first audit's fixes", observation 3) found the driver's compute cache is
        keyed by the PTX text and ILGPU's generated names come from process-wide
        counters, so every engine after the first in a process misses it regardless of
        an earlier run; the fact creates several engines and never warms a shared
        cache. Measured again: 8 m 2 s on Windows, 11 m 3 s under WSL2, both green. The
        assertions and the architecture and entry-point coverage are unchanged; only
        the duration claim was wrong.
      - **The wrapper inventory without a GPU**, on the hosted runners. Two text
        fixtures hold ILGPU 1.5.3's PTX of the probe kernel: one for SM_89, which
        defines the wrappers, and one for SM_120, which defines none. Their provenance
        is in the tests node's `BOOT.md`. On both, the inventory reads the same called
        set, the math list's libdevice functions. It reads them all as defined on
        SM_89 and none on SM_120. No `_param_` name is read as a call. The result is
        the same with LF and CRLF line ends.

        Implemented as `WrapperInventoryTests` (`tests/Execution.Tests/WrapperInventoryTests.cs`,
        5 facts, no GPU needed), over `tests/Execution.Tests/Ptx/probe.sm_89.ptx` and
        `probe.sm_120.ptx`, generated on the reference machine with libnvvm 13.4 the same
        day (a throwaway generator, not committed). Shown red once against the old
        `WrapperCall` regex (`__ilgpu__nv_[A-Za-z0-9_]+`, no `call`-site or comma
        requirement, `WrappersCalled` reading `m.Value` instead of a capture group): 3
        of the 5 facts failed —
        `OnSm89EveryCalledWrapperIsAlreadyDefined` and
        `BothArchitecturesCallTheSameWrappers`, "Assert.Equal() Failure: HashSets differ
        … Expected: [\"__nv_exp\", \"__nv_exp_param_0\", \"__nv_log\",
        \"__nv_log_param_0\", \"__nv_log10\", ···] … Actual: [\"__nv_exp\", \"__nv_log\",
        \"__nv_log10\", \"__nv_pow\", \"__nv_sqrt\", ···]", and
        `NoParameterNameIsReadAsACall`, "Assert.DoesNotContain() Failure: Filter matched
        in collection … Collection: [\"__nv_exp\", \"__nv_exp_param_0\", \"__nv_log\",
        \"__nv_log_param_0\", \"__nv_log10\", ···]" — the parameter names read as calls,
        exactly the defect. Reverted; a clean rebuild confirmed all 5 green again.
      - **The bind-time probe** (`Category=Cuda`). The real libnvvm is paired with a
        libdevice path to a file that is not libdevice bitcode, and discovery is off:
        - `Auto` binds the CPU accelerator, and `CudaSkippedBecause` names the
          libdevice post-link;
        - `Cuda` throws `AcceleratorUnavailableException` whose inner exception is the
          post-link's.

        ILGPU's own accelerator constructor does not refuse such a file at context
        creation (it validates libdevice's content only lazily, when the post-link
        actually compiles against it), so no substitute input was needed: a file that
        exists but holds arbitrary text reaches the post-link at bind and fails there
        with `NVVM_ERROR_COMPILATION`, wrapped as designed. Implemented as
        `AcceleratorChoiceTests.AnAutoFallbackNamesThePostLinkWhenTheProbeKernelCannotBind`
        and `...AnExplicitCudaRequestFailsWithThePostLinksOwnExceptionWhenTheProbeKernelCannotBind`,
        green on the reference machine.
      - **Nothing else moves.** No `Bits*.approved.txt`, `Throughput*.approved.txt` or
        `PublicSurface.approved.txt` changed
        (`git diff 8dfe20f --stat -- '**/Bits*.approved.txt' '**/Throughput*.approved.txt'
        '**/PublicSurface.approved.txt'` empty). The node's CUDA tests are green in
        Release (`dotnet test tests/Execution.Tests -c Release`, no filter, from a clean
        `bin`/`obj`): 134 of 134, the sweep and the throughput tripwire included. The
        fast suite (`APTHERMO_NO_CUDA=1 dotnet test APThermo.sln --filter
        "Category!=LongRunning"`, from a clean `bin`/`obj`): 3185 of 3185, none skipped
        (3178 before this change plus the 5 inventory facts and the 2 bind-time facts).
        The protocol lint: 0 errors, 0 warnings.
      - **The records.** `API.md` already stated the bind-time probe (under `Engine`,
        and in the Errors table) from the design; `CHANGELOG.md` names the fix under
        `[Unreleased]`'s "Fixed" (the release after 0.1.0 is 0.2.0, per the Diagnostics
        phase's binary break already recorded there).

- [x] 2026-09-26 — The audit's F2, F3 and observations (Constraints), every fact but
      one (noted below) shown red once against the code before the change:
      - **The bad library.** `AcceleratorChoice.Cuda` now loads libnvvm and asks its IR
        version, then reads the bitcode, through one new internal `LoadNvvm`, before
        `CreateAccelerator` (also new) ever creates a CUDA context; the session keeps
        that one `NvvmAPI` binding, so no second `NvvmAPI.Create` follows once the
        accelerator is up. A file named as the platform's libnvvm that is not a
        library, with the real bitcode and discovery off
        (`BadLibraryTests.ABadLibraryNamesBothPathsAndNeverReachesTheDevice`,
        `Category=Cuda`):
        - `Cuda` throws `AcceleratorUnavailableException` naming both paths;
        - `Auto` binds the CPU with a `CudaSkippedBecause` that names the library
          path;
        - on the reference machine, the free device memory after 20 such `Auto`
          creations is within 64 MiB of the memory before it (measured: 15 037 MiB
          before, 15 037 MiB after, 0 MiB lost — the audit's own run of the
          pre-fix code lost 3 800 MiB over the same 20 attempts).

        Shown red once against the pre-fix order (device before the library,
        unwrapped): the same fact failed —
        `Assert.Throws() Failure: Exception type was not an exact match Expected:
        typeof(APThermo.Execution.AcceleratorUnavailableException) Actual:
        typeof(System.BadImageFormatException)`, its inner exception "An attempt was
        made to load a program with an incorrect format. (0x8007000B)" — exactly the
        audit's own finding; reverted before committing.
      - **All cores.** `AcceleratorChoice.CpuDeviceFor(int)` sizes the CPU device from
        `Environment.ProcessorCount`. Below 4 processors the warp size alone carries the
        count (`Math.Max(2, count)`, one warp, one multiprocessor: ILGPU refuses a
        one-thread warp, so 2 and 3 match exactly and 1 does not). At 4 and above the
        warp size stays fixed at 4 — so 16 processors still reduce to exactly (4, 4, 1),
        the layout every bit and throughput record was measured against — and the count
        of whole 4-thread groups the processor count allows,
        `fourThreadGroups = processorCount / 4`, splits into a power-of-two warps count
        (its lowest set bit) and a multiprocessor count (the remaining factor), whose
        product reconstructs `fourThreadGroups` exactly; a processor count that is not a
        multiple of 4 falls back to the largest multiple of 4 not above it, since every
        group total this construction can reach is already at or under the count.

        ILGPU 1.5.3's `CPUDevice` constructor was measured directly by reflection
        (its summary records the figures): the warp size needs no upper bound and only
        refuses 1; the warps per multiprocessor must be a power of two, and every
        non-power-of-two value tried (3, 5, 6, 7, 9, 10, 12, 24, 48) throws
        `ArgumentOutOfRangeException`, misnaming `numThreadsPerWarp` although the warps
        argument is the one at fault; the multiprocessor count carries no constraint
        ILGPU checks at all, from 2 to 1 000 000. The corrected construction uses only
        layouts this probe confirmed the constructor accepts.

        Proven under `DOTNET_PROCESSOR_COUNT` 4, 12, 16 and 64, each its own
        `dotnet test` child process — `Environment.ProcessorCount` is read once, at
        process start — spawned by the same test class acting as its own worker
        (`AllCoresLayoutTests.TheCpuEngineReportsTheDocumentedLayoutAtEveryProcessorCountAndResultsDoNotMove`,
        `AllCoresLayoutWorker`, `tests/Execution.Tests`): the reported thread count is
        4, 12, 16 and 64 respectively, and a rocket batch's result hash (specific
        impulse, c*, thrust coefficient over every station) is identical at every
        count. A second, host-only fact
        (`TheAllCoresLayoutMatchesEveryProcessorCountOrTheDocumentedFallback`) asserts
        the layout's thread total, with no child process, at every count of 1, 2, 3, 4,
        6, 8, 12, 16, 20, 24, 32, 48, 64 and 128: exact at every one of them except 1
        (the ILGPU floor of 2 threads exceeds it) and 6 (not a multiple of 4, so the
        layout falls back to 4), each asserted against its documented fallback instead
        of equality.

        ⚠ 2026-09-26: this criterion first described the construction above as "one
        multiprocessor throughout", the warps count alone reaching the largest power of
        two not over the count. The coordinator's review found that this loses threads
        at every count that needs more than one multiprocessor to reach exactly: 12 → 8,
        24 → 16, 48 → 32, 20 → 16. The construction now chosen keeps the warp size fixed
        at 4 from 4 processors up (so 16 still reduces to (4, 4, 1)) and uses the
        multiprocessor count, not just the warps count, to reach every multiple of 4
        exactly, as ILGPU's own unconstrained multiprocessor argument allows. Shown red
        once against the superseded rule: the new host-only fact failed at 12 —
        `Assert.Equal() Failure: Values differ Expected: 12 Actual: 8` — matching the
        coordinator's own example; reverted before committing. The superseded rule's own
        red-once record (against `builder.CPU()`, ILGPU's fixed 16-thread
        `CPUDevice.Default`, failing the 4-processor child process with
        `Assert.Equal() Failure: Values differ Expected: 4 Actual: 16`) still holds for
        the corrected rule, unchanged by this correction.
      - **Observations.** `LibDevicePostLink.FailureMessage` now trims a log of NUL
        (`\0`) alongside whitespace (`TrimLog`), so a NUL-padded driver or libnvvm log
        carries no NUL in the exception message
        (`PostLinkTests.ALogWithNulPaddingIsTrimmedOfIt`, shown red once against a plain
        `.Trim()`: the message still held the NUL). `Engine.Create` refuses one of
        `LibNvvmPath`/`LibDevicePath` given without the other with an `ArgumentException`
        naming the missing option, rather than silently falling through to discovery as
        `("", path)` used to (`AcceleratorChoiceTests.AHalfGivenExplicitLibraryPairIsRefused`,
        shown red once: no exception thrown against the code before this change; the row
        `API.md` already carried from the design). `Engine.Upload` disposes the species
        buffers it already uploaded when the transport table's own upload then fails —
        correct by inspection of the try/catch/dispose pattern, with no dedicated
        reproduction: the tree's only path to a `TransportTable` is `TransportTable.Build`,
        which always produces an internally consistent shape, so no legitimate call
        makes `TransportTableBuffers.Upload` fail short of exhausting device memory.
      - **Nothing else moves.** No `Bits*.approved.txt`, `Throughput*.approved.txt` or
        the protocol tests node's `PublicSurface.approved.txt` changed (`git status
        --short -- '**/Bits*.approved.txt' '**/Throughput*.approved.txt'
        '**/PublicSurface.approved.txt'` empty); `Throughput.approved.txt` still reads
        "cpu: CPUAccelerator with 16 threads" on the reference machine, unchanged
        (16 processors reduces to the same (4, 4, 1) layout as before). The node's CUDA
        tests are green in Release. `API.md`'s Errors table already stated the
        half-pair refusal from the design; `Options.cs` and every "all cores" sentence
        of this node are now literally true, not only on the reference machine.

      Evidence, on the reference machine (RTX 5070 Ti, driver 13.4, CUDA toolkits
      12.9/13.3/13.4), from a tree with every `bin` and `obj` removed:
      - `dotnet build APThermo.sln`: 0 warnings, 0 errors;
      - `APTHERMO_NO_CUDA=1 dotnet test APThermo.sln --no-build --filter
        "Category!=LongRunning"`: 3191 total, 3190 passed, 0 skipped; the one failure,
        `Protocol.Tests.DeclarationTests.EveryDeclarationUnderATickExists` on
        `src/Performance/API.md`'s `RocketSolver.MaxThroatBisections`, predates this
        change (present on an untouched checkout of the same commit, `src/Performance`
        and `tests/Protocol.Tests` outside this coding task's subtree) and is the
        coding half of the Performance/Transport hidden-defect audit's own design
        commit (`9a6888f`), not yet landed;
      - `dotnet test tests/Execution.Tests -c Release` (no filter): 140 of 140 (134
        before this change plus six new facts: `ChunksStayWithinInt32OffsetsAtTableLimits`,
        `AllCoresLayoutWorker`, `TheCpuEngineReportsTheDocumentedLayoutAtEveryProcessorCountAndResultsDoNotMove`,
        `BadLibraryTests.ABadLibraryNamesBothPathsAndNeverReachesTheDevice`,
        `PostLinkTests.ALogWithNulPaddingIsTrimmedOfIt`,
        `AcceleratorChoiceTests.AHalfGivenExplicitLibraryPairIsRefused`), the
        100 000-case sweep and the throughput tripwire included;
      - `git status --short -- '**/Bits*.approved.txt' '**/Throughput*.approved.txt'
        '**/PublicSurface.approved.txt'` empty: no snapshot moved;
      - the protocol lint: 0 errors, 0 warnings.

      Evidence for the "All cores" correction above (2026-09-26, on the same reference
      machine), added to the evidence already recorded, not replacing it:
      - `dotnet build APThermo.sln -c Release`: 0 warnings, 0 errors;
      - `dotnet test tests/Execution.Tests -c Release` (no filter): 141 of 141 (the
        140 already recorded plus
        `TheAllCoresLayoutMatchesEveryProcessorCountOrTheDocumentedFallback`), the
        100 000-case sweep and the throughput tripwire included;
      - `APTHERMO_NO_CUDA=1 dotnet test APThermo.sln --no-build --filter
        "Category!=LongRunning"`: 3192 total, 3191 passed, 0 skipped; the one failure is
        the same pre-existing `DeclarationTests` fact named above, now fixed on `main`
        at `a6bc55d` (outside this coding task's subtree, not rebased onto here on the
        coordinator's own instruction);
      - `git status --short -- '**/Bits*.approved.txt' '**/Throughput*.approved.txt'
        '**/PublicSurface.approved.txt'` empty: no snapshot moved;
      - the protocol lint: 0 errors, 0 warnings.

- [x] 2026-09-27 — Every CUDA context of a process binds under WSL (Constraints).
      Implemented as `CudaWslDevices.Register` (`src/Execution/CudaWslDevices.cs`):
      tries `builder.Cuda()` first, every time (no static state records that a resolver
      was ever set); when that call throws `InvalidOperationException` ("A resolver is
      already set for the assembly"), registers the devices itself through ILGPU's
      internal `CudaDevice.GetDevices(configure, predicate, registry)`, reflected by
      name (the property and the method are internal to ILGPU, which grants this
      assembly no `InternalsVisibleTo`), with the same no-op `configure` and the same
      `predicate` (a device with a known architecture and an instruction set the PTX
      backend supports) the no-argument `Cuda()` overload passes to it. A missing
      member is an `AcceleratorUnavailableException` naming it.

      Evidence, on the reference machine:
      - **Red once, under WSL** (WSL2 Ubuntu 24.04, .NET SDK 10.0.112, CUDA 12.9
        libnvvm, a clone at `~/apthermo` whose `origin` is this repository, git
        checkout `89bb619` detached): the existing
        `AcceleratorChoiceTests.AnExplicitCudaRequestFailsWithThePostLinksOwnExceptionWhenTheProbeKernelCannotBind`
        (`dotnet test tests/Execution.Tests -c Release --filter
        "FullyQualifiedName~ProbeKernelCannotBind"`) failed on its second CUDA context
        (the `Auto` fact right before it in `AcceleratorChoiceTests` already having
        created the first) — "Assert.Contains() Failure … Not found: \"the math probe
        kernel could not be loaded\"", the message instead "the CUDA context could not
        be created (driver or device problem): A resolver is already set for the
        assembly", exactly the defect. A throwaway three-engine fact (this criterion's
        own, without `CudaWslDevices` yet — not committed) failed the same way on its
        second engine: `AcceleratorUnavailableException`, inner
        `InvalidOperationException` "A resolver is already set for the assembly.",
        through `CudaContextExtensions.CudaInternal` → `NativeLibrary.SetDllImportResolver`.
      - **Green after the fix, under WSL**, at `bfab662`: the new
        `CudaWslDevicesTests.EveryCudaEngineOfTheProcessBindsAndProbes` (three fresh
        CUDA engines, each binding and probing) and
        `AcceleratorChoiceTests.AnAutoFallbackNamesThePostLinkWhenTheProbeKernelCannotBind`
        together with `...AnExplicitCudaRequestFailsWithThePostLinksOwnExceptionWhenTheProbeKernelCannotBind`
        (the two bind-time probe facts, in one run) both green;
        `dotnet test tests/Execution.Tests -c Release`, no filter: 143 of 143, the
        100 000-case sweep and the throughput tripwire included, `git status --short`
        against `Bits*.approved.txt`, `Throughput*.approved.txt` and the protocol tests
        node's `PublicSurface.approved.txt` empty; the throughput ratio 27.48× (the
        actual run measured 34.16×, comfortably above 80 % of it and the root's 5×
        floor, so `Throughput.linux.approved.txt` was not re-approved).
      - **Green on Windows, before and after**: `dotnet test tests/Execution.Tests -c
        Release`, no filter, on a clean `bin`/`obj`: 143 of 143 both at `89bb619` (where
        the resolver defect does not exist, since `IsRunningOnWSL()` is false) and at
        `bfab662`; the throughput ratio at `bfab662` 28.38× (against the approved
        23.58×), no `Bits*.approved.txt`, `Throughput*.approved.txt` or
        `PublicSurface.approved.txt` changed; `protocol_lint` 0 errors, 0 warnings.
      - **The missing-member path**: `CudaWslDevicesTests.ARenamedIlgpuMemberNamesItself`
        calls the internal `CudaWslDevices.Reflect(registryPropertyName,
        getDevicesMethodName)` seam directly with a wrong name for each of the two
        members in turn (no WSL needed to reach it this way) and asserts the exception
        names it; the same call with the real names still resolves, proving the fact
        exercises a wrong name, not a broken reflection call.
- [x] 2026-09-28 — The audit fixes of 2026-09-28 (Constraints). Each fact is red once,
      against `5a732f0` or by the mutation named, then reverted:
      - **The probe (F1).** `Kernels.Probe` now calls `KernelMath.Min`/`Max` with the
        constant in both operand orders (`Min(v,1)`/`Max(v,1)` and `Min(1,v)`/`Max(1,v)`,
        `StrideCount` 12 → 14); `ProbeKernelTests` and `ArchitectureTests` compare every
        order against the CPU accelerator on CUDA. The two PTX fixtures are regenerated
        from the current probe on the reference machine and the wrapper-inventory facts
        (`WrapperInventoryTests`, no GPU) stay green against them (5/5).
      - **The launch budget (F2).** `LaunchBudgetTests` (7 facts): `LaunchBudget`'s
        arithmetic and `ChunkPlan.FirstChunkCases`/`NextChunkCases`, with injected times
        per case (no budget never bounds a chunk; a bounded one scales from the previous
        chunk's measured rate, never below one case; the first chunk is one wave with a
        budget and `Size` without one; later chunks stay within `Size` and the
        remainder); the timeout translation, an injected `CudaException` through
        `BatchRun.Execute` on the CPU accelerator becoming `AcceleratorUnavailableException`
        naming the limit, the chunk's case count and the CPU accelerator, red once by
        mismatching the catch filter; a non-timeout `CudaException` passes through
        unwrapped. `AcceleratorChoiceTests.TheCpuAcceleratorsBudgetIsUnboundedAndTheReferenceDevicesIsBounded`
        reads a real bind's `Engine.Budget`: unbounded on the CPU accelerator, bounded on
        the reference device (its run-time limit is enabled, on Windows and under WSL2).
        No kill is provoked anywhere: every timeout fact injects a `CudaException`
        directly into the launch delegate, on the CPU accelerator, never a real device or
        a real timeout; every real launch of this task stayed under a few seconds.
      - **The WSL workaround by `TargetSite`.** Green under WSL (`CudaWslDevicesTests.EveryCudaEngineOfTheProcessBindsAndProbes`,
        three engines in one process); `TheResolverAlreadySetFailureIsRecognisedByTargetSiteNotByMessage`
        constructs a real second-resolver failure (a fresh on-disk copy of an
        already-built assembly, loaded into its own load context, since
        `NativeLibrary.SetDllImportResolver` refuses a dynamic in-memory assembly) and a
        look-alike exception with the same English text but a different origin; red once
        by reverting `IsResolverAlreadySet` to a message-text match, which then accepted
        the look-alike.
      - **Small items.** `BatchConstructorsRefuseACountWhoseArrayOverflowsA32BitLength`
        (`EquilibriumBatch`, `RocketBatch`, `TransportBatch`, and a count just inside the
        bound still allocates); `AChunkBufferRefusesAHostArrayShorterThanTheChunkNeeds`
        (both directions); `ProbeMathRefusesAnInputCountWhoseOutputOverflowsA32BitOffset`;
        `ALogThatTrimsToNothingLeavesNoTrailingColon`, red once by narrowing the
        post-link's emptiness check from "trims to nothing" to "is null", which then left
        a trailing ": " with nothing after it.
      - **Guards (F7, F8, O2).** `AcceleratorChoice.Decide`/`Cuda` gained internal
        overloads taking an explicit `cudaForbidden` flag; `AnAutoFallbackSaysWhyCudaWasSkippedAndWhichPathsWereTried`,
        `AnExplicitCudaRequestWithPathsNowhereNamesEveryPathTried` and
        `CudaForbiddenRefusesBeforeDiscoveryEverRuns` call it directly and so run
        regardless of `APTHERMO_NO_CUDA`; red once by emptying the refusal's `tried` list
        unconditionally, which the first two facts had asserted a real list from.
        `ChunkPlanWiringTests.EachPipelinesChosenPlanRespectsItsOwnOffsetCap` (F8) drives
        three of the four pipelines' own buffer declarations (a new internal
        `DeclareBuffers` each, `SpeciesFunctionPipeline` excepted: every one of its
        strides is 1) through `ChunkPlan.For` and compares against an independently
        computed expected stride, never `buffers.MaxElementsPerCase` read back; red once
        with `MaxElementsPerCase => _buffers.Count` (still instance data, so the mutation
        compiles), all three theory rows failing. `SpeciesFunctionTests.CompareFunctions`
        (O2) is NaN-aware; `TheComparisonIsNaNAwareAndCatchesAMismatchOnlyOneSideMakesNaN`
        is red once by removing the two NaN branches, which then missed a value NaN on
        one accelerator and not the other.
      - `ArchitectureTests` gives each backend its own `NvvmAPI` (observation 4): a
        shared instance, freed once per backend's `Dispose`, only worked because the
        fixture's own CUDA engine kept the same libnvvm loaded; not independently
        reproduced with a different libnvvm (would need a second engine construction
        path this task did not build), accepted by inspection against ILGPU's
        `PTXBackend.Dispose` (`PTXBackend.cs:149-158`, cited by the audit).

      A genuine WSL race, found only on a full `-c Release` run of `tests/Execution.Tests`
      on real CUDA hardware (never on an isolated fact): `LaunchBudgetTests` had no
      xUnit `[Collection]`, so its two `Cuda`-tagged facts (a real `CudaException`
      construction, which touches the driver) could run on a separate thread
      concurrently with `EngineFixture`'s own lazy CUDA engine creation, and the two
      raced during the process's first real CUDA use: "CUDA device 0 was requested, but
      0 device(s) exist", 22 facts failing together, on about a third of full-suite runs.
      Fixed by joining `LaunchBudgetTests` to `EngineFixture.CollectionName`, serializing
      it against every other CUDA-touching class; not independently red-onced against
      the race itself, since the race was not reliably reproducible on demand, only
      observed and then absent over two full re-runs after the fix.

      Evidence, on the reference machine, from a tree with every `bin` and `obj`
      removed:
      - `dotnet build APThermo.sln`: 0 warnings, 0 errors;
      - `APTHERMO_NO_CUDA=1 dotnet test APThermo.sln --no-build --filter
        "Category!=LongRunning"`: every project green, `Execution.Tests` 159/159,
        `Protocol.Tests` 32/32 (with the `## Shape exceptions` table's `Engine` and the
        four pipelines' Ce re-measured for `LaunchBudget` and the new members);
      - `dotnet test tests/Execution.Tests -c Release` (no filter), on Windows:
        162/162, the 100 000-case sweep, the architecture fact and the throughput
        tripwire included, twice (once before and once after the collection fix, both
        green — the race was never observed on Windows);
      - the same command under WSL2 (Ubuntu 24.04, libnvvm 12.9, a throwaway scratch
        clone of this branch, never `~/apthermo`): 162/162 on the commit with the
        collection fix (a prior run of the commit before it hit the race above, 22
        failures, all resolved by the fix); `Throughput.linux.approved.txt` unchanged
        (27.48×, the 2026-09-19 figure — this task changed no numerical code path the
        throughput measures);
      - no `Bits*.approved.txt`, `Throughput*.approved.txt` or
        `Protocol.Tests/PublicSurface.approved.txt` differs from before this task's
        first commit, in this node's own subtree;
      - the protocol lint: 0 errors, 0 warnings.

      Under WSL the fast suite's Linux bit and approved-output comparisons of other
      nodes did not match at this task's base (`2744915`). That is expected: the
      numerical change of 2026-09-28 in `Equilibrium` moved the Windows records, and
      the root's platform constraint has the orchestrator record the Linux files
      under WSL after the merges. This node's own facts were green under WSL
      throughout. Reworded by the orchestrator at the merge: the coder's note
      retold other nodes' state, which AGENTS.md §8 keeps out of a node's document.
- [x] 2026-09-29 — The third audit pass of 2026-09-28 (part 2, finding 2 and the
      observation on `DeclareBuffers`) is closed.
      - **A lost context stays an `AcceleratorUnavailableException`.** After
        `CUDA_ERROR_LAUNCH_TIMEOUT` the context is lost: NVIDIA documents the error as
        sticky, and every later call on the context returns it. ILGPU 1.5.3 frees a
        CUDA buffer through `CudaException.VerifyDisposed(disposing, cuMemFree(…))`,
        which throws, with no catch on the way (`DisposeDriver`,
        `AcceleratorObject.Dispose(bool)`, `Accelerator.DisposeChildObject`). So the
        pipelines' `using var buffers`, and the engine's and the solver's disposal,
        would replace the translated exception with a raw `CudaException`. The rule,
        implemented as `AcceleratorSession.MarkLost`/`ThrowIfLost`/`DropsAfterLoss`
        (`AcceleratorSession.cs`):
        - `BatchRun.Launch` calls `session.MarkLost(timeout)` in the same catch that
          builds the translated `AcceleratorUnavailableException`, before throwing it;
        - `Engine.Guard` (every `Run` overload), `Upload` and `ProbeMath` call
          `_session.ThrowIfLost()` first: a later call on a lost session throws a new
          `AcceleratorUnavailableException` naming the earlier timeout (its message
          quoted) as its own message, with the earlier exception as its inner one — the
          consumer's remedy is a new engine or the CPU accelerator;
        - `AcceleratorSession.DropsAfterLoss(CudaError)` is the one decision behind
          every drop: true only when this session is marked lost and the error is
          `CUDA_ERROR_LAUNCH_TIMEOUT`, decided on the bare enum value so the decision
          itself needs no driver-touching `CudaException` to test (2026-09-29, the
          second review below); `DropsAfterLoss(CudaException)` is a thin extraction of
          `CudaException.Error` onto it, and is what every real catch filter reads.
          `AcceleratorSession.Dispose` reads it in three literal
          `try { … } catch (CudaException failure) when (DropsAfterLoss(failure))`
          blocks, one per disposed piece (`Nvvm`, the accelerator, the context), each
          proceeding to the next regardless (releases what it can); `Engine.DisposeAfterLoss`
          reads the same decision for `UploadedTables`' own buffers; `BatchRun`'s new
          `DisposeChunkBuffers` reads it for a pipeline's chunk buffers. Any other
          exception, or a session never marked lost, is unaffected;
        - no ILGPU type reaches a consumer; `API.md`'s errors table and the guide's GPU
          page (`docs/guide/gpu.md`) say that an engine that timed out is unusable.

        ⚠ 2026-09-29: the sketch said the drop happens "in one named place of this
        node, and nowhere else". The decision does (`DropsAfterLoss`, above), but the
        `catch` itself could not be that one place: CA2000 (this node's Diagnostics
        constraint) refuses a `new ChunkBuffers(session.Accelerator)` whose disposal is
        routed through a called method rather than a literal `Dispose()` call in the
        same method — confirmed by trying exactly that first and reading CA2000's own
        refusal. `BatchRun.Execute` therefore owns disposing the buffers it was given,
        in its own `finally`, through the new private `DisposeChunkBuffers` (the pass
        every pipeline's `Run` actually depends on for the drop); each pipeline's
        `using var buffers` still exists only to satisfy CA2000 at its own allocation
        site, and its own dispose call, reached after `BatchRun.Execute` already
        disposed the same buffers, finds every device buffer already disposed and does
        nothing (ILGPU's own dispose is idempotent, confirmed by reading
        `DisposeBase.DisposeDriver`'s `Interlocked.CompareExchange` guard). `Engine.DisposeAfterLoss`
        and `AcceleratorSession.Dispose`'s own three `catch` blocks read the same
        decision for the pieces CA2000 does not flag (an existing field's disposal, not
        a freshly allocated local).
      - **Evidence.** The timeout cannot be provoked on the reference machine (it resets
        the display driver), so every fact below injects its failure through a seam
        that needs no real device. Split across two kinds of fact, decided by whether
        the fact needs an actual `CudaException` (which loads the CUDA driver, `nvcuda`,
        into the process to build even on the CPU accelerator — see the second ⚠ below):
        - **`Cuda`-tagged, `Engine.CudaForbidden`-gated, on the reference machine only**
          (with `ALaunchTimeoutBecomesAnAcceleratorUnavailableExceptionNamingTheLimitAndTheRemedy`
          above): `ATimedOutEngineRefusesANewCall` runs the real translation
          (`Engine.RunBatchLoop` with a launch delegate that throws
          `CudaException(CUDA_ERROR_LAUNCH_TIMEOUT)`) and then calls `cpu.ProbeMath`,
          asserting `AcceleratorUnavailableException` naming "earlier launch timeout";
          `ATimedOutEnginesDisposalDropsTheStickyFailure` hands `Engine.DisposeAfterLoss`
          an injected `IDisposable` (`StickyDisposable`) whose first `Dispose` always
          throws `CudaException(CUDA_ERROR_LAUNCH_TIMEOUT)` and asserts no exception
          escapes. Both prove the real, driver-touching path end to end; the
          orchestrator runs them on the reference machine after the merge.
        - **No `Cuda` trait, no gate, run under `APTHERMO_NO_CUDA=1` on every runner
          hosted CI included**: `AnEngineMarkedLostRefusesANewCall` calls the new
          `Engine.MarkLost` with an injected `AcceleratorUnavailableException` — never a
          `CudaException` — then asserts `cpu.ProbeMath` refuses the same way, naming the
          same timeout as its inner exception;
          `DropsAfterLossMatchesOnlyTheStickyLaunchTimeoutOfALostSession` proves
          `AcceleratorSession.DropsAfterLoss(CudaError)`'s decision on the bare enum value
          alone (never lost, the sticky error → false; lost, the sticky error → true;
          lost, the wrong error → false), through the new `Engine.DropsAfterLoss(CudaError)`.

        ⚠ 2026-09-29 (first correction, this task): this evidence first read that
        `ATimedOutEngineRefusesANewCall` and `ATimedOutEnginesDisposalDropsTheStickyFailure`
        carried `[Trait("Category","Cuda")]` and returned at once under
        `Engine.CudaForbidden` — so under `APTHERMO_NO_CUDA=1`, the fast suite and every
        hosted runner, neither fact executed a single assertion, and the section's own
        claim was never actually proven by a run recorded here. Found on review. The
        fix removed the gate from both facts directly.

        ⚠ 2026-09-29 (second correction, this task): that fix was itself wrong on two
        counts, found on a second review. First, removing the gate made both facts
        construct a real `CudaException`, which loads `nvcuda` into the shared test
        process even on the CPU accelerator; to keep the fast suite green, the first fix
        narrowed `AcceleratorChoiceTests.NoCudaDriverIsLoadedInAProcessThatForbidsCuda`
        from a whole-process check to a before/after diff around `Engine.Create` alone —
        weakening a check written to guard the root's "CPU path needs no NVIDIA
        software" invariant process-wide, to make a test pass, exactly the taboo this
        root forbids. Second, an un-gated fact that constructs a `CudaException` is
        itself a hosted-CI risk: a runner with no NVIDIA driver at all may fail to build
        one, not merely fail to use one. The reviewer's fix (adopted here): restore
        `AcceleratorChoiceTests.cs` exactly as committed on `main`
        (`git checkout main -- tests/Execution.Tests/AcceleratorChoiceTests.cs`), keep
        `ATimedOutEngineRefusesANewCall` and `ATimedOutEnginesDisposalDropsTheStickyFailure`
        `Cuda`-tagged and gated exactly as first written, and add the decision itself
        (`AcceleratorSession.DropsAfterLoss(CudaError)`, a thin `DropsAfterLoss(CudaException)`
        extraction onto it) plus two new, un-gated CPU facts
        (`AnEngineMarkedLostRefusesANewCall`,
        `DropsAfterLossMatchesOnlyTheStickyLaunchTimeoutOfALostSession`) that drive
        `Engine.MarkLost`/`ProbeMath`/`DropsAfterLoss(CudaError)` directly, without any
        `CudaException`. `LaunchBudgetTests`' own class doc now says four `Cuda`-tagged
        facts, not two, since the count was already stale before this task touched it.

        Both new CPU facts shown red once on the reference machine, reverted, green
        again: `AnEngineMarkedLostRefusesANewCall` with `AcceleratorSession.ThrowIfLost`'s
        `if` condition changed to `_lostBy is { } timeout && false` (`Assert.Throws`
        failed, "No exception was thrown");
        `DropsAfterLossMatchesOnlyTheStickyLaunchTimeoutOfALostSession` with
        `AcceleratorSession.DropsAfterLoss(CudaError)` changed to match
        `CUDA_ERROR_OUT_OF_MEMORY` instead of the sticky timeout (`Assert.True` failed,
        expected true, actual false). The two `Cuda`-tagged facts and
        `NoCudaDriverIsLoadedInAProcessThatForbidsCuda` are unchanged from `main` and
        need no fresh red-once record here.

        Built and passing on the reference machine's CPU accelerator, in one process,
        the restored check and the two new facts together: `dotnet test
        tests/Execution.Tests --filter "Category!=LongRunning"` 163 of 163 under
        `APTHERMO_NO_CUDA=1` (159 before this task, 161 after the first, wrong fix, 163
        after the second). `dotnet test tests/Protocol.Tests --filter
        "Category!=LongRunning"` 35 of 35 (`TreeContractSnapshotTests` re-approved for
        `Engine.MarkLost`/`DropsAfterLoss(CudaError)`, both added to `API.md`;
        `ShapeTests` re-measured `Engine`'s efferent coupling at 31, one over its
        previous row, and the row above is updated with the reason). The protocol lint:
        0 errors, 0 warnings. The `Category=Cuda` run itself — the two facts that need a
        real CUDA driver to construct their own injected exception at all — is the
        orchestrator's to run on the reference machine, per this task's own instruction
        not to run CUDA tests from this worktree; not run here.
      - **One declaration of a pipeline's buffers.** `DeclareBuffers` and `Run` read the
        same private `Declare` method (`EquilibriumPipeline`, `RocketPipeline`,
        `TransportPipeline`), so the proof of the 32-bit offset cap cannot drift from
        the pipeline it proves. `Declare` takes the real batch for `Run` and a
        single-case (`EquilibriumBatch`/`TransportBatch`) or single-exit-array
        (`RocketBatch`) placeholder batch for `DeclareBuffers`, declares every buffer
        exactly once on the `ChunkBuffers` it is given, and returns the buffer handles
        together with the host output arrays `Run` assembles its result from; the
        `SpeciesFunctionPipeline` has no `DeclareBuffers` and is unchanged (every one of
        its strides is 1, `## Structure`).

      Evidence, on the reference machine (Windows), from a tree with every `bin` and
      `obj` removed:
      - `dotnet build APThermo.sln`: 0 warnings, 0 errors;
      - `APTHERMO_NO_CUDA=1 dotnet test APThermo.sln --filter "Category!=LongRunning"`:
        5429 of 5429, none skipped, `Execution.Tests` 161 of 161 among them (see
        above);
      - `git status --short -- '**/Bits*.approved.txt' '**/Throughput*.approved.txt'
        '**/PublicSurface.approved.txt'` empty: no snapshot moved (no public or
        internal-tree-contract shape changed; every new member is `internal`);
      - the protocol lint: 0 errors, 0 warnings.

      This CPU-side evidence is complete; the `Category=Cuda` run and the red-once
      mutation on real hardware stay the orchestrator's, per this task's own
      instruction not to run CUDA tests from this worktree.
- [ ] The rocket kernel's compile is bounded and released (2026-09-30, the root's
      criterion of that date).
      - **Release at dispose.** `Engine.Dispose` empties the kernel cache (`KernelCache`
        gains a `Clear`, called before the session is disposed), so a disposed engine that
        stays reachable (a field, a static fixture) holds no compiled kernel: the
        measured 3 GB of ILGPU IR of a live rocket kernel stay with the engine only while
        it is in use. A fact: after `Dispose` the cache is empty, and a `WeakReference` to
        a launcher taken before it is dead after a collection while the engine object is
        still referenced. Red without the `Clear`. Whether `Context.ClearCache` after each
        kernel load also pays (it took the kept IR of a live solver from 3 GB to 3 MB
        before the attribute, and did not lower the compile's peak) is measured after the
        attribute, with the later kernel loads' time, and kept only when it gains and
        changes no bit; the decision and the figures are recorded here.
      - **The guard.** A fact compiles the rocket kernel on a fresh CPU accelerator engine
        and asserts the compile's cost stays inside a bound. Measure first, in a fresh
        process, over five runs each: with the attribute (0.43 GB, 2.6 s in the
        investigation) and with it removed (11.2 GB, 49.7 s). The metric is a
        deterministic one if ILGPU offers it (the IR size of the compiled program), else
        managed plus native private memory or the managed heap after the compile; the
        bound at least 3 times above the measured green figure and at most half the red
        one, so machine load cannot flip it. Red with the attribute removed, green with
        it, stable over five runs; it fails on an empty measurement. Not `LongRunning`
        if under 30 s.
      - **No test allocates what it measures.** `AcceleratorChoiceTests`'s check of the
        32-bit bound (`new EquilibriumBatch(100_000, 20_000).ElementMoles.Length`, a 16 GB
        array made only to read its length) asks the batch's own bound instead: the check
        is one internal method the constructors call, and the fact calls it with the
        product just inside the bound and just over it. The constructor facts that throw
        stay. Red with the bound off by one.
      - **Records.** `API.md` and this node's `## Structure` name `KernelCache.Clear`; no
        public surface moves; the tree-contract snapshot moves by what it lists.
        The CUDA proof is the orchestrator's, after the merge.

## Taboos

- No numerical formula in this node: kernels only slice and call.
- No ILGPU.Algorithms, no `XMath`, no `LibDevice.*` calls: the wrappers are provided by
  the post-link and the numerical nodes call `System.Math`.
- No kernel whose wrappers go unchecked: `Context.Builder.LibDevice()` defines them for
  some targets and silently drops them for others (the post-link constraint).

  ⚠ 2026-09-26: stood "No reliance on `Context.Builder.LibDevice()` to produce
  wrappers: it does not." It does, for `compute_75` to `compute_90`; the taboo was
  written on SM_120 alone (the ⚠ of the invariant "Every CUDA kernel goes through the post-link").
- No fallback from an explicitly requested CUDA accelerator to the CPU: silent
  fallbacks hide the very failures this node exists to surface.
- No reduction, atomic or shared-memory construct in a kernel: determinism first.
