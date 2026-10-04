# ACCEPTANCE.md — tests/Execution.Tests

The node's acceptance criteria (AGENTS.md §6, §15), moved here from `BOOT.md` on
2026-10-03 when the balance-remnant correction would have taken the leaf over its limit.

- [x] 2026-09-12 — L0 and L1 green: `AcceleratorChoiceTests` (the CPU engine's
      description, paths nowhere, discovery paths, the variable forbids CUDA and
      `Auto` falls back, no driver loaded when forbidden, the ILGPU assertion,
      wrapper names, inconsistent batches refused, chunk bounds, result layouts),
      `ProbeKernelTests` (`TheCpuAcceleratorReproducesDotnetMathExactly`,
      `CudaMatchesTheCpuAcceleratorWithinTheUlpBoundForEveryFunction`).
- [x] 2026-09-12 — L2 green: `BatchTests` (`ARocketFamilyEqualsTheHostSolverBitForBit`
      over every family, `ChunkingAndRepetitionDoNotChangeABit`,
      `TheTransportPassEqualsTheHostEvaluationBitForBit`,
      `AnEquilibriumFamilyEqualsTheHostSolverBitForBit`); `CudaTests`
      (`ARocketFamilyOnCudaMatchesTheCpuAccelerator` over every family with
      the transport pass, `AnEquilibriumFamilyOnCudaMatchesTheCpuAccelerator`,
      `TheSweepOf100000CasesOnCudaMatchesTheCpuAcceleratorAndIsDeterministic`);
      `SpeciesFunctionTests` (`TheCpuAcceleratorEqualsTheHostFunctionsBitForBit`,
      `CudaMatchesTheCpuAcceleratorWithinTheTable`,
      `ASpeciesIndexOutsideTheTableIsRefusedBeforeAnyKernelRuns`).
- [x] 2026-09-12 — Benchmark approved file present with the measured figures and the
      date of the measurement on the reference machine (`Throughput.approved.txt`:
      RTX 5070 Ti, 100 000 cases, 4 stations, 11 species, CUDA 0.170 s, CPU
      accelerator 9.544 s with 16 threads, 56.28×).
- [x] 2026-09-12 — Every check proven non-degenerate once, each mutation applied
      alone and seen red: the first wrapper fragment dropped from the NVVM module
      (the probe test, with the wrapper's name in the error); `ExpectedIlgpuVersion`
      set to 1.5.2.0 (every test of the node); the temperature tolerance set to zero
      and the transport tolerance set to zero (the CUDA family test); the rocket
      kernel's chamber pressure perturbed by 1e-12 (the host bit-equality test); the
      chunk no longer bounded by the scratch memory (the chunk-bounds test); the share
      of different step counts set to zero, the second mole-fraction tier set back to
      1e-10, and the determinism check pointed at the CPU result (each: the sweep
      test).
- [x] 2026-09-14 — The three facts of 2026-09-14 (the level table's second L0 row):
      the `Auto` fallback with discovery off and the explicit paths nowhere yields
      the CPU engine and `CudaSkippedBecause` naming what was missing and the paths
      tried (`AcceleratorChoiceTests.AnAutoFallbackSaysWhyCudaWasSkippedAndWhichPathsWereTried`,
      the mirror of `AnExplicitCudaRequestWithPathsNowhereNamesEveryPathTried`),
      red against the code of `8e36a27` (where `AcceleratorInfo` said nothing) before
      `c10ab0e`; `ScratchBytes` of zero or less refused at `Create` naming the option
      (`ChunksAreBoundedByTheChunkSizeAndTheScratchMemory`); the post-link's
      missing-definition check names the wrapper when handed a wrapper body with one
      definition removed, without a GPU (`PostLinkTests.AWrapperBodyWithOneDefinitionRemovedNamesThatWrapper`
      and its two siblings) — the first non-degeneracy proof of the guard behind the
      node's third invariant.
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

- [x] 2026-09-15 — The GPU/CPU comparison logic of `CudaTests.cs` moved to
      `GpuCpuComparison.cs`, one stateful type built from the tolerance table
      (`MoleSample` gone): `ShapeTests.NoTypeSpansMoreThan400Lines` and
      `NoMethodSpansMoreThan60Lines` hold; the CUDA facts green on the reference machine
      → HISTORY.md#criterion-gpu-cpu-comparison-extracted-2026-09-15
- [x] 2026-09-17 — `Throughput.linux.approved.txt` recorded from a green run under WSL2
      on the reference machine (52.01×: CUDA 0.237 s, CPU accelerator 12.350 s;
      re-measured in Release on 2026-09-19 below), the 100 000-case sweep fact green in
      the same run → HISTORY.md#criterion-linux-throughput-file-2026-09-17
- [x] 2026-09-17 — Shown red once, on Windows: `Throughput.approved.txt`'s `ratio`
      line mutated from `56.28` to `999.00`, then `dotnet test tests/Execution.Tests
      --filter "FullyQualifiedName~ThroughputIsRecordedAndNotBelowTheApprovedRatio"`
      failed — "CUDA/CPU ratio 66.03 fell below 80 % of the approved 999.00
      (Throughput.approved.txt)" — naming the platform's own file, as
      `ApprovedPathFor` picks it. Reverted with `git checkout --
      tests/Execution.Tests/Throughput.approved.txt`; `dotnet test
      tests/Execution.Tests` confirmed 55/55 green again.
- [x] 2026-09-17 — `DiscoveryReportsTheToolkitPathsItExamined` no longer assumes that
      every machine offers the locator a candidate root (CI run 35258686217 failed it on
      `windows-latest`, where nothing was examined): every path tried has the platform's
      library file name or the `.bc` suffix, and the list is non-empty only where a
      candidate root exists;
      `LibDeviceDiscoveryTests.WindowsWithNoCudaPathAndNoToolkitBaseDirectoryExaminesNothing`
      pins the empty case; shown red once by a bogus entry seeded into `tried`; verified
      at `a0d0ebf`, 56 of 56 → HISTORY.md#criterion-discovery-tried-empty-2026-09-17
- [x] 2026-09-19 — The throughput tripwire compares within one build configuration
      (the ⚠ above): `BuildConfiguration.Current`, the `configuration:` line, and the
      refusal to compare across configurations. Both approved files re-measured on the
      reference machine, GPU idle confirmed before every timed run
      (`nvidia-smi --query-compute-apps` empty), with the release job's own filter
      (`dotnet test APThermo.sln -c Release --filter "Category=Cuda|Category=BitSnapshot"`):
      - Windows: three timed runs 23.58×, 22.13×, 24.54× (a 10 % spread); the median
        approved (`Throughput.approved.txt`: CUDA 0.151 s, CPU 3.557 s, 23.58×, CUDA
        kernel 0.132 s, both well above the root's 5× floor). The same filter green
        afterward, 330/330 across the touched dlls (`Thermo.Tests` 214,
        `Transport.Tests` 1, `Equilibrium.Tests` 1, `Performance.Tests` 99,
        `Problems.Tests` 1, `Execution.Tests` 13, `Cli.Tests` 1).
      - Linux: WSL2 Ubuntu-24.04 on the reference machine, user `student`, a fresh
        `git clone` of the branch made through `/mnt/c` (cloning the worktree directly
        fails: its `.git` file points at a Windows path) and deleted after the
        measurement. Three timed runs 26.93×, 28.35×, 27.48× (a 5 % spread); the median
        approved (`Throughput.linux.approved.txt`: CUDA 0.204 s, CPU 5.593 s, 27.48×,
        CUDA kernel 0.128 s). The same filter green afterward, 330/330 across the same
        dlls; the fast suite (`APTHERMO_NO_CUDA=1 dotnet test APThermo.sln --filter
        "Category!=LongRunning"`) 3101/3101, matching the Windows count of the same day.
      - The red-once record on both platforms and the lint and fast-suite runs of the
        re-measurement → HISTORY.md#throughput-rerecord-red-once-2026-09-19
- [x] 2026-09-26 — The post-link on every architecture (`Execution`'s criterion of the
      same date, which lists the facts and their red-once proofs): the two L0 and L1
      rows of that date green, the inventory facts on the hosted runners of both
      platforms, the architecture and bind-time facts on the reference machine in
      Release.

      The evidence on the reference machine (134 of 134 in Release, 3185 fast tests,
      `ShapeTests` 10 of 10) and which facts run where →
      HISTORY.md#architecture-evidence-2026-09-26

      The hosted runners of both platforms, recorded 2026-09-27: CI run 36324216630 of
      `b6a8c3a` is green on `ubuntu-latest` and `windows-latest`. Its fast suite
      (`APTHERMO_NO_CUDA=1`, `Category!=BitSnapshot`) includes `WrapperInventoryTests`,
      which carry no category, and the bind-time facts, which verify the refusal there.

      ⚠ 2026-09-26 to 2026-09-27: was "Still due: the hosted runners of both platforms"
      with the tick standing for the reference machine only, now CI run 36324216630 of
      `b6a8c3a` green on both → HISTORY.md#hosted-runners-still-due-2026-09-27

      The red-once proofs of the inventory facts and of the architecture algorithm →
      HISTORY.md#architecture-red-once-2026-09-26

- [x] 2026-09-26 — The audit's F2, F3, F4 and observations (`Execution`'s and
      `Execution.Chunks`' own criteria of the same date list the design and the facts):
      `BadLibraryTests`, `AllCoresLayoutTests`, `AcceleratorChoiceTests.ChunksStayWithinInt32OffsetsAtTableLimits`,
      `AcceleratorChoiceTests.AHalfGivenExplicitLibraryPairIsRefused` and
      `PostLinkTests.ALogWithNulPaddingIsTrimmedOfIt` are new; the four pre-existing
      chunk-plan facts of `ChunksAreBoundedByTheChunkSizeAndTheScratchMemory` pass a
      fourth `ChunkPlan.For` argument, unchanged in what they assert.

      The evidence on the reference machine (140 of 140 in Release, 3191 fast tests) and
      where the red-once messages are recorded →
      HISTORY.md#audit-f2-f3-evidence-2026-09-26

- [x] 2026-09-27 — The probe's input domain (the execution node's `BOOT.md`, Constraints).
      - `TheCpuAcceleratorReproducesDotnetMathExactly` is green over the whole domain
        (8192 decade values plus the 17 special inputs, 12 functions each); shown red
        once by perturbing `Abs`'s expected value by `+ 1.0` — "Abs(1E-13): host
        1.9999999999999, cpu accelerator 0.9999999999999" — reverted, green again.
      - `Kernels.Probe` now calls the thermo node's `KernelMath.Min`/`Max` instead of
        `System.Math.Min`/`Max` (root `BOOT.md`, 2026-09-27), because the audit's
        suspicion below was confirmed: on the reference device (RTX 5070 Ti, driver
        13.4), every function of the list equals the CPU accelerator on every one of
        the 17 special inputs, `KernelMath.Min`/`Max` included, all at 0 ULP.
        The output of `TheSpecialInputsAreRecordedAgainstCuda` in full (0 ULP on every
        special input for every function but one 1-ULP `Pow(1.37)(0.5)`) and the audit's
        suspicion confirmed for `System.Math.Min` and `Max` →
        HISTORY.md#special-inputs-table-2026-09-27
      - No other record moved: the PTX fixtures (`Ptx/probe.sm_89.ptx`,
        `Ptx/probe.sm_120.ptx`) are unchanged, and no `Bits*.approved.txt` or
        `Throughput*.approved.txt` differs from `main`.

      Evidence: `dotnet test tests/Execution.Tests -c Release` (no filter), 144/144 on
      the reference machine — `TheSweepOf100000CasesOnCudaMatchesTheCpuAcceleratorAndIsDeterministic`,
      `EveryArchitectureFromSm75UpPassesThePostLinkAndMatchesTheDevice` and
      `ThroughputIsRecordedAndNotBelowTheApprovedRatio` included, none of which moved
      a record; `APTHERMO_NO_CUDA=1 dotnet test APThermo.sln --filter
      "Category!=LongRunning"`, 4547/4547, none skipped; the protocol lint 0 errors,
      0 warnings.

- [x] 2026-09-28 — The second audit's Execution findings F1 and F2 and the guards part's
      F7, F8, O2 and observations (`Execution`'s and `Execution.Chunks`' own criteria of
      the same date list the design and the red-once record):
      `LaunchBudgetTests` (new, 7 facts), `ChunkPlanWiringTests` (new, a theory over 3
      pipelines), `AcceleratorChoiceTests.BatchConstructorsRefuseACountWhoseArrayOverflowsA32BitLength`,
      `.AChunkBufferRefusesAHostArrayShorterThanTheChunkNeeds`,
      `.ProbeMathRefusesAnInputCountWhoseOutputOverflowsA32BitOffset`,
      `.TheCpuAcceleratorsBudgetIsUnboundedAndTheReferenceDevicesIsBounded`,
      `.AnAutoFallbackSaysWhyCudaWasSkippedAndWhichPathsWereTried` and
      `.AnExplicitCudaRequestWithPathsNowhereNamesEveryPathTried` (rewritten to call the
      new `AcceleratorChoice.Decide(options, cudaForbidden)` seam directly, F7),
      `.CudaForbiddenRefusesBeforeDiscoveryEverRuns` (new), `PostLinkTests.ALogThatTrimsToNothingLeavesNoTrailingColon`,
      `CudaWslDevicesTests.TheResolverAlreadySetFailureIsRecognisedByTargetSiteNotByMessage`,
      `SpeciesFunctionTests.TheComparisonIsNaNAwareAndCatchesAMismatchOnlyOneSideMakesNaN`
      (O2) are new; `ProbeKernelTests`, `MathProbe`'s `Functions` and the two PTX
      fixtures move for F1 (12 → 14 outputs, both `Min`/`Max` operand orders); `ArchitectureTests`
      gives each backend its own `NvvmAPI` (observation 4).

      A WSL race found on a full Release run (22 facts failing, fixed by joining
      `EngineFixture.CollectionName`) and the evidence runs (162 of 162 on Windows and
      under WSL2) → HISTORY.md#second-audit-race-and-evidence-2026-09-28
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
- [x] 2026-10-03 — The throat fixtures are a CUDA family too, under the condensed tier
      (`## Invariants`): the 18 cases of `tests/Fixtures/cases/throat`, batched as 7
      families, run on CUDA and on the CPU accelerator and compare under the table
      (`CudaTests.AThroatFamilyOnCudaMatchesTheCpuAccelerator`), and the CPU accelerator
      equals the host solver bit for bit on them (`BatchTests.AThroatFamilyEqualsTheHostSolverBitForBit`);
      the tier is `GpuCpuTolerances.Entries["condensedMoleFraction"]` with its derivation,
      used for species at or after `SpeciesTable.GasCount` only. Red once with it at 1e-11:
      `LiOH(L)` of `li2o-throat_pc3MPa_h2.20625MJkg` and `AL2O3(a)` of
      `ap-htpb-al-throat_pc7MPa_dh-2.375MJkg` fail, 2 of 7. Merged `5dfbc0d` through the
      guard with `--cuda`: fast suite 5503, CUDA proofs 378, this node in Release 187, all
      green, the sweep included; Linux fast suite in WSL 5503 green.

      ⚠ 2026-10-03: L2's "every fixture family" did not hold: the comparison read only
      `cases/rocket`, and the throat family (plateau edges, bisection) never ran on CUDA.
- [x] 2026-10-03 — The balance-remnant correction (`## Invariants`): the corrected
      comparison and the residual assertion in `GpuCpuComparison`, with the guard's bound
      measured and recorded; a fact that the corrected and uncorrected quantities agree
      where `κ` is small; shown red once with the correction removed (the
      `three-element-example1` family on CUDA) and once with a residual injected above
      1e-13 (CPU); every CUDA family green on the reference machine, the five 0.2.1
      equilibrium families included.
      Evidence: `0c55da9`, merged `377998d` with `--cuda`: CUDA proofs 394 green, the five
      0.2.1 families and the sweep included; the guard's bound 2e-2, between the worst smooth
      row (2.1e-3) and the smallest kink (0.12); `BalanceRemnantTests` (CPU, the injected
      residual of 1e-12 reported); red without the correction on CUDA: the merge-guard run
      of `de16866` (the families alone), `three-element-example1` x(H2) 3.8e-9 at equal
      steps; WSL fast suite green on `377998d`.
- [x] 2026-10-03 — The appear-or-vanish rule of the balance-remnant correction is proven
      non-degenerate (AGENTS.md §13: no fixture family triggered it, 0 of 23 cases): a fact over
      a case whose condensed set differs between `b(1 − h)`, `b` and `b(1 + h)`, which the
      rule leaves uncorrected, red once with the rule disabled.
      Evidence: `PhaseOnsetTests.ACaseAtAPhaseOnsetGetsNoCorrectionAndTheComparisonStillPasses`
      (`4e57a6e`, merged `55b0584`): the example12 tp case at 300 K bisected on its carbon
      to within 1e-9 of the onset of C(gr), the three solves `Ok` with different condensed
      sets; red with the rule disabled (no station left without a derivative). At this
      onset the disagreement bound drops the gas rows too, so the fact proves the rule's
      own output, not that the rule alone prevents a wrong correction.
- [x] The step-share bound holds over the whole run (`## Invariants`, the ⚠ of 2026-10-04):
      `CudaTests.TheStepShareOverTheWholeRun` green on the reference machine after every CUDA
      family, the sweep included (the figure: differing stations over stations of the run).
      Without CUDA, proven 2026-10-04 by `StepShareLedgerTests` (6 facts: the coarse guard of a
      family of one, the family over the guard, the run over the share, the empty run, the
      ordering of the last fact, concurrent additions) and the trx of the run, which lists
      `TheStepShareOverTheWholeRun` after every other `CudaTests` case. Red once, 2026-10-04:
      the floor of one removed from `StepShareLedger.Allowed` (a family of one fails) and the
      run's bound multiplied by 1000 (a run of 999 stations with one flip passes), 2 of 6 red.
      On CUDA, 2026-10-04 (fixes-0.2.2 at 11fe71f6, Release, the whole `CudaTests` class, the sweep included, 68 of 68): "35 of 400725 stations differ (share 8.73E-005, bound 1e-3)".
- [x] The 0.2.2 gasless and bracketed families on CUDA (`## Purpose`, the L2 row of 2026-10-04):
      `CudaTests.ABracketedFamilyOnCudaMatchesTheCpuAccelerator` green on the reference machine for each
      family of `RecoveryFamilies.Names` (`gasless-ko2`, `gasless-nao2`, `bracket-calcite-1e5`,
      `bracket-magnesite-1e5`, `bracket-ap-htpb-al-20mpa`): equal statuses, `Ok` fields within the table,
      `NoGasPhase` amounts within the condensed tier, pressure exact and every other state field zero,
      the worst `noGasPhaseTemperature` deviation recorded. Without CUDA, proven 2026-10-04 on the CPU:
      `BracketedFamiliesTests` (what each family holds, the comparison over a second run on moved element
      moles passes on every family, and each deliberate break of it is refused: a nonzero field, a
      pressure and a tp temperature one ULP off, an hp temperature at 1e-9, a gas mole, a condensed amount
      at 1e-8, a status, and for the `Ok` cases an enthalpy at 1e-6) and
      `BatchTests.ABracketedFamilyEqualsTheHostSolverBitForBit` (the CPU accelerator equals the host solver
      bit for bit, `NoGasPhase` cases included). Red once, 2026-10-04: the host call over element moles moved
      by 1e-9 (5 of 5 `BatchTests` red) and the expected status of `AFamilyHoldsTheCasesItStandsFor` swapped (5 of 5 red).
      First CUDA run (the merge-guard of `763382cc`): the gasless families green, the three bracketed ones red on the
      step-share guard alone, every field and fraction inside the tiers; measured and resolved 2026-10-04 by the rule
      of `## Invariants` (bracketed cases: totals of `Iterations` not counted, gas fractions at the first tier;
      `ABracketedComparisonCountsNoStepsAndHoldsTheFirstTier` on the CPU), after which the five families and the
      launch-budget fact are green on the reference machine in Release (6 of 6, `CudaTests` without the long set 32 of 32).
      On CUDA, 2026-10-04 (11fe71f6, Release, `CudaTests`): green for all five families; worst `noGasPhaseTemperature` 2.05e-14 (gasless-ko2) and 9.7e-16 (gasless-nao2), worst bracketed gas mole fraction 2.06e-12.
- [x] One launch of cases that all bracket stays within the launch budget (`## Purpose`, the same L2 row):
      `CudaTests.AFamilyOfCasesThatAllBracketStaysWithinTheLaunchBudget` green on the reference machine,
      the kernel time of one launch of 16 384 (or one wave, if smaller) hp and sp cases of `gasless-ko2`
      below a quarter of `LaunchBudget.DefaultRunTimeLimit`, the figure in the test output. Without CUDA the
      check (`RecoveryFamilies.LaunchViolation`) is proven on the CPU accelerator, 2026-10-04:
      `BracketedFamiliesTests.TheLaunchBudgetCheckRefusesALaunchOverTheLimitAndACaseThatDidNotBracket`
      (a limit of zero and a case that ended `Ok` are refused).
      On CUDA, 2026-10-04 (11fe71f6, Release): 16 384 bracketing cases in one launch, kernel 361.3 ms against the 500 ms budget, 151 Newton steps per case on average.
- [ ] Seeded families and every equilibrium fixture table (2026-10-04, 0.2.2; `src/Execution/ACCEPTANCE.md`, criterion of
      that date; `## Purpose`, the L2 row): the families of `SeededFamilies` (`seeded-fixtures`, the tp fixtures of every table
      at half pressure, the bracketed calcite and magnesite states seeded 20 K above the plateau) and the family of every
      equilibrium fixture table (`FixtureBatches.EquilibriumTableFamilyNames`) equal the host solver bit for bit on the CPU
      accelerator (`BatchTests.ASeededFamilyEqualsTheHostSolverBitForBit`, `AnEquilibriumTableFamilyEqualsTheHostSolverBitForBit`)
      and the CPU accelerator within the table on CUDA (`CudaTests.ASeededFamilyOnCudaMatchesTheCpuAccelerator`,
      `AnEquilibriumTableFamilyOnCudaMatchesTheCpuAccelerator`); the bracketed seeded states are compared by the rule of
      `## Invariants`, "Bracketed cases"; the mechanism facts of `ChunkTransferTests`, `AcceleratorChoiceTests`,
      `ChunkPlanWiringTests` and `BatchTests` are green.
      ⚠ 2026-10-04: was "the states left out (`## Invariants`) still exceed the closure bound", now none left out: the relative closure of the solver puts them back (the 17-element table, the hp states of magnesite at 1e4 and 1e6 Pa); `BatchTests.TheLeftOutStatesStillExceedTheClosureBound` is retired → HISTORY.md#left-out-states
      Red once, 2026-10-04, each applied alone and reverted (`APTHERMO_NO_CUDA=1`, Debug):
      - `Kernels.Equilibrium` passing `false` whatever the flag: 19 of 19 seeded families of
        `ASeededFamilyEqualsTheHostSolverBitForBit` red (iterations or bits against the host), the 41 cold rows green;
      - `EquilibriumPipeline` declaring the moles `ClearedOutput` for a seeded batch: the same 19 red, the 41 green;
      - the seed checks of `EquilibriumBatch.Validate` removed: `ASeededBatchWhoseSeedDoesNotFitTheTableIsRefusedBeforeAnyKernelRuns` red
        ("No exception was thrown");
      - `InputOutput`'s `BytesPerCase` answering 0: `ASeededEquilibriumBatchDeclaresTheSameDeviceBytesAsAColdOne` red (109 612 against
        102 412, the 7 200 bytes of 900 species);
      - `InputOutput` dropped from `UploadChunk`'s branch: both `ChunkTransferTests` facts and `ASeededBatchIsIndependentOfChunking` red;
        from `DownloadChunk`'s branch: both `ChunkTransferTests` facts red; the upload's start taken at offset 0: the round trip and
        `ASeededBatchIsIndependentOfChunking` red.
      Not shown red here: the CUDA side of a seeded family handed a copy of the batch without its seed (it needs the device).
      Evidence so far (the coder's part, no GPU; the box stays unticked until the orchestrator's CUDA and WSL runs are in):
      - Families: `seeded-fixtures` 30 cases; the tp fixtures of 13 tables at half pressure, 83 cases in all (the cases whose cold
        solve ends `Ok`); `seeded-bracket-calcite` at 1e4, 1e5 and 1e6 Pa, 6 states each (hp and sp at 0.1, 0.3 and 0.5), and
        `seeded-bracket-magnesite` at 1e4 and 1e6 Pa, 6 states each; 143
        seeded cases in 19 families. The equilibrium tables: 16 families, 151 cases, every one `Ok` on the CPU accelerator and equal to
        the host solver bit for bit (coder 6, `APTHERMO_NO_CUDA=1`, Debug, 2026-10-04).
      - Findings of the coder before (closed 2026-10-04, `HISTORY.md#left-out-states`): the hp states of magnesite at 1e4 and 1e6 Pa
        closed the element balance of the CPU accelerator only to 1.0e-11 to 1.5e-11 on carbon, and the 17-element table's one case
        chlorine only to 7.5e-13; with the relative closure of the solver both are in their families at the table's 1e-13.
      - The fast set of the solution, Debug, `APTHERMO_NO_CUDA=1`: all eleven test projects green, `tests/Execution.Tests` 315 of 315
        (25 s alone, 72 s inside the parallel run of the solution); no `Bits*.approved.txt`, `Throughput*.approved.txt`, Docs approved
        record or `PublicSurface.approved.txt` changed (`git status` clean after the run); `TreeContract.approved.txt` moved with
        `API.md`; the protocol lint gives 0 and 0.
      - Seeded bracketed families are compared with `IterationsSumAttempts` and so add nothing to the step-share ledger; the
        seeded fixtures, the warm families and the table families do.
- [x] The comparison rules of `## Invariants`, "What a difference between the accelerators is not" (2026-10-04, coder 5 of 0.2.2;
      the box stays unticked until the orchestrator's CUDA run on the merged tree): the full `CudaTests` class in Release on the
      reference machine, a short run, no throughput measurement: 67 of 67 green (the throughput fact excluded), the eight failures
      of the run of de7cda2f resolved without a tier, share bound or case moved: 2026-10-04, `CudaTests` (the `measured:` lines
      of the run name the worst species, κ and balance residual of each case a rule decided). Without CUDA the four rules are
      proven by the four facts of `ComparisonRuleTests`, each red once with its rule alone broken (enthalpy bound at zero,
      `DataEffect` null, `NoiseFactor` 0, the guard at its entry), 2026-10-04.
      On the merged tree, 2026-10-04 (11fe71f6, Release, the whole `CudaTests` class with the sweep and the throughput fact): 68 of 68 green; the `measured:` lines unchanged from the coder's run.
