# BOOT.md — Performance

## Purpose

The rocket problem for one case with an infinite-area chamber: the chamber state at
assigned enthalpy and pressure, the throat by the sonic condition, the exit stations
at assigned area ratios or pressure ratios, in shifting equilibrium or in frozen flow,
and the performance figures: characteristic velocity, thrust coefficient, specific
impulse and vacuum specific impulse. It sits on `Equilibrium`, which it calls at every
station, and it is verified against the reference implementation's rocket tables.

⚠ Declared deviation (`AGENTS.md` §6, §12): the method is that of NASA RP-1311 Part I,
chapter 6 (rocket performance, infinite-area combustor): the pressure-ratio iteration
for the throat, the area-ratio iteration for the exit stations, the definitions of the
performance parameters. This document fixes the choices and limits; it does not
restate the equations. Whoever codes this node reads chapter 6.

## Invariants

- **Isentropic expansion.** Every station downstream of the chamber has the chamber
  entropy: `|s_station − s_chamber| ≤ 1e-9 · |s_chamber|` at convergence; a station
  violating it is reported as `NotConverged`.
- **Sonic throat.** At the throat `|u²/a² − 1| ≤ 4e-5` (the report's tolerance, with
  the equilibrium sound speed in equilibrium flow and the frozen one in frozen flow).
- **Area ratios are met by construction.** An exit station requested by area ratio
  satisfies `|(ρ_t u_t)/(ρ_e u_e) − ε| ≤ 1e-6 · ε` at convergence.
- **Frozen means frozen.** In frozen flow the composition downstream of the freezing
  station is bit-identical to the freezing station's composition; only temperature and
  pressure change.
- **Performance figures are defined once**, as in the report: `c* = p_c / (ρ_t u_t)`;
  `Isp = u_e` (effective exhaust velocity, m/s, ambient pressure equal to exit
  pressure); `Ivac = u_e + p_e / (ρ_e u_e)`; `C_F = Isp / c*`; `Mach = u / a` with the
  sound speed of the flow model.
- **Stateless, deterministic, bounded**, as `Equilibrium`: explicit inputs and
  scratch; iteration caps; a status per case.

## Dependencies

- [Equilibrium](../Equilibrium/API.md) — the hp and sp solves (shifting flow) and the
  frozen solve (frozen flow), the mixture state and its derivatives.
- [Thermo](../Thermo/API.md) — the table view, `MixtureState`, `CaseStatus`, the gas constant.

Outside the tree: ILGPU 1.5.3 (`ArrayView<T>`); NASA RP-1311 Part I, chapter 6.

## Constraints

Inherited from the root ([BOOT.md](../../BOOT.md)). In addition:

- Kernel-compatible C#; one case is one sequential program: chamber, throat, then
  the exit stations in the order given.
- Inputs per case: the element moles per kilogram, the reactant enthalpy per kilogram,
  the chamber pressure, the flow model (`ShiftingEquilibrium`, `FrozenAtChamber`,
  `FrozenAtThroat`), and a fixed number of exit stations per batch (possibly none),
  each either an area ratio (`≥ 1`, supersonic branch) or a pressure ratio `p_c/p_e (> 1)`.
- Throat: initial pressure ratio from the chamber `γ_s` as in the report (6.15);
  the momentum update of (6.17) on the throat pressure; at most 20 iterations, else
  `ThroatNotFound`. The report stops at `|u² − a²|/u² ≤ 4e-5` (6.16); this node goes
  on to `1e-10` when it can, so that the reported throat is at rounding level, and
  accepts the report's tolerance as `Ok` otherwise.
- Exit by area ratio: initial pressure ratio from the report's estimates, the
  correction of (6.23)–(6.24) on `ln(p_c/p_e)`; at most 20 iterations; the
  supersonic branch only, so an area ratio below 1 is `AreaRatioInvalid`. The report
  stops at `4e-5` on the correction (6.25); this node goes on to `1e-10` when it can.

  ⚠ 2026-09-12: stood "initial pressure ratio from the isentropic relation with the
  throat γ_s". The report's own estimates are used instead: the extrapolation with the
  derivative (6.23) from the previous station when both area ratios exceed 2, else its
  empirical formulas (6.21) for ratios up to 2 and (6.22) above. The formulas were
  recovered from the report's text, whose typography lost the range boundaries; the
  ranges are this node's reading, and they only change how many iterations a station
  takes, never where it converges.
- Exit by pressure ratio: one sp solve at `p_e`; the area ratio is an output.
- Each station's sp solve starts from the last converged station's composition and
  temperature as the estimate (a failed station is skipped over), in frozen flow from
  the freezing station's composition.
- Frozen flow: the freezing station's composition is copied once; downstream stations
  use `SolveFrozen`. In `FrozenAtChamber` flow the chamber's isentropic exponent,
  sound speed and derivatives are the frozen ones (section 6.5.3), as the reference
  reports them, while its heat capacities stay the equilibrium ones.
- Outputs per case: the chamber, throat and exit `MixtureState`s with velocity and
  Mach number, the mole numbers and multipliers at every station (for composition
  output and for `Transport`), the performance figures per station (the reference
  reports them at every station: `c*` everywhere, the throat's `C_F`, `Isp`, `Ivac`),
  a status per station, the equilibrium iteration counts, and the case status.
- Not in version 1: finite-area chamber, subsonic exit stations, freezing at an
  arbitrary station, the report's stop of a frozen expansion 50 K below the range of
  a condensed species present at the chamber (section 6.5.1).

## Structure

Decided 2026-09-14 (the clean-code pass; the root's code-shape constraint). The rocket
solve of one case is one public entry over internal static stage classes, all
kernel-compatible, one class per file in this directory and namespace, sharing the
existing view, scratch and result structs. Every floating-point expression keeps its
present form and its present order of evaluation; the bit snapshot of the tests node
(the acceptance criteria below) is the proof that the decomposition moved code and
rewrote no formula.

⚠ 2026-09-15 (distribution phase): "one public entry" and `RocketSolver`'s row below
stood before the API review of that day (fixed in `24156be`) found no
consumer scenario for it, `ExitSpecification`, `RocketProblem`, `RocketLayout` or
`RocketResult`: every use is `Execution` composing the kernel, `Problems` building a
batch, or this node's own tests. All five moved into `API.md`'s tree-contract
section; `APThermo.Performance.csproj` grants `InternalsVisibleTo` to `Execution`,
`Problems`, `Execution.Tests` and `Benchmarks`. `FlowModel` and `PerformanceFigures`
stay public. The entry point is internal now, reached only through the grant; the
decomposition itself (one class per stage) is unaffected.

| Class | Responsibility | Visibility |
|---|---|---|
| `RocketSolver` | the contract: the constants and `Solve`, reduced to the station order (clear the views, the chamber, the throat, the exits, the case status); holds no formula | internal (2026-09-15, distribution phase), contract unchanged |
| `ChamberSolve` | the chamber state at assigned enthalpy and pressure, made frozen where the flow model says so (sections 6.3.1 and 6.5.3); returns `ChamberReference` | internal |
| `ThroatSearch` | the sonic throat, equations (6.15)–(6.17), and what it defines: the mass flux and `c*`; returns `ThroatReference` | internal |
| `ExitStations` | the loop over the exits, the dispatch on `ExitSpecification` to `AreaRatioIteration` or `PressureRatioStation`, the estimate chain from station to station, the case status; holds no formula (Size, below) | internal |
| `AreaRatioIteration` | one exit assigned by area ratio: the initial `ln(p_c/p_e)` of (6.21)–(6.23), the correction of (6.24)–(6.25), an explicit outcome | internal |
| `PressureRatioStation` | one exit assigned by pressure ratio (6.3.6): the station pressure from the ratio, the solve at that pressure, the velocity, the area ratio and the figures as outputs | internal |
| `StationSolve` | one station's sp or frozen solve: the sub-views, the estimate composition copied in, the call into `Equilibrium` | internal |
| `StationFigures` | the energy equation, the area ratio and the figures of section 6.2, each written once | internal |

Carriers (`Carriers.cs`): `RocketContext` (the table view, the problem, the scratch
and the result, built once in `Solve`), `ChamberReference` (pressure, enthalpy,
entropy, `γ_s`), `ThroatReference` (pressure, mass flux, `c*`, the logarithm of the
pressure ratio, `γ_s`), `StationRequest` (the station index, pressure, temperature
estimate, entropy and flow), `ExitEstimate` (the extrapolation state carried between
exits), and the enums `StationFlow { Shifting, Frozen }` (in place of the boolean that
picked the solver) and `ExitOutcome { Converged, WithinReportTolerance,
NeverSupersonic, SolveFailed }`.

⚠ 2026-09-14, found in the coding: two of those carriers are not what the design
wrote. `ExitOutcome` has a fifth value, `NotMet`: the four above name every ending of
the iteration but one — twenty corrections whose last is above the report's tolerance
— which the code of `8e36a27` ended as `NotConverged` and which must keep ending so;
with four values that ending had no name and would have had to borrow one. And
`ExitEstimate` carries `Temperature` beside the extrapolation state: it is the
temperature estimate of the last station that converged, which the exit loop used to
read from that station and pass down, and carrying it here keeps
`AreaRatioIteration.At` at the six parameters the root's code shape allows. The
station's verdict is written by the iteration itself (`NotConverged` for
`NeverSupersonic` and for `NotMet`), so the exit loop reads one thing — the station
status — as it did before.

⚠ 2026-09-15: the reason is the estimate, not the count: the temperature and the
extrapolation state are both what the next exit starts from (Constraints); that
`AreaRatioIteration.At` stays at six parameters follows from it.

Decisions taken with the review of 2026-09-14:

- **A station that never went supersonic is `NotConverged`.** The area-ratio iteration
  accepted a station whose correction was never computed, because its acceptance test
  read an initial value of zero (the review's F-PF-01). The outcome is now a value:
  `NeverSupersonic` ends the station as `NotConverged`, as `API.md` always said, and
  the subsonic step of `0.1` in `ln(p_c/p_e)` is the named constant `SubsonicStep`. No
  fixture reaches the path; the tests node drives it through `AreaRatioIteration` from
  an estimate deep on the subsonic side.
- **Two velocity formulas, both named.** `u = sqrt(2(h_c − h))` was written five times,
  once clamped at zero for a pressure-ratio station. `StationFigures.Velocity` and
  `StationFigures.VelocityClamped` are the two, extracted verbatim; whether a negative
  radicand should be a clamp or a `NotConverged` station is one rule with the outcome
  above and is decided in a later session with its own test, not inside the
  decomposition.
- **The frozen chamber keeps its four lines: a declared deviation (AGENTS.md §12) from
  the one-source rule of the clean-code criteria (A5) and from this node's own
  taboo.** In `FrozenAtChamber` flow the chamber's `γ_s = Cp/Cv` and
  `a = sqrt(γ_s R T/M)` are computed here, a second time in the tree, because the
  reference reports the frozen exponent and sound speed on an otherwise equilibrium
  chamber state, which `Equilibrium`'s frozen solve cannot produce without also
  freezing the heat capacities. What lifts it: a public frozen-state helper in
  `Equilibrium`'s `MixtureProperties`, a root decision once that node's decomposition
  has landed. Until then `ChamberSolve` carries the four lines verbatim.
- **`InternalsVisibleTo` for the tests node** is added to the project, as `Transport`
  and `Equilibrium` have it, so that the stages are testable directly.
- **Size.** No method over 60 lines and no control flow nested deeper than 3 in every
  stage. `ExitStations` hands the station assigned by pressure ratio to
  `PressureRatioStation`, as it hands the one assigned by area ratio to
  `AreaRatioIteration`, and keeps the loop, the dispatch, the estimate chain and the
  case status, none of them a formula. Its efferent coupling by the dependency check's
  walk is expected at the root's limit of 14; above it, `ExitStations` is this node's
  composition root of the exits, its measured figure written into its row.

  ⚠ 2026-09-14: this bullet stood "no composition-root exception is expected,
  `ExitStations` staying at or under Ce 10 with `AreaRatioIteration` split from it".
  The estimate counted the names in the source. Measured by the dependency check's
  walk, which also counts the types of the fields a body reads and of the members it
  calls, the decomposition gave `ExitStations` 16, `ChamberSolve` 14,
  `AreaRatioIteration`, `RocketSolver` and `ThroatSearch` 13 and `StationSolve` 12.
  The root recalibrated its limit to 14 on that walk; `ExitStations` is the one stage
  above it.
- **The descriptors keep their constructors** (added 2026-09-14). `RocketProblem` (7
  parameters) and `RocketResult` (7) mirror, one argument per field, the case the
  kernel reads and the views the solver writes into, as `API.md` publishes them;
  grouping them would move the contract and re-emit the kernels. They are this node's
  declared exception to the parameter rule, on the root's condition that every
  creation names its arguments, wherever in the tree it stands; a scan of the
  construction sites found every one positional.

## Shape exceptions

The rows below are this node's declared exceptions to the root's code-shape constraint,
in the form the protocol tests node reads; their reasons are decisions of `## Structure`.

| Where | Rule | Measured | Reason |
|---|---|---|---|
| `RocketProblem.RocketProblem` | parameters | 7 | mirrors, one argument per field, the case the kernel reads, as `API.md` publishes it; grouping it would move the contract and re-emit the kernels (the decision "The descriptors keep their constructors"); every creation names its arguments |
| `RocketResult.RocketResult` | parameters | 7 | mirrors, one argument per field, the views the solver writes into, as `RocketProblem` above |

No type of this node names more than 14 distinct types of the tree by the dependency
check's walk (`ExitStations` and `ChamberSolve` tie at 14, the ceiling): the Size
bullet's composition-root exception, reserved above for `ExitStations` before
`PressureRatioStation` was split from it, is not claimed, and this node needs no
efferent-coupling row.

## Acceptance criteria

- [x] 2026-09-12 — For the reference rocket cases of the four propellants and
      RP-1311 example 8 (LOX/LH2) and example 12 (MMH/NTO, equilibrium and frozen with
      freezing at the throat), the chamber, throat and exit temperatures, pressures,
      `M`, `γ_s`, sound speed, Mach, `c*`, `C_F`, `Isp` and `Ivac` agree with the
      fixtures within the tolerance table; the list of compared fields is generated
      from the fixture. `Performance.Tests`,
      `RocketFixtureTests.TheRocketCaseReproducesTheReference` over the enumerated
      `cases/rocket` directory (89 that day): every numeric station output mapped by name to
      a field of `MixtureState` or `PerformanceFigures`, plus every listed mole
      fraction; left out by the fixtures node's caveats: the reference's `cv` at frozen
      stations and its gas-phase frozen heat capacities at stations with condensed
      species and transport on; the one subsonic station of example 8 is outside
      version 1. The `pressure` tolerance was calibrated to the reference's own
      iteration residual (the fixtures node's criteria).
- [x] 2026-09-12 — Exit stations requested by pressure ratio reproduce the reference
      area ratios, and stations requested by area ratio reproduce the reference
      pressure ratios: part of the same comparison (`areaRatio` at the pressure-ratio
      stations of examples 8 and 12, `pressureRatio` and `pressure` at every area-ratio
      station).
- [x] 2026-09-12 — Frozen flow at the throat and at the chamber both reproduce the
      reference: the same test over the `frozenAtChamber` and `frozenAtThroat`
      propellant cases and example 12 (`nfz = 2`), enumerated from the fixture directory.

      ⚠ 2026-09-15: this bullet stood "the 15 `frozenAtChamber` and 40 `frozenAtThroat`
      propellant cases". `frozenAtThroat` was 35 propellant cases (plus example 12)
      already on 2026-09-12, the date of this tick, at every commit the repair review
      checked (BASE and 7661ea9 alike): the count was not a figure that went stale with
      time, it was wrong when written. Found by the repair review (R-Performance-7); the
      bullet now points at the enumerated fixture directory instead of a typed count
      that can drift again.
- [x] 2026-09-14 — The invariants' tolerances (entropy, sonic condition, area ratio,
      frozen composition bit for bit, velocity from the energy equation) hold for every
      converged case of the enumerated rocket fixtures, one test per invariant:
      `InvariantTests.TheThroatIsSonic`,
      `InvariantTests.EntropyIsConstantAlongTheNozzle`,
      `InvariantTests.VelocityFollowsTheEnergyEquation`,
      `InvariantTests.AssignedAreaAndPressureRatiosAreMet`,
      `InvariantTests.TheCompositionIsFrozenAfterTheFreezingStation`; green on
      the decomposed code at `5cb2664`. Re-dated from 2026-09-12, when one theory,
      `InvariantTests.Entropy_sonic_throat_area_ratio_and_frozen_composition_hold`,
      held them over the 89 files of that day: the tests node split it (its F-TK-06
      and F-TK-07) and left the typed count out (F-TK-03), and this node's code was
      decomposed on this date, which a tick of an earlier date cannot prove
      (AGENTS.md §6).
- [x] 2026-09-12 — An area ratio below 1 returns `AreaRatioInvalid` for that station
      and leaves the other stations unaffected:
      `InvariantTests.AnAreaRatioBelowOneFailsItsStationOnly` (and
      `APressureRatioNotAboveOneFailsItsStationOnly`,
      `ACaseWithoutExitsGivesTheChamberAndTheThroat`,
      `ANonPositiveChamberPressureIsInvalidInput`).
- [x] 2026-09-12 — Runs unchanged inside an ILGPU kernel on the CPU accelerator with
      the same results as the host call: `KernelEqualityTests.KernelAndHostGiveTheSameBits`
      over the batches of fixtures sharing a table and an exit layout, enumerated from the
      fixture directory (states, figures, moles and statuses bit for bit).
- [x] 2026-09-14 — The decomposition of 2026-09-14 (`## Structure`): every type of the
      node within the root's code-shape constraint. The largest methods,
      `AreaRatioIteration.At` and `ThroatSearch.At`, are within the root's limit of 60
      lines of code (`ShapeTests.No_method_spans_more_than_60_lines`); before the
      decomposition `RocketSolver.Solve` alone was 238 lines in a 326-line file. The
      largest type is under 120 lines (`Carriers.cs`, seven small carriers, none of
      them individually near the limit); before, the single `RocketSolver` type was
      326 lines. Confirmed by the tree-wide inventory (nothing of this node in its
      list of types ≥ 250 or methods ≥ 60 lines) and by `Protocol.Tests` (9 of 9
      green, run before and after this node's work; its `ShapeTests` does not exist
      yet and re-measures this over the tree when that node has it). The
      public surface is unchanged: `Protocol.Tests.SurfaceTests` green against the
      unchanged `PublicSurface.approved.txt`; every new type of the decomposition
      (`RocketContext`, `ChamberReference`, `ThroatReference`, `ExitEstimate`,
      `ExitOutcome`, `StationFlow`, `StationRequest`, `ChamberSolve`, `ThroatSearch`,
      `ExitStations`, `AreaRatioIteration`, `StationSolve`, `StationFigures`) is
      `internal`, reached by the tests node only through the new
      `InternalsVisibleTo`. Every rocket fixture is bit for bit as at `8e36a27` on the
      CPU accelerator: `BitSnapshotTests` green against `Bits.approved.txt` recorded
      at step 0 (`c038877`) and unmoved since, through every later step including the
      tests-node refactor of the criterion below.
      `KernelEqualityTests` green (post its own F-TK-07 split). Every criterion above
      still green, and every criterion of `tests/Performance.Tests/BOOT.md`: the full
      fast suite, `dotnet test APThermo.sln --filter
      "Category!=LongRunning"` with `APTHERMO_NO_CUDA=1`, 2632 tests, 0 failed, 0
      skipped. The execution tests node's CUDA sweep and throughput benchmark are
      `Category=LongRunning`; the 2026-09-14 coding session's instructions directed
      leaving them to the orchestrator after the merge, so they were not run then —
      the one part of this criterion not verified that session.

      ⚠ 2026-09-14: the parenthetical on `Protocol.Tests` first read "9 of 9 green,
      `ShapeTests` included". The protocol tests node had no `ShapeTests` then (its
      Shape level is still planned): the nine were the existing reflection checks,
      and the shape figures above come from the tree-wide inventory.

      ⚠ 2026-09-15: this bullet named `AreaRatioIteration.At` at 53 physical lines
      (`ThroatSearch.At` next, at 52 physical lines): the 53 came from the tree-wide
      inventory (`inventory.py` at `5281b7e`), where `ShapeMeasures` did not yet
      exist, and it still counted blank and comment lines toward the span. `At` was
      53 lines only because
      `a2a891b`, later the same day, split its verdict into a separate `Close` to fit
      the then-current physical-line rule; the repair review found the split
      count-driven, writing a station's extrapolation state before its verdict for no
      reason the code itself states (R-Performance-1). Commit `9a8ee68` (2026-09-15)
      merges `Close` back into `At`/`Accept`, restoring the shape this bullet's own
      tick predates: `ShapeTests.No_method_spans_more_than_60_lines` now holds both
      methods within the root's 60-line-of-code limit.
- [x] 2026-09-14 — An exit station that never leaves the subsonic side of the sonic
      point is `NotConverged` and its neighbours are `Ok`:
      `Performance.Tests.SubsonicStationTests.AStationThatNeverLeavesTheSubsonicSideIsNotConverged`
      drives `AreaRatioIteration` (through `InternalsVisibleTo`) from an estimate two
      units of `ln(p_c/p_e)` below the throat's, where the twenty subsonic steps of
      `SubsonicStep` cannot reach the sonic point, and reads the station's status and
      its neighbours'. Seen red once against the acceptance test of `8e36a27`, which
      read the never-written correction as zero and returned the station `Ok`; green
      with `NeverSupersonic` ending the station as `NotConverged`. No fixture reaches
      the path: the bit snapshot of all 98 rocket fixtures did not move.
- [x] 2026-09-14 — The pressure-ratio station is a stage of its own (`## Structure`,
      Size): `PressureRatioStation` holds what `ExitStations.AtPressureRatio` held,
      moved with every expression in its form and its order of evaluation (its own
      file, `PressureRatioStation.cs`); `ExitStations` keeps the loop, the dispatch,
      the estimate chain and the case status, holding no formula. Efferent coupling
      by the dependency check's walk, measured by the scratch tool that reproduces
      it (`AGENTS.md` §13; the tool used for the root's recalibration to 14) run over
      this step's build: `ExitStations` 14, `ChamberSolve` 14, `AreaRatioIteration`
      13, `RocketSolver` 13, `ThroatSearch` 13, `StationSolve` 12,
      `PressureRatioStation` 11 — every stage at or under the root's limit of 14, so
      `ExitStations` does not need the composition-root exception the Size bullet
      allowed for, and this node needs no efferent-coupling row.

      ⚠ 2026-09-14: the last clause stood "this node needs no `## Shape exceptions`
      table". It was true when written, of the coupling rule this criterion measures;
      later the same day the descriptors' constructors became a declared exception to
      the parameter rule (the decision "The descriptors keep their constructors"), and
      the table now carries their two rows, while no coupling row is needed.
      `BitSnapshotTests.EveryRocketFixtureGivesTheRecordedBits` green with
      `Bits.approved.txt` unmoved (byte for byte before and after this step) and
      `KernelEqualityTests` green: `dotnet test tests/Performance.Tests`, 699 tests,
      0 failed, 0 skipped. The public surface is unchanged
      (`Protocol.Tests.SurfaceTests` green; the new type is internal). The execution
      tests node's fast set green on CUDA on the reference machine: `dotnet test
      tests/Execution.Tests --filter "Category!=LongRunning"`, no `APTHERMO_NO_CUDA`,
      41 tests, 0 failed, 0 skipped.
- [x] 2026-09-14 — Every creation of `RocketProblem` and `RocketResult` in the tree
      names its arguments (the decision "The descriptors keep their constructors"), the
      protocol tests node's named-construction fact green once it exists; the tests
      node's bit snapshot unchanged. A scan of every `new T(…)` and `T x = new(…)` in
      `src/` and `tests/` (a script outside the tree) finds four sites of each of the two
      types, in `Execution`'s `Kernels`, `Execution.Tests`' `HostSolves` and
      `Performance.Tests`' `KernelEqualityTests` and `RocketCase`, every argument named;
      the builds of `Execution`, `Execution.Tests` and `Performance.Tests` after the
      change carry the IL of the builds before it, method by method, string literals
      compared by value (one differs: the source path a test embeds at compile time),
      so no argument binds to another parameter; `Performance.Tests` (699) and the fast
      set of `Execution.Tests` (41) green; `tests/Performance.Tests/Bits.approved.txt`
      unchanged (blob `5aa32f2b` before and after). The fact,
      `ShapeTests.Every_wide_constructor_is_called_with_named_arguments`, is designed
      and not yet written; it takes over as the evidence when it is.
- [x] 2026-09-15 — Every ticked criterion above re-verified on the decomposed and
      repaired code at `62cd99e`: its tests green in the full suite
      (`APTHERMO_NO_CUDA=1`, every category, 3037 tests, none skipped), and
      CUDA-category evidence on the reference machine (`tests/Execution.Tests`, 41,
      and the long-running sweep and throughput tests).

## Taboos

- No second equilibrium solver or mixture-property formula here: call `Equilibrium`.
- No performance figure computed in more than one place; no conversion to seconds
  (that is the command line's convenience).
- No `float`, no exceptions, no allocations: kernel code.
- No finite-area chamber approximations smuggled in under a flag: version 1 is
  infinite-area only, and a later version gets its own design session.
