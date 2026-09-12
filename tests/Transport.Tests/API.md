# API.md — Transport.Tests

The node exposes nothing outward. Its contract points upward: what the parent may
consider proven about `Transport`.

## What this node guarantees

| Claim | Confirmed by | State |
|---|---|---|
| viscosity, conductivities, Prandtl numbers and the set's frozen heat capacity reproduce the reference within the tolerance table at every station of every rocket fixture run with transport, on the reference composition (worst 3.4e-8 relative on 2026-09-12), except the reacting fields at the nine stations where the reference is defective, where the defect is asserted to persist | L1 `StationTests` | ✅ |
| fits, intervals and units are evaluated as documented; species without data are estimated and reported, pairs are those of the database | L0 `FitTests`, L1 `StationTests` | ✅ |
| the evaluation gives the same bits inside a CPU-accelerator kernel as on the host | L1 `KernelEqualityTests` | ✅ |
| bad inputs are statuses | `InputTests` | ✅ |

## What the tests rely on

- The fixtures node's loader and tolerance table.
- The station compositions of the fixtures (mole fractions over the reference's MW).
- An ILGPU context with the CPU accelerator.
