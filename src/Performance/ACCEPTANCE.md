# Acceptance criteria

- [x] 2026-09-12 — The reference rocket cases (the four propellants, RP-1311 examples 8
      and 12 with freezing at the throat) agree with the fixtures within the tolerance
      table, the compared fields generated from the fixture: `Performance.Tests`,
      `RocketFixtureTests.TheRocketCaseReproducesTheReference` over the enumerated
      `cases/rocket`; left out: the fixtures node's caveats and the subsonic station of
      example 8. → HISTORY.md#crit-reference-rocket-cases
- [x] 2026-09-12 — Exit stations requested by pressure ratio reproduce the reference
      area ratios, and stations requested by area ratio reproduce the reference
      pressure ratios: part of the same comparison (`areaRatio` at the pressure-ratio
      stations of examples 8 and 12, `pressureRatio` and `pressure` at every area-ratio
      station).
- [x] 2026-09-12 — Frozen flow at the throat and at the chamber both reproduce the
      reference: the same test over the `frozenAtChamber` and `frozenAtThroat`
      propellant cases and example 12 (`nfz = 2`), enumerated from the fixture directory.

      ⚠ 2026-09-15: was "the 15 `frozenAtChamber` and 40 `frozenAtThroat` propellant
      cases", now the enumerated directory → HISTORY.md#crit-frozen-counts
- [x] 2026-09-14 — The invariants' tolerances (entropy, sonic condition, area ratio,
      frozen composition bit for bit, velocity from the energy equation) hold for every
      converged case of the enumerated rocket fixtures, one test per invariant:
      `InvariantTests.TheThroatIsSonic`,
      `InvariantTests.EntropyIsConstantAlongTheNozzle`,
      `InvariantTests.VelocityFollowsTheEnergyEquation`,
      `InvariantTests.AssignedAreaAndPressureRatiosAreMet`,
      `InvariantTests.TheCompositionIsFrozenAfterTheFreezingStation`; green on
      the decomposed code at `5cb2664`; re-dated from 2026-09-12
      → HISTORY.md#crit-invariants-redated
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
- [x] 2026-09-14 — The decomposition of 2026-09-14 (`## Structure`) keeps every type
      within the code-shape constraint (`ShapeTests.NoMethodSpansMoreThan60Lines`),
      the public surface (`Protocol.Tests.SurfaceTests`) and every rocket fixture's bits
      as at `8e36a27` (`BitSnapshotTests` against `Bits.approved.txt` recorded at
      `c038877`; `KernelEqualityTests`); the fast suite, 2632 tests, green.
      ⚠ 2026-09-14: was "9 of 9 green, `ShapeTests` included", now nine reflection
      checks → HISTORY.md#crit-decomposition
      ⚠ 2026-09-15: was `At` at 53 lines, now within 60 → HISTORY.md#crit-decomposition
- [x] 2026-09-14 — An exit station that never leaves the subsonic side of the sonic
      point is `NotConverged` and its neighbours are `Ok`:
      `Performance.Tests.SubsonicStationTests.AStationThatNeverLeavesTheSubsonicSideIsNotConverged`
      drives `AreaRatioIteration` from an estimate two units of `ln(p_c/p_e)` below the
      throat's; red against `8e36a27`, no fixture reaches the path.
      → HISTORY.md#crit-never-supersonic
- [x] 2026-09-14 — The pressure-ratio station is a stage of its own (`## Structure`,
      Size): `PressureRatioStation` holds what `ExitStations.AtPressureRatio` held, each
      expression in its form and order; efferent coupling by the dependency check's
      walk: `ExitStations` 14, `ChamberSolve` 14, `AreaRatioIteration`, `RocketSolver`,
      `ThroatSearch` 13, `StationSolve` 12, `PressureRatioStation` 11, so no coupling
      row; `BitSnapshotTests.EveryRocketFixtureGivesTheRecordedBits` and
      `KernelEqualityTests` green, `Bits.approved.txt` unmoved.
      ⚠ 2026-09-14: was "needs no `## Shape exceptions` table", now two rows
      → HISTORY.md#crit-pressure-ratio-station
- [x] 2026-09-14 — Every creation of `RocketProblem` and `RocketResult` in the tree
      names its arguments (the decision "The descriptors keep their constructors"): four
      sites of each, the IL of the builds equal before and after;
      `ShapeTests.EveryWideConstructorIsCalledWithNamedArguments` takes over as the
      evidence; `Bits.approved.txt` unchanged. → HISTORY.md#crit-named-arguments
- [x] 2026-09-15 — Every ticked criterion above re-verified on the decomposed and
      repaired code at `62cd99e`: its tests green in the full suite
      (`APTHERMO_NO_CUDA=1`, every category, 3037 tests, none skipped), and
      CUDA-category evidence on the reference machine (`tests/Execution.Tests`, 41,
      and the long-running sweep and throughput tests).
- [x] 2026-09-27 — Audit findings F1, F2, F3, F5, F6 and the entropy check, coded at
      `e1318c2` on the fixtures `efe7d7e`, `8c1c1cb`, each fact seen red once: the
      plateau-edge throat, six audit cases `Ok`
      (`ThroatFixtureTests.TheThroatCaseReproducesTheReference`, 10/10) with the three
      properties of `ThroatPlateauEdgeTests.cs` and
      `TheExample13SweepNeverEndsThroatNotFound`; `AnAreaRatioOfExactlyOneIsInvalid`,
      `AnAreaRatioJustAboveOneIsOkAndSupersonic`,
      `EveryAcceptedAreaRatioExitIsSupersonic`,
      `AnExitAtTheThroatsOwnAreaRatioIsNeverAcceptedOnAStaleSupersonicVerdict`,
      `AnUndefinedFlowModelIsInvalidInput`, `PressureRatioMatchesTheSolvedPressure`,
      `EntropyCheckTests`; no bit moves; build 0/0, `Performance.Tests` 937/937.
      ⚠ 2026-09-28: was a throat at the second maximum passing both plateau facts, now
      caught by the oracle fact below → HISTORY.md#crit-audit-2026-09-27
- [x] 2026-09-28 — The second hidden-defect audit (Performance: F1, F3, F4, F5, O1, O2;
      the guards part's O3, O4) is closed, each fact red at `5a732f0`, in
      `SecondAuditFixTests`: F1 `TheThroatIsTheOraclesFirstMaximum` (every rocket and
      throat fixture), `…OverTheApHtpbAlBand`, `…OverTheElementMixtureCases` (the
      throat's `ρu` not below `MassFluxOracle`'s); F3
      `ThePlateauEdgeAcceptsTheChamberSideOnTheAuditsLi2OAndBeOCases`; F4
      `TheGammaOneLimitProducesABracketInsteadOfThroatNotFound`,
      `TheThroatSearchNeverEndsThroatNotFoundAcrossTheLiOHPlateauAt7MPa` (50 cases;
      `RocketSolver.GammaOneTolerance` 1e-6 against a measured 5.53e-10); F5
      `AFrozenAtThroatExitAtOrAboveTheThroatIsInvalidInput`,
      `EveryOkStationCarriesOnlyFiniteFigures`; O1, O2
      `ABisectionAcceptedWithinTheSonicToleranceIsOk`,
      `TryRatioNeverLeavesAStaleRatioOnFailure`; O3, O4 the example-13 sweep asserts its
      count of `Ok` cases, `PlateauEdgeCases` is generated from the `throat` family. The
      bit move: `rp1311-example13` only (`tests/Performance.Tests/BOOT.md`). Evidence:
      build 0/0, fast suite 5396 of 5396, lint 0/0, CUDA 144 of 144 at `417bff0`.
      ⚠ 2026-09-28: was "the edge throat is single-phase and subsonic", true of the
      committed fixtures only → HISTORY.md#crit-second-audit
- [x] 2026-09-28 — The third audit pass of 2026-09-28 (part 2: finding 1 and the
      boundary cap) closed: `ThroatBracketSearch.AcceptPlateauEdge` (`1e-10·4^k` up to
      `1e-4`), `UpstreamChokeCheck.MaxPhaseBoundaries` (8) with `ThroatNotFound` on
      exhaustion
      (`ThirdPassFixTests.AWalkThatMeetsMoreBoundariesThanTheCapEndsThroatNotFound`);
      `ThirdPassFixTests.TheLi2OBandAt0Point3MPaNeverEndsThroatNotFound` (h 3.05625 to
      3.36875 MJ/kg, 26 cases) and `…At3MPa…` (h 2.15 to 2.2015625 MJ/kg, 12 cases):
      every case `Ok`, single-phase, subsonic, passing
      `SecondAuditFixTests.AssertFirstMaximum`, red at `c02e14d` (21 of 26, 10 of 12);
      no vacuous pass: `AssertFirstMaximum` fails on a non-`Ok` chamber or throat
      unless declared, each band is asserted non-empty; the fixture
      `li2o-throat_pc3MPa_h2.20625MJkg`; no `Bits*.approved.txt` moves; build 0/0,
      `Performance.Tests` 1429 of 1429.
      ⚠ 2026-09-28: was "the 3 MPa band's upper bound 2.2109 MJ/kg", now 2.2015625
      → HISTORY.md#crit-third-pass
- [x] 2026-09-30 — The rocket kernel's compile is bounded (the root's criterion of that
      date): `StationSolve.At` carries the attribute, read from the compiled method by
      `Performance.Tests.CompileSizeTests.TheStationSolveIsNotInlined`, red without it;
      every bit snapshot and CEA tolerance test green, no `Bits*.approved.txt` moved;
      the compile's figures are the execution node's (8.66 GB without, 0.34 GB with).
      Evidence: `2548e82`, `Performance.Tests` 1429 of 1429, lint 0/0; the CUDA proof is
      the root's. → HISTORY.md#crit-compile-size
- [x] 2026-10-02 — The throat's stop is a decision at `1e-8` and two momentum steps (the
      owner's decision, `## Constraints`), in the momentum loop and the single-set
      bisection: `ThroatToleranceTests.TheThroatDecidesAtOneEMinusEightAndTakesTwoMomentumSteps`,
      red for `1e-11` and for no tail; every CEA tolerance test green; 84 rocket fixtures
      re-approved in Performance and Problems, 9 rocket documents in Cli and the `rocket`
      example's output, Windows (`1828239`) and Linux (`15975ff`, keys equal); on the
      reference machine in Release every `Cuda`/`BitSnapshot` fact green, the AP/HTPB/Al
      families included, and the 100 000-case sweep with 35 of 400 000 stations at
      different Newton counts (696 at `1e-11`, guard 400). Merged `701d941`.
      ⚠ 2026-10-02: was ticked for `1e-11` alone → HISTORY.md#crit-throat-1e-11

- [x] 2026-11-14 — A05/A36 and the final area residual: AuditGuardTests and
  AreaRatioResidualTests (Performance.Tests) pass. The old source failed the
  distinct-set, ordinary-throat and frozen-failure controls; NoGasPhase's T,p
  exception is checked separately at the finalization boundary. Exact identities
  include every scratch slot and the largest species-table keys.
- [x] 2026-11-14 — The phase-boundary cap uses PhaseBoundaryWalk in Verify;
  PhaseBoundaryWalkTests rejects the unresolved stream at the cap. Replacing its
  refusal with Ok makes the negative control fail; reaching the candidate is green.
- [ ] The changed rocket program passes the orchestrator's CUDA/CPU equality and
  architecture runs, without blind approval of any moved bit snapshot.

- [x] 2026-11-14 — Integration coupling corrections: the full ShapeTests run reports
  no Performance violation after PhaseBoundaryEnd encapsulation and moving station
  payload/throat-figure writes into StationFigures. The existing formula-free walk
  exception is measured at 18 with its concrete types recorded. Evidence:
  `SCRATCH/audit/shape-before.log`, `shape-after.log` in audit-performance.
  The latter still fails only on stale foreign Solver/DeadEndRecheck findings.
- [x] 2026-11-14 — Refactoring leaves the rocket bits unchanged: full CPU Performance
  run 1489 pass, one pre-existing rp1311-example13 snapshot mismatch; restoring all
  five changed source files to pre-refactor HEAD reproduces the same cb1f950c... hash
  (approved 76703e3f...). No approved file changed. Evidence
  `SCRATCH/audit/performance-ce-full.log`, `performance-ce-old-source-bits.log`.
  Root must resolve that already-present numerical delta at integration.
