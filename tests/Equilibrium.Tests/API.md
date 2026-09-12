# API.md — Equilibrium.Tests

The node exposes nothing outward. Its contract points upward: what the parent may
consider proven about `Equilibrium`.

## What this node guarantees

| Claim | Confirmed by | State |
|---|---|---|
| tp, hp and sp solves reproduce the reference implementation within the fixtures node's tolerance table for every fixture case | L1 over the enumerated fixture directory | ⏳ |
| condensed species enter and leave as in the reference (AP/binder/aluminium, water condensation) | L1 condensed cases | ⏳ |
| element conservation and status codes behave as the invariants state | L0 | ⏳ |
| the solver gives the same bits inside a CPU-accelerator kernel as on the host | L1 kernel-equality tests | ⏳ |

## What the tests rely on

- The fixtures node's loader and tolerance table.
- Tables built with `Thermo` from the fixture's species list and the committed database.
- An ILGPU context with the CPU accelerator.
