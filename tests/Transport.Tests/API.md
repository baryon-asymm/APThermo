# API.md — Transport.Tests

The node exposes nothing outward. Its contract points upward: what the parent may
consider proven about `Transport`.

## What this node guarantees

| Claim | Confirmed by | State |
|---|---|---|
| viscosity, conductivities and Prandtl numbers reproduce the reference within the tolerance table at every station of every transport fixture case | L1 | ⏳ |
| fits and units are evaluated as documented; species without data are excluded and reported | L0 | ⏳ |
| the evaluation gives the same bits inside a CPU-accelerator kernel as on the host | L1 kernel-equality tests | ⏳ |

## What the tests rely on

- The fixtures node's loader and tolerance table.
- Station solutions produced by `Equilibrium` from the fixture inputs.
- An ILGPU context with the CPU accelerator.
