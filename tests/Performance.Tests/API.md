# API.md — Performance.Tests

The node exposes nothing outward. Its contract points upward: what the parent may
consider proven about `Performance`.

## What this node guarantees

| Claim | Confirmed by | State |
|---|---|---|
| chamber, throat and exit stations and the performance figures reproduce the reference within the tolerance table for every rocket fixture case, in shifting and frozen flow | L1 over the enumerated fixtures | ⏳ |
| the isentropic, sonic and area-ratio invariants hold on every converged case | L0 | ⏳ |
| the solver gives the same bits inside a CPU-accelerator kernel as on the host | L1 kernel-equality tests | ⏳ |

## What the tests rely on

- The fixtures node's loader and tolerance table; reflection over the result structs
  for the field list.
- Tables built with `Thermo`; an ILGPU context with the CPU accelerator.
