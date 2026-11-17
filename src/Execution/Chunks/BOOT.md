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

The production pipelines construct the plan with `ChunkPlan.For`. `BatchRun` uses the plan
limits and calibration results to choose each launch length. `ChunkPlan.Chunks()` enumerates
fixed-size slices for the planning tests.

⚠ 2026-11-17: this section previously named `.Chunks()` as a production caller path. The call-site inventory found it only in planning tests.

The production pipelines use `ChunkBuffers`'s six declaration methods (one per `ChunkTransfer`),
`Allocate`, `UploadChunk`, `DownloadChunk`,
`BytesPerCase` and `MaxElementsPerCase` (2026-09-26, the audit's F4), and
`ChunkBuffer<T>.View` on what a declaration returns — a
contract far narrower than the six types behind it: `IChunkBuffer`, `ChunkTransfer`
and the `Chunk` record are not explicitly named by production source outside this
node; mirroring Execution.Tests may name them and implement fault doubles. The cluster has a reason of
its own to change that the rest of `src/Execution` does not share: the chunking and
transfer policy (how big a chunk is, what moves when, in what order), not the kernel
loop that runs a chunk (`BatchRun`, the parent's own file) or the accelerator session
it runs on.

⚠ 2026-10-04: was five declaration methods, now six (`InputOutput`) → HISTORY.md#input-output-2026-10-04

⚠ 2026-11-14 (A28): was "IChunkBuffer, ChunkTransfer and Chunk are never named
outside this node". That absolute wording excluded the mirroring tests even though
BatchCleanupAuditTests already implements IChunkBuffer. The claim concerns production
source; mirroring internal-contract and fault-injection tests are explicitly permitted.

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
  (`ChunkBuffers.Input`/`Output`/`ClearedOutput`/`InputOutput`/`Scratch`/`Constant`); nothing later
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

- **Initial workload identity** (2026-11-14): capture the full declared inventory
  before Allocate, including constants uploaded there. Frame declaration order,
  direction, unmanaged type/size, host length and stride with every initial byte of
  Input, Constant and InputOutput; output/scratch contents contribute no bytes.
  SHA-256 identity follows API.md's versioned length framing. Unsupported declarations
  disable reuse for the complete inventory. Capture is refused after allocation or
  disposal; the caller keeps inputs stable through Run and retains the pre-download
  InputOutput identity. This adds no numerical rule or allocation/transfer change.

- ⚠ 2026-11-14: the static covering advanced its final partial chunk by `Size`,
  which wrapped near `int.MaxValue`. Advance by the emitted length so the covering
  terminates at `Count`, including the largest count; no large array is allocated.

- Chunk enumeration advances by its actual remaining length without integer overflow.
  Cleanup attempts every allocated buffer; rollback preserves the primary failure.
  Reserve ownership before buffer allocation; a failed allocation closes the group.
  Primary-aware disposal records unrelated cleanup failures instead of replacing a
  normalized timeout. A lost session's sticky error is the only ignorable device error.
  ⚠ 2026-11-14: declaration-order disposal previously stopped at its first failure;
  now all declared owners receive an attempt and diagnostics follow the primary.

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
    GPU or on the CPU accelerator, a table at `TableLimits` (20 360 doubles per case:
    9 · 2048 + 20 + 3 · 20 + 42² + 2 · 42) wrapped the offset at case 105 476. On CUDA a
    thread then wrote before its buffer (`CUDA_ERROR_ILLEGAL_ADDRESS`, or silent corruption). Found by the hidden-defect
    audit of 2026-09-26 by reading the IL; the allocation needed to run it was too
    large to try.

    ⚠ 2026-10-03: was 13 248 doubles per case (wrap at about case 162 100), now 20 360 (wrap at case 105 476) → HISTORY.md#doubles-per-case-2026-10-03
- **A launch fits a time budget** (2026-09-28, the second audit's Execution finding F2;
  the parent's `BOOT.md` has the rule and the evidence). `LaunchBudget` is internal to
  this node and holds no ILGPU type. It is given the device's run-time limit at bind
  time, or none. From that and the previous chunk's measured time per case it answers
  how many cases the next launch may take. `ChunkPlan.For` takes it as a fourth bound
  beside the count, the bytes and the offsets. A budget never changes a result: only
  the chunk boundaries move.
  - ⚠ A chunk was bounded by count, bytes and offsets only. Nothing bounded its
    duration against a display GPU's watchdog.
  - The mechanics, moved here from the parent's `BOOT.md` ([Execution](../BOOT.md),
    the second audit's F2; CONDENSE.md, rule 3, 2026-10-01):
      - The Chunks node holds an internal `LaunchBudget`, built at bind time from the
        device's kernel run-time-limit attribute. A device without the limit, and the
        CPU accelerator, have no budget. A device with it gets a quarter of the 2 s
        default.
      - `ChunkPlan` takes the budget as a fourth bound, beside count, bytes and offsets.
        On a constrained device an unknown workload starts at one case; later chunks use the
        measured budget estimate but grow by at most twice the preceding case count.
        Unbounded CPU/headless plans retain their size and results.
        ⚠ 2026-11-14: was an unmeasured device wave, now one case with bounded growth
        → HISTORY.md#launch-sampling-2026-11-14
      - This is a conservative sampling policy, not a duration guarantee. One case
        cannot be split; it may exceed the target. Retain timeout normalization and
        lost-session handling without provoking a driver reset as a test.
      - A repeat of exactly identified work may probe an observed initial prefix
        through the parent's [Calibration node](../Calibration/BOOT.md). Its prior
        cumulative cost must fit the same target; merged time is not yet measured.
        All further growth keeps the 2x cap, and current R09 observations stay live.
        ⚠ 2026-11-14: was one case for every run, now unknown work plus empirical
        identical-prefix reuse → HISTORY.md#identical-prefix-calibration-2026-11-14
      - Results do not depend on the chunking. The audit's `E13` found the same bits at
        chunk sizes 16 384, 1, 7 and 64, so no result bit moves.
- **Batches are processed in chunks** (moved here from the parent's `BOOT.md`,
  [Execution](../BOOT.md), batch layout, 2026-10-01):
  chunks of at most `ChunkSize` cases (default 16 384), and fewer when a chunk's
  scratch would exceed `ScratchBytes` (default 256 MB), so that memory stays bounded;
  the device buffers of a chunk are allocated once per run and reused; results are
  copied back per chunk.

  ⚠ 2026-09-12: was a chunk bounded by the case count only, now also by `ScratchBytes`,
  the rocket chunk's moles included → HISTORY.md#batch-layout-scratch-bound-2026-09-12
- **A buffer the kernel reads and writes** (2026-10-04, the seeded equilibrium batches of
  the parent's `API.md`). `InputOutput` uploads the chunk's host slice before the launch,
  as `Input` does, and downloads it after, as `Output` does, through the same pinned
  overloads and the same sentinel guard. Its device bytes count as any other buffer's,
  so it adds nothing to a plan that the `ClearedOutput` it replaces did not. The guard's
  argument holds only when no uploaded slice is the sentinel everywhere: a slice the
  kernel leaves as it was comes back as uploaded. The declaring pipeline guarantees that
  (the equilibrium seed is refused when not finite); a slice of sentinel bytes is refused
  like a lost download. Each chunk uploads its own slice once and downloads it once, so
  results stay independent of the chunking.
- **The cap is proven as wiring** (2026-09-28, the guards part's F8). Each pipeline's
  chosen plan is asserted against `MaxElementsPerCase`, not only `ChunkPlan.For` with
  explicit numbers.

- **Host memory crosses into ILGPU pinned** (2026-10-01, the release run of `v0.2.0`,
  run 36823164901). Every transfer between a host array and a device buffer goes
  through an ILGPU overload that pins the host memory for the transfer: the
  `Span<T>`/`ReadOnlySpan<T>` overloads of `CopyToCPU` and `CopyFromCPU` (on
  `Allocated.View.BaseView.SubView(0, span)`, the contiguous view the overload without
  a stream takes) or the `T[]` overload. A `ref` into a managed array never reaches an
  ILGPU transfer: not `CopyToCPU(ref T, long)`, not `CopyFromCPU(ref T, long)`, not
  their `*UnsafeAsync` forms.
  - Why: ILGPU 1.5.3's `ref T` overloads turn the reference into a raw pointer
    (`CPU/CPUMemoryBuffer.cs:491`, `Unsafe.AsPointer`) and then allocate and lock
    (`:527`, `AcceleratorObject.cs:54`, `:191-192`) before the copy runs. A compacting
    garbage collection in that window moves the array, and the copy reads or writes its
    old address. ILGPU's span and array overloads pin first
    (`ArrayViewExtensions.cs:1258`, `:1289`, `fixed`); its `ref` overloads leave the pin
    to the caller. A lost download leaves the host slice as it was, zero-initialised,
    and `CaseStatus.Ok` is 0: the consumer receives an `Ok` case with zero figures. A
    lost upload makes the kernel read the array's old location. The path is the same on
    CUDA, where ILGPU takes the pointer before it branches on the accelerator type.
  - The `int` casts the span overloads need are safe: `BatchLength.Of` (the parent's)
    caps every host array at `int.MaxValue` elements.
  - ⚠ 2026-10-01: the fix of the second audit's observation 6 (2026-09-28) called the
    `ref` copy unsafe for its bounds only and added the length check before it
    (`CheckHostLength`, its comment in `ChunkBuffer.cs`). The copy was unsafe for its
    address as well, since the first commit of the parent node (`53ec9fb`,
    2026-09-12), so 0.1.0 carries the defect. Found by the release run of `v0.2.0` on a
    hosted `ubuntu-latest` runner: one rocket case of `Problems.Tests` came back `Ok`
    with every transport figure 0 on all four stations, while the two runs of the same
    commit before it were green. The investigation of 2026-10-01 reproduced it: a
    probe that holds the accelerator's lock across a forced compacting collection loses
    a 4-element download deterministically, and 4000 solves of that case under
    `DOTNET_GCgen0size=0x10000` on 4 cores lost 7 to 9 downloads per run, 0 with the
    span overloads (the reports are kept out of the tree with the audits').
  - The length check stays: the span constructor refuses a slice past the array's end
    with its own `ArgumentOutOfRangeException`, but `CheckHostLength` names the chunk.

- **A download that wrote nothing is refused** (2026-10-01, the owner's decision for
  0.2.0: the fix and a guard). Before `DownloadChunk` copies an `Output`,
  `ClearedOutput` or `InputOutput` buffer, it fills the chunk's host slice with the sentinel, every byte
  `0xFF`. After the copy it refuses a slice whose every element still holds the
  sentinel: `InvalidOperationException` naming the element type, the chunk's offset and
  length and the words "a download from the accelerator left its host slice
  unwritten".
  - It catches any lost download, whatever the cause, and leaves no `Ok` with zero
    figures behind. A copy that ran writes the device's bytes over the whole slice. The
    device side holds what the kernel wrote, or zeros for a `ClearedOutput` slot no case
    wrote, so it is never the sentinel everywhere:
    - an `int` or `CaseStatus` slice of all `-1`: no status is `-1`;
    - a `double` slice in which every value is the NaN with every bit set: no kernel
      produces that NaN. The NaN of x86 arithmetic is `0xFFF8000000000000` and PTX's
      canonical NaN `0x7FFFFFFF…`, and a payload only propagates from an input that
      carries it.
    The coder lists the element type of every `Output` and `ClearedOutput` declaration
    of the four pipelines and confirms the argument for each. A type the argument does
    not cover (a `byte` or `bool` slice a kernel may fill with `0xFF`) is reported to
    the owner of this design, not guarded by a weaker rule.
  - A lost upload is not detectable here; the pinned transfer above is its only guard.
  - The cost is one fill and one scan of the host slice per download, against a solve
    per case; the throughput tripwire of the tests node shows it.
  - No result changes: the sentinel is always overwritten by a copy that ran, so every
    bit snapshot holds unchanged.

## Acceptance criteria

→ [ACCEPTANCE.md](ACCEPTANCE.md)

## Taboos

- No `ref` into a managed array passed to an ILGPU transfer: the array can move under
  the copy (the constraint above, 2026-10-01).
- No public type: a cluster that needs one stays at the parent's own level instead
  (root `BOOT.md`, 2026-09-15).
- No CUDA type (`ILGPU.Runtime.Cuda`): inherited from the root, unchanged by the split.
- No second rule for a chunk's size or for a buffer's transfer direction outside
  `ChunkPlan.For` and the `ChunkTransfer` a declaration is given: a second place that
  decides either drifts from this one.
- No formula: this node moves bytes and slices ranges; it computes nothing a
  numerical node's kernel would recognise as physics.
