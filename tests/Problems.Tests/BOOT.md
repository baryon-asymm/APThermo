# BOOT.md — Problems.Tests

## Purpose

The definition of what "`Problems` is ready" means, and the front door of the whole
tree's acceptance: the end-to-end comparison with the reference implementation runs here.

| Level | What it checks | Against what (source of truth) | State |
|---|---|---|---|
| L0 | element moles and reactant enthalpy per kilogram; mass normalization; oxidizer-to-fuel split; custom reactants; candidate species selection; input validation | the fixtures' recorded `b_i`, `h_0` and product lists | ⏳ |
| L1 | single-case solves through the library for the fixture cases (rocket and equilibrium) | the fixtures node's reference outputs and its tolerance table | ⏳ |
| L2 | the four reference propellants end to end: chamber, throat, exits, performance, equilibrium and frozen flow, with and without transport; a sweep as one batch equals the same cases solved one by one | the fixtures; the single-case results | ⏳ |
| Protocol | the tree invariant, documents against code | `AGENTS.md`, the surface snapshot | ⏳ |

## Invariants

- Reference is files; tolerances from the fixtures node; every fixture case enumerated.
- L2 runs on the CPU accelerator in the default command; the same cases on CUDA are
  the execution tests node's business.
- The list of golden files for the root's first acceptance criterion is the fixture
  directory listing, produced by the test at run time.

## Dependencies

- [Problems](../../src/Problems/API.md) — what is being checked.
- [Data](../../src/Data/API.md) — the database.
- [Fixtures](../Fixtures/API.md) — reference cases and the tolerance table.

Outside the tree: xunit.

## Constraints

- Part of the default test command; no CUDA.
- Paths from the repository root; no writes into the working directory.

## Acceptance criteria

- [ ] L0 green (date, test names).
- [ ] L1 green for every fixture case (date, test names, generated list).
- [ ] L2 green for the four reference propellants and the sweep-equals-single-cases
      test (date, test names).
- [ ] Every check proven non-degenerate once: a reactant amount altered, a reference
      value altered, a species dropped from the selection rule, each seen red.

## Taboos

- Do not loosen a tolerance for green; do not exclude a failing propellant.
- Do not compute `b_i` or `h_0` in the test with the code under test.
