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
  this node's own types; `ILGPU.Runtime.Cuda` appears in no signature.
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
  accelerator that produced it (kind, device name, ILGPU version, libdevice path or
  none).
- **Deterministic batches.** No atomics, no reductions, no shared memory: each case
  writes only its own slots, so a batch result is bit-identical between two runs on
  the same accelerator.
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

- **Accelerator choice** (`AcceleratorKind.Auto`): CUDA if a CUDA device exists,
  `APTHERMO_NO_CUDA` is not `1`, and libnvvm and libdevice are found; otherwise the
  CPU accelerator with all cores. `AcceleratorKind.Cuda` fails instead of falling back
  and names what was missing.
- **libdevice discovery order**: an explicit path pair in the options; then the
  `CUDA_PATH` directory; then `C:\Program Files\NVIDIA GPU Computing Toolkit\CUDA\v*`
  from the newest version down; in each root both `nvvm\bin\nvvm64_40_0.dll` (12.x
  layout) and `nvvm\bin\x64\nvvm64_40_0.dll` (13.x layout) are tried, with
  `nvvm\libdevice\libdevice.10.bc`. The context is created with
  `LibDevice(dllPath, bitcodePath)` so that ILGPU emits the intrinsic calls.
- **The post-link**, the one place in the tree that knows ILGPU internals: compile
  the entry point with the accelerator's backend; collect the distinct `__ilgpu__nv_*`
  names from the PTX; build an NVVM module from ILGPU's own wrapper fragments (the
  private static `fragments` dictionary of `ILGPU.Backends.PTX.PTXLibDeviceNvvm`,
  read by reflection) with the header in the order libnvvm accepts (`target triple`,
  `target datalayout`, then `!nvvmir.version`); compile it with ILGPU's `NvvmAPI`
  for `compute_80` (the PTX target ILGPU 1.5.3 emits); strip `.version`, `.target`
  and `.address_size` from the result; insert it right after the `.address_size` line
  of the kernel PTX; set the private backing field of `PTXCompiledKernel.PTXAssembly`
  by reflection; load with `LoadAutoGroupedKernel`. The ILGPU assembly version and the
  presence of every reflected member are asserted once at startup, and a mismatch is
  an error that names the ILGPU version.
- **Batch layout**: structure of arrays for inputs and outputs; the case index is the
  thread index; per-case scratch is a slice of a batch-sized buffer laid out by the
  numerical nodes' `ScratchLayout`; batches are processed in chunks (default 16 384
  cases) so that memory stays bounded; results are copied back per chunk.
- **Kernels**: one entry point per program (`Equilibrium`, `Rocket`, and `Transport`
  as a pass over the stations of a finished rocket or equilibrium batch); each entry
  point does nothing but slice the views for its case and call the numerical node.
- **Warm-up**: kernel compilation and post-link happen on first use per accelerator
  and are cached for the accelerator's lifetime; the time is reported separately from
  the run time.
- **Host-side errors are exceptions** (missing libdevice, ILGPU version mismatch,
  out-of-memory); per-case failures are statuses in the output arrays.
- Reference figures on the reference machine, measured 2026-09-12 on a probe
  (2^20 threads × 256 iterations of `Exp` + `Log`): libdevice `Exp`, `Log`, `Pow`,
  `Sqrt`, `Log10` within 3 ULP of .NET; 11.8 G evaluations per second; ×7.9 over
  16 CPU threads. These bound expectations; they are not requirements.

## Acceptance criteria

- [ ] Probe kernel with every function of the root's math list: loads on CUDA through
      the post-link, and its results equal the CPU accelerator within the tolerance
      table (execution tests node).
- [ ] The rocket batch of 100 000 cases on CUDA equals the same batch on the CPU
      accelerator within the tolerance table; the compared fields are enumerated by
      reflection over `MixtureState` and `PerformanceFigures`.
- [ ] Throughput: the 100 000-case rocket batch on CUDA is at least 5× faster than
      on the CPU accelerator with all cores on the reference machine; the measured
      figures are written to the approved benchmark file (long-running test).
- [ ] With `APTHERMO_NO_CUDA=1` every test of this node passes on the CPU accelerator
      and no CUDA API is called (verified by the absence of `nvcuda` in the loaded
      modules of the test process).
- [ ] With `AcceleratorKind.Cuda` and libdevice paths pointing nowhere, the error
      names every path that was tried.
- [ ] The ILGPU version and reflected members are asserted at startup; a mutation
      test proves the assertion fails loudly.
- [ ] Two runs of the same batch on the same accelerator are bit-identical.

## Taboos

- No numerical formula in this node: kernels only slice and call.
- No ILGPU.Algorithms, no `XMath`, no `LibDevice.*` calls: the wrappers are provided by
  the post-link and the numerical nodes call `System.Math`.
- No reliance on `Context.Builder.LibDevice()` to produce wrappers: it does not.
- No fallback from an explicitly requested CUDA accelerator to the CPU: silent
  fallbacks hide the very failures this node exists to surface.
- No reduction, atomic or shared-memory construct in a kernel: determinism first.
