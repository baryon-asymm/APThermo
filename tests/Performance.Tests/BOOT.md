# BOOT.md — Performance.Tests

## Purpose

The definition of what "`Performance` is ready" means.

| Level | What it checks | Against what (source of truth) | State |
|---|---|---|---|
| L0 | the invariants on a converged case: constant entropy, sonic throat, area ratio met, frozen composition, velocity from the energy equation; status on invalid exits and inputs | the invariants' tolerances | ✅ |
| L1 | rocket cases of the fixtures node (LOX/LH2 example 8, MMH/NTO example 12 equilibrium and frozen, the four reference propellants): stations, `c*`, `C_F`, `Isp`, `Ivac`, area and pressure ratios, compositions | the fixtures node's reference outputs and its tolerance table | ✅ |
| L1 | the solver inside a CPU-accelerator kernel gives the same bits as the host call | the host call | ✅ |
| Protocol | the tree invariant, documents against code | `AGENTS.md`, the surface snapshot | ⏳ |

## Invariants

- **Reference is files**; **tolerances come from the fixtures node**; **every fixture
  case is enumerated**, as in the equilibrium tests node.
- The compared fields are enumerated by reflection over `MixtureState` and
  `PerformanceFigures`, so a new field is compared without a code change or fails
  loudly if the fixture lacks it.

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

## Taboos

- Do not loosen a tolerance for green; do not exclude a failing case.
- Do not compare a subset of fields chosen by hand.
