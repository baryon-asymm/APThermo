# BOOT.md — Performance.Tests

## Purpose

The definition of what "`Performance` is ready" means.

| Level | What it checks | Against what (source of truth) | State |
|---|---|---|---|
| L0 | the invariants on a converged case: constant entropy, sonic throat, area ratio met; status on invalid exits | the invariants' tolerances | ⏳ |
| L1 | rocket cases of the fixtures node (LOX/LH2 example 8, MMH/NTO example 12 equilibrium and frozen, the four reference propellants): stations, `c*`, `C_F`, `Isp`, `Ivac`, area and pressure ratios | the fixtures node's reference outputs and its tolerance table | ⏳ |
| L1 | the solver inside a CPU-accelerator kernel gives the same bits as the host call | the host call | ⏳ |
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

## Acceptance criteria

- [ ] L0 green (date, test names).
- [ ] L1 green for every rocket fixture case, equilibrium and frozen (date, test
      names, generated list).
- [ ] Every check proven non-degenerate once: a reference `Isp` altered, the sonic
      tolerance loosened in a copy, a frozen case run as shifting, each seen red.

## Taboos

- Do not loosen a tolerance for green; do not exclude a failing case.
- Do not compare a subset of fields chosen by hand.
