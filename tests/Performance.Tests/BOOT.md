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
- **This node keeps its own reader of a fixture's outputs** (2026-09-14, the
  architecture review's F-AR-03): the field-name mapping (`StationComparison.Fields`)
  and the set of fields that belong to another node (`TransportFields`) stay here, not
  in the harness, which holds no formula and no tolerance. This node reads a station
  with performance figures on top of the state `Equilibrium.Tests` reads alone, and
  `Problems.Tests` reads a station with transport figures on top of that; a shared
  reader would have to know all three shapes, which would put it above the nodes its
  readers' own consumers test. Only the trace-threshold selection line moved out, to
  the fixtures node's `ToleranceTable.MoleFractionField` (the same F-AR-03 finding: it
  stood typed, with its selection line, in this node and in `Equilibrium.Tests` and
  `Problems.Tests` alike).

## Dependencies

- [Performance](../../src/Performance/API.md) — what is being checked.
- [Equilibrium](../../src/Equilibrium/API.md) — scratch layout for the solves.
- [Thermo](../../src/Thermo/API.md) — tables.
- [Data](../../src/Data/API.md) — the database.
- [Fixtures](../Fixtures/API.md) — reference cases and the tolerance table.
- [Harness](../Harness/API.md) — the CPU host, bit comparison and fixture families.

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

## Shape exceptions

Added 2026-09-14 by the design session, after the protocol tests node's measurements found
this constructor over the root's six parameters. This node's own `RocketBatchViews` is the
parameter struct of one rocket kernel launch, one argument per view of the batch, as ILGPU
takes a kernel's arguments and as the execution node's struct of the same name is declared;
grouping the views would re-shape the launch the kernel equality tests mirror. On the
root's condition for such a type its creation names its arguments; it passes them by
position today (the criterion below).

| Where | Rule | Measured | Reason |
|---|---|---|---|
| `RocketBatchViews.RocketBatchViews` | parameters | 16 | the parameter struct of one rocket kernel launch, one argument per view; its creation names its arguments |

## Acceptance criteria

- [x] 2026-09-14 — L0 green: `InvariantTests.The_throat_is_sonic`,
      `Entropy_is_constant_along_the_nozzle`, `Velocity_follows_the_energy_equation`,
      `Assigned_area_and_pressure_ratios_are_met`,
      `The_composition_is_frozen_after_the_freezing_station` over the enumerated
      rocket fixture directory, plus `An_area_ratio_below_one_fails_its_station_only`,
      `A_pressure_ratio_not_above_one_fails_its_station_only`,
      `A_case_without_exits_gives_the_chamber_and_the_throat`,
      `A_non_positive_chamber_pressure_is_invalid_input`. (Re-dated from 2026-09-12:
      the F-TK-06 split below renamed the theory; the hand-typed file count is gone,
      F-TK-03.)
- [x] 2026-09-14 — L1 green for every rocket fixture case, equilibrium and frozen:
      `RocketFixtureTests.The_rocket_case_reproduces_the_reference` over the enumerated
      directory; `KernelEqualityTests.Kernel_and_host_give_the_same_bits` over its
      batches, each a family of fixtures sharing a table and an exit layout. (Re-dated
      from 2026-09-12: the hand-typed file and batch counts are gone, F-TK-03.)
- [x] 2026-09-14 — Every check proven non-degenerate once, by mutation runs, each
      restored afterwards: a reference `Isp` raised by 0.1 % at one exit (that fixture
      case red); the frozen-at-chamber fixtures run as shifting flow (every
      frozen-at-chamber fixture red); the kernel given a different chamber temperature
      estimate (every batch's `KernelEqualityTests` test red); area ratios below 1
      accepted by the solver (that fixture case red). (Re-dated from 2026-09-12: the
      hand-typed counts are gone, F-TK-03; the sonic/area-ratio mutation below moved
      to its own paragraph.)

      ⚠ 2026-09-14: this criterion stood "the solver's sonic and area-ratio tolerances
      loosened to `1e-2` and `4e-2` (88 of 89 fixture cases and 89 of 89 invariant
      cases red)". Re-run today before any other change, it turns nothing red: every
      fixture's throat and exit search now reaches `RocketSolver.TightTolerance`
      (`1e-10`) within `MaxThroatIterations`/`MaxAreaRatioIterations` before the loop
      ever consults the report-tolerance fallback — a refinement that postdates the
      2026-09-12 run and that this session's decomposition carried over unchanged (the
      Bits level below is the proof it moved no formula). `SonicTolerance` and
      `AreaRatioTolerance` are therefore dead code for every fixture of the current
      tree; the criterion below mutates `TightTolerance` instead, the constant that
      actually gates convergence, and records what it found.
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
- [x] 2026-09-14 — The invariants are one type and one test each (the test review's
      F-TK-06 and F-TK-07): `RocketInvariants` returns the violated invariants of a
      solution as messages, one method per invariant (`SonicThroat`, `ConstantEntropy`,
      `EnergyEquation`, `AssignedExit`, `FrozenComposition`), each under fifteen lines
      and nesting at most two, with the two previously inline tolerances (the energy
      equation, the pressure ratio) promoted to `VelocityTolerance` and
      `PressureRatioTolerance` beside the three existing ones and their origin named.
      `InvariantTests` becomes one test per invariant (`The_throat_is_sonic`,
      `Entropy_is_constant_along_the_nozzle`, `Velocity_follows_the_energy_equation`,
      `Assigned_area_and_pressure_ratios_are_met`,
      `The_composition_is_frozen_after_the_freezing_station`) over the enumerated
      rocket fixture directory, each a three-line body. Non-degeneracy: with
      `RocketSolver.TightTolerance` loosened from `1e-10` to `1e-2` (the ⚠ above, in
      place of the now-inert `SonicTolerance`/`AreaRatioTolerance`),
      `The_throat_is_sonic` turns red on every fixture and
      `Assigned_area_and_pressure_ratios_are_met` on every fixture that carries an
      exit, while `Entropy_is_constant_along_the_nozzle`,
      `Velocity_follows_the_energy_equation` and
      `The_composition_is_frozen_after_the_freezing_station` stay green throughout —
      a sharper proof than the single combined theory gave, because entropy and the
      energy-equation identity are guaranteed by the equilibrium solve itself and the
      frozen copy is bit-exact, none of the three sensitive to the throat/exit
      search's own convergence. `KernelEqualityTests.Kernel_and_host_give_the_same_bits`
      is split at its two seams into `Fill` (`RocketBatchBuffers`: allocates and
      uploads one batch) and `AssertSameBits` (the field-by-field comparison), each
      under 60 lines; the kernel given a different chamber temperature estimate still
      turns every batch red. The self-consistency comparisons of
      `An_area_ratio_below_one_fails_its_station_only` name the `SelfConsistency`
      constant instead of an inline `1e-9` (F-TK-10). The hand-typed counts of files,
      flows and batches leave the criteria above; the enumerated directory is the
      list. Every mutation restored afterwards; the Bits level did not move (no
      `src/Performance` file changed for this criterion).
- [x] 2026-09-15 — The creation of this node's `RocketBatchViews` in
      `KernelEqualityTests` names its arguments, in the order of the parameters (the
      root's condition on a declared wide constructor, the row of
      `## Shape exceptions`), verified by `named-construction-scan.py . RocketBatchViews`
      (two sites tree-wide, this node's and the execution node's own type of the same
      name, both fully named); the node's bit snapshot unchanged (`Bits.approved.txt`
      hash `5aa32f2bbf679cdd0f47749b0780059ba89faa62`, the fast suite 699/699 green).
- [x] 2026-09-15 — `RocketInputs` (8 parameters) and `RocketSolution` (9), both in
      `RocketHost.cs`, restructured within the root's limit, along domain axes;
      neither is a declared exception, so no row was added to `## Shape exceptions`.
      `RocketInputs` split into the case's chemical system (`ChemicalSystem`:
      `Elements`, `ElementMoles`, `Products`, 3 parameters), its combustion
      conditions (`ChamberPressure`, `ReactantEnthalpy`, `Flow`, kept directly), and
      its exit layout (`ExitPlan`: `Values`, `Kinds`, 2 parameters), for 5 parameters
      on `RocketInputs` itself. `RocketSolution` split into its per-station numerical
      outcome (`RocketOutcome`: `Stations`, `Moles`, `Multipliers`, `Figures`,
      `StationStatus`, `Iterations`, 6 parameters) alongside `Table`, `Inputs` and the
      overall `Status`, for 4 parameters on `RocketSolution` itself. Both keep the old
      field names as forwarding properties, so the ~25 existing read call sites across
      `RocketCase.cs`, `KernelEqualityTests.cs`, `StationComparison.cs`,
      `SubsonicStationTests.cs`, `RocketInvariants.cs` and `RocketFixtureTests.cs` are
      unchanged; only the two construction sites (`RocketInputs.Of`,
      `RocketCase.Read`) and the three `with { ExitValues = …, ExitKinds = … }`
      expressions of `InvariantTests.cs` (rewritten as `with { Exits = new
      ExitPlan(…) }`, since a computed forwarding property has no `init` accessor for
      `with` to target) changed.

      Verified: 699/699 tests green, `Bits.approved.txt` hash unchanged
      (`5aa32f2bbf679cdd0f47749b0780059ba89faa62`), protocol lint 0/0.

## Taboos

- Do not loosen a tolerance for green; do not exclude a failing case.
- Do not compare a subset of fields chosen by hand.
