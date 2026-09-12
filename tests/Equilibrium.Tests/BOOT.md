# BOOT.md — Equilibrium.Tests

## Purpose

The definition of what "`Equilibrium` is ready" means.

| Level | What it checks | Against what (source of truth) | State |
|---|---|---|---|
| L0 | the internal dense solver on small systems; element conservation of a converged result; status codes on invalid input; the absent-element mask | analytic solutions; the invariant's tolerance; a table without the element | ✅ |
| L1 | tp, hp and sp solves for the fixture mixtures: composition, temperature, `M`, `MW`, `Cp_eq`, `γ_s`, sound speed; condensed species inclusion (AP/binder/aluminium, RP-1311 example 14); frozen mode | the fixtures node's reference outputs and its tolerance table; the frozen stations of the reference rocket cases | ✅ |
| L1 | the solver inside a CPU-accelerator kernel gives the same bits as the host call | the host call | ✅ |
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
- The fields compared are read from the fixture: every numeric output with a
  `MixtureState` field of the same name (the transport fields belong to `Transport`);
  a species below the reference's print threshold of 5e-6 is compared with the
  `moleFractionTrace` entry, the others with `moleFraction`. At a frozen rocket
  station `cvFrozen`, `cvEquilibrium` and `mach` are not compared (the reference computes
  no Cv there; see the Fixtures `BOOT.md`), and `cpEquilibrium` is compared with the
  node's frozen Cp.
- Frozen mode is checked on the frozen stations of the reference rocket cases: the
  composition of the last equilibrium station (moles from its mole fractions and `M`)
  is expanded at constant entropy to each frozen station's pressure.
- The dense solver is internal to `Equilibrium` and reached through
  `InternalsVisibleTo`; the batch struct of the kernel test is public because ILGPU
  compiles kernels only over public parameter types.

## Acceptance criteria

- [x] 2026-09-12 — L0 green: `DenseSolverTests` (5 tests), `InvalidInputTests` (8),
      `AbsentElementTests` (3 cases), `ElementConservationTests` over the 106 tp, hp
      and sp files.
- [x] 2026-09-12 — L1 green for every fixture case of kinds tp, hp, sp, including the
      condensed cases: `FixtureSolveTests` over the enumerated directories (46 + 34 +
      26 files); `CondensedSpeciesTests` (96 cases with condensed candidates, the
      alumina and the water-condensation tests); `FrozenModeTests` over the 51 rocket
      fixtures with frozen stations and 3 self-consistency cases;
      `KernelEqualityTests` over the 8 table families (106 cases).
- [x] 2026-09-12 — Every check proven non-degenerate once, by mutation runs on the
      reference machine, each restored afterwards: a reference `cpEquilibrium` raised by
      1 % in a tp fixture (1 red); the solver's convergence tolerances loosened to
      `0.5e-2` and `1e-2` (3 red of 106); `AL2O3(L)` omitted from the products of the AP
      chamber hp fixture while the reference keeps it (the condensed-set test and the
      solve comparison red); the solver's inclusion test disabled (23 red of 96); the
      kernel given a different estimate flag (8 red); the conservation invariant
      tightened to `1e-20` (106 red); a frozen-station reference temperature raised by
      1 K (1 red); negative abundances accepted by the solver (1 red).

## Taboos

- Do not loosen a tolerance for green; do not exclude a failing fixture case.
- Do not compute expectations with the code under test.
- Do not skip when the fixtures are missing.
