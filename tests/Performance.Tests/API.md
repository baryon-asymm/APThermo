# API.md — Performance.Tests

The node exposes nothing outward. Its contract points upward: what the parent may
consider proven about `Performance`.

## What this node guarantees

| Claim | Confirmed by | State |
|---|---|---|
| chamber, throat and exit stations, their compositions and the performance figures reproduce the reference within the tolerance table for every rocket fixture case, in shifting and frozen flow (subsonic stations excepted) | L1 over the enumerated fixtures (`RocketFixtureTests`) | ✅ |
| the isentropic, sonic, area-ratio and frozen-composition invariants hold on every converged case; invalid exits fail their station only | L0 (`InvariantTests`) | ✅ |
| the solver gives the same bits inside a CPU-accelerator kernel as on the host | L1 kernel-equality tests (`KernelEqualityTests`) | ✅ |

## What the tests rely on

- The fixtures node's loader and tolerance table; reflection over the result structs
  for the field list.
- Tables built with `Thermo`; an ILGPU context with the CPU accelerator; a public
  `RocketBatchViews` struct of this node as the kernel parameter of the equality test.
