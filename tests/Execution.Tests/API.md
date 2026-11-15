# API.md — Execution.Tests

## Calibration decision diagnosis ⏳

The original throughput path retains three post-return internal value observations
alongside its original three timed runs. Diagnostics distinguish actual identity,
presence, age/expiry stage, generation and publication; no prior result/time is reused.
Host known-answer controls use the same production calibration/BatchRun boundaries.
An actual Linux run provides current decisions, not a retrospective cause claim.

The node exposes nothing outward. Its contract points upward: what the parent may
consider proven about `Execution`, and it holds the approved throughput figures that other
nodes refer to. Since 2026-10-05 it holds no tolerance table: CUDA and the CPU accelerator
are compared by their bits.

## What this node guarantees ✅

⚠ 2026-11-15: R09 below previously said actual-device maximum verification was
pending. Both final c0df budget phases now pass at the unchanged 500 ms target;
the own-node acceptance records current launch maxima and complete coverage.

| Claim | Confirmed by | State |
|---|---|---|
| every function of the root's math list, `KernelMath.Fma` and the unfused product and sum beside it included, is linked on CUDA and equals the CPU accelerator bit for bit on every input (a NaN's payload aside), and the CPU accelerator reproduces the host's own functions bit for bit | L1 probe kernel | ✅ (2026-10-05) |
| CUDA batches equal CPU-accelerator batches bit for bit, in every field of every case: the status, the iteration count (the bracketed cases' sum of attempts included), the state, the figures and every amount, on 100 000 cases, on every rocket and throat fixture family, on the equilibrium families of the 0.2.1 fixtures, on every equilibrium fixture table (tp, hp and sp), on the seeded families, on the 0.2.2 gas-plateau families and on the 0.2.2 gasless and bracketed families (`RecoveryFamilies`), on the 0.2.2 trace-gas families (`TraceGasFamilies`, 2026-10-05), the transport pass and the species-function batch included | L2 (`CudaTests`, `SpeciesFunctionTests`) | ✅ (2026-10-05) |
| the exact comparison is not degenerate: every kind of difference, down to one unit in the last place, is refused with a message naming the case and the field | L0 (`ExactComparisonTests`) | ✅ (2026-10-05) |
| every host-compared station of every family closes the element balance to a relative residual of at most `ElementBalance.ClosureBound`, 1e-13 | L2 (`BatchTests`) | ✅ |
| every observed synchronized launch of the unchanged bracketing and trace-gas workloads meets the original per-launch budget, with nonempty count and complete case coverage; cumulative Kernel remains a throughput measure | L2 (`CudaTests.AFamilyOfCasesThatAllBracketStaysWithinTheLaunchBudget`, `AFamilyOfTraceGasStatesStaysWithinTheLaunchBudget`), R09 | ✅ (2026-11-15, actual c0df Windows/Linux budget phases; [acceptance](ACCEPTANCE.md)) |
| batches on the CPU accelerator seeded by moles equal the solver called with the same seed, bit for bit, and independently of the chunking: the `seeded` fixtures, warm starts at half pressure, the bracketed gas-plateau states seeded 20 K above the plateau (2026-10-04) | L2 (`BatchTests.ASeededFamilyEqualsTheHostSolverBitForBit`, `ASeededBatchIsIndependentOfChunking`) | ✅ (2026-10-04) |
| batches on the CPU accelerator equal the numerical nodes called case by case, bit for bit, the rocket and throat families, the 0.2.1 and 0.2.2 equilibrium families and the species-function batch included | L2 | ✅ |
| batches are deterministic and independent of chunking | L2 | ✅ |
| CUDA is at least 5× faster than the CPU accelerator with all cores on the reference machine, and the measured figure is recorded | Benchmark, `Throughput.approved.txt` | ✅ |
| CUDA can be forbidden and the node then never touches the CUDA driver; an explicit CUDA request that cannot be met fails with a message; a CUDA engine loads no library of the CUDA Toolkit | L0 | ✅ (2026-10-05) |
| an `Auto` fallback to the CPU accelerator says why on the accelerator description; a scratch bound of zero or less is refused | L0 (`AcceleratorChoiceTests`) | ✅ |
| the post-link inlines the fused multiply-add, marks every double multiplication, addition and subtraction `.rn`, and refuses a fused instruction it did not write, an `.approx` instruction of doubles and an external function, on PTX text without a GPU and on every architecture from SM_75 on the reference device | L0 (`PostLinkTests`), L1 (`ArchitectureTests`) | ✅ (2026-10-05) |

⚠ 2026-10-05: was "within 4 ULP" for the probe, "within the tolerance table" for every
batch comparison, a claim that every station's mole fractions are compared as `ln x − Σ D ρ`
and the post-link naming "a wrapper whose definition is missing" and "every path tried",
and several rows still ⏳ for the CUDA run; now bit equality, the closure bound alone, and
the post-link's own refusals → HISTORY.md#exact-comparison-2026-10-05

⚠ 2026-11-14 (R09): the earlier sampling note below incorrectly retained an
aggregate 500 ms obligation. The original F2 watchdog target concerns each
synchronized launch; first-one-case sampling makes a run contain several launches.
The maximum, not the sum, last sample or mean, is now checked against the SAME
500 ms limit over the SAME workloads. Count and case coverage refuse missing
observations. Aggregate Kernel and every throughput gate keep their old meaning.
C0 sums of 6523.4 ms (bracketing) and 668.8 ms (trace gas) remain failed aggregate
observations; no per-launch maximum can be reconstructed from them. This correction
does not establish safety until the actual-device maxima pass, or repair a separate
throughput failure. The previous wording is retained verbatim:

> ⚠ 2026-11-14: the previous budget row and messages called a workload "one launch"
> and equated it with the first device wave. The bounded engine now starts with one
> case and grows at most twice per sample. These facts retain their workload count
> and aggregate kernel-time ceiling, measured across the sampled run. Historical
> device evidence remains dated; re-verification under the new policy is open.

## What the tests rely on

- The fixtures node's reference propellant inputs for building the batches: every
  rocket fixture, and every throat fixture (no exit stations), grouped into families by
  element list, product list and exit layout, and the tp, hp and sp fixtures of one propellant, or the named 0.2.1 families of tp fixtures sharing one element list and candidate list, as an equilibrium batch.
  The 0.2.2 gas-plateau families are not fixtures: `GasPlateauFamilies` computes their inputs with the numerical nodes (the plateau
  temperature by bisection on the condensed set of host tp solves, the reaction enthalpy from the species functions), because no
  reference exists on a gas-participating plateau.
  The 0.2.2 gasless and bracketed families (`RecoveryFamilies`) are computed the same way: the enthalpy and entropy of a gasless tp state
  from the condensed moles the solver found and the species functions, the melting temperature by bisection on the condensed set, the
  AP/HTPB/Al targets from tp states of the fixtures' own table; a batch result carries no multipliers, so those are not compared.
  The 0.2.2 trace-gas families (`TraceGasFamilies`, 2026-10-05) are computed from the tree's own solves too: a mixture by its element ratio with a
  trace excess or deficit of one element, a grid of temperatures and pressures, and the enthalpy and entropy of the converged tp states as the targets
  of hp and sp cases. A host solve over a scratch filled with a value no solve writes (`HostSolves.EquilibriumProbed`) tells the cases the
  pass settled (a verdict's anchor in `Tie.Elements.Multipliers` and more Newton steps than one attempt takes) and the hp and sp cases the
  temperature bracket settled (`BracketEnds` written); `BOOT.md` states what that proves and what it does not.
  The seeded families take their seeds from the CPU accelerator's own runs of the fixtures node's inputs (the `seeded` kind's
  `seed.temperature`; the tp fixtures of every equilibrium table) and of `GasPlateauFamilies`' computed systems (2026-10-04).
- `ExactComparison.cs` in this node: the one comparison of two results, by bits, for a
  rocket, an equilibrium, a transport and a species-function result.
- `ElementBalance.cs` in this node: the residual of the element balance of a station and the
  closure bound, 1e-13, with its derivation.
- `Throughput.approved.txt` in this node: build configuration, device name, ILGPU
  version, CPU accelerator and threads, cases, stations, species, CUDA time, CPU time,
  ratio, CUDA kernel time, the Newton steps per case of the rocket sweep and the CUDA
  kernel seconds per Newton step (`iterations_per_case`, `cuda_kernel_seconds_per_iteration`,
  2026-10-04), date. The fact refuses to compare a run against a file measured in a
  different configuration (2026-09-19, BOOT.md), and fails on a file that carries no
  per-iteration figure (2026-10-04).

⚠ 2026-09-12: the sketch named the table `Tolerances.cs`; the file was
`GpuCpuTolerances.cs`, so that it was not mistaken for the fixtures node's tolerance
table against the reference. Both are gone (2026-10-05).

## Trace-gas verification roles ✅

`TraceGasFamiliesTests.AFamilyHoldsTheCasesItStandsFor` checks the selected inputs'
observable CPU results against plain host solves: status, work counters, exact
state/composition and the existing element-closure bound. The separately named
`MechanismDiagnosticEverySelectedCaseCarriesTheDeclaredPassObservable` checks
scratch-anchor/bracket and iteration inference, with nonempty family coverage.
The two existing controlled observable facts remain: neither isolated condition
is sufficient, and the plain/probed solves agree. This diagnostic depends on
internal writers; it does not independently prove a physical equilibrium or
instrument entry into the trace-gas routine. Family selection itself still uses
the documented mechanism inference.

⚠ 2026-11-14: the per-case mechanism assertion previously lived in the same fact
as the backend/status comparison. The split makes an internal-path change distinct
from a numerical result failure; it removes no controlled observable check.

## Throughput observations ✅

The fixed three timed CPU/CUDA runs retain their R09 scalar summaries in run order:
launch count, covered cases, longest synchronized-launch duration and its case
count/ordinal, alongside cumulative Kernel. Output occurs only after all timing
samples are captured. Workload, warm-ups, elapsed capture points, independent
medians, original record lines and numerical acceptance gates are unchanged.
`CachedKernelDiagnostics` uses ILGPU 1.5.3's public `KernelUtil.TryGetKernel` on the
already cached launcher. Where available it reports cached PTX UTF-8 bytes/SHA-256,
virtual `.reg` declaration-line count and FunctionInfo local-allocation byte sizes.
Missing kernel, PTX or FunctionInfo is explicitly unavailable; diagnostics enable
no compiler options. No new field retains a kernel or IR, and the full PTX is not
printed. IR local allocations and virtual declarations are compiler metadata,
not measured physical spills, physical registers or occupancy.

## Isolated launch-plan diagnostic ✅

`CudaLaunchPlanDiagnosticTests.DiagnosticOnlyFrozenThroughputBoundedVersusStaticLaunches`
is an opt-in `CudaDiagnostic`/`LongRunning` experiment, excluded from the ordinary
`Category=Cuda` release matrix. It requires an actual CUDA
engine and the usual bounded device policy, then compares that policy with a
scoped `LaunchBudget.None` on the SAME engine, table and original 100000-case
frozen throughput batch. It restores the original session budget in `finally`.
Static one-launch probes copy prefixes of the SAME batch: 1, 2, 4, 8192 and 16384
cases, rather than submitting 100000 single-case chunks. The private session field
is read only by this explicitly authorized test-side reflection; no production
option or default changes. It reports three timed runs per measurement, R09
summaries, outcomes and Newton work, and compares bounded/static numerical results
with the existing exact comparator. It does not collect a new per-launch trace.
A passing static experiment is NOT throughput regression closure and writes no
approval or replacement throughput record. The original throughput fact and its
5x/80%/115% gates remain unchanged. Hardware execution belongs to the root's lane.

Select this experiment explicitly, under the root's serialized GPU lane:

```console
dotnet test tests/Execution.Tests/APThermo.Execution.Tests.csproj -c Release --filter "FullyQualifiedName=APThermo.Execution.Tests.CudaLaunchPlanDiagnosticTests.DiagnosticOnlyFrozenThroughputBoundedVersusStaticLaunches"
```

⚠ 2026-11-14: its initial `Cuda` classification would also run the scheduling
experiment in the ordinary release category. Only this diagnostic fact now uses
`CudaDiagnostic`; actual-CUDA checks, `LongRunning` and original gates remain.

## Bounded repeat calibration controls ✅

CalibrationIdentityTests covers equal-content reuse and every key/descriptor scalar.
CalibrationPrefixTests checks complete initial boundaries, cumulative cost, overrun,
partial completion, expiry and cancellation. CalibrationLoopTests drives the actual
BatchRun on a CPU accelerator with synthetic bounded policy: current InputOutput
transfers, current computation and current synchronized telemetry remain mandatory.
CalibrationLifecycleTests checks primary-preserving loop faults, lifecycle boundaries
and weak ownership with a still-live positive control. This is host evidence only.
The ordinary CUDA and throughput facts still own device duration and original gates.
CalibrationCudaTests.ActualCudaReportsColdHitAndChangedInputWithExactResults is a small actual-device cold/hit/changed-input control. Forbidden CUDA and unbounded-device branches explicitly report that they provide no bounded-hit evidence. Root owns hardware execution.

## Throughput-record validity ✅

Approved `ratio` and per-iteration figures share a finite, strictly positive scalar guard. Current per-iteration
measurements must be finite and nonnegative; finite signed zero remains a valid
observation for this upper-bound comparison. Invalid measurements have their own
validity failure, while finite valid values retain the original inclusive 115 %
ceiling. A breach reports synchronized wall time without claiming a code-only
cause: launch partition, clocks and competing load can affect it. No record,
workload, median, measurement boundary or acceptance threshold changes.

⚠ 2026-11-15 (A28/R11): the previous comment attributed per-step time to the
compiled kernel alone and the error excluded the machine as a cause. The R10
launch-partition diagnostic disproved that interpretation. New parsed-record controls reproduced accepted positive infinity/overflowed approved figures and negative measured values on the unchanged source (9 assertions red, 18 controls passing); `ThroughputRecordTests` covers the corrected finite domain and unchanged inclusive ceiling.
⚠ 2026-11-15 (R11 ratio follow-up): `CudaTests` previously parsed the approved
ratio directly. Its unchanged 5x/80 % expressions now live in the portable
`ThroughputRecord.RatioViolation`; the same scalar validator also guards the
per-step baseline. Before the guard, six nonpositive/negative-infinite baselines
silently passed with a valid per-step line. The old-reader run had 12 failures
and 33 passing controls; missing/malformed and other nonfinite values now have a
baseline-validity message. Positive 18.52, finite extremes and inclusive original
ratio floors are controlled independently. No approved value or gate changed.
⚠ 2026-11-15 (R12): CalibrationLoopTests previously treated the real host's
adaptive Stopwatch partition as always yielding prefix15. Its hit/current-work
control and final original-A hit now prepare deterministic observations through
the existing cache transaction, then run real uploads, computation, synchronization
and downloads. A slower controlled partition proves an exact prefix10 and makes
the old15 assertion red without sleeps/retries. The cold-A-to-new-input-B check
still consumes the actual cold run's entry, with no intervening training. Stale
input identity, post-download identity and skipped-work mutations remain red.
Exact prefix unit tests and the hardware CUDA control retain their assertions.
