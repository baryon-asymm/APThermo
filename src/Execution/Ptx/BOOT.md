# BOOT.md — Execution.Ptx

## Purpose

A child node of `src/Execution` (its `BOOT.md`, `## Structure`), split out of that node
on 2026-10-01 because the rules of three of its files filled a third of its document. It
owns the two files that know ILGPU's CUDA internals:

- `PtxPostLink` rewrites the PTX of a compiled CUDA kernel so that its arithmetic is the
  CPU's, refuses what it cannot vouch for, and trial-loads the result: the one place in
  the tree that knows ILGPU's internals.
- `CudaWslDevices` registers the CUDA devices of every context of a process under WSL,
  where ILGPU 1.5.3 binds only the first.

The rest of `src/Execution` reaches this node through `PtxPostLink.Link`, `.IlgpuVersion`
and `.AssertIlgpu`, and `CudaWslDevices.Register` (`API.md`). The cluster has a reason of
its own to change: the versions of ILGPU and of the CUDA driver, which the session, the
engine and the kernel loop do not share.

⚠ 2026-10-05: was three files (`LibDeviceLocator`, `LibDevicePostLink`, `CudaWslDevices`)
that found libnvvm and libdevice and completed the wrappers a kernel called, now two →
HISTORY.md#libdevice-retired-2026-10-05

⚠ 2026-10-01: was rejected as a child on 2026-09-15 (three types, the bar being about
five), now a node, because about 90 lines of rules bind only these files and the parent
was over the §15 limit → HISTORY.md#child-nodes-decision-2026-09-15

## Invariants

The invariants of the parent ([BOOT.md](../BOOT.md)) about the post-link, the checked
driver results and the CPU path hold here unchanged; this node is where they are kept true.

- **Nothing here touches CUDA on the CPU path.** `AssertIlgpu` and `IlgpuVersion` read
  ILGPU's metadata; `Rewrite`, `Guard` and `TargetArch` work on text; `Link` and
  `Register` run only after the accelerator choice decided to try CUDA.
- **No state is kept between calls.** The statics are constants, the compiled patterns and
  the `Lazy` reflected member; nothing records the result of a link or a registration.
- **The rewrite adds no rounding the CPU lacks and removes none it has.** Every
  instruction of doubles that the C# wrote as one IEEE operation is one such instruction in
  the PTX after the rewrite: a `.rn` multiplication, addition or subtraction, an `fma.rn`
  where the source wrote `Math.FusedMultiplyAdd` and nowhere else.

## Dependencies

[Execution](../API.md) — `AcceleratorUnavailableException` (for `CudaWslDevices`).

Outside the tree: ILGPU 1.5.3 (`ILGPU`, `ILGPU.Backends.PTX`, `ILGPU.Runtime`,
`ILGPU.Runtime.Cuda`); the CUDA driver through ILGPU's `CudaAPI`. No libnvvm and no
libdevice (2026-10-05).

## Constraints

Inherited from the parent ([BOOT.md](../BOOT.md)) and, through it, from the root. In
addition:

- Every type here is `internal`; none becomes public (root `BOOT.md`, `## Delivery`,
  "Tree contracts").
- No project of its own: the `.cs` files compile into `src/Execution`'s assembly under
  the namespace `APThermo.Execution.Ptx`, mirroring this directory from the tree root
  (`AGENTS.md` §1).
- The root's code-shape constraint applies unchanged. `PtxPostLink` is `partial` for the
  `[GeneratedRegex]` members only, the one use the root allows.

- **The post-link**, the one place in the tree that knows ILGPU internals (2026-10-05).
  Its stages, in order:
  1. The Execution node compiles the entry point with the CUDA accelerator's backend,
     whose context has no `LibDevice()`. The PTX holds no libdevice call: `Exp`, `Log` and
     `Pow` are the tree's own C#, and `Sqrt`, `Floor`, `Ceiling` and `Abs` are single
     instructions.
  2. **Inline the fused multiply-add.** ILGPU 1.5.3 emits `Math.FusedMultiplyAdd` as a call
     of an undefined external function: three parameters stored, one call, the result
     loaded, in one block. Each such block becomes the one instruction `fma.rn.f64` with
     the block's own operands and result; the declaration of the function goes when no call
     of it is left.
  3. **Mark every unrounded arithmetic instruction.** A PTX compiler is free to contract a
     `mul.f64` and an `add.f64` into a fused multiply-add; the CPU never does. Every
     `mul.f64`, `add.f64` and `sub.f64` becomes `.rn.f64`, which PTX defines as never
     contracted. The count is reported.
  4. **Guard** (`Rewrite` ends with it): the PTX is refused, naming the target and the
     offending text, when it holds
     - an `fma` or `mad` instruction of doubles in any form other than the `fma.rn.f64`
       it wrote, or a different number of them than the call blocks it inlined;
     - an instruction of doubles with the `.approx` modifier, which is not correctly
       rounded and so not the same on every device;
     - an external function, declared here and defined elsewhere, so read by no guard
       (a call of the fused multiply-add that stage 2 did not recognise is one).
  5. Bind the accelerator's context to the calling thread and load the PTX once through the
     CUDA driver as a trial, so that a refusal carries the driver's log; the module is
     destroyed again.
  6. Set the private backing field of `PTXCompiledKernel.PTXAssembly` by reflection to the
     rewritten text, and load with `LoadAutoGroupedKernel`.

  The target is read from the kernel's own `.target sm_XX` line, not fixed. `Link` reports
  what it did as a value, so that the tests can see it; it stays internal. The CPU
  accelerator loads the same method through `LoadAutoGroupedKernel(MethodInfo)` without any
  of this. The ILGPU assembly version and the type of the reflected member are asserted
  once per process, at the first `Engine.Create`, and a mismatch is an error that names
  the ILGPU version.

  ⚠ 2026-10-05: was an inventory of the libdevice wrappers a kernel called and ILGPU left
  undefined, compiled with libnvvm and inserted → HISTORY.md#libdevice-retired-2026-10-05

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

- A driver log is trimmed of NUL padding as well as white space (the trial load's message
  carried 45 NULs).

## Acceptance criteria

The criteria of the parent ([ACCEPTANCE.md](../ACCEPTANCE.md)) that name these files
hold: the architecture fact, the checked results, the post-link facts on PTX text and the
WSL facts of `tests/Execution.Tests` (its `BOOT.md`).

- [x] 2026-10-01 — The split changes no behaviour on the CPU path: no code line moves but
      the namespace and the `using` lines (`git diff -M` of the three files), `dotnet
      build APThermo.sln` gives 0 warnings and 0 errors, `APTHERMO_NO_CUDA=1 dotnet test
      APThermo.sln --no-build --filter "Category!=LongRunning"` is green in every project
      (`Execution.Tests` 170 of 170, the discovery, post-link and WSL facts included) but
      `Protocol.Tests.LintTests`, red only for nodes over their §15 limit outside this
      subtree, and no `Bits*.approved.txt`, `Throughput*.approved.txt`,
      `PublicSurface.approved.txt` or `TreeContract.approved.txt` changed.
- [x] 2026-10-01 — The execution tests on CUDA are green on the reference machine, the
      100 000-case sweep and the throughput tripwire included: `dotnet test
      tests/Execution.Tests -c Release`, 173 of 173 on Windows after the split was merged
      into `protocol-3.1`.

## Taboos

- No public type here: undocumented surface is a contract nobody agreed to.
- No result of a driver call ignored, no `LibDevice.*`, `XMath` or ILGPU.Algorithms, no
  instruction of the PTX that no guard has read (the parent's and the root's taboos,
  unchanged in substance).
- No CUDA type in a signature that leaves `src/Execution`.
