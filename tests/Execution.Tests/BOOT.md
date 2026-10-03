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
| L2 | every fixture family and a 100 000-case sweep on CUDA equal the CPU accelerator; the CPU accelerator equals the numerical nodes called case by case; determinism of two runs; chunking gives the same result as one chunk; the species-function batch against the host functions and across accelerators; the element balance of every compared station closes to 1e-13 and the equilibrium families are compared with the balance-remnant correction (2026-10-03, `BalanceRemnantTests`) | the CPU accelerator and the host calls; reflection-enumerated fields (`BatchTests`, `CudaTests`, `SpeciesFunctionTests`) | ✅ |
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

  A third mole-fraction tier (the owner's decision of 2026-10-03): a **condensed**
  species not below the floor is compared at relative 1e-9 whatever the Newton counts.
  Derivation: a condensed amount on a phase plateau is ill-conditioned, with `d ln x/d ln
  p` of 59 to 290 measured at the throat fixtures' plateau stations (`LiOH(L)`,
  `AL2O3(a)`) against 2 to 6 for gases; the two accelerators' throat pressures differ by
  up to 8.4e-13 in `ln p`, so 290 × 8.4e-13 = 2.4e-10, and the solve's own floor for such
  an amount is about 1e-10 (1.31e-10 under injected noise on the CPU). On CUDA on
  2026-10-03 the worst were `LiOH(L)` 9.5e-11 (0.95 of the old tier) and `AL2O3(a)`
  4.0e-11; every other condensed value of the throat and rocket fixtures was at most
  1.1e-12. Gaseous species keep the two tiers above.

  **Balance-remnant correction** (2026-10-03, the orchestrator under the owner's grant of
  that day; no tier moves). Each accelerator closes the element balance only to a relative
  residual `ρ_i = Σ_j a_ij n_j / b_i − 1` of about 1e-14 (4.9e-14 the worst over every
  compared family), and a species the balance sets as a small difference of large amounts
  carries that residual amplified. In `three-element_rp1311-example1` the remnant x(H2) has
  `κ = Σ_i |∂ ln x/∂ ln b_i|` = 4.2e5; CUDA and the CPU differed in it by 3.8e-9 at equal
  Newton counts, and the CPU alone moves it as far under ±1 ulp of `b` or a warm restart.
  The difference equals the first-order prediction `Σ_i D_ij (ρ_i^cuda − ρ_i^cpu)`,
  `D_ij = ∂ ln x_j/∂ ln b_i`, to 2.4e-15. Two rules follow, for the equilibrium families
  (tp, hp and sp batches, whose input is `b`):
  - a mole fraction is compared as `ln x_j − Σ_i D_ij ρ_i` on each side, at the tiers
    above. `D` comes from central differences (`h` = 1e-8) through the CPU accelerator over
    the same batch; a case gets no correction when a condensed species appears or vanishes
    between `b(1 ± h)`, and a species gets none when the two one-sided differences of its
    `ln x` disagree by more than 2e-2 of its largest `|D_ij|` (or of 1 where that is
    smaller). The bound is `GpuCpuTolerances.Entries["sensitivityDisagreement"]`, measured
    on the CPU on 2026-10-03 over the six equilibrium families (23 cases, 206 compared
    species): the appear-or-vanish rule dropped 0 of 23 cases; the disagreement was at most
    2.1e-3 on the smooth rows (the H2 remnant, `κh` = 4.2e-3: the two differ by about
    `0.5 κh` from the curvature) and 0.12 to 0.60 at the kinks (the threshold-flip KClO4
    and NaClO4 cases, 13 species), with 6 noise-dominated rows of HCHO in lox-rp1 (`κ` 6e-7,
    9 to 11); the bound is 9.5 times above the worst smooth row and 5.8 below the smallest
    kink, and the guard drops 19 of the 206 species, each with `κ` at most 1.35, whose
    correction is at most 1.4e-13, a seven-hundredth of the tier;
  - every compared station of every family asserts `ρ_i ≤ 1e-13` on both accelerators
    (twice the worst measured), which also catches a defect that breaks conservation.

  Measured after the correction: H2 2.4e-15, the worst gas row at equal steps 3.98e-11.
  The worst `ρ` on the CPU accelerator, 2026-10-03: 3.9e-15 to 4.9e-14 over the six
  equilibrium families (the threshold-flip KClO4 the worst), 3.8e-15 to 4.0e-14 over the
  rocket and throat families (`nto-udmh` the worst). Facts without CUDA
  (`BalanceRemnantTests`): the corrected comparison accepts what the uncorrected one
  refuses on `three-element-example1` (a second CPU run over `b` moved by 16 ULP stands for
  the other accelerator, H2 3.3e-9 against 8.7e-15); the two agree to `κ` times the
  residuals where `κ` is small; the guard keeps the remnant and drops a kink; a residual of
  1e-12 injected into a result is reported, in the equilibrium and in the rocket comparison.
  The appear-or-vanish rule at a phase onset (`PhaseOnsetTests`): a tp case of the example12 table built at the
  boundary where C(gr) appears, found by bisection on the carbon moles through the CPU accelerator, has no derivative and
  no corrected species, and its comparison passes uncorrected.
  Rejected: a tier scaled by κ (it loosens); dropping the family (the only CUDA coverage
  of rule A's tie); comparing the multipliers (`π_H` carries the same conditioning). The
  probes: the orchestrator's scratchpad, `probe/` (2026-10-03).
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
- **The all-cores layout fact is end-to-end** (2026-10-03, root `BOOT.md`, Test time budgets):
  `AllCoresLayoutTests.TheCpuEngineReportsTheDocumentedLayoutAtEveryProcessorCountAndResultsDoNotMove`
  starts a `dotnet test` process per processor count, so it carries `Category=EndToEnd` and runs in the
  end-to-end set, not the fast set (`Protocol.Tests`' End-to-end level holds the rule).
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

→ [ACCEPTANCE.md](ACCEPTANCE.md)

## Taboos

- Do not loosen the GPU/CPU tolerance for green: a divergence is a finding about
  math functions or code generation and gets a design session. The second tier of
  the mole-fraction entry is not such a loosening: it is derived from the solver's
  stopping rule and guarded by the bound on the share of stations it applies to.
- Do not mark a CUDA test skipped on a machine without CUDA: absence is a failure
  unless CUDA is forbidden explicitly.
- Do not commit the actual throughput file.
