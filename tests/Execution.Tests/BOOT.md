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
| L0 | the rocket kernel's compile is bounded and released (2026-09-30): the first rocket run of a fresh CPU engine allocates under 2 GiB on the calling thread, `StationSolve.At` is not inlined, a disposed engine holds no launcher and no compiled program, and the 32-bit bounds of the batch constructors and of the probe are checked on `BatchLength.Of` and `MathProbe.OutputLength` without allocating (`RocketCompileTests`, `AcceleratorChoiceTests.TheBatchLengthBoundIsInclusiveOfTheLargestArrayLength`, `TheProbeOutputLengthBoundIsInclusiveOfTheLargestOffset`) | the measured figures in `Execution`'s `BOOT.md` (criterion of 2026-09-30), the root's Compile size constraint | ✅ (2026-09-30) |
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

  ⚠ 2026-09-12: the sketch had one tier, 1e-10 on every mole fraction above the
  floor. The 100 000-case sweep showed 12 mole fractions at 2 of its 400 000
  stations beyond it, by up to 3.3e-10, both at stations where CUDA had taken 3
  Newton steps and the CPU accelerator 2 or the reverse. The equilibrium solver
  polishes until its corrections are below 1e-11, the rounding floor of its linear
  solves; a last-ULP difference between libdevice and .NET flips that threshold at
  91 stations of the sweep, and the accelerator that takes one polish step more
  moves by up to 1.4e-11 in temperature and, through `(H_j/RT) Δln T + Σ a_ij Δπ_i`,
  by a few 1e-10 in the mole fraction of a minor species. Where the step counts
  agree the worst deviations are 3.4e-13 on temperature, 9e-12 on a mole fraction
  and 2e-12 on any other field. The second tier is derived from the polish
  threshold, not from the measurement; the share of such stations is bounded so
  that a systematic divergence (a single-precision or CORDIC function would flip
  the count everywhere) cannot hide behind the second tier. The root's invariant
  carries the same note.

  ⚠ 2026-09-14: "in one file" was not true: the front door tests node copied the
  mole-fraction floor (1e-8) and the polish-threshold tier (1e-9) for its reordered
  union batches, and the protocol forbids it to read this node's code. Found by the
  clean-code review (F-TF-05). Resolved the same day: the two entries moved to the
  fixtures node's tolerance table, which both nodes already depend on
  (`tests/Fixtures/tolerances.json`, `moleFractionFloor` and
  `polishThresholdRelative`, with their derivations); `GpuCpuTolerances.MoleFractionFloor`
  and `MoleFractionRelative` now take that table and read the two entries from it, and
  this node's own table keeps only the GPU-specific entries (temperature, moleFraction,
  state, figures, transport, functions) that have no place in a table of comparisons
  with the reference.
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

  ⚠ 2026-09-17: this bullet assumed one approved file. The root's platform constraint
  keeps a Windows and a Linux record for the bit snapshots (`Harness`'s `BOOT.md`), and
  the same reasoning applies here: a benchmark run under WSL2 times a CPU accelerator
  and a CUDA path that both include the hypervisor's virtualization overhead, so its
  ratio is not comparable to a native Windows run. `ApprovedPathFor` picks
  `Throughput.approved.txt` or `Throughput.linux.approved.txt` for the running
  platform, the same one place `Bits.approved.txt`'s per-node counterpart uses; the
  actual file is written beside whichever one is read, so `Throughput.linux.actual.txt`
  on Linux. The root's 5× floor is not a per-platform figure and applies to both.

  ⚠ 2026-09-19: the approved files were Debug measurements (`dotnet test` without
  `-c`) compared against a Release run, the configuration the release workflow and the
  benchmarks node use. The release rehearsal (35439111934) failed the fact at 29.48×
  against 80 % of the approved 56.28×, with no code regression: every other Cuda and
  BitSnapshot fact was green (Fable 5.1's analysis of the run's tables). The mechanism:
  the CPU accelerator executes the batch's kernels from the assemblies' IL — ILGPU's
  CPU accelerator, not a native `System.Math` call path — so the host build
  configuration changes its speed by roughly 2.8× (Debug ≈9.5 s, Release ≈3.4–3.7 s on
  Windows for the 100 000-case sweep, the benchmarks node's 2026-09-15 figures); the
  CUDA kernel is compiled once through libnvvm regardless of the host configuration, so
  its own time (≈0.15–0.2 s) does not move. A Debug-vs-Release comparison therefore
  compares two different CPU speeds under one name. `BuildConfiguration.Current`
  (`#if DEBUG`) names the running configuration; the approved file now carries a
  `configuration:` line, and the fact refuses to compare across configurations, naming
  both in its message. Shown red once on each platform: the fact run in Debug against
  the Release-approved file failed — "this run is Debug, but Throughput.approved.txt
  was measured in Release; the CPU accelerator executes the kernels from the
  assemblies' IL, so its speed depends on the build configuration (BOOT.md); run in
  Release to compare against it" on Windows, the Linux run naming
  `Throughput.linux.approved.txt` the same way. Both files are re-approved from Release
  runs (the acceptance criterion below); the 80 % and the root's 5× floors are
  unchanged. `SweepRun` also times each side as the median of three timed runs after
  the warm-up instead of one, and repeats the CUDA warm-up five times (the GPU leaves
  its idle P-state over several launches, not one), so a single slow or fast sample
  does not move the tripwire; the CUDA determinism check still gets two independent
  runs.
- **No expected value is typed into a test**: the CPU accelerator is compared with
  the numerical nodes called directly over the same buffers, CUDA with the CPU
  accelerator, and the reference temperature of the equilibrium family comes from the
  fixture.

## Dependencies

- [Execution](../../src/Execution/API.md) — what is being checked.
- [Execution.Chunks](../../src/Execution/Chunks/API.md) — `Chunk` and `ChunkPlan`, in
  the chunk-plan unit facts (a child node of `Execution`, 2026-09-15).
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

  ⚠ 2026-09-28: stood "about three minutes, most of it the driver compiling the rocket
  kernel once per architecture". The second audit's warm-up measurement (finding, "The
  first audit's fixes", observation 3) found the driver's compute-cache JIT for a fresh
  engine takes 55–59 s on a cache miss and the rocket kernel itself 13–22 s to compile,
  and the architecture fact creates several engines and compiles every entry point for
  eleven architectures: the reference machine measured 8 m 2 s on Windows and 11 m 3 s
  under WSL, not about three minutes. The floor the fact needs stays unmeasured; this
  is a corrected duration, not a new bound.
- The PTX fixtures (2026-09-26) are ILGPU 1.5.3's PTX of `Kernels.Probe`, taken before
  the post-link from a `PTXBackend` for SM_89 and for SM_120 with libnvvm 13.4 on the
  reference machine. They are text, generated once and committed with a header comment
  naming ILGPU, libnvvm, the architecture and the date. They are inputs of the
  inventory, not expected values: the facts assert only what the root's math list and
  the regime imply (which wrappers are called, whether they are defined). They are
  regenerated when ILGPU is upgraded, which the version assertion already forces to be
  a deliberate act, or when `Kernels.Probe` itself changes shape.

  ⚠ 2026-09-28: the second audit found the fixtures (`Ptx/probe.sm_120.ptx`) still
  showed the pre-2026-09-27 probe (stepping by 10, three fewer outputs, no `Pow`
  exponent variety, no `KernelMath` calls), while the inventory facts kept passing:
  they assert only the wrapper-name relationship the math list and the regime imply,
  never the literal output count, so a stale fixture is not caught by the tests it
  feeds. The two files are regenerated on the reference machine from the current
  `Kernels.Probe` (14 outputs, the F1 fix's two extra `KernelMath.Min`/`Max` orders
  included), same method (a `PTXBackend` per architecture, before the post-link),
  header dated 2026-09-28.
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
  - Suspected by the audit, not run: `.NET`'s `Math.Max(NaN, x)` is NaN, while PTX
    `max.f64` returns the other operand. The coder records what CUDA returns for every
    special input. If CUDA and the CPU accelerator differ on any, the coder stops and
    reports the list; the decision on it is the owner's, since it bears on the root's
    GPU-equals-CPU invariant. No tolerance is widened, and no input is dropped to pass.
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
      `BatchBuilders` is gone, replaced by `FixtureBatches.cs` (`RocketInputs`,
      `RocketFamily`, fixtures to families and batches), `HostSolves.cs` (one case
      through the numerical nodes over the accelerator's own buffers, returning the
      named record structs `HostRocketCase`, `HostEquilibriumCase`,
      `HostTransportStation` instead of tuples), `BitEquality.cs` (`SameBits`,
      `BitDifferences<T>`) and `SweepRun.cs` (the long-running sweep, not named by
      F-TF-06 but sharing none of the three axes above); no method over 60 lines or
      nested deeper than 3, covered by the protocol tests node's `ShapeTests`, all
      ten facts green at `62cd99e`; every L2 fact green bit for bit after the split
      (`APThermo.Execution.Tests.dll`: 41 passed) and the
      node's mutations re-run alone and seen red where the touched code moved: the
      rocket kernel's chamber pressure perturbed by a relative `1e-12`
      (`batch.ChamberPressures[index] * (1.0 + 1e-12)` in `Kernels.Rocket`) reddened
      `ARocketFamilyEqualsTheHostSolverBitForBit` for every family, and the
      chunk bound with its memory clamp removed from `ChunkPlan.For` reddened
      `ChunksAreBoundedByTheChunkSizeAndTheScratchMemory`; both reverted
      and the suite green again before committing. The hand-typed fact counts left
      the criteria above; the listed names are the list.

      ⚠ 2026-09-15: "nested deeper than 3" was not measured: `inventory.py` counts
      lines only, and `BatchTests.ARocketFamilyEqualsTheHostSolverBitForBit`
      and `SpeciesFunctionTests.CudaMatchesTheCpuAcceleratorWithinTheTable`
      nested 4 deep at this tick's commit (`7a3dedb`). Found by the repair review
      (R-Execution.Tests-4); both were brought to 3 on 2026-09-15 (the criterion
      below), where this document's earlier silence on the point is corrected.
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

      ⚠ 2026-09-15: `MoleSample` is no longer local to `CudaTests.cs`, and
      `CompareRocket` is gone: both moved to `GpuCpuComparison.cs` the same day, the
      criterion below.

      Two nesting-depth-4 violations found by the same review were fixed alongside:
      `BatchTests.ARocketFamilyEqualsTheHostSolverBitForBit`'s
      species-by-species mole loop, four levels deep inside the case loop, the
      station loop and its own species loop, moved to `StationMoleDifferences`
      (nesting 2 on its own); `SpeciesFunctionTests.CudaMatchesTheCpuAcceleratorWithinTheTable`'s
      three-function comparison, four levels deep inside the family loop, the entry
      loop and its own function loop, moved to `CompareFunctions` (nesting 2 on its
      own). Neither method nests deeper than 3 now.

      Verified: build clean, 0 warnings; 41 of 41 fast tests green
      (`APThermo.Execution.Tests.dll`); `protocol_lint`
      0 errors, 0 warnings; `Protocol.Tests` 9 of 9 green. This node keeps no
      `Bits.approved.txt` of its own (its bit comparisons run the host call inside
      the same test, not against a recorded snapshot), so there is no hash to
      compare before and after.

      ⚠ 2026-09-15: "the old field names stay as forwarding properties, so every read
      call site is unchanged" did not age well, for the same reason the equivalent
      shortcut in the Performance.Tests node did not: `ChemicalSystem` mixed a
      family's shared axis with one fixture's own. `RocketFamilies` groups fixtures by
      `BatchKey`, and `BatchKey` never read `ElementMoles` — only `Elements`,
      `Products` and the exit kinds — confirming `ElementMoles` was never part of what
      a family shares; it is one fixture's own starting composition, exactly like
      `ReactantEnthalpy`, which already sat outside `ChemicalSystem`. Found by the
      repair review (R-Execution.Tests-1). `ChemicalSystem` narrowed to `Elements`,
      `Products` (2 parameters); a new `Mixture` record holds `ElementMoles` and
      `ReactantEnthalpy` (2 parameters); `RocketInputs` keeps `System`, `Mixture`,
      `ChamberPressure`, `Flow`, `Exits`, `Transport`, still 6 parameters. The
      forwarding properties (`Elements`, `ElementMoles`, `Products`, `ExitValues`,
      `ExitKinds`) are gone; the four read call sites this document said were
      unchanged (`RocketFamily.Batch`, `RocketFamilies`, `Sweep`, all in
      `FixtureBatches.cs`) and the fifth this document did not mention
      (`AcceleratorChoiceTests.InconsistentBatchesAreRefusedBeforeAnyKernelRuns`)
      all name `.System.` or `.Mixture.` or `.Exits.` directly now. No behaviour
      changed: the same fields, on the same two records, under new names one level
      down.

      By the same `CouplingMeasures` run, `FixtureBatches` itself moved from Ce=14 to
      Ce=17 (`ChemicalSystem`, `Mixture` and `ExitPlan` newly named directly in
      `RocketFamilies` and `Sweep`, for the same reason as `RocketCase` in the
      Performance.Tests node); its test-fixture neighbours measure
      `AcceleratorChoiceTests` Ce=32, `BatchTests` Ce=31, `CudaTests` Ce=26,
      `HostSolves` Ce=28, `SpeciesFunctionTests` Ce=16, none touched by this cut. The
      root limits the efferent coupling of the `src` types only, so a test type's
      figure is recorded, not limited.

      Verified: build clean, 0 warnings; 41 of 41 fast tests green; `protocol_lint`
      0 errors, 0 warnings.

- [x] 2026-09-15 — The GPU/CPU comparison logic `CudaTests.cs` carried alongside its `[Fact]`/
      `[Theory]` methods — `MoleSample`, `CompareRocket`, `CompareMoles`, `Record`,
      `Worst` — moved to a new file, `GpuCpuComparison.cs`: one stateful type,
      `GpuCpuComparison`, built from the tolerance table once per test and holding the
      worst deviation per field and the different-step count as it accumulates them,
      with `Rocket(RocketBatchResult, RocketBatchResult, RocketFamily)` (was
      `CompareRocket`, 3 parameters), `Moles(double[], double[], long, SpeciesTable,
      bool, string)` (was `CompareMoles`, 6 parameters, its four `MoleSample` fields
      unpacked back to plain parameters — `MoleSample` had one caller-supplied field
      per call and was never kept, so it named no concept of its own), `Record` (the
      `GpuCpuTolerances.Compare` callback), a new `CountSteps(bool)` and `Worst()` as
      its methods. `MoleSample` is gone. The three CUDA test methods that owned a
      `worst` dictionary, and either a `differentSteps` local incremented inline
      (the equilibrium family test) or passed by `ref` into `CompareRocket` (the
      rocket family and sweep tests), now own one `GpuCpuComparison` instead, call
      `.CountSteps(sameSteps)` where they used to increment their own local, and read
      `.DifferentSteps` back: the equilibrium test's step count moved from a local
      variable to the same shared counter `Rocket` itself feeds, so all three tests
      now count steps the same way. No behaviour change: the same comparisons, the
      same tolerance calls, the same accumulation — `worst` shared across a test
      method's rocket-then-transport phases
      (`ARocketFamilyOnCudaMatchesTheCpuAccelerator`) is still one dictionary
      shared the same way, now the one instance's private field instead of a local
      passed to both phases. `ShapeTests.NoTypeSpansMoreThan400Lines` and
      `ShapeTests.NoMethodSpansMoreThan60Lines` both hold for `CudaTests` and
      `GpuCpuComparison`; `GpuCpuComparison`'s own Ce is 8, `CudaTests`' own Ce is 26,
      unchanged from before the cut, both recorded by `CouplingMeasures` and not
      limited, since the root's coupling rule holds for `src` types only.

      This is a mechanical port: `Rocket`/`Moles`/`CountSteps` cannot be exercised
      without a CUDA device, so the CPU-only fast suite (`APTHERMO_NO_CUDA=1`, which
      makes `RequireCuda()` return null and every CUDA-marked test return before
      reaching this code) proves only that it builds and that every other fact stays
      green; the actual arithmetic is unchanged from the moved code, read side by
      side at the move. Applies R-Execution.Tests-2 of the repair review.

      Verified on the reference machine, `APTHERMO_NO_CUDA` unset, one run at a
      time: `ProbeKernelTests.CudaMatchesTheCpuAcceleratorWithinTheUlpBoundForEveryFunction`
      green (R-Execution-2's own guard, exercising the merged `CompileWrappers`);
      `CudaTests.ARocketFamilyOnCudaMatchesTheCpuAccelerator` and
      `AnEquilibriumFamilyOnCudaMatchesTheCpuAccelerator` together, 9 of 9
      green (every rocket family plus the equilibrium family, through `Rocket`,
      `Moles`, `CountSteps` and `Record` on real hardware); then
      `TheSweepOf100000CasesOnCudaMatchesTheCpuAcceleratorAndIsDeterministic`
      alone, green (400 000 stations through `Rocket`, both accelerators agreeing
      within the tolerance table, CUDA deterministic across two runs).
      `ThroughputIsRecordedAndNotBelowTheApprovedRatio` was not run, as the
      decision records.
- [x] 2026-09-17 — `Throughput.linux.approved.txt` recorded from a green run under
      WSL2 on the reference machine (.NET SDK 10.0.112, this node's harness change on
      top of `df0368d`): RTX 5070 Ti, 100 000 cases, 4 stations, 11 species, CUDA
      0.237 s, CPU accelerator 12.350 s with 16 threads, 52.01×, comfortably above the
      root's 5× floor though below the Windows file's 56.28× (WSL2's virtualization
      overhead falls on both the CPU and the CUDA timings, per the tripwire
      invariant's ⚠ above). Before approving, the rest of the same `dotnet test
      tests/Execution.Tests` run was confirmed to need nothing else: 54/55, the one
      failure the expected "no approved throughput file" case, the 100 000-case
      correctness sweep (`TheSweepOf100000CasesOnCudaMatchesTheCpuAcceleratorAndIsDeterministic`)
      already green in it. With the file in place, the same command gave 55/55; the
      fast suite (`APTHERMO_NO_CUDA=1 dotnet test APThermo.sln --filter
      "Category!=LongRunning"`) stayed 3098/3098 and `protocol_lint` gave 0 errors,
      0 warnings, both unaffected by this node's own change.
- [x] 2026-09-17 — Shown red once, on Windows: `Throughput.approved.txt`'s `ratio`
      line mutated from `56.28` to `999.00`, then `dotnet test tests/Execution.Tests
      --filter "FullyQualifiedName~ThroughputIsRecordedAndNotBelowTheApprovedRatio"`
      failed — "CUDA/CPU ratio 66.03 fell below 80 % of the approved 999.00
      (Throughput.approved.txt)" — naming the platform's own file, as
      `ApprovedPathFor` picks it. Reverted with `git checkout --
      tests/Execution.Tests/Throughput.approved.txt`; `dotnet test
      tests/Execution.Tests` confirmed 55/55 green again.
- [x] 2026-09-17 — `DiscoveryReportsTheToolkitPathsItExamined` assumed every
      machine offers the locator at least one candidate root, so `Assert.NotEmpty(tried)`
      held unconditionally. The first CI run on the public repository (GitHub Actions
      run 35258686217) failed it on `windows-latest`: no CUDA toolkit, no `CUDA_PATH`,
      so `LibDeviceLocator.Locate` truly examined nothing and `tried` was empty; the
      `ubuntu-latest` job passed only because its discovery unconditionally names the
      fixed `<glob root>/cuda` candidate before checking whether it exists (root
      `BOOT.md`'s Linux `ToolkitRoots`), so `tried` is never empty there. An empty list
      is the honest answer on a bare Windows runner, not a defect of discovery, so the
      fact now asserts only what holds everywhere: every path ever tried has the
      platform's own library file name or the `.bc` suffix (`Assert.All`, unconditional,
      still fails on a wrong-shape path), and `Assert.NotEmpty` applies only when a
      candidate root exists — `CUDA_PATH` set, or (Windows) a versioned directory
      already under the default toolkit base; Linux always has the fixed candidate, so
      the new helper `ACandidateToolkitRootExists` returns `true` unconditionally there.
      Shown red once: `LibDeviceLocator.Locate`'s internal `tried` list was seeded with
      a bogus `"MUTATION-wrong-shape.txt"` entry before its early-return checks; `dotnet
      test tests/Execution.Tests --filter
      "FullyQualifiedName~DiscoveryReportsTheToolkitPathsItExamined"` failed —
      `Assert.All() Failure: 1 out of 4 items in the collection did not pass` naming the
      bogus entry — then the mutation was reverted and the file diffed byte-identical
      against the pre-mutation copy. Reproduced the CI condition on the reference
      machine (which has the toolkit, so the environment-driven fact itself cannot be
      driven empty) through the internal seam instead: `LibDeviceLocator.Locate(new
      EngineOptions(), LocatorPlatform.Windows, _ => null, @"C:\nonexistent-ci-toolkit-base")`
      returned `(null, null, [])`, matching the CI failure exactly; with `CUDA_PATH` and
      `CUDA_HOME` cleared but the real toolkit base left in place (`C:\Program
      Files\NVIDIA GPU Computing Toolkit\CUDA`, versions v12.9/v13.3/v13.4) the same
      overload still found `v13.4`'s dll and bitcode, `tried` non-empty, confirming
      discovery falls back to the directory scan when only the environment variable is
      missing. `LibDeviceDiscoveryTests.WindowsWithNoCudaPathAndNoToolkitBaseDirectoryExaminesNothing`
      pins the same empty-tried case deterministically, alongside the existing
      `AnUnsupportedPlatformDoesNoDiscovery`. Verified on the reference machine at
      `a0d0ebf` (which has `CUDA_PATH` set, so the environment fact's non-empty branch
      is exercised for real): `dotnet test tests/Execution.Tests` (CUDA included, the
      new fact among them) 56/56; `APTHERMO_NO_CUDA=1 dotnet test APThermo.sln --filter
      "Category!=LongRunning"` 3101/3101, none skipped; `protocol_lint` 0 errors, 0
      warnings; every `Bits*.approved.txt`, `Throughput*.approved.txt` and the
      protocol tests node's `PublicSurface.approved.txt` unchanged (`git status
      --short` names only the three files this fix touched).
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
      - Shown red once on both platforms: the fact run in Debug (`dotnet test
        tests/Execution.Tests --filter
        "FullyQualifiedName~ThroughputIsRecordedAndNotBelowTheApprovedRatio"`)
        against its platform's freshly re-approved, Release-measured file failed with
        the message quoted in the ⚠ above, naming `Throughput.approved.txt` on Windows
        and `Throughput.linux.approved.txt` on Linux.
      - `protocol_lint` 0 errors, 0 warnings on both platforms both before and after;
        the Windows fast suite (`APTHERMO_NO_CUDA=1 dotnet test APThermo.sln --filter
        "Category!=LongRunning"`) 3101/3101 throughout, none skipped; every
        `Bits*.approved.txt` and the protocol tests node's `PublicSurface.approved.txt`
        unchanged.
- [x] 2026-09-26 — The post-link on every architecture (`Execution`'s criterion of the
      same date, which lists the facts and their red-once proofs): the two L0 and L1
      rows of that date green, the inventory facts on the hosted runners of both
      platforms, the architecture and bind-time facts on the reference machine in
      Release.

      Evidence, on the reference machine (Windows, RTX 5070 Ti, driver 13.4, CUDA
      toolkits 12.9/13.3/13.4), from a tree with every `bin` and `obj` removed:
      - `dotnet build APThermo.sln`: 0 warnings, 0 errors;
      - `APTHERMO_NO_CUDA=1 dotnet test APThermo.sln --no-build --filter
        "Category!=LongRunning"`: 3185 of 3185, none skipped (3178 before this change
        plus `WrapperInventoryTests`' 5 facts and the two new
        `AcceleratorChoiceTests` bind-time facts; `ArchitectureTests`' one fact is
        `Category=LongRunning` and so excluded here);
      - `dotnet test tests/Execution.Tests -c Release` (no filter): 134 of 134, the
        100 000-case sweep and the throughput tripwire included;
      - no `Bits*.approved.txt`, `Throughput*.approved.txt` or the protocol tests
        node's `PublicSurface.approved.txt` differs from `8dfe20f`;
      - the protocol lint: 0 errors, 0 warnings;
      - the protocol tests node's `ShapeTests`: 10 of 10 (the extraction of
        `KernelCache.Load` to a static method and `AcceleratorChoice`'s new
        `ProbeBinding` moved no type past its coupling or size limit).

      `WrapperInventoryTests` is pure text and regex over the two committed fixtures,
      with no OS-conditional code and no native call. The architecture and bind-time
      facts are `Category=Cuda` and run only where a device exists, as every other CUDA
      fact of this node does.

      The hosted runners of both platforms, recorded 2026-09-27: CI run 36324216630 of
      `b6a8c3a` is green on `ubuntu-latest` and `windows-latest`. Its fast suite
      (`APTHERMO_NO_CUDA=1`, `Category!=BitSnapshot`) includes `WrapperInventoryTests`,
      which carry no category, and the bind-time facts, which verify the refusal there.

      ⚠ 2026-09-26 to 2026-09-27: until that run this paragraph read "Still due: the
      hosted runners of both platforms", with the tick standing for the reference
      machine only. The coder had ticked the whole criterion with the Linux half argued
      rather than run, and said so in its report; the orchestrator held it open at the
      merge until a run existed.

      Red-once proofs, both reverted before committing:
      - `WrapperInventoryTests` against the pre-fix `WrapperCall` regex
        (`__ilgpu__nv_[A-Za-z0-9_]+`, no `call`-site or comma requirement,
        `WrappersCalled` reading the whole match instead of a capture group): 3 of 5
        facts failed, `OnSm89EveryCalledWrapperIsAlreadyDefined` and
        `BothArchitecturesCallTheSameWrappers` with "Assert.Equal() Failure: HashSets
        differ … Expected: [\"__nv_exp\", \"__nv_exp_param_0\", \"__nv_log\", …] …
        Actual: [\"__nv_exp\", \"__nv_log\", \"__nv_log10\", …]" and
        `NoParameterNameIsReadAsACall` with "Assert.DoesNotContain() Failure: Filter
        matched in collection … Collection: [\"__nv_exp\", \"__nv_exp_param_0\", …]".
      - `ArchitectureTests`' algorithm, reproduced directly against `LibDevicePostLink`
        as it stood at `9c33398` (a throwaway repro, not committed, compiling
        `Kernels.Probe` for SM_75, SM_80, SM_86, SM_89 and SM_90 and calling the old
        `Link`): every one threw `InvalidOperationException`, "the kernel calls the
        libdevice wrapper __nv_exp_param_0, for which ILGPU 1.5.3.0 has no fragment.",
        the message `Execution`'s criterion predicted.

- [x] 2026-09-26 — The audit's F2, F3, F4 and observations (`Execution`'s and
      `Execution.Chunks`' own criteria of the same date list the design and the facts):
      `BadLibraryTests`, `AllCoresLayoutTests`, `AcceleratorChoiceTests.ChunksStayWithinInt32OffsetsAtTableLimits`,
      `AcceleratorChoiceTests.AHalfGivenExplicitLibraryPairIsRefused` and
      `PostLinkTests.ALogWithNulPaddingIsTrimmedOfIt` are new; the four pre-existing
      chunk-plan facts of `ChunksAreBoundedByTheChunkSizeAndTheScratchMemory` pass a
      fourth `ChunkPlan.For` argument, unchanged in what they assert.

      Evidence, on the reference machine, from a tree with every `bin` and `obj`
      removed: `dotnet build APThermo.sln` 0 warnings, 0 errors;
      `APTHERMO_NO_CUDA=1 dotnet test APThermo.sln --no-build --filter
      "Category!=LongRunning"` 3191 total, 3190 passed, 0 skipped (the one failure is
      `Protocol.Tests.DeclarationTests.EveryDeclarationUnderATickExists` on
      `src/Performance/API.md`, outside this node's subtree and predating this change);
      `dotnet test tests/Execution.Tests -c Release` (no filter) 140 of 140 (134 before
      plus the six facts named above), the 100 000-case sweep and the throughput
      tripwire included; no `Bits*.approved.txt`, `Throughput*.approved.txt` or the
      protocol tests node's `PublicSurface.approved.txt` changed; the protocol lint
      0 errors, 0 warnings. The red-once messages are recorded in `Execution`'s own
      criterion, alongside the one fact (the upload-disposal fix) that has no dedicated
      reproduction and is verified by inspection instead, as that criterion says.

- [x] 2026-09-27 — The probe's input domain (Constraints).
      - `TheCpuAcceleratorReproducesDotnetMathExactly` is green over the whole domain
        (8192 decade values plus the 17 special inputs, 12 functions each); shown red
        once by perturbing `Abs`'s expected value by `+ 1.0` — "Abs(1E-13): host
        1.9999999999999, cpu accelerator 0.9999999999999" — reverted, green again.
      - `Kernels.Probe` now calls the thermo node's `KernelMath.Min`/`Max` instead of
        `System.Math.Min`/`Max` (root `BOOT.md`, 2026-09-27), because the audit's
        suspicion below was confirmed: on the reference device (RTX 5070 Ti, driver
        13.4), every function of the list equals the CPU accelerator on every one of
        the 17 special inputs, `KernelMath.Min`/`Max` included, all at 0 ULP.
        `TheSpecialInputsAreRecordedAgainstCuda`'s own output, in full:

        | Input | Exp | Log | Log10 | Pow(1.37) | Pow(1.4) | Pow(4.6) | Sqrt | Abs | Min | Max | Floor | Ceiling |
        |---|---|---|---|---|---|---|---|---|---|---|---|---|
        | 1, 0.5, 1.5, 2, 2.5, 1e-300, −0.5, −1.5, −2.5, 1e-300 | 0 | 0 | 0 | 0 or 1* | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 |
        | 0, −0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 |
        | +∞ | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 |
        | −∞ | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 |
        | NaN | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 |
        | smallest/largest subnormal, smallest normal | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 |

        \* `Pow(1.37)(0.5)`: cpu `0.38689124838559746`, cuda `0.3868912483855974`, 1
        ULP — inside `GpuCpuTolerances.MathUlp` (4), a libdevice call already covered
        by `CudaMatchesTheCpuAcceleratorWithinTheUlpBoundForEveryFunction`'s wider
        domain; not a `Min`/`Max` finding. Every `Min`/`Max` row is 0 ULP throughout,
        NaN included, so the audit's suspicion (`.NET`'s `Math.Max(NaN, x)` is NaN
        while PTX `max.f64` returns `x`) is confirmed for `System.Math.Min`/`Max` and
        closed by routing the probe, and every numerical node, through `KernelMath`.
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

      A genuine WSL race, not part of the design: a full `-c Release` run of this
      project on real CUDA hardware found 22 facts failing together with "CUDA device 0
      was requested, but 0 device(s) exist", traced to `LaunchBudgetTests` running
      outside `EngineFixture.CollectionName` and so able to touch the CUDA driver
      (`CudaException`'s constructor) on a separate thread from `EngineFixture`'s own
      lazy CUDA engine creation. Fixed by joining the collection; `Execution`'s own
      criterion has the fuller account, since the fix could not be shown red-once in the
      usual sense (the race was observed, not reliably reproducible on demand).

      Evidence, on the reference machine: `dotnet build APThermo.sln` 0 warnings,
      0 errors; `APTHERMO_NO_CUDA=1 dotnet test APThermo.sln --no-build --filter
      "Category!=LongRunning"`, `Execution.Tests` 159/159, `Protocol.Tests` 32/32;
      `dotnet
      test tests/Execution.Tests -c Release` (no filter) 162/162 on Windows, run twice
      (once before and once after the collection fix — the race never surfaced on
      Windows), and 162/162 under WSL2 on the collection-fixed commit (a prior run of
      the commit before it hit the race, 22 failures, all traced to the same cause and
      resolved by the fix); no `Bits*.approved.txt`, `Throughput*.approved.txt` or
      `Protocol.Tests/PublicSurface.approved.txt` differs from before this task's first
      commit; the protocol lint 0 errors, 0 warnings. Six other test nodes
      (`Equilibrium.Tests`, `Thermo.Tests`, `Performance.Tests`, `Problems.Tests`,
      `Docs.Tests`, `Cli.Tests`) fail Linux bit or approved-output comparisons under
      WSL; confirmed pre-existing for `Performance.Tests` by a direct check against two
      earlier commits (`Execution`'s own criterion has the detail) and reported, not
      fixed, since every one of those nodes is outside this task's subtree.
- [x] 2026-09-30 — The compile guard and the bound check of 2026-09-30 are proved (the execution node's
      criterion of that date owns their design): each fact red once with what it guards
      undone, one process's run of the whole project below 2 GB of private memory at its
      peak on the reference machine, recorded here with the figure.

      Evidence: the facts are `RocketCompileTests` (`TheRocketKernelCompilesWithinItsAllocationBound`,
      `TheStationSolveIsNotInlined`, `ADisposedEngineHoldsNoLauncher`,
      `ADisposedEngineKeepsNoCompiledProgram`) and, in `AcceleratorChoiceTests`,
      `TheBatchLengthBoundIsInclusiveOfTheLargestArrayLength` and
      `TheProbeOutputLengthBoundIsInclusiveOfTheLargestOffset`. Each was seen red with what
      it guards undone, applied alone:
      - the attribute removed from `StationSolve.At`: the guard and the attribute fact both
        fail, ten of ten runs of the guard (five Debug, five Release);
      - `Launchers.Clear()` removed from `Engine.Dispose`: `ADisposedEngineHoldsNoLauncher`
        fails on the count, and with the count assertion removed on the weak reference;
      - `Context.ClearCache` removed from `KernelCache.Clear`: `ADisposedEngineKeepsNoCompiledProgram`
        fails (the launcher fact stays green, which is why the second fact exists);
      - the bound of `BatchLength.Of` turned from `>` into `>=`: the bound fact fails;
      - the bound of `MathProbe.OutputLength` moved 14 counts down and 14 up, each alone: its
        fact fails both times (`>` against `>=` is not observable there: no count multiplies
        to exactly `int.MaxValue`, which is prime).

      ⚠ 2026-09-30: the criterion named one allocation to remove, the 16 GB array of
      `BatchConstructorsRefuseACountWhoseArrayOverflowsA32BitLength`. Measuring the project's
      peak found a second: `ProbeMathRefusesAnInputCountWhoseOutputOverflowsA32BitOffset` built
      a `double[153 391 690]` (1.2 GB) to make `Engine.ProbeMath` refuse it, and the test host
      of `AcceleratorChoiceTests` alone peaked at 1.37 GiB. With it in place the whole
      project's test host peaked at 1.81 to 2.11 GiB over the runs made, on both sides of the
      criterion's bound, so a run could not be called below it. The fact is replaced by one on
      `MathProbe.OutputLength`, the bound `ProbeMath` now calls, asked of the count just inside
      and just over the limit. What the old fact proved and the new one does not: that
      `ProbeMath` itself refuses (the refusal needs an input array of the size it refuses).
      `ProbeMath` reaches the bound through one call, and the positive path is exercised by
      `ProbeKernelTests`; a wiring that skipped the call would pass the new fact. Named here so
      the owner can decide whether that trade stands.

      The figures of the guard and the per-run table are in the execution node's criterion.
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

## Taboos

- Do not loosen the GPU/CPU tolerance for green: a divergence is a finding about
  math functions or code generation and gets a design session. The second tier of
  the mole-fraction entry is not such a loosening: it is derived from the solver's
  stopping rule and guarded by the bound on the share of stations it applies to.
- Do not mark a CUDA test skipped on a machine without CUDA: absence is a failure
  unless CUDA is forbidden explicitly.
- Do not commit the actual throughput file.
