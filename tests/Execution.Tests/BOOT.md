# BOOT.md — Execution.Tests

## Purpose

The definition of what "`Execution` is ready" means. This node owns the exact comparison
of CUDA with the CPU accelerator (2026-10-05: no tolerance table is left) and the approved
throughput figures.

| Level | What it checks | Against what (source of truth) | State |
|---|---|---|---|
| L0 | accelerator choice and the environment variable; the reason of an `Auto` fallback on the accelerator description (`CudaSkippedBecause`), proven on every runner through the seam that forces CUDA allowed; an explicit request that cannot be met fails; ILGPU version and reflected member asserted; batch validation; chunk bounds; result layouts; a scratch bound of zero or less is refused at `Create`; a CUDA engine loads no library of the CUDA Toolkit (`AcceleratorChoiceTests`) | documented behaviour; mutation of the assertion | ✅ (2026-10-05) |
| L0 | the post-link on PTX text, no GPU (`PostLinkTests`): a call of `Math.FusedMultiplyAdd` becomes `fma.rn.f64` with its own operands and the declaration goes with the last call; every `mul`, `add` and `sub` of doubles is marked `.rn` and nothing else is touched; a fused or `mad` instruction the post-link did not write, an `.approx` instruction of doubles, an external function and an unrecognised call are refused naming the target; every non-success `CudaError` and a NUL-padded or empty log give the one message shape | the `API.md` of `Execution`'s `Ptx` node; fragments typed in the facts in the shape ILGPU 1.5.3 emits, which `ArchitectureTests` finds in the real kernels | ✅ (2026-10-05) |
| L0 | the exact comparison is not degenerate (`ExactComparisonTests`): two runs of one batch compare clean, and a status, an iteration count, one unit in the last place of a state field, a figure, an amount, a transport figure, a species function, a range flag, a signed zero, a NaN and a shape each fail with their own message, on a result of each kind | two CPU runs of the same batch, each broken once | ✅ (2026-10-05) |
| L1 | the probe kernel with every function of the root's math list, `KernelMath.Fma` and the unfused product and sum included, loads through the post-link on CUDA and equals the CPU accelerator bit for bit on every input, NaN payload aside; the CPU accelerator reproduces the host's own functions (`KernelMath` and the IEEE operations) bit for bit (`ProbeKernelTests`) | the CPU accelerator and the host functions | ✅ (2026-10-05) |
| L1 | every architecture ILGPU 1.5.3 declares from SM_75 up: every entry point compiled for it passes the post-link and loads on the reference device, the post-link marks arithmetic `.rn` on each, inlines the probe's fused multiply-add, the PTX equals the device's own up to ILGPU's generated names and the `.target` line, and the probe returns the device's own bits and the CPU accelerator's; an engine binds CUDA only after the probe kernel loads (`ArchitectureTests`, `ByteVectorTests`) | the engine's own CUDA kernels and probe, the CPU accelerator | ✅ (2026-10-05) |
| L0 | the CPU accelerator is sized for `Environment.ProcessorCount`, proven at 4, 16 and 64 in child processes, with identical batch results (`AllCoresLayoutTests`); a chunk stays within 32-bit offsets at the tree's own size limits (`AcceleratorChoiceTests.ChunksStayWithinInt32OffsetsAtTableLimits`) | `Execution`'s `BOOT.md` and `API.md`, the audit's F3 and F4 | ✅ (2026-10-05) |
| L0 | the rocket kernel's compile is bounded and released (2026-09-30): the first rocket run of a fresh CPU engine allocates under 7 GiB in the whole process, every thread counted (2026-10-05), a disposed engine holds no launcher and no compiled program, and the 32-bit bounds of the batch constructors and of the probe are checked on `BatchLength.Of` and `MathProbe.OutputLength` without allocating (`RocketCompileTests`, `AcceleratorChoiceTests.TheBatchLengthBoundIsInclusiveOfTheLargestArrayLength`, `TheProbeOutputLengthBoundIsInclusiveOfTheLargestOffset`) | the measured figures in `Execution`'s `BOOT.md` (criterion of 2026-09-30), the root's Compile size constraint | ✅ (2026-10-05) |
| L2 | every rocket and throat fixture family; every equilibrium fixture table, tp, hp and sp, one family per table (`FixtureBatches.EquilibriumTableFamily`); the `seeded` fixtures, the tp fixtures of every table warm-started at half pressure and the bracketed calcite and magnesite states seeded 20 K above the plateau (`SeededFamilies`); the computed gas-plateau families (`GasPlateauFamilies`); the 0.2.2 gasless verdict and temperature bracket (`RecoveryFamilies`: KO2 and NaO2 at their exact stoichiometry where the gas vanishes, a gasless melting plateau, the gas plateaus of CaCO3 and MgCO3, AP/HTPB/Al hp states at 20 MPa), each family keeping the cases the CPU accelerator ends `NoGasPhase` or `Ok` as it stands for; the 0.2.2 trace-gas pass (`TraceGasFamilies`: the magnesite carbon dioxide walk at 1e7 Pa, and KCl, CaCO3, Li2O and NaCl with a trace excess or deficit of one element, with the hp and sp states of the KCl data junction, of Li2O and of the cold NaCl gas, each family keeping the cases the host solver ends `Ok` through the pass or `NoGasPhase`, the pass observed on the CPU in `TraceGasFamiliesTests`); and a 100 000-case sweep on CUDA equal the CPU accelerator in every field of every case, bit for bit, the bracketed cases' summed iteration counts included, and the transport pass and the species-function batch likewise (`CudaTests`, `SpeciesFunctionTests`); the CPU accelerator equals the numerical nodes called case by case, a seeded case with its seed; determinism of two runs, the sweep's included; chunking gives the same result as one chunk; the element balance of every host-compared station closes to `ElementBalance.ClosureBound` (`BatchTests`); one launch of cases that all bracket stays inside the launch budget (`CudaTests.AFamilyOfCasesThatAllBracketStaysWithinTheLaunchBudget`; on the CPU `BracketedFamiliesTests`), and so does one launch of the trace-gas hp and sp states of the KCl family (`CudaTests.AFamilyOfTraceGasStatesStaysWithinTheLaunchBudget`; on the CPU `TraceGasFamiliesTests`) | the CPU accelerator and the host calls; reflection-enumerated fields (`ExactComparison`) | ✅ (2026-10-05) |
| Benchmark | throughput of the 100 000-case batch on CUDA against the CPU accelerator with all cores | the approved figures file for the running platform (`Throughput.approved.txt`, `Throughput.linux.approved.txt` on Linux, 2026-09-17), asymmetry: may improve, must not regress below 80 % of the approved ratio or below the root's 5×, nor rise above 115 % of the approved CUDA kernel time per Newton step (2026-10-04) (`CudaTests.ThroughputIsRecordedAndNotBelowTheApprovedRatio`) | ✅ |
| Protocol | the tree invariant, documents against code | `AGENTS.md`, the surface snapshot | ✅ (2026-09-13, the Protocol.Tests node) |

⚠ 2026-10-04: was "every fixture family", now the families named: the `seeded` kind had never run in a batch, and most tp, hp and sp fixture tables were not batched → HISTORY.md#l2-every-family-2026-10-04

⚠ 2026-10-05: was the rocket compile bounded at 2 GiB allocated on the calling thread, now at 7 GiB allocated in the whole process → HISTORY.md#compile-bound-process-wide-2026-10-05

The bound of `RocketCompileTests.TheRocketKernelCompilesWithinItsAllocationBound` (2026-10-05, release 0.2.2, coder 16). ILGPU
compiles on a pool sized by the machine's cores, so the share of a compile that lands on the calling thread is a figure of the
machine: hosted `windows-latest` (4 cores, Release) measured 2 566 509 328 bytes against 1 008 787 280 here, and failed a bound
that held on the commit before it. The bytes of every thread together are the compile's own size. The same fact, one fresh process
per row, `APTHERMO_NO_CUDA=1`, `DOTNET_PROCESSOR_COUNT` set, the reference machine:

| Configuration | Processors | Calling thread (bytes) | Whole process (bytes) |
|---|---|---|---|
| Release | 16 | 1 005 250 800 | 3 576 888 496 |
| Release | 16, in the fast set of 289 facts | 1 005 747 448 | 3 576 409 888 |
| Release | 8 | 1 049 884 200 | 3 577 609 720 |
| Release | 4 | 1 456 857 072 | 3 576 929 760 |
| Release | 2 | 1 600 315 544 | 3 575 907 960 |
| Debug | 16 | 1 098 500 216 | 3 890 141 000 |
| Debug | 4 | 1 759 460 056 | 3 888 768 888 |

The calling thread varies by 59 % across the counts (1.01 to 1.60 GB); the whole process by 0.05 % (3 575.9 to 3 577.6 MB in
Release, 3 888.8 to 3 890.1 MB in Debug), and by 0.5 MB between the fact alone and the fast set, where the three classes outside
the `engine` collection run beside it. The bound is 7 GiB (7 516 192 768): 2.10 times the Release figure and 1.93 times the
Debug one, a margin that covers another runtime's or platform's allocation pattern, and below the red side: the whole process
allocates at least what its calling thread does, and the calling thread of the compile without the attribute on
`StationSolve.At` allocated at least 6 853 MiB (Release) and 8 261 MiB (Debug) on the kernel of 2026-09-30
(`HISTORY.md#compile-bound-red-once-list-2026-09-30`). The red run under the new measure is the open criterion of `ACCEPTANCE.md`.

The pass observable of `TraceGasFamilies` (2026-10-05, release 0.2.2, coder 17). The trace-gas pass is internal to `Equilibrium`'s
`TraceGas` child and no contract of the tree reports that a case entered it, so a family proves that its cases reach it from what the
contracts do document, on the host (`HostSolves.EquilibriumProbed`: the same solve over a scratch filled with a value no solve writes,
bit-identical to the plain one, held by `TraceGasFamiliesTests`). A tp case that ended `Ok` reaches the pass when `Tie.Elements.Multipliers`
holds an anchor (the gas-phase verdict ran after a failed ordinary attempt and left the pass's anchor there, `Equilibrium.GasPhase`'s
`API.md`) and its `Iterations` exceed `EquilibriumSolver.MaxNewtonSteps`; an hp or sp case when `BracketEnds` was written (the temperature
bracket ran, so the ordinary iteration failed, `Equilibrium.Recovery`'s `API.md`) and its `Iterations` exceed the same bound. Neither
condition suffices alone, and the facts show each one false on a real state (a junction state of 72 ordinary steps with no anchor, a
2000 K state with an anchor and 21 steps). This is an inference from documented writers, not an observation of the call: it cannot tell
the pass from another attempt that follows the same verdict, and for an hp or sp case it rests on the bracket's own seams to the pass.
A per-case observable owned by the `TraceGas` or `Equilibrium` node would replace it (the report of coder 17, AGENTS.md section 11).

## Invariants

- **CUDA equals the CPU accelerator, bit for bit** (2026-10-05, item 13 of 0.2.2). The tree's
  own `Exp`, `Log` and `Pow` are correctly rounded and the post-link keeps CUDA from fusing a
  multiplication into an addition, so the two accelerators run one program on one set of IEEE
  operations, and no tolerance between them is left to state: `ExactComparison` holds every field
  of every result equal by its bits, the statuses and the iteration counts too, on every
  family, the sweep and the species functions. The one thing it does not compare is a NaN's
  payload and sign, which the hardware chooses (the probe's `SameValue`: `Abs(NaN - 1)` is
  `0x7FF8…` on the CPU and `0x7FFF…` on CUDA); a NaN is a failed case, reported by its status.
  Nothing of the old table survives here but `ElementBalance.ClosureBound`, 1e-13, which
  bounds a property of one solution (the closure of its element balance), not a difference
  between two. The mole-fraction floor and the polish-threshold tier belong to
  `tests/Fixtures` and serve the comparison with the reference.
  ⚠ 2026-10-05: was the GPU/CPU tolerance table (relative 1e-10 and 1e-9 tiers, 4 ULP on the
  probe, a share of one station in a thousand with different Newton counts, a correction of
  the balance remnant and four rules of what a difference is not) → HISTORY.md#exact-comparison-2026-10-05
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
  20 % or below 5×, and when the rocket sweep's CUDA kernel time per Newton step
  (`cuda_kernel_seconds_per_iteration`, the kernel seconds over the steps summed over every
  station of every case; `iterations_per_case` records that workload) exceeds 115 % of the
  approved figure (2026-10-04, `ThroughputRecord`). The figure is the median of the three
  timed runs' kernel times over the steps (`CudaTiming`, 2026-10-05), the same statistic as
  `cuda_seconds`; the median of five such runs varies by 0.2 % (Windows) and 0.3 % (WSL2). A
  record without a valid per-iteration line fails with a message naming the file, never
  skips: a platform's file is re-approved from a run on that platform.

  ⚠ 2026-10-05: was the figure of the first timed run's kernel time, now the median of the
  timed runs' kernel times. The first timed run's kernel time is not representative: three
  quiet runs measured `cuda_seconds` 0.355, 0.354, 0.355 and the first run's kernel 0.376,
  0.277, 0.349 s (1.680e-07, 1.237e-07, 1.557e-07 per step), once above the median total, and
  the check failed at 120.6 % in the merge guard. The median of the three, which one outlier
  does not move, is the figure compared and recorded (`cuda_kernel_seconds`); the first run's
  result still goes to the determinism pair (runs 0 and 1). Over five runs per platform after
  the fix the median per-step figure spread 1.236 to 1.238e-07 (Windows) and 1.233 to
  1.237e-07 (WSL2), while the first run's own was 1.235 to 1.371e-07 and 1.233 to 1.438e-07,
  up to 16 % above the median in one run of five and 10 to 11 % in two of the Windows five: no
  usable margin against the 115 % limit. The CUDA warm-up of five launches is kept; the
  outlier is the first timed run only, which the median absorbs. The records were re-approved
  from these runs (`ACCEPTANCE.md`).

  ⚠ 2026-10-04: was the ratio alone, now the kernel time per Newton step too; a ratio
  moves with the CPU's load and the transfer overhead and let +25 % on the kernel (0.2.1
  to 0.2.2, a loop-live local passed by reference to a `NoInlining` call) pass the floor
  → HISTORY.md#throughput-per-iteration-2026-10-04

  ⚠ 2026-10-04: was the Windows record of 2026-09-19 (ratio 23.58, CUDA 0.151 s, CPU
  3.557 s), now ratio 21.29 (CUDA 0.249 s, CPU 5.306 s, kernel 0.176 s): the old record
  does not reproduce even at its own commit (v0.1.0 measures 19.7 to 21.2 today; the
  transfer overhead grew) → HISTORY.md#throughput-per-iteration-2026-10-04

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
  fixture. A seeded family's seeds are the CPU accelerator's own results (a tp batch at the
  fixture's seed temperature, a cold batch of the same cases, a tp state 20 K above a plateau),
  handed identically to both accelerators, so a CUDA comparison compares the seeded solve alone
  (2026-10-04).
⚠ 2026-10-04: was "states left out of the families for their element balance" (the 17-element table and the hp states of magnesite at 1e4 and 1e6 Pa), now none: the relative closure of the solver puts every one back in its family → HISTORY.md#left-out-states

## Dependencies

- [Execution](../../src/Execution/API.md) — what is being checked.
- [Execution.Chunks](../../src/Execution/Chunks/API.md) — `Chunk` and `ChunkPlan`, in
  the chunk-plan unit facts (a child node of `Execution`, 2026-09-15).
- [Execution.Ptx](../../src/Execution/Ptx/API.md) — `PtxPostLink` and `CudaWslDevices`, in the
  post-link and WSL facts (a child node of `Execution`, 2026-10-01; was `LibDevice`).
- [Thermo](../../src/Thermo/API.md) — tables, `MixtureState`, `CaseStatus`.
- [Equilibrium](../../src/Equilibrium/API.md) — the solver and scratch layout called case by case on the host.
- [Performance](../../src/Performance/API.md) — `PerformanceFigures`, the rocket solver called on the host, flow models.
- [Transport](../../src/Transport/API.md) — the transport table and evaluation called on the host, `TransportFigures`.
- [Data](../../src/Data/API.md) — the database.
- [Fixtures](../Fixtures/API.md) — the reference propellant inputs used to build the
  batches, and the reference temperature tolerance of the CEA comparison.
- [Harness](../Harness/API.md) — bit comparison (`Bits.Same`, `Bits.Differences`) and
  the per-platform approved path (`ApprovedSnapshot.ApprovedPathFor`, 2026-09-17).

Outside the tree: xunit; ILGPU 1.5.3; an NVIDIA GPU with its driver for the CUDA category
(no CUDA Toolkit, 2026-10-05).

## Constraints

- The CPU-only part of the node runs in the default test command; the CUDA category
  runs in the full set on the reference machine; the long-running category (the
  sweep and the benchmark, about 20 s, and since 2026-09-26 the architecture fact) is
  excluded from the fast set.

  ⚠ 2026-09-28: was "about three minutes" for the architecture fact, now measured 8 m 2
  s on Windows and 11 m 3 s under WSL (a corrected duration, not a new bound) →
  HISTORY.md#long-running-duration-2026-09-28
- The post-link facts (`PostLinkTests`) take their PTX from fragments typed in the facts, in the
  shape ILGPU 1.5.3 emits; the fixtures `Ptx/probe.sm_89.ptx` and `Ptx/probe.sm_120.ptx`, which
  served the wrapper inventory, are gone with it (2026-10-05). `ArchitectureTests` is what finds
  those shapes in the real kernels of every target.
  ⚠ 2026-10-05: was two PTX fixtures of the probe kernel, with a header naming libnvvm, feeding
  the inventory of libdevice wrappers → HISTORY.md#exact-comparison-2026-10-05
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
  - The comparison counts NaN on both sides as equal (whatever its payload) and compares ±0 and
    ±∞ by their bits, with the sign.
  - The CPU accelerator must still reproduce the host's own functions bit for bit on every
    input (2026-10-05: `KernelMath` for `Exp`, `Log`, `Pow`, `Min`, `Max` and `Fma`,
    `System.Math` for the IEEE ones).
  - The audit's suspicion about `Math.Max(NaN, x)` and the instruction of 2026-09-27 to
    record what CUDA returns and to stop on a divergence →
    HISTORY.md#probe-domain-audit-suspicion-2026-09-27
  - Measured 2026-09-27 on the reference device, by the coder of that day: every
    function of the list equals the CPU accelerator on every input, except `Math.Min`
    and `Math.Max` with a NaN operand (CPU NaN, CUDA the other operand). The owner
    decided the root's `KernelMath`. The probe therefore runs `KernelMath.Min` and
    `KernelMath.Max` in place of `Math.Min` and `Math.Max`, and they must equal the CPU
    on every input, NaN included. Measured 2026-10-05 with the tree's own `Exp`, `Log` and
    `Pow`: all 15 functions equal on all 123 135 values of the probe domain, but one
    NaN payload (`Abs(NaN - 1)`).

## Acceptance criteria

→ [ACCEPTANCE.md](ACCEPTANCE.md)

## Taboos

- Do not introduce a tolerance between the accelerators for green: a divergence is a finding
  about the program, the post-link or the hardware's arithmetic, and gets a design session
  (2026-10-05: the accelerators are exactly equal, so any tolerance would hide a defect).
- Do not mark a CUDA test skipped on a machine without CUDA: absence is a failure
  unless CUDA is forbidden explicitly.
- Do not commit the actual throughput file.
