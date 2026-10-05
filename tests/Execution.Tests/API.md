# API.md — Execution.Tests

The node exposes nothing outward. Its contract points upward: what the parent may
consider proven about `Execution`, and it holds the approved throughput figures that other
nodes refer to. Since 2026-10-05 it holds no tolerance table: CUDA and the CPU accelerator
are compared by their bits.

## What this node guarantees ✅

| Claim | Confirmed by | State |
|---|---|---|
| every function of the root's math list, `KernelMath.Fma` and the unfused product and sum beside it included, is linked on CUDA and equals the CPU accelerator bit for bit on every input (a NaN's payload aside), and the CPU accelerator reproduces the host's own functions bit for bit | L1 probe kernel | ✅ (2026-10-05) |
| CUDA batches equal CPU-accelerator batches bit for bit, in every field of every case: the status, the iteration count (the bracketed cases' sum of attempts included), the state, the figures and every amount, on 100 000 cases, on every rocket and throat fixture family, on the equilibrium families of the 0.2.1 fixtures, on every equilibrium fixture table (tp, hp and sp), on the seeded families, on the 0.2.2 gas-plateau families and on the 0.2.2 gasless and bracketed families (`RecoveryFamilies`), the transport pass and the species-function batch included | L2 (`CudaTests`, `SpeciesFunctionTests`) | ✅ (2026-10-05) |
| the exact comparison is not degenerate: every kind of difference, down to one unit in the last place, is refused with a message naming the case and the field | L0 (`ExactComparisonTests`) | ✅ (2026-10-05) |
| every host-compared station of every family closes the element balance to a relative residual of at most `ElementBalance.ClosureBound`, 1e-13 | L2 (`BatchTests`) | ✅ |
| one launch of cases that all bracket stays within the launch budget | L2 (`CudaTests.AFamilyOfCasesThatAllBracketStaysWithinTheLaunchBudget`) | ✅ |
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
