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

- **No CUDA type leaves this node.** The public surface names ILGPU only through
  this node's own types and, in the kernel parameter structs, ILGPU's `ArrayView`;
  `ILGPU.Runtime.Cuda` appears in no signature.
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
newer, libnvvm (`nvvm64_40_0.dll`) and `libdevice.10.bc` from a CUDA Toolkit 12.8 or
newer.

## Constraints

Inherited from the root ([BOOT.md](../../BOOT.md)). In addition:

- **Accelerator choice** (`AcceleratorKind.Auto`): CUDA if `APTHERMO_NO_CUDA` is not
  `1`, libnvvm and libdevice are found, the device at the requested index exists and
  the context and accelerator can be created; otherwise the CPU accelerator with all
  cores. `AcceleratorKind.Cuda` fails instead of falling back and names what was
  missing, with every path tried. `AcceleratorKind.Cpu` never looks for CUDA.
- **libdevice discovery order**: an explicit path pair in the options; then, unless
  `LibDeviceDiscovery` is off, the `CUDA_PATH` directory; then
  `%ProgramFiles%\NVIDIA GPU Computing Toolkit\CUDA\v*` from the newest version
  down; in each root both `nvvm\bin\nvvm64_40_0.dll` (12.x layout) and
  `nvvm\bin\x64\nvvm64_40_0.dll` (13.x layout) are tried, with
  `nvvm\libdevice\libdevice.10.bc`. The context is created with
  `LibDevice(dllPath, bitcodePath)` so that ILGPU emits the intrinsic calls.
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

| Type | Responsibility | Visibility |
|---|---|---|
| `Engine` | the composition root: `Create` delegating to the choice, `Upload`, the four `Run` overloads delegating to their pipelines, `ProbeMath`, `Dispose`; no loop, no arithmetic, no ILGPU call except through the session. Named here as the composition root the root's Ce rule allows above its limit: four typed `Run` overloads name twelve types by themselves (Ce 26 by the dependency check's walk on 2026-09-14) | public, contract as `API.md` says |
| `AcceleratorSession` | owns one ILGPU context, one accelerator, the optional NvvmAPI and the `AcceleratorInfo`; disposes them in order, once, and disposes what was built when the build fails | internal |
| `AcceleratorChoice` | turns `EngineOptions` into an `AcceleratorDecision` by the rules under Constraints: the session, the reason CUDA was skipped when it was, the paths tried | internal |
| `KernelCache` | typed kernel launchers, compiled and post-linked on first use, one per entry-point name; reports the warm-up time | internal |
| `RunTimer` | the four phases of one run as named scopes; produces `RunTimings` | internal |
| `ChunkPlan` | the one rule deciding how many cases a launch takes, from the case count, the device bytes per case and the options; enumerates the chunks | internal |
| `ChunkBuffers` | the device side of one program's chunk: each buffer declared once with its host array, its direction and its per-case stride; uploads and downloads a chunk | internal |
| `BatchRun` | the loop and nothing else: per chunk, upload, launch and synchronise, download, each in its timer scope | internal |
| `EquilibriumPipeline`, `RocketPipeline`, `TransportPipeline`, `SpeciesFunctionPipeline` | one per program: declare its host arrays, device buffers and views struct, assemble its result; no formula. Named here as the composition roots of their programs' runs, which the root's Ce rule allows above its limit: each names its program's batch, result and views types and the tables' buffers and views besides the run's machinery (the session, the plan, the chunk buffers, the loop, the timer, the kernel cache). By the dependency check's walk on 2026-09-14, a constructed generic type counted once: `RocketPipeline` 23, `TransportPipeline` 22, `EquilibriumPipeline` 21, `SpeciesFunctionPipeline` 17 | internal |
| `Kernels` | the registry of entry points: each slices the views of its case and calls the numerical node; no formula. Named here as the registry the root's Ce rule allows above its limit (Ce 25 by the dependency check's walk on 2026-09-14, 22 by the review's textual count the same day: one views struct, one layout class and one solver per program, which no split removes) | internal |
| `MathProbe` | the probe of the root's math list, in a file of its own; `StrideCount` is the internal constant the kernel strides by, tied to `FunctionCount` by a test, and the function list is asserted to have that length | public, contract unchanged |
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
`ProbeKernelTests.The_kernels_stride_constant_matches_the_function_list` asserts `StrideCount ==
FunctionCount` so the two cannot drift silently.

Decisions taken with the review of 2026-09-14:

- **The fallback says why.** `Auto` keeps falling back to the CPU accelerator, and the
  reason no longer dies in a discarded exception: `AcceleratorInfo` gains
  `CudaSkippedBecause` (null when CUDA was not tried or was bound), the message of the
  failure that turned the choice, with the paths tried where they apply. A contract
  change, recorded in `API.md` with its ⚠, the snapshot moving in the same commit; the
  command line prints it in the `devices` listing and in every document's
  `run.accelerator` (a later change of that node).
- **The missing-definition guard names the wrapper.** The check parses the wrapper text
  libnvvm returned for its `.func` definitions and compares the set with the names the
  kernel calls; it is testable without a GPU by handing it a wrapper body with one
  definition removed, and the tests node does exactly that.
- **The chunk bound counts every buffer.** `ChunkBuffers` sums the per-case strides it
  declares, so `ScratchBytes` bounds the device bytes of a chunk by construction (until
  now only the scratch and the moles were counted); results do not depend on chunking
  (Invariants), so no result moves. `ScratchBytes` must be positive, like `ChunkSize`.
- **The views structs keep their constructors.** `RocketBatchViews` (17 parameters)
  and `EquilibriumBatchViews` (12) are kernel parameter descriptors ILGPU requires to
  be public; grouping their views would re-emit the kernels and move the contract.
  They are this node's declared exception to the parameter rule, and so are the
  constructors of `RocketBatchResult` (10) and `EquilibriumBatchResult` (7), which
  mirror the batch results `API.md` publishes, one argument per property. The
  pipelines are the only callers of the four, and every call names its arguments, as
  the root requires of a mirrored shape. The other two views structs take six
  parameters and are within the rule.

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

## Acceptance criteria

- [x] 2026-09-12 — Probe kernel with every function of the root's math list: loads on
      CUDA through the post-link, and its results equal the CPU accelerator within the
      tolerance table (execution tests node; `ProbeKernelTests`:
      `The_cpu_accelerator_reproduces_dotnet_math_exactly`,
      `Cuda_matches_the_cpu_accelerator_within_the_ulp_bound_for_every_function`).
- [x] 2026-09-12 — The rocket batch of 100 000 cases on CUDA equals the same batch on
      the CPU accelerator within the tolerance table; the compared fields are
      enumerated by reflection over `MixtureState` and `PerformanceFigures`
      (`CudaTests.The_sweep_of_100000_cases_on_cuda_matches_the_cpu_accelerator_and_is_deterministic`,
      long-running). The table has two tiers for mole fractions, by whether both
      accelerators stopped after the same number of Newton steps at the station: see
      the tests node's invariants for the finding behind it.
- [x] 2026-09-12 — Throughput: the 100 000-case rocket batch on CUDA is at least 5×
      faster than on the CPU accelerator with all cores on the reference machine; the
      measured figures are written to the approved benchmark file (long-running test
      `CudaTests.Throughput_is_recorded_and_not_below_the_approved_ratio`;
      `tests/Execution.Tests/Throughput.approved.txt`: 56.28×).
- [x] 2026-09-12 — With `APTHERMO_NO_CUDA=1` every test of this node passes on the CPU
      accelerator and no CUDA API is called (verified by the absence of `nvcuda` and
      `nvvm` in the loaded modules of the test process:
      `AcceleratorChoiceTests.No_cuda_driver_is_loaded_in_a_process_that_forbids_cuda`;
      the whole solution's suite run with the variable set, see the root's criteria).
- [x] 2026-09-12 — With `AcceleratorKind.Cuda` and libdevice paths pointing nowhere,
      the error names every path that was tried
      (`AcceleratorChoiceTests.An_explicit_cuda_request_with_paths_nowhere_names_every_path_tried`,
      `Discovery_reports_the_toolkit_paths_it_examined`).
- [x] 2026-09-12 — The ILGPU version and reflected members are asserted at startup; a
      mutation test proves the assertion fails loudly
      (`AcceleratorChoiceTests.The_ilgpu_assertion_fails_loudly_for_another_version`
      asserts against a wrong version; the mutation of `ExpectedIlgpuVersion` in the
      tests node's evidence list makes every test of the node red).
- [x] 2026-09-12 — Two runs of the same batch on the same accelerator are bit-identical
      (`BatchTests.Chunking_and_repetition_do_not_change_a_bit` on the CPU accelerator,
      the sweep test above on CUDA).
- [x] 2026-09-12 — The species-function batch equals the host calls of `Thermo`'s
      functions bit for bit on the CPU accelerator and matches CUDA within the tolerance
      table, inside and outside the records' ranges (`SpeciesFunctionTests`:
      `The_cpu_accelerator_equals_the_host_functions_bit_for_bit`,
      `Cuda_matches_the_cpu_accelerator_within_the_table`,
      `A_species_index_outside_the_table_is_refused_before_any_kernel_runs`).
- [x] 2026-09-14 — The decomposition of 2026-09-14 (`## Structure`): no type or method
      of the node above the root's code-shape limits (`python inventory.py .`: no
      `src/Execution` type at or above 250 lines, largest method
      `RocketPipeline.Run` at 51 lines; the declared exceptions are the four views
      structs' constructors and `Engine`'s and `Kernels`' Ce, both named in `##
      Structure`; the protocol tests node's `ShapeTests` does not exist on this
      branch yet, so this reading is the inventory script the task names, not yet the
      reflection check) — the public surface changed only by
      `AcceleratorInfo.CudaSkippedBecause`, in `c10ab0e` alone
      (`git diff 6af23b1..HEAD -- tests/Protocol.Tests/PublicSurface.approved.txt`:
      one line added, that property; `Protocol.Tests.SurfaceTests` green against it
      unchanged since); `BatchTests.Chunking_and_repetition_do_not_change_a_bit`, the
      probe, species-function and accelerator-choice tests green
      (`AerospacePropellantThermodynamics.Execution.Tests.dll`: 41 passed); the fast
      suite of the whole solution green (`dotnet test
      AerospacePropellantThermodynamics.sln --filter "Category!=LongRunning"` with
      `APTHERMO_NO_CUDA=1`: 2147 passed, 0 failed, 0 skipped). The CUDA sweep and the
      throughput benchmark are the orchestrator's to run once at the end, after the
      merge, on the reference machine (not run from this worktree).
- [x] 2026-09-14 — The fallback names its reason: with `Auto`, `LibDeviceDiscovery`
      off and the explicit paths pointing nowhere, the engine is the CPU one and
      `CudaSkippedBecause` names what was missing and the paths tried
      (`AcceleratorChoiceTests.An_auto_fallback_says_why_cuda_was_skipped_and_which_paths_were_tried`,
      the mirror of `An_explicit_cuda_request_with_paths_nowhere_names_every_path_tried`);
      committed in `c10ab0e`, where the equivalent test against the code of `8e36a27`
      (where `AcceleratorInfo` said nothing) would have been red.
- [x] 2026-09-14 — The missing-definition guard of the post-link is proven
      non-degenerate: a wrapper body with one definition removed makes the check name
      that wrapper, without a GPU
      (`PostLinkTests.A_wrapper_body_with_one_definition_removed_names_that_wrapper`,
      `..._with_every_definition_removed_names_every_wrapper`, and
      `A_call_site_is_not_mistaken_for_a_definition` against the two-substring-search
      shape the guard had before `23ccc1d`, which read the whole linked text instead
      of the wrapper body alone).
- [x] 2026-09-14 — `ScratchBytes` of zero or less is refused at `Create` naming the
      option, like `ChunkSize`
      (`AcceleratorChoiceTests.Chunks_are_bounded_by_the_chunk_size_and_the_scratch_memory`,
      committed in `fcb1128` with the chunk-plan extraction); the "inconsistent lengths" row was
      never a separate row of `API.md`'s error table by the time this branch started
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
      `ShapeTests.Every_wide_constructor_is_called_with_named_arguments`, is designed
      and not yet written; it takes over as the evidence when it is.

## Taboos

- No numerical formula in this node: kernels only slice and call.
- No ILGPU.Algorithms, no `XMath`, no `LibDevice.*` calls: the wrappers are provided by
  the post-link and the numerical nodes call `System.Math`.
- No reliance on `Context.Builder.LibDevice()` to produce wrappers: it does not.
- No fallback from an explicitly requested CUDA accelerator to the CPU: silent
  fallbacks hide the very failures this node exists to surface.
- No reduction, atomic or shared-memory construct in a kernel: determinism first.
