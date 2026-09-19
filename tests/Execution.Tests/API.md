# API.md — Execution.Tests

The node exposes nothing outward. Its contract points upward: what the parent may
consider proven about `Execution`, and it holds two things other nodes refer to: the
GPU/CPU tolerance table and the approved throughput figures.

## What this node guarantees ✅

| Claim | Confirmed by | State |
|---|---|---|
| every function of the root's math list is linked on CUDA and matches the CPU accelerator within 4 ULP, and the CPU accelerator reproduces `System.Math` bit for bit | L1 probe kernel | ✅ |
| CUDA batches equal CPU-accelerator batches within the tolerance table, for all fields enumerated by reflection over `MixtureState`, `PerformanceFigures` and `TransportFigures`, on 100 000 cases and on every fixture family | L2 | ✅ |
| batches on the CPU accelerator equal the numerical nodes called case by case, bit for bit, the species-function batch included | L2 | ✅ |
| batches are deterministic and independent of chunking | L2 | ✅ |
| CUDA is at least 5× faster than the CPU accelerator with all cores on the reference machine, and the measured figure is recorded | Benchmark, `Throughput.approved.txt` | ✅ |
| CUDA can be forbidden and the node then never touches the CUDA driver; an explicit CUDA request that cannot be met names every path tried | L0 | ✅ |
| an `Auto` fallback to the CPU accelerator says why on the accelerator description; a scratch bound of zero or less is refused; the post-link names a wrapper whose definition is missing | L0 (2026-09-14: `AcceleratorChoiceTests`, `PostLinkTests`) | ✅ |

## What the tests rely on

- The fixtures node's reference propellant inputs for building the batches: every
  rocket fixture grouped into families by element list, product list and exit layout,
  and the tp, hp and sp fixtures of one propellant as an equilibrium batch.
- `GpuCpuTolerances.cs` in this node: the GPU/CPU table with derivations, the ULP
  bound of the probe and the bound on the share of stations at which the accelerators
  stop after different numbers of Newton steps; the mole-fraction floor and the
  different-step relative tier are read from the fixtures node's tolerance table
  (`moleFractionFloor`, `polishThresholdRelative`), which this node's own table no
  longer duplicates (2026-09-14, F-TF-05).
- `Throughput.approved.txt` in this node: build configuration, device name, ILGPU
  version, CPU accelerator and threads, cases, stations, species, CUDA time, CPU time,
  ratio, CUDA kernel time, date. The fact refuses to compare a run against a file
  measured in a different configuration (2026-09-19, BOOT.md).

⚠ 2026-09-12: the sketch named the table `Tolerances.cs`; the file is
`GpuCpuTolerances.cs`, so that it is not mistaken for the fixtures node's tolerance
table against the reference.
