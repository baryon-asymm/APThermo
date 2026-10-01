# BOOT.md — Execution.Tests

## Purpose

The definition of what "`Execution` is ready" means. This node owns the tolerance
table for CUDA against the CPU accelerator and the approved throughput figures.

| Level | What it checks | Against what (source of truth) | State |
|---|---|---|---|
| L0 | accelerator choice and the environment variable; libdevice discovery messages; ILGPU version and reflected members asserted; batch validation; chunk bounds; result layouts | documented behaviour; mutation of the assertion (`AcceleratorChoiceTests`) | ✅ |
| L0 | the reason of an `Auto` fallback is on the accelerator description (`CudaSkippedBecause`), naming what was missing and the paths tried; a scratch bound of zero or less is refused at `Create`; the post-link's missing-definition guard names the wrapper whose definition is absent, driven without a GPU through a wrapper body with one definition removed (`PostLinkTests`) | the `API.md` of `Execution` (2026-09-14) | ✅ (2026-09-14) |
| L1 | the probe kernel with every function of the root's math list loads through the post-link on CUDA and matches the CPU accelerator; the CPU accelerator reproduces `System.Math` bit for bit | the CPU accelerator and `System.Math`, the GPU/CPU tolerance table (`ProbeKernelTests`) | ✅ |
| L0 | the post-link's wrapper inventory over ILGPU 1.5.3's own PTX of the probe kernel, one fixture with the wrappers defined (SM_89) and one without (SM_120): the called set from `call` sites only, the defined set from `.func` headers, the missing set, with LF and CRLF line ends (2026-09-26) | the text fixtures `Ptx/probe.sm_89.ptx` and `Ptx/probe.sm_120.ptx`, whose provenance is under Constraints | ✅ (2026-09-26) |
| L1 | every architecture ILGPU 1.5.3 declares from SM_75 up: every entry point compiled for it passes the post-link and loads on the reference device, both paths of the post-link occur, the PTX equals the device's own up to ILGPU's generated names and the `.target` line, and the probe returns the device's own bits; an engine binds CUDA only after the probe kernel loads, and a post-link failure at bind is the `Auto` fallback's reason or the explicit request's exception (2026-09-26) | the engine's own CUDA kernels and probe, the CPU accelerator, the GPU/CPU tolerance table | ✅ (2026-09-26) |
| L0 | the library is checked before the device: a bad libnvvm names both paths and never leaks device memory (`BadLibraryTests`); the CPU accelerator is sized for `Environment.ProcessorCount`, proven at 4, 16 and 64 in child processes, with identical batch results (`AllCoresLayoutTests`); a chunk stays within 32-bit offsets at the tree's own size limits (`AcceleratorChoiceTests.ChunksStayWithinInt32OffsetsAtTableLimits`); a NUL-padded log is trimmed of it (`PostLinkTests`); a half-given library path pair is refused (`AcceleratorChoiceTests.AHalfGivenExplicitLibraryPairIsRefused`) (2026-09-26) | `Execution`'s `BOOT.md` and `API.md`, the audit's F2, F3 and F4 | ✅ (2026-09-26) |
| L0 | the rocket kernel's compile is bounded and released (2026-09-30): the first rocket run of a fresh CPU engine allocates under 2 GiB on the calling thread, a disposed engine holds no launcher and no compiled program, and the 32-bit bounds of the batch constructors and of the probe are checked on `BatchLength.Of` and `MathProbe.OutputLength` without allocating (`RocketCompileTests`, `AcceleratorChoiceTests.TheBatchLengthBoundIsInclusiveOfTheLargestArrayLength`, `TheProbeOutputLengthBoundIsInclusiveOfTheLargestOffset`) | the measured figures in `Execution`'s `BOOT.md` (criterion of 2026-09-30), the root's Compile size constraint | ✅ (2026-09-30) |
| L2 | every fixture family and a 100 000-case sweep on CUDA equal the CPU accelerator; the CPU accelerator equals the numerical nodes called case by case; determinism of two runs; chunking gives the same result as one chunk; the species-function batch against the host functions and across accelerators | the CPU accelerator and the host calls; reflection-enumerated fields (`BatchTests`, `CudaTests`, `SpeciesFunctionTests`) | ✅ |
| Benchmark | throughput of the 100 000-case batch on CUDA against the CPU accelerator with all cores | the approved figures file for the running platform (`Throughput.approved.txt`, `Throughput.linux.approved.txt` on Linux, 2026-09-17), asymmetry: may improve, must not regress below 80 % of the approved ratio or below the root's 5× (`CudaTests.ThroughputIsRecordedAndNotBelowTheApprovedRatio`) | ✅ |
| Protocol | the tree invariant, documents against code | `AGENTS.md`, the surface snapshot | ✅ (2026-09-13, the Protocol.Tests node) |

## Invariants

- **The GPU/CPU tolerance table lives here, in one file** (`GpuCpuTolerances.cs`),
  and every entry carries its derivation: relative 1e-10 on temperature; relative
  1e-10 on mole fractions not below 1e-8 at stations where both accelerators stopped
  after the same number of Newton steps, and 1e-9 where they did not; relative 1e-9
  on the other state fields, the performance figures and the transport figures; 4 ULP
  on the probe kernel's math functions (3 measured on the reference machine); at most
  one station in a thousand may stop after different numbers of Newton steps; 1e-10
  on the species functions, taken on the larger of 1 and the value, because H°/RT of a
  reference element at 298.15 K cancels to zero by construction and the condensed fits
  cancel by up to five decades (1.7e-11 measured on liquid water's Cp/R; the entry was
  first written as 1e-12 relative and calibrated on that measurement the same day).

  ⚠ 2026-09-12: was one tier, 1e-10 on every mole fraction above the floor, now 1e-9
  where the two accelerators took different numbers of Newton steps (derived from the
  polish threshold) and a bound on the share of such stations →
  HISTORY.md#tolerance-two-tiers-2026-09-12

  ⚠ 2026-09-14: was the whole table "in one file", now the mole-fraction floor and the
  polish-threshold tier live in `tests/Fixtures/tolerances.json` and this node keeps the
  GPU-specific entries → HISTORY.md#tolerance-table-not-in-one-file-2026-09-14
- **Bit comparison goes through the harness** (2026-09-14): `BitEquality.cs`'s
  `SameBits` and `BitDifferences<T>` were, field for field, the harness's `Bits.Same`
  and `Bits.Differences<T>`; the file is gone and every call site of this node reads
  `APThermo.Harness.Bits` instead, so the acceptance
  criterion below that names `BitEquality.cs` as one of the F-TF-06 split's four files
  now names a file this node no longer has.
- **CUDA tests are marked** `Category=Cuda` and the sweep and the benchmark also
  `Category=LongRunning`; when `APTHERMO_NO_CUDA=1` is set a CUDA-category test
  verifies the refusal of an explicit CUDA request and returns, so the full suite
  passes in that process; on a machine without CUDA and without the variable the
  CUDA tests fail with the accelerator message, they do not skip.
- **The approved throughput file is a tripwire**: a run writes `Throughput.actual.txt`
  next to it; the test fails when the ratio falls below the approved one by more than
  20 % or below 5×.

  ⚠ 2026-09-17: was one approved throughput file, now one per platform:
  `ApprovedPathFor` picks `Throughput.approved.txt` or `Throughput.linux.approved.txt`,
  the actual file is written beside the one read, and the root's 5× floor applies to
  both → HISTORY.md#throughput-file-per-platform-2026-09-17

  ⚠ 2026-09-19: was the approved files Debug measurements compared against Release runs,
  now a `configuration:` line in each file (`BuildConfiguration.Current`) and a refusal
  to compare across configurations; each side is timed as the median of three runs after
  the warm-up and the CUDA warm-up repeated five times; the 80 % and 5× floors are
  unchanged → HISTORY.md#throughput-build-configuration-2026-09-19
- **No expected value is typed into a test**: the CPU accelerator is compared with
  the numerical nodes called directly over the same buffers, CUDA with the CPU
  accelerator, and the reference temperature of the equilibrium family comes from the
  fixture.

## Dependencies

- [Execution](../../src/Execution/API.md) — what is being checked.
- [Execution.Chunks](../../src/Execution/Chunks/API.md) — `Chunk` and `ChunkPlan`, in
  the chunk-plan unit facts (a child node of `Execution`, 2026-09-15).
- [Execution.LibDevice](../../src/Execution/LibDevice/API.md) — `LibDeviceLocator`,
  `LibDevicePostLink` and `CudaWslDevices`, in the discovery, post-link and WSL facts (a
  child node of `Execution`, 2026-10-01).
- [Thermo](../../src/Thermo/API.md) — tables, `MixtureState`, `CaseStatus`.
- [Equilibrium](../../src/Equilibrium/API.md) — the solver and scratch layout called case by case on the host.
- [Performance](../../src/Performance/API.md) — `PerformanceFigures`, the rocket solver called on the host, flow models.
- [Transport](../../src/Transport/API.md) — the transport table and evaluation called on the host, `TransportFigures`.
- [Data](../../src/Data/API.md) — the database.
- [Fixtures](../Fixtures/API.md) — the reference propellant inputs used to build the
  batches, and the mole-fraction floor and polish-threshold tier of the tolerance table.
- [Harness](../Harness/API.md) — bit comparison (`Bits.Same`, `Bits.Differences`) and
  the per-platform approved path (`ApprovedSnapshot.ApprovedPathFor`, 2026-09-17).

Outside the tree: xunit; ILGPU 1.5.3; an NVIDIA GPU with driver, libnvvm and
libdevice for the CUDA category.

## Constraints

- The CPU-only part of the node runs in the default test command; the CUDA category
  runs in the full set on the reference machine; the long-running category (the
  sweep and the benchmark, about 20 s, and since 2026-09-26 the architecture fact) is
  excluded from the fast set.

  ⚠ 2026-09-28: was "about three minutes" for the architecture fact, now measured 8 m 2
  s on Windows and 11 m 3 s under WSL (a corrected duration, not a new bound) →
  HISTORY.md#long-running-duration-2026-09-28
- The PTX fixtures (2026-09-26) are ILGPU 1.5.3's PTX of `Kernels.Probe`, taken before
  the post-link from a `PTXBackend` for SM_89 and for SM_120 with libnvvm 13.4 on the
  reference machine. They are text, generated once and committed with a header comment
  naming ILGPU, libnvvm, the architecture and the date. They are inputs of the
  inventory, not expected values: the facts assert only what the root's math list and
  the regime imply (which wrappers are called, whether they are defined). They are
  regenerated when ILGPU is upgraded, which the version assertion already forces to be
  a deliberate act, or when `Kernels.Probe` itself changes shape.

  ⚠ 2026-09-28: was the PTX fixtures still showing the probe before 2026-09-27, now
  regenerated from the current `Kernels.Probe` (14 outputs) →
  HISTORY.md#ptx-fixtures-stale-2026-09-28
- Paths from the repository root; the actual throughput file is the only write, next
  to the approved one, and it is git-ignored.
- One engine per accelerator is shared by the collection; tests that need a fresh
  engine (warm-up timing, chunk size) create and dispose their own.

- **The probe covers the solver's whole input domain** (2026-09-27, the guards audit of
  2026-09-26, F11). Until now the inputs were 1e-13 to 1e13 plus a few positive values,
  and `Pow` took the exponent 1.37 only. The solver's `exp` arguments are mostly
  negative, `Floor` and `Ceiling` never saw a negative value, and `Min` and `Max` never
  saw NaN.
  - The inputs gain the negatives of the decade span, ±0, ±∞, NaN, the smallest
    subnormal and the largest subnormal, the smallest normal, and −0.5, −1.5 and −2.5.
    `Pow` is probed at the exponents 1.37, 1.4 and 4.6 (`MathProbe` in the execution
    node, which holds the list).
  - The comparison counts NaN on both sides as equal and compares ±0 and ±∞ exactly,
    with the sign.
  - The CPU accelerator must still reproduce `System.Math` bit for bit on every input.
  - The audit's suspicion about `Math.Max(NaN, x)` and the instruction of 2026-09-27 to
    record what CUDA returns and to stop on a divergence →
    HISTORY.md#probe-domain-audit-suspicion-2026-09-27
  - Measured 2026-09-27 on the reference device, by the coder of that day: every
    function of the list equals the CPU accelerator on every input, except `Math.Min`
    and `Math.Max` with a NaN operand (CPU NaN, CUDA the other operand). The owner
    decided the root's `KernelMath`. The probe therefore runs `KernelMath.Min` and
    `KernelMath.Max` in place of `Math.Min` and `Math.Max`, and they must equal the CPU
    on every input, NaN included.

## Acceptance criteria

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

## Taboos

- Do not loosen the GPU/CPU tolerance for green: a divergence is a finding about
  math functions or code generation and gets a design session. The second tier of
  the mole-fraction entry is not such a loosening: it is derived from the solver's
  stopping rule and guarded by the bound on the share of stations it applies to.
- Do not mark a CUDA test skipped on a machine without CUDA: absence is a failure
  unless CUDA is forbidden explicitly.
- Do not commit the actual throughput file.
