# ACCEPTANCE.md — Execution

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
      above the root's code-shape limits, the declared exceptions being the four views
      structs' constructors and the Ce of `Engine` and `Kernels`; `ShapeTests` green at
      `62cd99e`; the public surface moved only by `AcceleratorInfo.CudaSkippedBecause`
      (`c10ab0e`); `BatchTests.ChunkingAndRepetitionDoNotChangeABit` and the fast suite
      green → HISTORY.md#criterion-decomposition-2026-09-14

      ⚠ 2026-09-14: was "no method above the limits" evidenced by line counts only, now
      nesting too (`LibDeviceLocator.Locate` was 4 deep, brought to 3 in `5e3a24b`) →
      HISTORY.md#decomposition-nesting-not-measured-2026-09-14

      ⚠ 2026-09-15: was figures of the code at `42efbe7`, now `## Shape exceptions`
      holds ten rows and this node records no line figure of its own →
      HISTORY.md#decomposition-shape-rows-2026-09-15
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
      `EquilibriumBatchResult`), the emitted kernels unchanged;
      `ShapeTests.EveryWideConstructorIsCalledWithNamedArguments` takes over as the
      evidence → HISTORY.md#criterion-wide-constructors-named-2026-09-14
- [x] 2026-09-15 — `Engine.ProbeMath`'s dead `RunTimer` is gone, `Engine`'s Ce 26 → 25,
      held by `ShapeTests.EveryShapeExceptionIsMeasuredAndStillNeeded` (repair review,
      R-Execution-1) → HISTORY.md#criterion-dead-run-timer-2026-09-15
- [x] 2026-09-15 — `LibDevicePostLink.CompileAgainstLibdevice` merged back into
      `CompileWrappers` (R-Execution-2): `ShapeTests.NoControlFlowNestsDeeperThan3` and
      `NoMethodSpansMoreThan60Lines` hold, and
      `ProbeKernelTests.CudaMatchesTheCpuAcceleratorWithinTheUlpBoundForEveryFunction`
      is green on the reference machine →
      HISTORY.md#criterion-compile-against-libdevice-merged-2026-09-15
- [x] 2026-09-15 — Every ticked criterion above re-verified on the decomposed and
      repaired code at `62cd99e`: its tests green in the full suite
      (`APTHERMO_NO_CUDA=1`, every category, 3037 tests, none skipped), and
      CUDA-category evidence on the reference machine (`tests/Execution.Tests`, 41,
      and the long-running sweep and throughput tests).
- [x] 2026-09-15 (distribution phase) — Linux libdevice discovery (`cf87211`):
      `LibDeviceLocator` branches on `OperatingSystem.IsWindows()` / `IsLinux()` and is
      covered by `tests/Execution.Tests/LibDeviceDiscoveryTests.cs` through the internal
      `Locate` seam over fake toolkit trees (12 facts, all green, the ordering fact
      shown red once); the snapshots and the lint unchanged →
      HISTORY.md#criterion-linux-libdevice-discovery-2026-09-15

- [x] 2026-09-26 — No libnvvm or driver result is ignored: no `NvvmResult` or
      `CudaError` returned by a call of the post-link is discarded (no `_ =` on such a
      call remains in `LibDevicePostLink.cs`); the result-to-exception method is tested
      with every non-success `NvvmResult` and a failing `CudaError`; the CUDA tests of
      this node are green in Release, the long-running sweep and the throughput tripwire
      included, with no `Bits*` or `Throughput*` record moving; the fast suite and the
      lint are green.

      The implementation (the two `ThrowIfFailed` overloads, the checked calls, the
      release rule) and the evidence of the run (the `PostLinkTests` facts, 3178 fast
      tests, 126 of 126 in Release) →
      HISTORY.md#criterion-no-result-ignored-implementation-2026-09-26

      ⚠ 2026-09-26, review: was the message naming the call and the result only, now
      leading with "the libdevice post-link for {arch}" and the library; the release in
      `ReleaseProgram` is best-effort without a catch →
      HISTORY.md#no-result-ignored-message-review-2026-09-26

- [x] 2026-09-26 — Every GPU architecture (the root's criterion of the same date; audit
      finding F1).
      - **The architecture fact** (`Category=Cuda`, `Category=LongRunning`). The architectures are every
        `CudaArchitecture` ILGPU 1.5.3 declares from SM_75 up, and the entry points are
        every entry point of `Kernels`; both lists come from reflection. For each
        pair, the fact compiles the entry point with a `PTXBackend` for that
        architecture and the session's libnvvm, passes it through `Link`, and loads it
        on the reference device. It then asserts four things:
        - both paths of the post-link occur: at least one architecture where ILGPU
          defined every wrapper and `Link` compiled none, and one where `Link` compiled
          them all;
        - every kernel's PTX equals the same entry point's PTX for the device's own
          architecture, once ILGPU's generated numeric suffixes, comment lines, blank
          lines and the `.target` line are set aside;
        - the probe launched from each architecture's kernel returns the engine's own
          CUDA probe bit for bit;
        - it stays within `GpuCpuTolerances.MathUlp` of the CPU accelerator.

        The implementation of the architecture fact (11 architectures, SM_75 to SM_121,
        and 5 entry points; both post-link paths measured) and its red-once record →
        HISTORY.md#architecture-fact-implementation-2026-09-26

        ⚠ 2026-09-28: was "about three minutes" for the fact, now 8 m 2 s on Windows and
        11 m 3 s under WSL2, the assertions unchanged →
        HISTORY.md#architecture-fact-duration-2026-09-28
      - **The wrapper inventory without a GPU**, on the hosted runners. Two text
        fixtures hold ILGPU 1.5.3's PTX of the probe kernel: one for SM_89, which
        defines the wrappers, and one for SM_120, which defines none. Their provenance
        is in the tests node's `BOOT.md`. On both, the inventory reads the same called
        set, the math list's libdevice functions. It reads them all as defined on
        SM_89 and none on SM_120. No `_param_` name is read as a call. The result is
        the same with LF and CRLF line ends.

        The implementation of the inventory facts (`WrapperInventoryTests`, 5 facts) and
        their red-once record against the old `WrapperCall` regex →
        HISTORY.md#wrapper-inventory-implementation-2026-09-26
      - **The bind-time probe** (`Category=Cuda`). The real libnvvm is paired with a
        libdevice path to a file that is not libdevice bitcode, and discovery is off:
        - `Auto` binds the CPU accelerator, and `CudaSkippedBecause` names the
          libdevice post-link;
        - `Cuda` throws `AcceleratorUnavailableException` whose inner exception is the
          post-link's.

        The implementation of the bind-time facts and why no substitute input was needed
        → HISTORY.md#bind-time-probe-implementation-2026-09-26
      The evidence run of the criterion (134 of 134 in Release, 3185 fast tests) and the
      records it moved (`CHANGELOG.md`) →
      HISTORY.md#architecture-nothing-else-moves-2026-09-26

- [x] 2026-09-26 — The audit's F2, F3 and observations (Constraints; the observations of the post-link
      stand in `LibDevice/BOOT.md`), every fact but
      one (noted below) shown red once against the code before the change:
      - **The bad library.** `AcceleratorChoice.Cuda` now loads libnvvm and asks its IR
        version, then reads the bitcode, through one new internal `LoadNvvm`, before
        `CreateAccelerator` (also new) ever creates a CUDA context; the session keeps
        that one `NvvmAPI` binding, so no second `NvvmAPI.Create` follows once the
        accelerator is up. A file named as the platform's libnvvm that is not a
        library, with the real bitcode and discovery off
        (`BadLibraryTests.ABadLibraryNamesBothPathsAndNeverReachesTheDevice`,
        `Category=Cuda`):
        - `Cuda` throws `AcceleratorUnavailableException` naming both paths;
        - `Auto` binds the CPU with a `CudaSkippedBecause` that names the library
          path;
        - on the reference machine, the free device memory after 20 such `Auto`
          creations is within 64 MiB of the memory before it (measured: 15 037 MiB
          before, 15 037 MiB after, 0 MiB lost — the audit's own run of the
          pre-fix code lost 3 800 MiB over the same 20 attempts).

        The red-once record of the bad-library fact against the pre-fix order →
        HISTORY.md#bad-library-red-once-2026-09-26
      - **All cores.** `AcceleratorChoice.CpuDeviceFor(int)` sizes the CPU device from
        `Environment.ProcessorCount`. Below 4 processors the warp size alone carries the
        count (`Math.Max(2, count)`, one warp, one multiprocessor: ILGPU refuses a
        one-thread warp, so 2 and 3 match exactly and 1 does not). At 4 and above the
        warp size stays fixed at 4 — so 16 processors still reduce to exactly (4, 4, 1),
        the layout every bit and throughput record was measured against — and the count
        of whole 4-thread groups the processor count allows,
        `fourThreadGroups = processorCount / 4`, splits into a power-of-two warps count
        (its lowest set bit) and a multiprocessor count (the remaining factor), whose
        product reconstructs `fourThreadGroups` exactly; a processor count that is not a
        multiple of 4 falls back to the largest multiple of 4 not above it, since every
        group total this construction can reach is already at or under the count.

        The measurement of ILGPU's `CPUDevice` constructor and the proofs under
        `DOTNET_PROCESSOR_COUNT` 4, 12, 16 and 64 →
        HISTORY.md#all-cores-ilgpu-measurements-2026-09-26

        ⚠ 2026-09-26: was the layout "one multiprocessor throughout" (12 → 8, 24 → 16,
        48 → 32 threads), now the warp size fixed at 4 from 4 processors up and the
        multiprocessor count reaching every multiple of 4 →
        HISTORY.md#all-cores-one-multiprocessor-2026-09-26
      The observations' facts (NUL-trimmed logs, the half-given library pair, the upload
      disposal) and the evidence runs of the audit's F2, F3 and observations (140 of 140
      and 141 of 141 in Release) →
      HISTORY.md#audit-f2-f3-observations-and-evidence-2026-09-26

- [x] 2026-09-27 — Every CUDA context of a process binds under WSL (Constraints;
      the rule now stands in `LibDevice/BOOT.md`).
      Implemented as `CudaWslDevices.Register` (`src/Execution/LibDevice/CudaWslDevices.cs`):
      tries `builder.Cuda()` first, every time (no static state records that a resolver
      was ever set); when that call throws `InvalidOperationException` ("A resolver is
      already set for the assembly"), registers the devices itself through ILGPU's
      internal `CudaDevice.GetDevices(configure, predicate, registry)`, reflected by
      name (the property and the method are internal to ILGPU, which grants this
      assembly no `InternalsVisibleTo`), with the same no-op `configure` and the same
      `predicate` (a device with a known architecture and an instruction set the PTX
      backend supports) the no-argument `Cuda()` overload passes to it. A missing
      member is an `AcceleratorUnavailableException` naming it.

      The evidence: the red run under WSL at `89bb619`, the green runs on both platforms
      at `bfab662` and the missing-member fact → HISTORY.md#wsl-evidence-2026-09-27
- [x] 2026-09-28 — The audit fixes of 2026-09-28 (Constraints). Each fact is red once,
      against `5a732f0` or by the mutation named, then reverted:
      - **The probe (F1).** `Kernels.Probe` now calls `KernelMath.Min`/`Max` with the
        constant in both operand orders (`Min(v,1)`/`Max(v,1)` and `Min(1,v)`/`Max(1,v)`,
        `StrideCount` 12 → 14); `ProbeKernelTests` and `ArchitectureTests` compare every
        order against the CPU accelerator on CUDA. The two PTX fixtures are regenerated
        from the current probe on the reference machine and the wrapper-inventory facts
        (`WrapperInventoryTests`, no GPU) stay green against them (5/5).
      - **The launch budget (F2).** `LaunchBudgetTests` (7 facts): `LaunchBudget`'s
        arithmetic and `ChunkPlan.FirstChunkCases`/`NextChunkCases`, with injected times
        per case (no budget never bounds a chunk; a bounded one scales from the previous
        chunk's measured rate, never below one case; the first chunk is one wave with a
        budget and `Size` without one; later chunks stay within `Size` and the
        remainder); the timeout translation, an injected `CudaException` through
        `BatchRun.Execute` on the CPU accelerator becoming `AcceleratorUnavailableException`
        naming the limit, the chunk's case count and the CPU accelerator, red once by
        mismatching the catch filter; a non-timeout `CudaException` passes through
        unwrapped. `AcceleratorChoiceTests.TheCpuAcceleratorsBudgetIsUnboundedAndTheReferenceDevicesIsBounded`
        reads a real bind's `Engine.Budget`: unbounded on the CPU accelerator, bounded on
        the reference device (its run-time limit is enabled, on Windows and under WSL2).
        No kill is provoked anywhere: every timeout fact injects a `CudaException`
        directly into the launch delegate, on the CPU accelerator, never a real device or
        a real timeout; every real launch of this task stayed under a few seconds.
      - **The WSL workaround by `TargetSite`.** Green under WSL (`CudaWslDevicesTests.EveryCudaEngineOfTheProcessBindsAndProbes`,
        three engines in one process); `TheResolverAlreadySetFailureIsRecognisedByTargetSiteNotByMessage`
        constructs a real second-resolver failure (a fresh on-disk copy of an
        already-built assembly, loaded into its own load context, since
        `NativeLibrary.SetDllImportResolver` refuses a dynamic in-memory assembly) and a
        look-alike exception with the same English text but a different origin; red once
        by reverting `IsResolverAlreadySet` to a message-text match, which then accepted
        the look-alike.
      - The small items' facts (the 32-bit batch-length refusals, the `ChunkBuffer`
        host-array check, the `ProbeMath` input cap, the empty-log message) →
        HISTORY.md#audit-small-items-facts-2026-09-28
      The guards (F7, F8, O2), the WSL race on `LaunchBudgetTests` and its fix, and the
      evidence runs (162 of 162 on Windows and under WSL2) →
      HISTORY.md#audit-guards-race-and-evidence-2026-09-28
- [x] 2026-09-29 — The third audit pass of 2026-09-28 (part 2, finding 2 and the
      observation on `DeclareBuffers`) is closed.
      - **A lost context stays an `AcceleratorUnavailableException`.** After
        `CUDA_ERROR_LAUNCH_TIMEOUT` the context is lost: NVIDIA documents the error as
        sticky, and every later call on the context returns it. ILGPU 1.5.3 frees a
        CUDA buffer through `CudaException.VerifyDisposed(disposing, cuMemFree(…))`,
        which throws, with no catch on the way (`DisposeDriver`,
        `AcceleratorObject.Dispose(bool)`, `Accelerator.DisposeChildObject`). So the
        pipelines' `using var buffers`, and the engine's and the solver's disposal,
        would replace the translated exception with a raw `CudaException`. The rule,
        implemented as `AcceleratorSession.MarkLost`/`ThrowIfLost`/`DropsAfterLoss`
        (`AcceleratorSession.cs`):
        - `BatchRun.Launch` calls `session.MarkLost(timeout)` in the same catch that
          builds the translated `AcceleratorUnavailableException`, before throwing it;
        - `Engine.Guard` (every `Run` overload), `Upload` and `ProbeMath` call
          `_session.ThrowIfLost()` first: a later call on a lost session throws a new
          `AcceleratorUnavailableException` naming the earlier timeout (its message
          quoted) as its own message, with the earlier exception as its inner one — the
          consumer's remedy is a new engine or the CPU accelerator;
        - `AcceleratorSession.DropsAfterLoss(CudaError)` is the one decision behind
          every drop: true only when this session is marked lost and the error is
          `CUDA_ERROR_LAUNCH_TIMEOUT`, decided on the bare enum value so the decision
          itself needs no driver-touching `CudaException` to test (2026-09-29, the
          second review below); `DropsAfterLoss(CudaException)` is a thin extraction of
          `CudaException.Error` onto it, and is what every real catch filter reads.
          `AcceleratorSession.Dispose` reads it in three literal
          `try { … } catch (CudaException failure) when (DropsAfterLoss(failure))`
          blocks, one per disposed piece (`Nvvm`, the accelerator, the context), each
          proceeding to the next regardless (releases what it can); `Engine.DisposeAfterLoss`
          reads the same decision for `UploadedTables`' own buffers; `BatchRun`'s new
          `DisposeChunkBuffers` reads it for a pipeline's chunk buffers. Any other
          exception, or a session never marked lost, is unaffected;
        - no ILGPU type reaches a consumer; `API.md`'s errors table and the guide's GPU
          page (`docs/guide/gpu.md`) say that an engine that timed out is unusable.

        ⚠ 2026-09-29: was the drop of the sticky failure "in one named place of this
        node", now one decision (`DropsAfterLoss`) read by several catches →
        HISTORY.md#lost-context-catch-place-2026-09-29
      The evidence: why the timeout cannot be provoked and the two kinds of fact (the
      `Cuda`-tagged ones on the reference machine and the CPU ones everywhere) →
      HISTORY.md#lost-context-evidence-seams-2026-09-29

        ⚠ 2026-09-29: was the evidence "proven" by two gated facts that returned early
        under `APTHERMO_NO_CUDA=1`, now facts that run everywhere →
        HISTORY.md#lost-context-first-correction-2026-09-29

        ⚠ 2026-09-29: was the gate removed from both facts (which loaded `nvcuda` and
        weakened `NoCudaDriverIsLoadedInAProcessThatForbidsCuda`), now the gate restored
        and two CPU facts on the decision itself →
        HISTORY.md#lost-context-second-correction-2026-09-29
      - **One declaration of a pipeline's buffers.** `DeclareBuffers` and `Run` read the
        same private `Declare` method (`EquilibriumPipeline`, `RocketPipeline`,
        `TransportPipeline`), so the proof of the 32-bit offset cap cannot drift from
        the pipeline it proves. `Declare` takes the real batch for `Run` and a
        single-case (`EquilibriumBatch`/`TransportBatch`) or single-exit-array
        (`RocketBatch`) placeholder batch for `DeclareBuffers`, declares every buffer
        exactly once on the `ChunkBuffers` it is given, and returns the buffer handles
        together with the host output arrays `Run` assembles its result from; the
        `SpeciesFunctionPipeline` has no `DeclareBuffers` and is unchanged (every one of
        its strides is 1, `## Structure`).

      The evidence run of the criterion (5429 of 5429 fast tests, no snapshot moved) →
      HISTORY.md#lost-context-evidence-run-2026-09-29
- [x] 2026-09-30 — The rocket kernel's compile is bounded and released (2026-09-30, the root's
      criterion of that date). The CUDA proof is the root's, after the merge.
      - **Release at dispose.** `Engine.Dispose` calls `KernelCache.Clear` before it disposes
        the session. `Clear` drops every launcher and clears the ILGPU context's caches
        (`Context.ClearCache(ClearCacheMode.Everything)`, which the library documents as
        touching no accelerator cache), so a disposed engine that stays reachable (a field,
        a static fixture) holds no compiled kernel. Two facts, `RocketCompileTests`:
        - `ADisposedEngineHoldsNoLauncher`: after `Dispose` the cache is empty, and a
          `WeakReference` to a launcher taken before it is dead after a collection while the
          engine object is still referenced. Red without the `Clear` call in `Dispose`, and
          red on the weak reference alone with the count assertion removed (both seen).
        - `ADisposedEngineKeepsNoCompiledProgram`: the managed heap after `Dispose` and a
          full collection is under a quarter of what the engine kept while in use, measured
          against the heap before the run; it fails on an empty measurement (under 32 MiB
          kept). Seen red without the context call: 150 709 016 bytes kept live and
          150 683 800 after `Dispose`. Green: 150 714 944 and 385 120. In the red state of
          the inlining (the attribute removed): 4 157 812 152 bytes kept live and 228 048
          after `Dispose`.

        ⚠ 2026-09-30: was "`KernelCache` gains a `Clear`" as the whole release, now
        `Clear` also clears the context's caches (143 MiB to 0) and a second fact
        watches the heap → HISTORY.md#release-at-dispose-context-cache-2026-09-30

        Decision on `Context.ClearCache` after each kernel load: not adopted (the
        equilibrium kernel's later load is 2.5 times slower after it and the peak is not
        lower); the call runs at `Dispose` only →
        HISTORY.md#clear-cache-after-each-load-not-adopted-2026-09-30
      - **The guard.** `RocketCompileTests.TheRocketKernelCompilesWithinItsAllocationBound`:
        the first rocket run, one case, of a fresh CPU accelerator engine allocates under
        2 GiB on the calling thread (`GC.GetAllocatedBytesForCurrentThread`). It fails when
        the run reports no warm-up (nothing was compiled) and below 32 MiB (an empty
        measurement). Not `LongRunning`: 3.4 s green, about 2 s of it the compile.
        The fact that `StationSolve.At` carries `NoInlining` is the performance tests node's
        (`CompileSizeTests.TheStationSolveIsNotInlined`), moved there on 2026-09-30.

        The metric chosen for the guard (bytes allocated on the calling thread) and the
        five fresh-process runs each of Debug and Release, with the attribute on and
        removed → HISTORY.md#compile-guard-metric-and-runs-2026-09-30

        The bound is 2 GiB, 6.3 times the largest green figure (324 MiB) and 0.30 of the
        smallest red one (6 853 MiB). Red with the attribute removed in all 10 runs, green
        with it in all 10, and the guard fails on an empty measurement (its floor).
      - **No test allocates what it measures.** `BatchLength.Of` is the one method the three
        batch constructors call for the 32-bit bound (since 2026-09-28, observation 6; the
        design's "one internal method" already existed). `AcceleratorChoiceTests`'s
        16 GB array made to read a length is gone; `TheBatchLengthBoundIsInclusiveOfTheLargestArrayLength`
        calls `BatchLength.Of` at `int.MaxValue` and just under it (three products) and just
        over it (three), and the constructor facts that throw stay. Red with the bound off
        by one (`>=` in place of `>`, seen). The constructor's own success at the limit is no
        longer exercised, only the arithmetic it calls.

        ⚠ 2026-09-30: was one allocation named (the 16 GB array), now a second removed
        too (`ProbeMath`'s 1.2 GB input, replaced by `MathProbe.OutputLength`) →
        HISTORY.md#probe-allocation-replaced-2026-09-30
      - **Records.** `API.md` names `KernelCache`, `BatchLength` and `MathProbe.OutputLength`
        and `Engine.Launchers` in its tree-contract section, and the tree-contract
        snapshot (`tests/Protocol.Tests/TreeContract.approved.txt`, replaced in the same
        commit) moves by exactly those. No package surface moves:
        `PublicSurface.approved.txt` is unchanged.

      The evidence at `ee3c598` and the per-project peak table (0.75 to 1.61 GiB per
      process tree) → HISTORY.md#compile-bound-evidence-table-2026-09-30
