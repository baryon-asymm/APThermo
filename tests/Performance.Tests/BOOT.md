# BOOT.md — Performance.Tests

## Purpose

The definition of what "`Performance` is ready" means.

| Level | What it checks | Against what (source of truth) | State |
|---|---|---|---|
| L0 | the invariants on a converged case: constant entropy, sonic throat, area ratio met, frozen composition, velocity from the energy equation; status on invalid exits and inputs | the invariants' tolerances | ✅ |
| L1 | rocket cases of the fixtures node (LOX/LH2 example 8, MMH/NTO example 12 equilibrium and frozen, the four reference propellants): stations, `c*`, `C_F`, `Isp`, `Ivac`, area and pressure ratios, compositions | the fixtures node's reference outputs and its tolerance table | ✅ |
| L1 | the solver inside a CPU-accelerator kernel gives the same bits as the host call | the host call | ✅ |
| L0 | an exit station that never leaves the subsonic side is `NotConverged` and its neighbours `Ok`, driven through the `AreaRatioIteration` stage from an estimate deep on the subsonic side (2026-09-14) | the `API.md` of `Performance` | ✅ (2026-09-14) |
| Bits | the host solve of every rocket fixture gives the recorded bits: one line per fixture in `Bits.approved.txt`, the fixture's path and the SHA-256 of the raw bits of the stations' states, moles, multipliers, figures, station statuses, iteration counts and the case status, in that order | the approved snapshot, recorded at `8e36a27` before the decomposition of 2026-09-14 | ✅ (2026-09-14) |
| Protocol | the tree invariant, documents against code | `AGENTS.md`, the surface snapshot | ✅ (2026-09-13, the Protocol.Tests node) |

## Invariants

- **Reference is files**; **tolerances come from the fixtures node**; **every fixture
  case is enumerated**, as in the equilibrium tests node.
- The compared fields are enumerated by reflection over `MixtureState` and
  `PerformanceFigures`, so a new field is compared without a code change or fails
  loudly if the fixture lacks it.
- **The bits are a tripwire, not a contract** (2026-09-14): the Bits level guards the
  numerics against unnoticed change the way the surface snapshot guards the contract
  (`AGENTS.md` §13). A moved line in `Bits.approved.txt` is legitimate only with the
  numerical change that moved it named in the same commit; a decomposition, a
  renaming or a reordering of code moves no line. The snapshot is of the CPU
  accelerator on the reference machine's runtime; a runtime update that moves lines
  is re-approved with that reason recorded here. A fixture absent from the snapshot
  fails the test with instructions, as the surface snapshot does.
- The node owns the tolerances of comparisons that are not with the reference: the
  invariants' tolerances and the self-consistency and identity tolerances are named
  constants of the node with their origin in a comment, never literals in an
  assertion (2026-09-14).

## Dependencies

- [Performance](../../src/Performance/API.md) — what is being checked.
- [Equilibrium](../../src/Equilibrium/API.md) — scratch layout for the solves.
- [Thermo](../../src/Thermo/API.md) — tables.
- [Data](../../src/Data/API.md) — the database.
- [Fixtures](../Fixtures/API.md) — reference cases and the tolerance table.

Outside the tree: xunit; ILGPU 1.5.3 (CPU accelerator only).

## Constraints

- Part of the default test command; no CUDA.
- Paths from the repository root; no writes into the working directory.
- The exits of a fixture are its pressure ratios, then its supersonic area ratios, in
  the reference's station order; subsonic area ratios (one station of example 8) are
  outside version 1 and are left out of the solver's stations and of the comparison.
- Left out of the comparison by the fixtures node's caveats: `cvFrozen` and
  `cvEquilibrium` at frozen stations, `cpFrozen` and `cvFrozen` at a station with
  condensed species when transport is on (gas-phase values in the reference). A
  species below the reference's print threshold of 5e-6 is compared with the
  `moleFractionTrace` entry, the others with `moleFraction`.
- The batch struct of the kernel test is public because ILGPU compiles kernels only
  over public parameter types; a batch is a family of fixtures sharing a table and an
  exit layout.

## Acceptance criteria

- [x] 2026-09-12 — L0 green:
      `InvariantTests.Entropy_sonic_throat_area_ratio_and_frozen_composition_hold` over
      the 89 rocket files, `An_area_ratio_below_one_fails_its_station_only`,
      `A_pressure_ratio_not_above_one_fails_its_station_only`,
      `A_case_without_exits_gives_the_chamber_and_the_throat`,
      `A_non_positive_chamber_pressure_is_invalid_input`.
- [x] 2026-09-12 — L1 green for every rocket fixture case, equilibrium and frozen:
      `RocketFixtureTests.The_rocket_case_reproduces_the_reference` over the enumerated
      directory (89 files: 34 shifting, 15 frozen at chamber, 40 frozen at throat);
      `KernelEqualityTests.Kernel_and_host_give_the_same_bits` over its 6 batches.
- [x] 2026-09-12 — Every check proven non-degenerate once, by mutation runs on the
      reference machine, each restored afterwards: a reference `Isp` raised by 0.1 % at
      one exit (1 red); the solver's sonic and area-ratio tolerances loosened to `1e-2`
      and `4e-2` (88 of 89 fixture cases and 89 of 89 invariant cases red); the
      frozen-at-chamber fixtures run as shifting flow (15 red); the kernel given a
      different chamber temperature estimate (6 red); area ratios below 1 accepted by
      the solver (1 red).
- [x] 2026-09-14 — Bits level green:
      `BitSnapshotTests.Every_rocket_fixture_gives_the_recorded_bits` over the
      enumerated rocket directory against `Bits.approved.txt`, recorded before any
      code of the decomposition moved (the code of `8e36a27`: the two commits between
      it and the snapshot changed documents only) and unchanged after it. Seen red
      twice on the reference machine, each restored afterwards: the `0.5` of the
      throat's initial pressure estimate (`RocketSolver`, equation 6.15) raised by one
      ulp to `0.5000000000000001` — every one of the enumerated fixtures red; one line
      removed from `Bits.approved.txt` — that fixture red, naming it, with the
      instruction to approve, and the other fixtures green.
- [x] 2026-09-14 — The never-supersonic outcome:
      `SubsonicStationTests.A_station_that_never_leaves_the_subsonic_side_is_not_converged`
      drives `AreaRatioIteration` (through the node's new `InternalsVisibleTo`) from an
      estimate two units of `ln(p_c/p_e)` below the throat's, so that the twenty
      subsonic steps of the iteration cannot reach the sonic point, and asserts
      `NotConverged` for that station and `Ok` for the chamber, the throat and the exit
      after it. Seen red against the acceptance test of `8e36a27`, where the station
      came back `Ok`. The stage is driven over a `RocketCase`, the node's buffers of
      one case, which the host solve now uses as well.
- [ ] The invariants are one type and one test each (2026-09-14, the test review's
      F-TK-06 and F-TK-07): `RocketInvariants` returns the violated invariants of a
      solution as messages, one private method per invariant, the two unnamed
      tolerances (the energy equation, the pressure ratio) promoted to named
      constants beside the three existing ones; the theory becomes one test per
      invariant (`The_throat_is_sonic`, `Entropy_is_constant_along_the_nozzle`,
      `Velocity_follows_the_energy_equation`, `Assigned_area_and_pressure_ratios_are_met`,
      `The_composition_is_frozen_after_the_freezing_station`), and the mutation of the
      sonic and area-ratio tolerances above turns exactly those two red;
      `KernelEqualityTests.Kernel_and_host_give_the_same_bits` is split at its two seams
      (fill and launch the batch; assert the same bits) so that no method exceeds 60
      lines. The hand-typed counts of files, flows and batches leave the criteria
      above; the enumerated directory is the list.

## Taboos

- Do not loosen a tolerance for green; do not exclude a failing case.
- Do not compare a subset of fields chosen by hand.
