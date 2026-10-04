# API.md — Execution.Tests

The node exposes nothing outward. Its contract points upward: what the parent may
consider proven about `Execution`, and it holds two things other nodes refer to: the
GPU/CPU tolerance table and the approved throughput figures.

## What this node guarantees ✅

| Claim | Confirmed by | State |
|---|---|---|
| every function of the root's math list is linked on CUDA and matches the CPU accelerator within 4 ULP, and the CPU accelerator reproduces `System.Math` bit for bit | L1 probe kernel | ✅ |
| CUDA batches equal CPU-accelerator batches within the tolerance table, for all fields enumerated by reflection over `MixtureState`, `PerformanceFigures` and `TransportFigures`, on 100 000 cases, on every rocket and throat fixture family (the throat families since 2026-10-03; a condensed species at its own tier) and on the equilibrium families of the 0.2.1 fixtures (the three-element, threshold-flip and gas-column salt fixtures, one family per table, 2026-10-03) | L2 | ✅ |
| every compared station of every family (rocket, throat, equilibrium) closes the element balance to a relative residual of at most 1e-13 on both accelerators, and the equilibrium families' mole fractions are compared as `ln x − Σ D ρ`, the balance remnants of both accelerators removed, at the unchanged tiers (2026-10-03; `BalanceRemnantTests` on the CPU, the CUDA families on the reference machine) | L2 | ✅ (2026-10-03) |
| CUDA batches equal CPU-accelerator batches within the tolerance table on the 0.2.2 gas-plateau families: hp and sp states on the plateaus of boiling water, ammonium chloride, calcium hydroxide and calcium carbonate, whose inputs the tree computes itself (`GasPlateauFamilies`) | L2 (`CudaTests.AGasPlateauFamilyOnCudaMatchesTheCpuAccelerator`) | ✅ (2026-10-03, merge `2ebebad`) |
| CUDA batches equal CPU-accelerator batches on the 0.2.2 gasless and bracketed families (`RecoveryFamilies`: the exact-stoichiometry peroxides KO2 and NaO2 where the gas vanishes, a gasless melting plateau, carbonate plateaus started cold, AP/HTPB/Al below the water band): equal statuses, `Ok` fields within the table, `NoGasPhase` amounts within the table with the pressure (and a tp case's temperature) exact and every other state field zero; one launch of cases that all bracket stays within the launch budget | L2 (`CudaTests.ABracketedFamilyOnCudaMatchesTheCpuAccelerator`, `AFamilyOfCasesThatAllBracketStaysWithinTheLaunchBudget`) | ⏳ |
| CUDA batches equal CPU-accelerator batches within the tolerance table on every equilibrium fixture table (tp, hp and sp, one family per table, 2026-10-04) | L2 (`CudaTests.AnEquilibriumTableFamilyOnCudaMatchesTheCpuAccelerator`) | ⏳ (the reference machine's CUDA run) |
| batches on the CPU accelerator seeded by moles equal the solver called with the same seed, bit for bit, and independently of the chunking: the `seeded` fixtures, warm starts at half pressure, the bracketed gas-plateau states seeded 20 K above the plateau (2026-10-04) | L2 (`BatchTests.ASeededFamilyEqualsTheHostSolverBitForBit`, `ASeededBatchIsIndependentOfChunking`) | ✅ (2026-10-04) |
| CUDA batches seeded by moles equal CPU-accelerator batches within the tolerance table on the same families (2026-10-04) | L2 (`CudaTests.ASeededFamilyOnCudaMatchesTheCpuAccelerator`) | ⏳ (the reference machine's CUDA run) |
| batches on the CPU accelerator equal the numerical nodes called case by case, bit for bit, the rocket and throat families, the 0.2.1 and 0.2.2 equilibrium families and the species-function batch included | L2 | ✅ |
| batches are deterministic and independent of chunking | L2 | ✅ |
| CUDA is at least 5× faster than the CPU accelerator with all cores on the reference machine, and the measured figure is recorded | Benchmark, `Throughput.approved.txt` | ✅ |
| CUDA can be forbidden and the node then never touches the CUDA driver; an explicit CUDA request that cannot be met names every path tried | L0 | ✅ |
| an `Auto` fallback to the CPU accelerator says why on the accelerator description; a scratch bound of zero or less is refused; the post-link names a wrapper whose definition is missing | L0 (2026-09-14: `AcceleratorChoiceTests`, `PostLinkTests`) | ✅ |

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
- `GpuCpuTolerances.cs` in this node: the GPU/CPU table with derivations, the ULP
  bound of the probe and the bound on the share of stations at which the accelerators
  stop after different numbers of Newton steps; the condensed-species mole-fraction tier
  (relative 1e-9 whatever the Newton counts, 2026-10-03); the mole-fraction floor and the
  different-step relative tier are read from the fixtures node's tolerance table
  (`moleFractionFloor`, `polishThresholdRelative`), which this node's own table no
  longer duplicates (2026-09-14, F-TF-05).
- `GpuCpuTolerances.cs` also holds the balance-residual bound (`balanceResidual`, 1e-13), the guard's bound for the
  correction (`sensitivityDisagreement`) and the step of the central differences
  (`SensitivityStep`, 1e-8), each with its derivation (2026-10-03).
- `Throughput.approved.txt` in this node: build configuration, device name, ILGPU
  version, CPU accelerator and threads, cases, stations, species, CUDA time, CPU time,
  ratio, CUDA kernel time, date. The fact refuses to compare a run against a file
  measured in a different configuration (2026-09-19, BOOT.md).

⚠ 2026-09-12: the sketch named the table `Tolerances.cs`; the file is
`GpuCpuTolerances.cs`, so that it is not mistaken for the fixtures node's tolerance
table against the reference.
