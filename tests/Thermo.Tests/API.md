# API.md — Thermo.Tests

The node exposes nothing outward. Its contract points upward: what the parent may
consider proven about `Thermo`.

## What this node guarantees

| Claim | Confirmed by | State |
|---|---|---|
| the species functions reproduce the NASA polynomials of the committed data to 1e-12 relative | L0 against independently generated fixtures | ⏳ |
| the functions agree with NIST-JANAF within the fits' stated accuracy for the spot species | L0 JANAF fixture | ⏳ |
| interval selection and extrapolation follow the documented rule | L0 boundary tests | ⏳ |
| tables preserve order and stoichiometry; foreign elements are refused | L1 builder tests | ⏳ |
| the functions give the same bits inside a CPU-accelerator kernel as on the host | L1 kernel-equality tests | ⏳ |
| `R` equals the reference implementation's value | L0 constant test | ⏳ |

## What the tests rely on

- The fixtures node's loader for generated values and the R fixture.
- An ILGPU context with the CPU accelerator, created per test class.
- A tolerance table in one file of this node (`Tolerances.cs`), with the derivation of
  each entry in a comment.
