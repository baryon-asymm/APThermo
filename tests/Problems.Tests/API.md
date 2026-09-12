# API.md — Problems.Tests

The node exposes nothing outward. Its contract points upward: what the parent may
consider proven about `Problems` and, through it, about the system.

## What this node guarantees ✅

| Claim | Confirmed by | State |
|---|---|---|
| every rocket fixture (the four reference propellants in shifting and frozen flow, with and without transport, and the RP-1311 rocket examples) reproduces the reference implementation end to end within the tolerance table, the transport fields on the tree's own composition | L1, L2 (`RocketTests.The_rocket_case_reproduces_the_reference_end_to_end`) | ✅ |
| every tp, hp and sp fixture reproduces the reference from its propellant, and as a state record in a batch over the union of elements of its group | L1, L2 (`EquilibriumTests`) | ✅ |
| propellant definitions are turned into mass fractions, element moles, enthalpy and candidate species exactly as the reference does (the ratio path within the reference's single precision) | L0 (`PropellantTests`) | ✅ |
| a sweep solved as one batch equals its cases solved one by one, an elemental mixture equals its propellant, identical problems give identical results alone and in one call, each bit for bit | L2 (`RocketTests`) | ✅ |
| invalid inputs are rejected by name or index before any kernel runs; a failing station is a status, not an exception | L0, L2 (`RejectionTests`, `RocketTests.A_failing_station_is_a_status_and_not_an_exception`) | ✅ |

## What the tests rely on

- The fixtures node's loader and tolerance table, and the caveats of the reference's
  fields recorded in its BOOT.md.
- The committed database under `data/`.
- The CPU accelerator through the solver's engine options, and a CPU engine of the
  execution node to evaluate the transport solver on the reference's composition.
