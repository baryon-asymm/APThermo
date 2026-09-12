# API.md — Problems.Tests

The node exposes nothing outward. Its contract points upward: what the parent may
consider proven about `Problems` and, through it, about the system.

## What this node guarantees

| Claim | Confirmed by | State |
|---|---|---|
| the four reference propellants reproduce the reference implementation end to end within the tolerance table, in shifting and frozen flow, with transport | L2 | ⏳ |
| propellant definitions are turned into element moles, enthalpy and candidate species exactly as the reference does | L0 | ⏳ |
| a sweep solved as one batch equals its cases solved one by one | L2 | ⏳ |
| invalid inputs are rejected by name before any kernel runs | L0 | ⏳ |

## What the tests rely on

- The fixtures node's loader and tolerance table.
- The committed database under `data/`.
- The CPU accelerator through `Problems`' solver options.
