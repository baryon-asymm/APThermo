# BOOT.md — Execution.Chunks

## Purpose

A child node of `src/Execution` (its `BOOT.md`, the child-nodes decision of
2026-09-15), split out in the same clean-code line as the rest of that node's
`## Structure`. It owns two things a batch's device run needs and nothing else:

- `ChunkPlan` decides how many cases one launch takes — from the case count, the
  device bytes a chunk costs per case and the engine options — and enumerates the
  chunks that cover a batch.
- `ChunkBuffers` lets a pipeline declare its device buffers once — a host array (or
  none, for scratch), a direction and a per-case stride — and then allocates,
  uploads and downloads every declared buffer together, in declaration order, around
  one chunk's launch. `ChunkBuffer<T>` is one such declared buffer; `IChunkBuffer` is
  the shape `ChunkBuffers` holds them by; `ChunkTransfer` names the direction; `Chunk`
  is one launch's slice of a batch (its offset and length).

The rest of `src/Execution` reaches this through `ChunkPlan.For`/`.Chunks()`,
`ChunkBuffers`'s five declaration methods, `Allocate`, `UploadChunk`, `DownloadChunk`,
`BytesPerCase` and `MaxElementsPerCase` (2026-09-26, the audit's F4), and
`ChunkBuffer<T>.View` on what a declaration returns — a
contract far narrower than the six types behind it: `IChunkBuffer`, `ChunkTransfer`
and the `Chunk` record are never named outside this node. The cluster has a reason of
its own to change that the rest of `src/Execution` does not share: the chunking and
transfer policy (how big a chunk is, what moves when, in what order), not the kernel
loop that runs a chunk (`BatchRun`, the parent's own file) or the accelerator session
it runs on.

## Invariants

- **One rule sizes a chunk.** `ChunkPlan.For` is the only place a chunk's case count
  is computed: the engine options' `ChunkSize`, clamped by the device bytes a chunk of
  one case costs against `ScratchBytes`, never above the batch's case count and never
  below one case. No other type in this node or its caller recomputes it.
- **Results do not depend on chunking.** Nothing here carries state from one chunk to
  the next — `ChunkBuffers` re-declares nothing between chunks of the same run, and a
  buffer's host array is addressed by the chunk's own offset and length. This is the
  parent node's determinism invariant (its `BOOT.md`, `## Structure`, "the chunk bound
  counts every buffer"); this node is where it is kept true.
- **A buffer's declaration is its single source of truth.** A `ChunkBuffer<T>` knows
  its own host array, direction and per-case stride from the call that declared it
  (`ChunkBuffers.Input`/`Output`/`ClearedOutput`/`Scratch`/`Constant`); nothing later
  restates them, and `ChunkBuffers.BytesPerCase` — the number `ChunkPlan.For` clamps
  against — is the sum of what the declarations already said, never a separately
  maintained total.
- **Declaration order is transfer order.** `ChunkBuffers.Allocate`, `UploadChunk`,
  `DownloadChunk` and `Dispose` walk the declared buffers in the order they were
  declared; a pipeline that declares in a different order gets a different transfer
  order, never a different result (the invariant above).
- **No CUDA type.** Only ILGPU's accelerator-neutral runtime types are named here
  (`Accelerator`, `ArrayView<T>`, `MemoryBuffer1D<T, Stride1D.Dense>`); nothing in this
  node references `ILGPU.Runtime.Cuda` (root `BOOT.md`, the CPU-path invariant and
  taboo, inherited unchanged).

## Dependencies

[Execution](../API.md)

Outside the tree: ILGPU 1.5.3 (`ILGPU`, `ILGPU.Runtime` — `Accelerator`, `ArrayView<T>`,
`MemoryBuffer1D<T, Stride1D.Dense>`, `Stride1D`; no `ILGPU.Runtime.Cuda` type).

## Constraints

- Every type here is `internal`; none becomes public. A child of a node never carries
  a public type: that would move consumers' `using` directives and the surface
  snapshot, which is the parent's decision to make, not this node's (root `BOOT.md`,
  2026-09-15, "Internal types stay internal").
- No project of its own: this node's `.cs` files compile into
  `src/Execution`'s assembly through the SDK's default glob, under the namespace
  `APThermo.Execution.Chunks`, mirroring this directory from
  the tree root (`AGENTS.md` §1; root `BOOT.md`, the 2026-09-15 constraint on child
  nodes).
- The root's code-shape constraint applies unchanged: every type here is well under
  400 lines of code and every method under 60; `ChunkBuffers` and `ChunkBuffer<T>`
  each name well under 14 types of the tree. No row of this node's own is needed in
  its `## Shape exceptions` table.
- SI units do not apply here: every quantity this node handles is a count, a byte
  size or an offset, never a physical quantity.

- **A chunk's buffers stay within 32-bit offsets** (2026-09-26, the audit's finding F4).
  The kernels slice their buffers with `Index1D` arithmetic, which is 32-bit.
  `ChunkPlan.For` therefore also caps a chunk so that `chunk × perCase` of every
  declared buffer stays within `int.MaxValue` elements. `ChunkBuffers` exposes the
  largest per-case element count of its declarations beside `BytesPerCase`. The
  kernels and their PTX do not change.
  - ⚠ Only bytes bounded a chunk. With `ScratchBytes` above 16 GiB, legal on a large
    GPU or on the CPU accelerator, a table at `TableLimits` (13 248 doubles per case)
    wrapped the offset at case 162 100. On CUDA a thread then wrote before its buffer
    (`CUDA_ERROR_ILLEGAL_ADDRESS`, or silent corruption). Found by the hidden-defect
    audit of 2026-09-26 by reading the IL; the allocation needed to run it was too
    large to try.

- **A launch fits a time budget** (2026-09-28, the second audit's Execution finding F2;
  the parent's `BOOT.md` has the rule and the evidence). `LaunchBudget` is internal to
  this node and holds no ILGPU type. It is given the device's run-time limit at bind
  time, or none. From that and the previous chunk's measured time per case it answers
  how many cases the next launch may take. `ChunkPlan.For` takes it as a fourth bound
  beside the count, the bytes and the offsets. A budget never changes a result: only
  the chunk boundaries move.
  - ⚠ A chunk was bounded by count, bytes and offsets only. Nothing bounded its
    duration against a display GPU's watchdog.
- **The cap is proven as wiring** (2026-09-28, the guards part's F8). Each pipeline's
  chosen plan is asserted against `MaxElementsPerCase`, not only `ChunkPlan.For` with
  explicit numbers.

## Acceptance criteria

- [x] 2026-09-15 — The split changes no result: `tests/Execution.Tests`' bit-for-bit
      chunking test passes after the move, chunked against unchunked, on the CPU
      accelerator (`BatchTests.ChunkingAndRepetitionDoNotChangeABit`) and its
      chunk-plan unit facts
      (`AcceleratorChoiceTests.ChunksAreBoundedByTheChunkSizeAndTheScratchMemory`),
      part of the 43/43 green run below.
- [x] 2026-09-15 — `dotnet build APThermo.sln` clean;
      `dotnet test tests/Execution.Tests` 43/43 and `dotnet test tests/Protocol.Tests`
      19/19, both green with `APTHERMO_NO_CUDA=1`; the protocol lint (`python -X utf8
      tools/protocol-lint/protocol_lint.py . --exclude templates`) at 0 errors,
      0 warnings; every `Bits.approved.txt` and `PublicSurface.approved.txt`
      unchanged by `git hash-object` before and after the move (this node's own
      coding-task report, recorded in `04b31eb`).
- [x] 2026-09-15 — `dotnet test tests/Execution.Tests --filter "Category!=LongRunning"`
      green at 41/41 with `APTHERMO_NO_CUDA` unset, on the reference machine, the same
      count as the pre-split tree (no test method added or removed by this split): the
      libdevice post-link ran on the kernel module this node's buffers feed, and the
      namespace change did not touch it.

- [x] 2026-09-26 — The element cap. A host fact plans a table at `TableLimits` (13 248
      doubles per case, this node's own worked example above) with `ScratchBytes` of
      64 GiB and `ChunkSize` of `int.MaxValue`, and asserts `chunk × perCase ≤
      int.MaxValue`; a second plan with the default options is unaffected by the new
      cap, confirming no other plan moves
      (`AcceleratorChoiceTests.ChunksStayWithinInt32OffsetsAtTableLimits`, `tests/Execution.Tests`).

      Shown red once against the byte-only rule (`ChunkPlan.For`'s offset cap replaced
      by `long.MaxValue`): `dotnet test tests/Execution.Tests --filter
      "FullyQualifiedName~ChunksStayWithinInt32OffsetsAtTableLimits"` failed — "648394 *
      13248 overflows a 32-bit offset" — then reverted, the same command green again.
      `ChunkBuffers.MaxElementsPerCase` and `IChunkBuffer.ElementsPerCase` added
      alongside `BytesPerCase`/`ElementsPerCase`; the four pipelines
      (`EquilibriumPipeline`, `RocketPipeline`, `TransportPipeline`,
      `SpeciesFunctionPipeline`) pass `buffers.MaxElementsPerCase` into `ChunkPlan.For`
      beside `buffers.BytesPerCase`; the kernels and their PTX unchanged. Verified on
      the reference machine: `dotnet build APThermo.sln` 0 warnings, 0 errors;
      `APTHERMO_NO_CUDA=1 dotnet test APThermo.sln --no-build --filter
      "Category!=LongRunning"` green on this node's own facts (the one unrelated
      pre-existing failure, `Protocol.Tests.DeclarationTests.EveryDeclarationUnderATickExists`
      on `src/Performance/API.md`'s `RocketSolver.MaxThroatBisections`, predates this
      change and is outside this subtree, from the pending Performance/Transport design
      of `9a6888f`); `dotnet test tests/Execution.Tests -c Release` green, the sweep and
      throughput tripwire included, no `Bits*`/`Throughput*`/`PublicSurface.approved.txt`
      record moved; the protocol lint 0 errors, 0 warnings.

## Taboos

- No public type: a cluster that needs one stays at the parent's own level instead
  (root `BOOT.md`, 2026-09-15).
- No CUDA type (`ILGPU.Runtime.Cuda`): inherited from the root, unchanged by the split.
- No second rule for a chunk's size or for a buffer's transfer direction outside
  `ChunkPlan.For` and the `ChunkTransfer` a declaration is given: a second place that
  decides either drifts from this one.
- No formula: this node moves bytes and slices ranges; it computes nothing a
  numerical node's kernel would recognise as physics.
