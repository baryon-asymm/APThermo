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

## Taboos

- No numerical formula in this node: kernels only slice and call.
- No ILGPU.Algorithms, no `XMath`, no `LibDevice.*` calls: the wrappers are provided by
  the post-link and the numerical nodes call `System.Math`.
- No reliance on `Context.Builder.LibDevice()` to produce wrappers: it does not.
- No fallback from an explicitly requested CUDA accelerator to the CPU: silent
  fallbacks hide the very failures this node exists to surface.
- No reduction, atomic or shared-memory construct in a kernel: determinism first.
