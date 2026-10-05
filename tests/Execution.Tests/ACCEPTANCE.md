# ACCEPTANCE.md — tests/Execution.Tests

The node's acceptance criteria (AGENTS.md §6, §15), moved here from `BOOT.md` on
2026-10-03 when the balance-remnant correction would have taken the leaf over its limit.

- [x] 2026-10-05 — The rocket compile bound is a property of the code, not of the machine (the L0 row of `BOOT.md`; release 0.2.2,
      coder 16). `RocketCompileTests.TheRocketKernelCompilesWithinItsAllocationBound` reads the bytes
      allocated by every thread of the process (`GC.GetTotalAllocatedBytes(precise: true)`) over the first rocket run of a fresh CPU
      engine and bounds them at 7 GiB; the calling thread's share is printed beside it and asserted on nothing. Evidence, 2026-10-05,
      the table of `BOOT.md`: the whole process allocated 3 575.9 to 3 577.6 MB in Release at 2, 4, 8 and 16 processors (spread 0.05 %)
      and 3 888.8 to 3 890.1 MB in Debug at 4 and 16, while the calling thread allocated 1.01 to 1.60 GB; the fast set (289 facts,
      Release, `APTHERMO_NO_CUDA=1`) is green and its figure for the fact differs from the fact alone by 0.5 MB. Hosted
      `windows-latest` had failed the calling-thread bound with 2.57 GB. ⚠ 2026-10-05: was the calling thread's allocation under
      2 GiB, now the process's under 7 GiB → HISTORY.md#compile-bound-process-wide-2026-10-05
- [x] 2026-10-05 — `TheRocketKernelCompilesWithinItsAllocationBound` seen red under the new measure with the attribute of
      `StationSolve.At` removed (a scratch edit in `src/Performance`, reverted; run by the orchestrator, Release,
      `APTHERMO_NO_CUDA=1`, 16 processors): 113 080 797 032 bytes in the process (44 785 589 192 on the calling thread),
      warm-up 263.22 s, against the bound of 7 516 192 768: red by a factor of 15.
- [x] 2026-10-05 — CUDA equals the CPU accelerator bit for bit on every family (`## Invariants`, item 13 of 0.2.2, owner
      decisions O1 and O6). `ExactComparison` (`Rocket`, `Equilibrium`, `Transport`, `Functions`) compares the bits of every
      field of every result, the statuses and iteration counts included, and returns at most 30 mismatches, each naming the
      case, the field and both values; the facts that run it on CUDA are the families of `CudaTests`: `ARocketFamilyOnCudaMatchesTheCpuAccelerator`,
      `AThroatFamilyOnCudaMatchesTheCpuAccelerator`, `AnEquilibriumFamilyOnCudaMatchesTheCpuAccelerator`, `ANamedEquilibriumFamilyOnCudaMatchesTheCpuAccelerator`,
      `AnEquilibriumTableFamilyOnCudaMatchesTheCpuAccelerator`, `ASeededFamilyOnCudaMatchesTheCpuAccelerator`,
      `AGasPlateauFamilyOnCudaMatchesTheCpuAccelerator`, `ABracketedFamilyOnCudaMatchesTheCpuAccelerator`, the 100 000-case sweep
      (`TheSweepOf100000CasesOnCudaEqualsTheCpuAcceleratorBitForBitAndIsDeterministic`), `SpeciesFunctionTests.CudaEqualsTheCpuAcceleratorBitForBit`
      and `ProbeKernelTests.CudaEqualsTheCpuAcceleratorBitForBitForEveryFunction`; the families are enumerated from the fixture
      directories and `FixtureBatches`, never typed. The CPU accelerator equals the host solver bit for bit as before (`BatchTests`).
      Evidence, 2026-10-05, the tree merged with coder 9's balancing-record rule: Windows, Debug, `Category=Cuda` 80 of 81 (the
      throughput fact refuses a Debug run, as designed), Release throughput and compile facts 4 of 4; WSL2 Ubuntu 24.04, Release,
      `Category=Cuda` 81 of 81 (the sweep, the architecture fact and the throughput fact included, 27 m 37 s). No tier, share bound, correction or case was needed to get there.
- [x] 2026-10-05 — The comparison is proven non-degenerate: `ExactComparisonTests` (7 facts: two runs compare clean; an
      equilibrium and a rocket result differing in one thing, a transport and a function result differing in one value, a signed
      zero and a NaN told apart, results of different shapes refused before any value is read, at most the shown mismatches
      returned). Red once, 2026-10-05, with `ExactComparison.cs` mutated and reverted: doubles compared by value in place of bits (`ASignedZeroAndANaNAreTold` red); the status and count comparison disabled (`ARocketResultDifferingInOneThingIsRefusedWithItsOwnMessage`, the equilibrium one and the transport and function one red); the cap raised from 30 to 3 000 000 (`AComparisonReturnsAtMostTheShownMismatches` red); a fourth mutation, the struct comparison removed, is refused by the analyzers (IDE0060) before it can run.
- [x] 2026-10-05 — The tolerance machinery is gone: no `GpuCpuTolerances`, `GpuCpuComparison`, `StepShareLedger`, `ComparisonRule`,
      balance-remnant or phase-onset type or fact remains in this node (a search of `tests/Execution.Tests` finds none), and the
      only bound that survives, `ElementBalance.ClosureBound` (1e-13), bounds one solution's element balance.
- [x] 2026-10-05 — Throughput (`## Invariants`, the approved throughput file): both records re-approved from Release runs, the
      median of three on each platform, on a machine shared with other test runs (the orchestrator re-measures on a quiet one).
      Windows: CUDA 0.360 s, CPU accelerator 7.217 s, ratio 20.07, kernel 1.426e-07 s per Newton step (runs 1.426e-07, 1.256e-07,
      1.433e-07; 22.403 steps per case, unchanged). WSL2: ratio 31.07, kernel 1.364e-07 s per step (runs 1.288e-07, 1.364e-07,
      1.537e-07), the file now carrying the per-step lines, so the Linux throughput fact passes. Before: 7.85e-08 s per step and
      5.3 s on the CPU accelerator (2026-10-04, quiet): the kernel +82 % and the CPU +36 %, the cost of correctly rounded `exp`,
      `log` and `pow` in double-double arithmetic on both accelerators. The 115 % tripwire reads the new figure. The compile guard
      (`RocketCompileTests`, 3 facts) is green in Release with the new kernel.

- [x] 2026-09-14 — The support code in shape (the test review's F-TF-06 and F-TF-13):
      `BatchBuilders` replaced by `FixtureBatches.cs`, `HostSolves.cs` (named record
      structs) and `SweepRun.cs`; no method over 60 lines or nested deeper than 3
      (`ShapeTests`, ten facts green at `62cd99e`); every L2 fact green bit for bit
      after the split and the node's mutations re-run alone and seen red where the
      touched code moved → HISTORY.md#criterion-support-code-in-shape-2026-09-14

      ⚠ 2026-09-15: was "nested deeper than 3" evidenced by `inventory.py` line counts
      alone, now measured: two methods were 4 deep and were brought to 3 →
      HISTORY.md#nesting-depth-not-measured-2026-09-15
- [x] 2026-09-15 — Two more constructions restructured to the root's parameter limit:
      `RocketInputs` (`FixtureBatches.cs`) fell
      from 9 to 6 parameters, split along the domain axes of a rocket fixture's
      chemical system (`ChemicalSystem`: elements, element moles, products - 3
      parameters) and its exit layout (`ExitPlan`: values, kinds - 2 parameters), the
      combustion conditions and the transport flag kept flat; the old field names
      stay as forwarding properties, so every read call site is unchanged and only
      the one construction site, in `RocketInputs.Of`, changed. `CudaTests.CompareMoles`
      fell from 8 to 5 parameters: the two mole arrays under comparison, the index
      into them and the species table that reads them became `MoleSample` (4
      parameters), a type local to `CudaTests.cs`; its two call sites (one in
      `AnEquilibriumFamilyOnCudaMatchesTheCpuAccelerator`, the other in the
      private `CompareRocket`, itself called from the rocket-family and the sweep
      tests) construct it in place of the four separate parameters.

      ⚠ 2026-09-15: was `MoleSample` local to `CudaTests.cs` and `CompareRocket` there,
      now both moved to `GpuCpuComparison.cs` the same day →
      HISTORY.md#mole-sample-moved-2026-09-15

      The two nesting-depth-4 fixes of the same review (`StationMoleDifferences`,
      `CompareFunctions`) and the run that verified them →
      HISTORY.md#nesting-fixes-evidence-2026-09-15

      ⚠ 2026-09-15: was `ChemicalSystem` holding `ElementMoles`, with forwarding
      properties for the old field names, now a `Mixture` record holds `ElementMoles`
      and `ReactantEnthalpy` and the forwarding properties are gone →
      HISTORY.md#chemical-system-forwarding-properties-2026-09-15

- [x] 2026-09-17 — Shown red once, on Windows: `Throughput.approved.txt`'s `ratio`
      line mutated from `56.28` to `999.00`, then `dotnet test tests/Execution.Tests
      --filter "FullyQualifiedName~ThroughputIsRecordedAndNotBelowTheApprovedRatio"`
      failed — "CUDA/CPU ratio 66.03 fell below 80 % of the approved 999.00
      (Throughput.approved.txt)" — naming the platform's own file, as
      `ApprovedPathFor` picks it. Reverted with `git checkout --
      tests/Execution.Tests/Throughput.approved.txt`; `dotnet test
      tests/Execution.Tests` confirmed 55/55 green again.
- [x] 2026-09-30 — The compile guard and the bound check of 2026-09-30 are proved (the execution node's
      `ACCEPTANCE.md`, criterion of that date, owns their design): each fact red once with what it guards
      undone, one process's run of the whole project below 2 GB of private memory at its
      peak on the reference machine, recorded here with the figure.

      Evidence: the facts are `RocketCompileTests` (`TheRocketKernelCompilesWithinItsAllocationBound`,
      `ADisposedEngineHoldsNoLauncher`,
      `ADisposedEngineKeepsNoCompiledProgram`) and, in `AcceleratorChoiceTests`,
      `TheBatchLengthBoundIsInclusiveOfTheLargestArrayLength` and
      `TheProbeOutputLengthBoundIsInclusiveOfTheLargestOffset`. Each was seen red with what
      it guards undone, applied alone:
      - The red-once record of each fact (the attribute removed from `StationSolve.At`,
        `Launchers.Clear()`, `Context.ClearCache`, the bounds of `BatchLength.Of` and of
        `MathProbe.OutputLength`) → HISTORY.md#compile-bound-red-once-list-2026-09-30

      ⚠ 2026-09-30: was one allocation named for removal (the 16 GB array), now a second
      removed too (`ProbeMath`'s 1.2 GB input, replaced by a fact on
      `MathProbe.OutputLength`); the owner may decide whether the trade stands →
      HISTORY.md#probe-allocation-replaced-2026-09-30

      The figures of the guard and the per-run table are in the execution node's `ACCEPTANCE.md`, criterion of that date.
      The peak private memory of one process's run of the whole project, `APTHERMO_NO_CUDA=1`,
      `Category!=LongRunning`, on this machine (60 GB, shared; the orchestrator confirms
      whether it is the reference machine), the largest process being the test host, the
      peak read from the process's own peak commit, sampled every 50 ms: Debug over five
      runs 0.92, 0.88, 0.91, 0.89 and 0.93 GiB; Release over three runs 0.84, 0.81 and
      0.81 GiB. Below 2 GiB with a wide margin. The process tree, the children of
      `AllCoresLayoutTests` included, peaked at 1.60 to 1.65 GiB in Debug and 1.48 to 1.50 GiB
      in Release. Before the inlining bound the same project exceeded 27 GB in one process
      tree (the root's Compile size constraint); with the bound and before the second
      allocation was removed, the test host peaked at 1.81 to 2.11 GiB.

- [x] 2026-10-01 — The transfers of the `Chunks` child node are proved (2026-10-01): the compacting
      collection fact and the guard fact of `src/Execution/ACCEPTANCE.md`'s criterion of
      that date live here, in `ChunkTransferTests`, on the CPU accelerator, and run in the
      fast suite under `APTHERMO_NO_CUDA=1`. The collection fact holds ILGPU's private
      `Accelerator.syncRoot` from a helper thread for at most the duration of one forced
      collection, with a timeout on every wait so that a broken construction fails in
      seconds rather than hanging the run.

      Evidence: `ChunkTransferTests` (3 facts: `AChunkDownloadSurvivesACompactingCollectionInsideItsTransfer`,
      `ADownloadThatLeftItsSliceUnwrittenIsRefused`, `OnlyASliceOfSentinelBytesHoldsOnlyTheSentinel`),
      green in Debug and in Release, 22 runs in a row, and in the fast suites of both
      configurations (Debug 170 of 170 here, the node's full count). Red once, 2026-10-01:
      with the `ref` overload restored in `DownloadChunk` and the guard removed, the host array
      holds `[0, 0, 0, 0]` and the guard fact fails too; with the `ref` overload restored and
      the guard kept, the downloading thread throws "left its host slice unwritten"; with only
      the check after the copy removed, the guard fact's first assertion fails.

      The three points where the construction differs from the design's sketch (a
      download thread of its own, the unpinned sibling array below the host array, the
      first download before the attempts) →
      HISTORY.md#chunk-transfer-construction-differences-2026-10-01
- [x] One launch of cases that all bracket stays within the launch budget (`## Purpose`, the same L2 row):
      `CudaTests.AFamilyOfCasesThatAllBracketStaysWithinTheLaunchBudget` green on the reference machine,
      the kernel time of one launch of 16 384 (or one wave, if smaller) hp and sp cases of `gasless-ko2`
      below a quarter of `LaunchBudget.DefaultRunTimeLimit`, the figure in the test output. Without CUDA the
      check (`RecoveryFamilies.LaunchViolation`) is proven on the CPU accelerator, 2026-10-04:
      `BracketedFamiliesTests.TheLaunchBudgetCheckRefusesALaunchOverTheLimitAndACaseThatDidNotBracket`
      (a limit of zero and a case that ended `Ok` are refused).
      On CUDA, 2026-10-04 (11fe71f6, Release): 16 384 bracketing cases in one launch, kernel 361.3 ms against the 500 ms budget, 151 Newton steps per case on average.
- [x] 2026-10-05 — The throughput tripwire reads the median kernel time (coder 15 of 0.2.2; the ⚠ 2026-10-05 of `BOOT.md`):
      `SweepRun` takes `cuda_kernel_seconds` and the per-iteration figure from the median of the three timed runs' kernel times
      (`CudaTiming`), proven without a GPU by `ThroughputRecordTests.TheKernelTimeOfTheFigureIsTheMedianOfTheTimedRunsInAnyOrder`
      (3 cases; red once with the median replaced by the first run: expected 0.349 s, actual 0.376 and 0.277 s).
      - Five quiet Release runs on Windows (`TheSweepOf100000` and `ThroughputIsRecorded`), per step in 1e-07 s, first timed run
        under the old rule against the median under the new: 1.237 / 1.236, 1.371 / 1.237, 1.359 / 1.237, 1.235 / 1.236,
        1.238 / 1.238 (old spread 11.0 %, new 0.2 %); ratios 18.54, 18.21, 18.35, 18.52, 18.56.
      - Five quiet Release runs in WSL2 (Ubuntu-24.04, a tree of this commit under `~`): 1.235 / 1.235, 1.234 / 1.234, 1.438 / 1.235,
        1.271 / 1.237, 1.233 / 1.233 (old spread 16.6 %, new 0.3 %); ratios 26.50, 25.74, 25.92, 25.77, 26.51.
      - `Throughput.approved.txt` re-approved at the median of the five Windows runs: ratio 18.52 (CUDA 0.355 s, CPU 6.570 s, the
        run of the median ratio), kernel 0.277 s, 1.237e-07 s per step. `Throughput.linux.approved.txt` at the run of the median
        ratio, which is also the median per step: ratio 25.92 (CUDA 0.369 s, CPU 9.570 s), kernel 0.277 s, 1.235e-07 s per step.
        The earlier records (1.426e-07 and 1.364e-07) were measured with the first-run rule on the tree before the own math.

- [x] 2026-10-04 — The throughput tripwire per Newton step (coder 7 of 0.2.2; the ⚠ 2026-10-04 of `BOOT.md`):
      `CudaTests.ThroughputIsRecordedAndNotBelowTheApprovedRatio` writes `iterations_per_case` (22.403) and
      `cuda_kernel_seconds_per_iteration` and fails above 115 % of the approved figure (`ThroughputRecord`); the rule and the
      failure of a record without the figure are proven without a GPU by `ThroughputRecordTests` (7 facts).
      - Shown red once, on the Windows reference machine, Release: with `EquilibriumSolver.cs` and `StationSolve.cs` at d38e3f89
        (the loop-live `IterationState` passed by `ref` to `TraceGasPass.Run`) the fact failed with "the CUDA kernel time per
        Newton step 1.023e-07 s is 130.3 % of the approved 7.849e-08 s", the ratio check alone passing at 17.65 (floor 17.03);
        with the fix (51c5515c, 1cee836d) green.
      - `Throughput.approved.txt` re-approved from two quiet Release runs of 2026-10-04: kernel 0.175 and 0.176 s, 7.820e-08 and
        7.849e-08 s per step, ratio 20.71 and 21.29 (25 % background CPU load); a third run inside the solution-wide Release run
        measured 7.834e-08 (kernel 0.176 s). The record of 2026-09-19 (ratio 23.58) does not reproduce, even at v0.1.0
        → HISTORY.md#throughput-per-iteration-2026-10-04
      - Windows, Release, `dotnet test APThermo.sln -c Release --filter "Category=Cuda|Category=BitSnapshot"`: green in every
        project, `Execution.Tests` 84 of 84 (the bits unchanged: no `Bits*.approved.txt` moved, `ByteVectorTests` included).