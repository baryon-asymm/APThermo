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
    On a count ILGPU's warp layout cannot express exactly, the nearest layout not above
    the count is used, and the choice is documented where it is made. On the reference
    machine the count is 16, the layout is today's, and no throughput record moves.
    Results do not depend on the thread count (Invariants: deterministic batches).
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
| `Engine` | the composition root: `Create` delegating to the choice, `Upload`, the four `Run` overloads delegating to their pipelines, `ProbeMath` (the one run without a pipeline: allocates, launches and reads back the probe over the session's accelerator), `Dispose`; no loop, no arithmetic, no ILGPU call except through the session. Named here as the composition root the root's Ce rule allows above its limit: four typed `Run` overloads name twelve types by themselves (Ce 25 by the dependency check's walk on 2026-09-15) | internal (2026-09-15, distribution phase; F1, `API.md`'s ⚠), contract as `API.md`'s tree-contract section says |
| `AcceleratorSession` | owns one ILGPU context, one accelerator, the optional NvvmAPI and the `AcceleratorInfo`; disposes them in order, once, and disposes what was built when the build fails | internal |
| `AcceleratorChoice` | turns `EngineOptions` into an `AcceleratorDecision` by the rules under Constraints: the session, the reason CUDA was skipped when it was, the paths tried | internal |
| `KernelCache` | typed kernel launchers, compiled and post-linked on first use, one per entry-point name; reports the warm-up time | internal |
| `RunTimer` | the four phases of one run as named scopes; produces `RunTimings` | internal |
| `Chunks/` (child node, `APThermo.Execution.Chunks`) | the chunking policy and one program's chunk device buffers: `Chunk`, `ChunkPlan`, `ChunkBuffer<T>`, `ChunkBuffers`, `ChunkTransfer`, `IChunkBuffer`; its own `BOOT.md`/`API.md` hold the contract | internal |
| `BatchRun` | the loop and nothing else: per chunk, upload, launch and synchronise, download, each in its timer scope | internal |
| `EquilibriumPipeline`, `RocketPipeline`, `TransportPipeline`, `SpeciesFunctionPipeline` | one per program: declare its host arrays, device buffers and views struct, assemble its result; no formula. Named here as the composition roots of their programs' runs, which the root's Ce rule allows above its limit: each names its program's batch, result and views types and the tables' buffers and views besides the run's machinery (the session, the plan, the chunk buffers, the loop, the timer, the kernel cache). By the dependency check's walk on 2026-09-14, a constructed generic type counted once: `RocketPipeline` 23, `TransportPipeline` 22, `EquilibriumPipeline` 21, `SpeciesFunctionPipeline` 17 | internal |
| `Kernels` | the registry of entry points: each slices the views of its case and calls the numerical node; no formula. Named here as the registry the root's Ce rule allows above its limit (Ce 25 by the dependency check's walk on 2026-09-14, 22 by the review's textual count the same day: one views struct, one layout class and one solver per program, which no split removes) | internal |
| `MathProbe` | the probe of the root's math list, in a file of its own; `StrideCount` is the internal constant the kernel strides by, tied to `FunctionCount` by a test, and the function list is asserted to have that length | internal (2026-09-15, distribution phase), contract unchanged |
| `LibDevicePostLink` | the post-link as the sequence of its stages, each a method or a small internal type: the wrapper inventory of the kernel PTX (called at `call` sites, defined by `.func` headers; 2026-09-26), the NVVM module from the fragments of the missing wrappers, the compilation, the insertion after the header, the definition check as a set comparison over the wrapper text, the trial load; `Link` returns what it did | internal |

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
| `Engine` | efferent coupling | 25 | the composition root: `Create` delegating to the choice, `Upload`, the four `Run` overloads delegating to their pipelines, `ProbeMath` (the one run without a pipeline: allocates, launches and reads back the probe over the session's accelerator), `Dispose`; no loop, no arithmetic, no ILGPU call except through the session |
| `Kernels` | efferent coupling | 25 | the registry of entry points: each slices the views of its case and calls the numerical node; no formula |
| `RocketPipeline` | efferent coupling | 23 | the composition root of its program's run: declares its host arrays, device buffers and views struct, assembles its result; no formula |
| `TransportPipeline` | efferent coupling | 22 | the same case as `RocketPipeline` above |
| `EquilibriumPipeline` | efferent coupling | 21 | the same case as `RocketPipeline` above |
| `SpeciesFunctionPipeline` | efferent coupling | 17 | the same case as `RocketPipeline` above |
| `RocketBatchViews.RocketBatchViews` | parameters | 17 | a kernel parameter descriptor (internal since 2026-09-15, the ⚠ under "The views structs keep their constructors"); grouping its views would re-emit the kernels and move the contract; every creation names its arguments |
| `EquilibriumBatchViews.EquilibriumBatchViews` | parameters | 12 | the same case as `RocketBatchViews` above |
| `RocketBatchResult.RocketBatchResult` | parameters | 10 | mirrors the batch result `API.md` publishes, one argument per property, as `RocketBatchViews` above |
| `EquilibriumBatchResult.EquilibriumBatchResult` | parameters | 7 | the same case as `RocketBatchResult` above |

`SpeciesFunctionBatchViews` and `TransportBatchViews`, the other two views structs
`## Structure` names, take six parameters each and need no row, as it already says.

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
      - **The architecture fact** (`Category=Cuda`, `Category=LongRunning`, about three
        minutes on the reference machine). The architectures are every
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
        reference machine (RTX 5070 Ti, SM_120, libnvvm 13.4), first (cold JIT cache)
        run 3 m 41 s, subsequent runs about 14 s once the CUDA driver's own compute
        cache is warm. `SM_75`..`SM_90` measured `DefinedByIlgpu.Count > 0` and
        `Compiled.Count == 0` (ILGPU defined every wrapper); `SM_100`..`SM_121`
        measured the reverse (the post-link compiled every wrapper), so both paths are
        exercised. Every architecture's normalized PTX equalled the device's own
        (`SM_120`'s), and the probe matched the engine's own CUDA probe bit for bit and
        the CPU accelerator within `GpuCpuTolerances.MathUlp` on every architecture.

        Shown red once, reproduced directly against `LibDevicePostLink` as it stands at
        `9c33398` (a throwaway repro compiling `Kernels.Probe` for `SM_75`, `SM_80`,
        `SM_86`, `SM_89`, `SM_90` and calling the old `Link`, not committed): every one
        threw `InvalidOperationException`, "the kernel calls the libdevice wrapper
        __nv_exp_param_0, for which ILGPU 1.5.3.0 has no fragment.", exactly the message
        this criterion predicted.
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

- [ ] 2026-09-26 — The audit's F2, F3 and observations (Constraints). Evidence due, each
      fact red once against the code before the change:
      - **The bad library.** A file named as the platform's libnvvm that is not a
        library, with the real bitcode and discovery off:
        - `Cuda` throws `AcceleratorUnavailableException` naming both paths;
        - `Auto` binds the CPU with a `CudaSkippedBecause` that names the library
          path;
        - on the reference machine, the free device memory after 20 such `Auto`
          creations is within 64 MiB of the memory before (`Category=Cuda`).
      - **All cores.** Under `DOTNET_PROCESSOR_COUNT` 4, 16 and 64, the CPU engine
        reports that many threads in `AcceleratorInfo.ThreadsOrMultiprocessors`, or
        the documented nearest layout. A batch gives the same bits at each count. The
        fact runs the processor counts in child processes.
      - **Observations.** A message built from a NUL-padded log holds no NUL. An
        upload made to fail after the species buffers leaves no live buffer. A half
        pair is refused naming the missing option.
      - **Nothing else moves.** No bit, throughput or surface record changes; the
        node's CUDA tests are green in Release. `API.md` states the half-pair refusal
        and the thread count. `Options.cs` and every "all cores" sentence stay true.

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
