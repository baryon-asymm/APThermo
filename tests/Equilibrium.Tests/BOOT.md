# BOOT.md — Equilibrium.Tests

## Purpose

The definition of what "`Equilibrium` is ready" means.

| Level | What it checks | Against what (source of truth) | State |
|---|---|---|---|
| L0 | the internal dense solver on small systems; element conservation of a converged result; status codes on invalid input; the absent-element mask | analytic solutions; the invariant's tolerance; a table without the element | ✅ |
| L1 | tp, hp and sp solves for the fixture mixtures: composition, temperature, `M`, `MW`, `Cp_eq`, `γ_s`, sound speed; condensed species inclusion (AP/binder/aluminium, RP-1311 example 14); frozen mode | the fixtures node's reference outputs and its tolerance table; the frozen stations of the reference rocket cases | ✅ |
| L1 | the solver inside a CPU-accelerator kernel gives the same bits as the host call | the host call | ✅ |
| L2 | states the reference cannot reach: the pinned pair at a cut, the refusal where no admissible set exists, no condensed candidate with positive gain left out of an `Ok` status | the node's own condensed-species rule (`BOOT.md`), not the reference | ✅ |
| Bits | the host solve of every tp, hp and sp fixture case gives the recorded bits: one line per case in `Bits.approved.txt`, the case file and the SHA-256 of the raw bits of the moles, the multipliers, every field of the state, the status and the iteration count, in that order | the approved snapshot, recorded at `8e36a27` before the decomposition of 2026-09-14 | ✅ |
| Protocol | the tree invariant, documents against code | `AGENTS.md`, the surface snapshot | ✅ (2026-09-13, the Protocol.Tests node) |

## Invariants

- **Reference is files**: fixture cases carry element moles, pressure, target, the
  reference composition and properties; nothing is typed into tests.
- **Tolerances come from the fixtures node** (one table for all comparisons with the
  reference) and are not overridden locally.
- **Every fixture case is compared**: the test enumerates the fixture directory; a new
  fixture file is a new test case without code changes.
- **The bits are a tripwire, not a contract** (2026-09-14): the Bits level guards the
  numerics against unnoticed change the way the surface snapshot guards the contract
  (`AGENTS.md` §13). A moved line in `Bits.approved.txt` is legitimate only with the
  numerical change that moved it named in the same commit; a decomposition, a
  renaming or a reordering of code moves no line. The snapshot is of the CPU
  accelerator on the reference machine's runtime; a runtime update that moves lines
  is re-approved with that reason recorded here. A fixture case absent from the
  snapshot fails the test with instructions, as the surface snapshot does.
- **This node owns the tolerance of a comparison that is not with the reference**
  (2026-09-14): two paths of this tree reaching the same state (an equilibrium solve
  and a frozen one at its composition; a plateau state reached twice) or an algebraic
  identity of one state have no entry in the fixtures node's table to ask, so their
  tolerance is a named constant of this node, `Tolerances.cs`, with its origin in a
  comment, rather than a literal at the assertion (F-TK-10).

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

- [x] 2026-09-14 — L0 green: `DenseSolverTests` (5 tests), `InvalidInputTests` (8),
      `AbsentElementTests` (3 cases), `ElementConservationTests` over the enumerated
      tp, hp and sp files.
- [x] 2026-09-14 — L1 green for every fixture case of kinds tp, hp, sp, including the
      condensed cases: `FixtureSolveTests` over the enumerated tp, hp and sp
      directories; `CondensedSpeciesTests` over the fixture cases with condensed
      candidates (the alumina and the water-condensation tests); `FrozenModeTests`:
      `Frozen_stations_of_the_reference_are_reproduced_from_the_frozen_composition`
      over the reference rocket cases with a frozen station, and the self-consistency
      cases, one per problem kind, over
      `Frozen_mode_at_the_equilibrium_composition_recovers_the_equilibrium_state`,
      `A_frozen_state_reports_the_frozen_heat_capacities_as_the_equilibrium_ones` and
      `A_frozen_state_carries_the_ideal_gas_derivatives`; `KernelEqualityTests` over
      the table families of the enumerated directory.

      ⚠ 2026-09-14: until this date these two criteria carried hand-typed fixture
      counts (106 tp/hp/sp files, 96 condensed cases, 51 frozen-station fixtures, 8
      table families) that had fallen behind the fixtures node's directories, in one
      case since before the tick was written (AGENTS.md §8: a number repeating the
      length of a list diverges at the list's first change). Dropped in favour of the
      enumerated directory itself, which is the list; found by the test review of
      2026-09-14 (F-TK-03). `FrozenModeTests`' single named test is replaced by the
      three tests its split into the same day (F-TK-11).
- [x] 2026-09-14 — L2 green: `PlateauTests.An_enthalpy_inside_the_ALN_gap_pins_the_pieces_at_the_cut`,
      `An_enthalpy_no_admissible_set_can_hold_is_refused_rather_than_lied_about`, and
      `An_ok_solution_leaves_no_condensed_candidate_with_positive_inclusion_gain` (a
      theory over every hp fixture case). The class named itself L2 in its own comment
      since 2026-09-14 while this node's level table and criteria did not; both now
      say the same thing (F-TK-02).
- [x] 2026-09-12 — Every check proven non-degenerate once, by mutation runs on the
      reference machine, each restored afterwards: a reference `cpEquilibrium` raised by
      1 % in a tp fixture (1 red); the solver's convergence tolerances loosened to
      `0.5e-2` and `1e-2` (3 red of 106); `AL2O3(L)` omitted from the products of the AP
      chamber hp fixture while the reference keeps it (the condensed-set test and the
      solve comparison red); the solver's inclusion test disabled (23 red of 96); the
      kernel given a different estimate flag (8 red); the conservation invariant
      tightened to `1e-20` (106 red); a frozen-station reference temperature raised by
      1 K (1 red); negative abundances accepted by the solver (1 red).
- [x] 2026-09-14 — Bits level green:
      `BitSnapshotTests.Every_fixture_case_gives_the_recorded_bits` over the
      enumerated tp, hp and sp directories against `Bits.approved.txt`, recorded from
      the code of `8e36a27` before any code of the decomposition moved (one line per
      enumerated fixture file, the directories being the list). Seen red twice, each
      mutation applied alone and restored: the solver's `StandardPressure` perturbed
      by one ULP (`1.0e5` → `100000.00000000001`), which reported 22 cases with moved
      hashes; and one line deleted from the approved file, which reported that case
      with the instruction to approve.

      ⚠ 2026-09-14: this criterion was written the same day predicting "every case
      red" for the perturbed constant. Wrong: the Newton iteration polishes until its
      corrections fall below `1e-11` and its fixed point absorbs a last-ULP change of
      an input in 93 of the 115 cases — a one-ULP shift of `ln(p/p°)` is below the
      rounding of the sums it enters. The 22 that did move are what makes the check
      non-degenerate; the figure is recorded rather than the quantifier
      (`AGENTS.md` §8: an absolute word needs proof or a caveat).

## Taboos

- Do not loosen a tolerance for green; do not exclude a failing fixture case.
- Do not compute expectations with the code under test.
- Do not skip when the fixtures are missing.
