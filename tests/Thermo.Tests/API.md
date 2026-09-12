# API.md — Thermo.Tests

The node exposes nothing outward. Its contract points upward: what the parent may
consider proven about `Thermo`.

## What this node guarantees

| Claim | Confirmed by | State |
|---|---|---|
| the species functions reproduce the NASA polynomials of the committed data to 1e-12 | L0: `FunctionFixtureTests.Functions_equal_the_independent_evaluation` | ✅ 2026-09-12 |
| the functions agree with NIST-JANAF for the spot species within the per-species tolerance recorded in `janaf.json` (plausibility, not fit accuracy) | L0: `JanafTests` | ✅ 2026-09-12 |
| interval selection and extrapolation follow the documented rule | L0: `IntervalRuleTests`, the out-of-range fixture points | ✅ 2026-09-12 |
| tables preserve order, stoichiometry and intervals; foreign elements, duplicates and reactant-only records are refused | L1: `TableBuilderTests` | ✅ 2026-09-12 |
| the functions give the same bits inside a CPU-accelerator kernel as on the host | L1: `KernelEqualityTests` | ✅ 2026-09-12 |
| `R` equals the reference implementation's value | L0: `FunctionFixtureTests.R_equals_the_reference_package_constant` | ✅ 2026-09-12 |

## What the tests rely on

- The fixtures node's loader for generated values and the R fixture.
- An ILGPU context with the CPU accelerator, created per test class (`CpuFixture`).
- The fixtures node's tolerance table for the generated fixtures; for the JANAF
  comparison, the tolerance per species inside `janaf.json` with its reason.

  ⚠ 2026-09-12: stood "a tolerance table in one file of this node (`Tolerances.cs`)".
  The only tolerance this node decides is the JANAF one, and it differs per species
  for a reason that belongs next to the data; it lives in the fixture file instead.
