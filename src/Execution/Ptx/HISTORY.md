# HISTORY.md — Execution.Ptx

Append-only store of what `BOOT.md` no longer needs to state as current truth
(`AGENTS.md`, §15). Newest entry first. Every entry names the date it was moved, the
`BOOT.md` section it came from, and carries the original text in full, unedited. The
start procedure (`AGENTS.md`, §10) does not read this file; it is reached only by
following a dated pointer left in `BOOT.md` at the place the text used to stand. This
node was `src/Execution/LibDevice` until 2026-10-05; the entries of its earlier life are
in the parent's `HISTORY.md`.

---

<a id="libdevice-retired-2026-10-05"></a>

## 2026-10-05 — from "## Purpose", "## Invariants", "## Dependencies", "## Constraints" and "## Acceptance criteria" — libnvvm and libdevice retired

The tree's own correctly rounded `Exp`, `Log` and `Pow` replaced the three `System.Math` functions and `Log10`, so no kernel calls a libdevice function; the node's three files became two. The text as it stood:

> - `LibDeviceLocator` finds libnvvm and `libdevice.10.bc`: the explicit path pair of the
>   engine options, then the toolkit directories of the platform.
> - `LibDevicePostLink` completes a compiled CUDA kernel with the libdevice wrappers it
>   calls and ILGPU did not define, and trial-loads the result: the one place in the tree
>   that knows ILGPU's internals.

> The rest of `src/Execution` reaches this node through `LibDeviceLocator.Locate` and
> `.LibraryFileName`, `LibDevicePostLink.Link`, `.IlgpuVersion` and `.AssertIlgpu`, and
> `CudaWslDevices.Register` (`API.md`). The cluster has a reason of its own to change:
> the versions of ILGPU, libnvvm and the CUDA driver, which the session, the engine and
> the kernel loop do not share.

> The invariants of the parent ([BOOT.md](../BOOT.md)) about the post-link, the
> checked libnvvm and driver results, the wrapper list and the CPU path hold here
> unchanged; this node is where they are kept true.
>
> - **Nothing here touches CUDA on the CPU path.** `LibDeviceLocator.Locate` reads the
>   environment and the file system; `AssertIlgpu` and `IlgpuVersion` read ILGPU's
>   metadata; `Link` and `Register` run only after the accelerator choice decided to try
>   CUDA.

> [Execution](../API.md) — `EngineOptions` (the explicit path pair and `LibDeviceDiscovery`
> for `LibDeviceLocator`) and `AcceleratorUnavailableException` (for `CudaWslDevices`).
>
> Outside the tree: ILGPU 1.5.3 (`ILGPU`, `ILGPU.Backends.PTX`, `ILGPU.Runtime`,
> `ILGPU.Runtime.Cuda`); libnvvm and the CUDA driver through ILGPU's `NvvmAPI` and
> `CudaAPI`; `libdevice.10.bc` of a CUDA Toolkit (the parent's and the root's
> `## Dependencies` give the versions).

> - No project of its own: the `.cs` files compile into `src/Execution`'s assembly under
>   the namespace `APThermo.Execution.LibDevice`, mirroring this directory from the tree
>   root (`AGENTS.md` §1).

> - **libdevice discovery order**: an explicit path pair in the options is tried first,
>   on every platform. Unless `LibDeviceDiscovery` is off, the platform is then chosen
>   with `OperatingSystem.IsWindows()` / `IsLinux()`; any other OS does no discovery (the
>   explicit pair is still tried, and the CPU accelerator is used when it is absent
>   too).
>
>   On **Windows**: the roots are the `CUDA_PATH` directory, then
>   `%ProgramFiles%\NVIDIA GPU Computing Toolkit\CUDA\v*` from the newest version down;
>   in each root both `nvvm\bin\nvvm64_40_0.dll` (12.x layout) and
>   `nvvm\bin\x64\nvvm64_40_0.dll` (13.x layout) are tried, with
>   `nvvm\libdevice\libdevice.10.bc`.
>
>   On **Linux**: the roots are `CUDA_PATH`, then `CUDA_HOME`, then `/usr/local/cuda`,
>   then `/usr/local/cuda-*` from the newest version down; in each root
>   `nvvm/lib64/libnvvm.so` is tried, with `nvvm/libdevice/libdevice.10.bc`.
>
>   On both platforms a root already tried (`CUDA_PATH` repeated among the versioned
>   roots, or equal to `CUDA_HOME` on Linux) is skipped, and a root whose library exists
>   but whose bitcode does not is passed over rather than accepted.
>
>   The context is created with `LibDevice(dllPath, bitcodePath)` so that ILGPU emits
>   the intrinsic calls.
>
>   ⚠ 2026-09-15: was the Windows roots and `nvvm64_40_0.dll` only, now a platform branch
>   with the Linux roots and `libnvvm.so` →
>   HISTORY.md#libdevice-discovery-linux-2026-09-15

> - **The post-link**, the one place in the tree that knows ILGPU internals. Its stages,
>   in order:
>   1. Compile the entry point with the CUDA accelerator's backend.
>   2. Take the wrapper inventory of the kernel PTX (2026-09-26):
>      - the wrappers *called* are the `__ilgpu__nv_*` names at `call` instructions
>        only, never parameter names or `ld.param` operands;
>      - the wrappers *defined* are the names of the kernel's own `.func` headers.
>
>      Both sets drop the `__ilgpu` prefix, as the fragment keys do.
>   3. The *missing* wrappers are those called and not defined.
>      - When no wrapper is called, the kernel is returned untouched, without a trial
>        load (as before).
>      - When none is missing, nothing is compiled or inserted: ILGPU defined them all.
>   4. Otherwise build an NVVM module from ILGPU's own fragments of the missing wrappers
>      only. The fragments are the private static `fragments` dictionary of
>      `ILGPU.Backends.PTX.PTXLibDeviceNvvm`, read by reflection. The header goes in the
>      order libnvvm accepts: `target triple`, `target datalayout`, then
>      `!nvvmir.version`.
>   5. Compile that module with ILGPU's `NvvmAPI` for the `compute_XX` of the kernel's
>      `.target sm_XX` line.
>   6. Strip `.version`, `.target` and `.address_size` from the result.
>   7. Insert it right after the kernel's `.address_size` line.
>   8. Check that every missing wrapper now has a definition in the inserted text.
>   9. Bind the accelerator's context to the calling thread and load the PTX once through
>      the CUDA driver API as a trial, so that a refusal carries the driver's log. This
>      happens on both paths of stage 3.
>   10. When anything was inserted, set the private backing field of
>       `PTXCompiledKernel.PTXAssembly` by reflection.
>   11. Load with `LoadAutoGroupedKernel`.
>
>   The reason the completion needs no branch for a kernel with some wrappers defined and
>   some missing (ILGPU defines all of a kernel's fragments or none) →
>   HISTORY.md#post-link-mixed-definitions-rationale-2026-09-26
>
>   `Link` reports what it did as a value (the wrappers ILGPU defined, the wrappers it
>   compiled), so that the tests can see which path a kernel took. It stays internal.
>
>   ⚠ 2026-09-26: was "collect the distinct `__ilgpu__nv_*` names" and compile and insert
>   unconditionally, now the inventory of wrappers called against defined and only the
>   missing ones inserted → HISTORY.md#post-link-wrapper-inventory-2026-09-26
>
>   ⚠ 2026-09-12: was the wrappers compiled for a fixed `compute_80` with no trial load,
>   now the target read from the kernel's PTX and a trial load on the bound thread →
>   HISTORY.md#post-link-target-and-trial-load-2026-09-12

> - A driver or libnvvm log is trimmed of NUL padding as well as white space (the
>   trial load's message carried 45 NULs).

> The criteria of the parent ([ACCEPTANCE.md](../ACCEPTANCE.md)) that name these files
> hold unchanged: the post-link's architecture fact, the checked results, the discovery
> facts and the WSL facts of `tests/Execution.Tests` (its `BOOT.md`).

> - No result of a libnvvm or driver call ignored, no reliance on ILGPU's own wrapper
>   generation, no `LibDevice.*`, `XMath` or ILGPU.Algorithms (the parent's and the root's
>   taboos, unchanged).
