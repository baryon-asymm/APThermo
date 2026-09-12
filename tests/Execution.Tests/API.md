# API.md — Execution.Tests

The node exposes nothing outward. Its contract points upward: what the parent may
consider proven about `Execution`, and it holds two things other nodes refer to: the
GPU/CPU tolerance table and the approved throughput figures.

## What this node guarantees

| Claim | Confirmed by | State |
|---|---|---|
| every function of the root's math list is linked on CUDA and matches the CPU accelerator within 4 ULP | L1 probe kernel | ⏳ |
| CUDA batches equal CPU-accelerator batches within the tolerance table, for all fields enumerated by reflection, on 100 000 cases | L2 | ⏳ |
| batches are deterministic and independent of chunking | L2 | ⏳ |
| CUDA is at least 5× faster than the CPU accelerator with all cores on the reference machine, and the measured figure is recorded | Benchmark, `Throughput.approved.txt` | ⏳ |
| CUDA can be forbidden and the node then never touches the CUDA driver | L0 | ⏳ |

## What the tests rely on

- The fixtures node's reference propellant inputs for building the batch.
- `Tolerances.cs` in this node: the GPU/CPU table with derivations.
- `Throughput.approved.txt` in this node: device name, ILGPU version, cases,
  CUDA time, CPU time, ratio, date.
