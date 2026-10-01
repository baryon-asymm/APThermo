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
  0.2.0: the fix and a guard). Before `DownloadChunk` copies an `Output` or
  `ClearedOutput` buffer, it fills the chunk's host slice with the sentinel, every byte
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

- [x] 2026-09-28 — A launch fits a time budget (the second audit's Execution finding
      F2). `LaunchBudget` holds no ILGPU type (`NoBudgetNeverBoundsAChunk`,
      `ABoundedBudgetScalesFromThePreviousChunksTimePerCase`,
      `ABoundedBudgetNeverAnswersBelowOneCase`), `ChunkPlan.FirstChunkCases` and
      `.NextChunkCases` stay within `Size` and the batch's remainder with and without a
      budget (`TheFirstChunkIsSizeWithNoBudgetAndOneWaveWithABudget`,
      `NextChunkCasesStaysWithinSizeAndTheRemainder`), and `BatchRun`'s timeout
      translation is exercised with an injected `CudaException`, no real device or real
      timeout (`ALaunchTimeoutBecomesAnAcceleratorUnavailableExceptionNamingTheLimitAndTheRemedy`,
      `ANonTimeoutCudaFailurePassesThroughUnwrapped`, the second gated the same way as
      the library-discovery facts elsewhere in the parent node: `CudaException`'s own
      constructor asks the driver for the error text). All seven in
      `tests/Execution.Tests/LaunchBudgetTests.cs`.

      Shown red once: `BatchRun.Launch`'s catch filter changed from
      `CUDA_ERROR_LAUNCH_TIMEOUT` to `CUDA_ERROR_OUT_OF_MEMORY` —
      `ALaunchTimeoutBecomesAnAcceleratorUnavailableExceptionNamingTheLimitAndTheRemedy`
      failed, the injected `CudaException` passing through unwrapped instead of
      becoming `AcceleratorUnavailableException` — then reverted, the same command
      green again (`dotnet test tests/Execution.Tests --filter
      "FullyQualifiedName~LaunchBudgetTests"`, 7/7).

      Verified on the reference machine: `dotnet build tests/Execution.Tests` 0
      warnings, 0 errors (the Cli.Tests project of a concurrent full-suite run held its
      own copy of `APThermo.Execution.dll`, so this node's own project is built and
      tested directly rather than the whole solution for this step; the full-solution
      build is re-verified separately); `dotnet test tests/Protocol.Tests` 32/32 with
      `src/Execution/BOOT.md`'s `## Shape exceptions` numbers re-measured for `Engine`
      (25 → 30, `Budget`/`RunBatchLoop` added) and the four pipelines (`LaunchBudget`
      now named through `session.Budget`); the protocol lint 0 errors, 0 warnings.

- [x] 2026-09-28 — The cap is proven as wiring (the guards part's F8). Each of
      `EquilibriumPipeline`, `RocketPipeline` and `TransportPipeline` gained an
      internal `DeclareBuffers` method mirroring its `Run`'s buffer declarations with
      empty host arrays (no device allocation); a fact feeds each a large synthetic
      case count through `ChunkPlan.For` directly and asserts the chosen plan's
      `Size × expectedLargestStride` stays within a 32-bit offset, where
      `expectedLargestStride` is computed independently from the same production
      layout formulas (`ScratchLayout`, `RocketLayout`, `TransportLayout`), never by
      reading `buffers.MaxElementsPerCase` back — the read-back would make the fact
      pass under any mutation of that property, since the wrong number would then
      compare against itself.
      `ChunkPlanWiringTests.EachPipelinesChosenPlanRespectsItsOwnOffsetCap`
      (`tests/Execution.Tests/ChunkPlanWiringTests.cs`), a theory over the three
      pipelines (`SpeciesFunctionPipeline` excepted: every one of its strides is 1, so
      the cap is vacuous there and it gained no `DeclareBuffers`).

      Shown red once: `ChunkBuffers.MaxElementsPerCase` changed from the declared
      buffers' own maximum to `_buffers.Count` (still instance data, so the mutation
      compiles) — all three theory rows failed, each naming its own pipeline and an
      overflow the mutated, too-small stride no longer caught — then reverted, the
      same filter green again (`dotnet test tests/Execution.Tests --filter
      "FullyQualifiedName~ChunkPlanWiringTests"`, 3/3). This is exactly the mutation
      `AGENTS.md` §13 warns a check must survive: the earlier fact
      (`ChunksStayWithinInt32OffsetsAtTableLimits`, above) calls `ChunkPlan.For` with
      explicit numbers, and would not have caught a pipeline itself passing the wrong
      `MaxElementsPerCase` — only a fact that drives the real pipeline construction
      does.

      Verified on the reference machine alongside the criterion above (same build and
      protocol-test evidence).

- [x] 2026-10-01 — Host transfers are pinned, and an unwritten download is refused (2026-10-01, the two
      constraints above).
      - `ChunkBuffer<T>`'s `UploadChunk` and `DownloadChunk` call ILGPU's span overloads;
        no `ref` transfer is left in `src`.
      - A fact in `tests/Execution.Tests`, "a chunk download survives a compacting
        collection inside its transfer": a `ChunkBuffers` with one `Output<int>` buffer,
        the device filled with a pattern; a helper thread holds ILGPU's
        `Accelerator.syncRoot` (a private field, reached by reflection; ILGPU's version
        is asserted by the parent's `LibDevicePostLink.AssertIlgpu`) while the main
        thread enters `DownloadChunk`, runs `GC.Collect(0, GCCollectionMode.Forced,
        blocking: true, compacting: true)` and releases it; the fact asserts that the
        pattern arrived in the host array, and that the collection compacted: a second,
        unpinned array of the same age, allocated beside the host array, has another
        address after the collection than before it, so the fact cannot pass on a
        collection that moved nothing. Red once with the `ref` overload restored and the guard removed
        (the host holds zeros), and with the `ref` overload restored and the guard kept
        (the guard's `InvalidOperationException`).
      - A fact on the guard alone: a slice that holds the sentinel everywhere is
        refused naming the type, the offset and the length; a slice with one element
        written passes; an empty slice is not refused (nothing to download). Red once
        with the check removed.
      - The protocol tests node's check that no `src` method calls an ILGPU transfer
        with a by-reference parameter (its `BOOT.md`), red at `05e2d39` on the two sites.
      - Every bit snapshot unchanged, the throughput tripwire within its floor, the
        fast suite green on Windows and under WSL2, the execution tests node green on
        CUDA on the reference machine.
      - The stress of the investigation, run by the orchestrator in WSL2 on the merged
        tree: 4000 solves of `nto-udmh_of2.6_pc2MPa_shiftingEquilibrium` under
        `DOTNET_GCgen0size=0x10000` on 4 cores, 0 lost downloads and 0 refusals.

      Evidence so far (2026-10-01, the coder's part; the box stays unticked until the CUDA
      run and the stress above are in):
      - `ChunkBuffer<T>` calls ILGPU's span overloads on `Allocated.View.BaseView.SubView(0,
        span)`; no `ref` transfer is left in `src`
        (`Protocol.Tests.InvariantTests.NoSrcMethodPassesHostMemoryToAnIlgpuTransferByReference`).
      - The facts are `Execution.Tests.ChunkTransferTests` (its `BOOT.md`, criterion of this
        date). Red once: the `ref` overload restored with the guard removed, the host holds
        `[0, 0, 0, 0]`; the `ref` overload restored with the guard kept, the downloading
        thread's `InvalidOperationException` ("left its host slice unwritten"); the check
        after the copy removed, the guard fact's first assertion fails.
      - The element type of every `Output` and `ClearedOutput` declaration of the four
        pipelines, listed from the sources of `EquilibriumPipeline`, `RocketPipeline`,
        `TransportPipeline` and `SpeciesFunctionPipeline`:

        | Type | Declared as | The all-ones slice cannot be written because |
        |---|---|---|
        | `int` | `status`, `iterations`, `stationStatus` (`Output`); the species-function `inRange` flag (`Output`, 0 or 1) | no status (`CaseStatus` 0 to 7), no iteration count and no flag is -1 |
        | `double` | `moles` (`ClearedOutput`), the species-function `cpOverR`, `hOverRT`, `sOverR` (`Output`) | an element would have to be the NaN with every bit set, and no kernel writes it (x86 arithmetic gives `0xFFF8…`, PTX `0x7FFFFFFF…`, a payload only propagates from an input) |
        | `MixtureState` | `states` (`ClearedOutput`), `stations` (`ClearedOutput`) | a struct of `double` properties and nothing else (19 at this date): the `double` argument holds for each field |
        | `PerformanceFigures` | `figures` (`ClearedOutput`) | doubles only, same argument |
        | `TransportFigures` | `figures` (`Output`) | `double` properties and a few `int` counts: one `double` field that no kernel writes as the all-ones NaN is enough, same argument |

        No `byte`, `bool` or other type the argument does not cover occurs, so the rule
        needed no weakening.
      - The by-reference rule of the protocol tests node is narrower than its first wording:
        ILGPU's pinning span overloads take their span `in`, a by-reference parameter too,
        so the fact refuses a by-reference parameter that is not a span (that node's `BOOT.md`).
      - Debug and Release, `APTHERMO_NO_CUDA=1`: the solution builds with 0 warnings and 0
        errors, the fast suite is 5442 of 5442 in Debug (bit snapshots included, no
        `Bits*.approved.txt` changed) and 5099 of 5099 in Release without the bit snapshots;
        `ChunkTransferTests` passed 22 of 22 runs in a row; the protocol lint gives 0 and 0.
        Not run here: the execution tests on CUDA, the throughput tripwire, the WSL2 run
        and the stress.

      Evidence of the orchestrator, 2026-10-01, merged as `93f29c9`, on the reference machine:
      - Windows, Release: the release job's filter (`Category=Cuda|Category=BitSnapshot`)
        green in every project, no approved record moved; `tests/Execution.Tests` on CUDA
        172 of 173, the 100 000-case sweep and every architecture included. The one red
        fact was the throughput tripwire at 11.35×, with the CUDA kernel at 0.331 s against
        about 0.15 s: another project's test process held the GPU at 100 %. Once it ended,
        three runs of the tripwire gave 22.69×, 19.96× and 19.39× against the floor of
        18.86× (80 % of 23.58×). The commit before the fix (`1756692`), measured the same
        way right after, gave 20.08×, 12.55× and 19.70×: the guard's cost is inside the
        run-to-run spread, and no throughput record is re-approved.
      - WSL2, Release: the release job's filter green, the Linux bit snapshots included;
        `tests/Execution.Tests` on CUDA 172 of 173, the tripwire red at 19.09× while the
        other project held the GPU, and three runs on the free GPU 31.65×, 29.26× and
        30.79× against the floor of 21.98× (80 % of 27.48×).
      - The stress, WSL2 on 4 cores (`taskset -c 0-3`), `DOTNET_GCgen0size=0x10000`,
        `APTHERMO_NO_CUDA=1`, the investigation's harness over the public `Solver`: three
        runs of 4000 solves of `nto-udmh_of2.6_pc2MPa_shiftingEquilibrium`, about 3 000
        gen0 collections each, 0 stations `Ok` with zero transport, 0 other differences
        from the first solve and 0 refusals (a refusal would have ended the run). The same
        harness lost 7 to 9 downloads per run before the fix.

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
