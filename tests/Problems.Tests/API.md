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
| a composition that does not weigh one kilogram with the database's atomic weights within the front door's tolerance is refused through every front door, naming the record, the mass in grams and the tolerance; one that does is solved | L0 (`RejectionTests.A_composition_that_does_not_weigh_one_kilogram_is_rejected_with_its_mass_and_the_tolerance`, `A_reactant_record_whose_molar_mass_contradicts_its_formula_is_caught_at_the_solve`) | ✅ |
| the tolerance a mixture declares is the one applied, and the mass of every mixture is reported and equals the sum over the database's atomic weights; every fixture's recorded element moles weigh one kilogram within 1.7e-5 | L0 (`RejectionTests.The_tolerance_a_mixture_declares_is_the_one_applied`, `PropellantTests.The_recorded_element_moles_of_every_fixture_weigh_one_kilogram_within_the_derivation_figure`, `Results_carry_the_mass_of_their_mixture`) | ✅ |
| a condensed record cut by the species table is reported once under its database name, an assigned enthalpy inside the `ALN(L)` gap solves, and a sweep across the alumina plateau stays on the isentrope | L2 (`SplitRecordTests`) | ✅ 2026-09-13 (recorded 2026-09-14) |
| a state record with exits solves as its rocket case; the rules of the record's shape refuse by index; a batch mixing transport and none equals each problem alone; a ratio and pressure product as one batch equals its cases one by one; every method of a disposed solver refuses | L2 (`RocketTests`, `RejectionTests`, the contract facts of 2026-09-14) | ⏳ 2026-09-14 |
| the front door's result of every fixture is bit for bit what it was before the front door's decomposition | Bits (`BitSnapshotTests`, `Bits.approved.txt`) | ⏳ 2026-09-14 |

## What the tests rely on

- The fixtures node's loader and tolerance table, and the caveats of the reference's
  fields recorded in its BOOT.md.
- The committed database under `data/`.
- The CPU accelerator through the solver's engine options, and a CPU engine of the
  execution node to evaluate the transport solver on the reference's composition.
- `Bits.approved.txt` in this node: one line per fixture, the fixture path and the
  SHA-256 of the raw bits of its front-door result (2026-09-14).
