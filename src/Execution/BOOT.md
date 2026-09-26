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
- **Every CUDA kernel goes through the post-link.** ILGPU 1.5.3's own libdevice
  wrapper generation is never relied on (it is defective with libnvvm 12.9 and 13.3,
  see Constraints); a kernel whose PTX calls a `__ilgpu__nv_*` wrapper that the
  post-link did not provide is refused at load with the wrapper's name in the error.
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

Outside the tree: ILGPU 1.5.3 (NuGet); for CUDA an NVIDIA driver with CUDA 12.8 or
newer, libnvvm (`nvvm64_40_0.dll` on Windows, `libnvvm.so` on Linux) and
`libdevice.10.bc` from a CUDA Toolkit 12.8 or newer.

⚠ 2026-09-15 (distribution phase): stood "libnvvm (`nvvm64_40_0.dll`)", naming the
Windows file only, before the root's Platform constraint (`cf87211`) added Linux as a
supported platform, CUDA included. Linux ships the same library as `libnvvm.so`; the
line now names both.

## Constraints

Inherited from the root ([BOOT.md](../../BOOT.md)). In addition:

- **Accelerator choice** (`AcceleratorKind.Auto`): CUDA if `APTHERMO_NO_CUDA` is not
  `1`, libnvvm and libdevice are found, the device at the requested index exists and
  the context and accelerator can be created; otherwise the CPU accelerator with all
  cores. `AcceleratorKind.Cuda` fails instead of falling back and names what was
  missing, with every path tried. `AcceleratorKind.Cpu` never looks for CUDA.
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
- **The post-link**, the one place in the tree that knows ILGPU internals: compile
  the entry point with the CUDA accelerator's backend; collect the distinct
  `__ilgpu__nv_*` names from the PTX; build an NVVM module from ILGPU's own wrapper
  fragments (the private static `fragments` dictionary of
  `ILGPU.Backends.PTX.PTXLibDeviceNvvm`, read by reflection) with the header in the
  order libnvvm accepts (`target triple`, `target datalayout`, then
  `!nvvmir.version`); compile it with ILGPU's `NvvmAPI` for the `compute_XX` of the
  `.target sm_XX` line of the kernel PTX; strip `.version`, `.target` and
  `.address_size` from the result; insert it right after the `.address_size` line
  of the kernel PTX; check that every wrapper called has a definition; bind the
  accelerator's context to the calling thread and load the linked PTX once through
  the CUDA driver API as a trial, so that a refusal carries the driver's log; set the
  private backing field of `PTXCompiledKernel.PTXAssembly` by reflection; load with
  `LoadAutoGroupedKernel`. The CPU accelerator loads the same method through
  `LoadAutoGroupedKernel(MethodInfo)` without any of this. The ILGPU assembly version
  and the presence and types of every reflected member are asserted once per
  process, at the first `Engine.Create`, and a mismatch is an error that names the
  ILGPU version.

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
| `LibDevicePostLink` | the post-link as the sequence of its stages, each a method or a small internal type: the NVVM module from the fragments, the compilation, the insertion after the header, the definition check as a set comparison over the wrapper text, the trial load | internal |

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

## Taboos

- No numerical formula in this node: kernels only slice and call.
- No ILGPU.Algorithms, no `XMath`, no `LibDevice.*` calls: the wrappers are provided by
  the post-link and the numerical nodes call `System.Math`.
- No reliance on `Context.Builder.LibDevice()` to produce wrappers: it does not.
- No fallback from an explicitly requested CUDA accelerator to the CPU: silent
  fallbacks hide the very failures this node exists to surface.
- No reduction, atomic or shared-memory construct in a kernel: determinism first.
