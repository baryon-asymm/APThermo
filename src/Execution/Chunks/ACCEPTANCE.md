- [x] 2026-11-14 — Initial identity: BufferIdentityTests enumerates the actual
  ChunkTransfer inventory, changes every initial input mode/constant/seed, signed
  zero/NaN payload, type, order, length and stride; output contents are ignored but
  structure is framed. A clone matches, a seed changed by download does not. Capture
  after Allocate/dispose is refused and unsupported declarations disable reuse.
  Evidence: buffer-identity-final-green.trx (25/25 including BatchCleanupAuditTests
  and ChunkTransferTests), in SCRATCH/audit of audit-buffer-identity. Multi-window
  input spans 196627 bytes and checks first/middle/final-byte mutations. Omitting the
  last byte from each actual hash span makes both the independent wire oracle and
  multi-window control red (buffer-identity-omitted-byte-red.trx, 2/2 failed).
  The independent closed role table is compared with Enum.GetValues<ChunkTransfer>;
  a temporary defined FutureInput left unclassified makes completeness red while
  AppendIdentity still refuses it (buffer-identity-new-mode-red.trx: 1 fail/1 pass).
  All mutations restored. Source build: 0 warnings/errors; strict lint: 0/0.
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

- [x] 2026-09-26 — The element cap. A host fact plans a table at `TableLimits` (a planner
      input of 13 248 doubles per case, typed in the fact and kept as such; the worked
      example above states the current figure) with `ScratchBytes` of
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
        is asserted by the parent's `PtxPostLink.AssertIlgpu` (2026-10-05, was `LibDevicePostLink`)) while the main
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

- [x] 2026-10-05 — `InputOutput` (the constraint above, 2026-10-04): the round trip
      (`ChunkTransferTests.AnInputOutputBufferUploadsItsSliceAndReturnsTheDevicesValues`), the guard on its download
      (`ChunkTransferTests.AnInputOutputSliceOfSentinelBytesIsRefused`), chunk independence of a seeded batch
      (`BatchTests.ASeededBatchIsIndependentOfChunking`). Its one declaration is the equilibrium moles of a seeded
      batch, element type `double`, finite by the batch's validation, so the 2026-10-01 argument covers it. Red once:
      `InputOutput` dropped from `UploadChunk`'s branch, then from `DownloadChunk`'s, then the upload's start taken at
      offset 0 — each named fact red, each reverted.
      Evidence: the facts and the mutations 2026-10-04 (`cc531473`), the three facts green again 2026-10-05 on the
      CPU accelerator (Release, `APTHERMO_NO_CUDA=1`); on CUDA the buffer is the seeded families' path,
      `CudaTests.ASeededFamilyOnCudaMatchesTheCpuAccelerator` bit for bit, Windows and WSL2, 2026-10-05 (the parent's
      seeded-batch criterion, `../ACCEPTANCE.md`).
