# BOOT.md — Equilibrium.Tests

## Purpose

The definition of what "`Equilibrium` is ready" means.

| Level | What it checks | Against what (source of truth) | State |
|---|---|---|---|
| L0 | the internal dense solver on small systems; element conservation of a converged result; status codes on invalid input | analytic solutions; the invariant's tolerance | ⏳ |
| L1 | tp, hp and sp solves for the fixture mixtures: composition, temperature, `M`, `Cp_eq`, `γ_s`, sound speed; condensed species inclusion (AP/binder/aluminium, RP-1311 example 14); frozen mode | the fixtures node's reference outputs and its tolerance table | ⏳ |
| L1 | the solver inside a CPU-accelerator kernel gives the same bits as the host call | the host call | ⏳ |
| Protocol | the tree invariant, documents against code | `AGENTS.md`, the surface snapshot | ⏳ |

## Invariants

- **Reference is files**: fixture cases carry element moles, pressure, target, the
  reference composition and properties; nothing is typed into tests.
- **Tolerances come from the fixtures node** (one table for all comparisons with the
  reference) and are not overridden locally.
- **Every fixture case is compared**: the test enumerates the fixture directory; a new
  fixture file is a new test case without code changes.

## Dependencies

- [Equilibrium](../../src/Equilibrium/API.md) — what is being checked.
- [Thermo](../../src/Thermo/API.md) — building the tables of the fixture species lists.
- [Data](../../src/Data/API.md) — loading the database.
- [Fixtures](../Fixtures/API.md) — reference cases and the tolerance table.

Outside the tree: xunit; ILGPU 1.5.3 (CPU accelerator only).

## Constraints

- Part of the default test command; no CUDA.
- Paths from the repository root; no writes into the working directory.
- The species list of a fixture case is the reference's product list, so that the
  comparison is of solvers, not of selection rules (selection is `Problems`' concern).

## Acceptance criteria

- [ ] L0 green (date, test names).
- [ ] L1 green for every fixture case of kinds tp, hp, sp, including the condensed
      cases (date, test names, the generated list of cases).
- [ ] Every check proven non-degenerate once: a reference value altered, the
      convergence tolerance loosened in a copy, a condensed species omitted, each seen red.

## Taboos

- Do not loosen a tolerance for green; do not exclude a failing fixture case.
- Do not compute expectations with the code under test.
- Do not skip when the fixtures are missing.
