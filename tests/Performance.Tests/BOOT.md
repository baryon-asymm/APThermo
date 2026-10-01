# BOOT.md — Performance.Tests

## Purpose

The definition of what "`Performance` is ready" means.

| Level | What it checks | Against what (source of truth) | State |
|---|---|---|---|
| L0 | the invariants on a converged case: constant entropy, sonic throat, area ratio met, frozen composition, velocity from the energy equation; status on invalid exits and inputs | the invariants' tolerances | ✅ |
| L1 | rocket cases of the fixtures node (LOX/LH2 example 8, MMH/NTO example 12 equilibrium and frozen, the four reference propellants): stations, `c*`, `C_F`, `Isp`, `Ivac`, area and pressure ratios, compositions | the fixtures node's reference outputs and its tolerance table | ✅ |
| L1 | the solver inside a CPU-accelerator kernel gives the same bits as the host call | the host call | ✅ |
| L0 | an exit station that never leaves the subsonic side is `NotConverged` and its neighbours `Ok`, driven through the `AreaRatioIteration` stage from an estimate deep on the subsonic side (2026-09-14) | the `API.md` of `Performance` | ✅ (2026-09-14) |
| L0 | `StationSolve.At` carries `NoInlining`, so ILGPU compiles one copy of the equilibrium solve for the rocket program's seven call sites (2026-09-30, `CompileSizeTests.TheStationSolveIsNotInlined`; the compile's allocation bound is the execution tests node's) | the compiled method's implementation flags | ✅ (2026-09-30) |
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
  fails the test with instructions, as the surface snapshot does; a line of the
  snapshot that names no current fixture fails a test of its own instead of staying
  silent (`EveryApprovedLineNamesARocketFixture`, 2026-09-15).

  ⚠ 2026-09-17: was one snapshot file, now a Windows and a Linux record, picked by
  `Harness.ApprovedSnapshot.ApprovedPathFor` → HISTORY.md#bits-per-platform

  ⚠ 2026-09-19: was bits as a record of the platform, now of the reference machine:
  `EveryRocketFixtureGivesTheRecordedBits` and `EveryApprovedLineNamesARocketFixture`
  carry `[Trait("Category", "BitSnapshot")]`, filtered out of the hosted fast suite
  → HISTORY.md#bits-reference-machine
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
- The batch struct of the kernel test is public; a batch is a family of fixtures
  sharing a table and an exit layout.

  ⚠ 2026-09-15 (distribution phase): was "ILGPU compiles kernels only over public
  parameter types", now `InternalsVisibleTo("ILGPURuntime")` suffices; the struct stays
  public → HISTORY.md#kernel-struct-public

- **No NaN-blind predicate** (2026-09-26, the guards audit of 2026-09-26 (`Audit 5`, the hidden-defect audit's fifth part), F5 and F6):
  - Every tolerance predicate of this node is written so that NaN fails it
    (`!(|a − b| <= tol)`), or asserts finiteness first. This covers
    `RocketInvariants` and `StationComparison`, and every other predicate the coder
    finds by searching for `> ` against a tolerance.
  - A fact asserts `Velocity == SpecificImpulse` and both finite at every `Ok` station
    of every fixture, since the fixtures carry no `velocity` to compare with.
  - The frozen-station cv skip applies only where the reference shows the defect's
    signature: `cvFrozen` and `cvEquilibrium` 0, or the freezing station's values.
    Anywhere else the fields are compared.
  - ⚠ `state.Velocity = NaN` at every station left this node 601/601 green, and a
    fixture's frozen exit cv edited from 0 to 2500 and 9999 left it green too.

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

- [x] 2026-09-14 — L0 green: `InvariantTests.TheThroatIsSonic`,
      `EntropyIsConstantAlongTheNozzle`, `VelocityFollowsTheEnergyEquation`,
      `AssignedAreaAndPressureRatiosAreMet`,
      `TheCompositionIsFrozenAfterTheFreezingStation` over the enumerated
      rocket fixture directory, plus `AnAreaRatioBelowOneFailsItsStationOnly`,
      `APressureRatioNotAboveOneFailsItsStationOnly`,
      `ACaseWithoutExitsGivesTheChamberAndTheThroat`,
      `ANonPositiveChamberPressureIsInvalidInput`. (Re-dated from 2026-09-12:
      the F-TK-06 split below renamed the theory; the hand-typed file count is gone,
      F-TK-03.)
- [x] 2026-09-14 — L1 green for every rocket fixture case, equilibrium and frozen:
      `RocketFixtureTests.TheRocketCaseReproducesTheReference` over the enumerated
      directory; `KernelEqualityTests.KernelAndHostGiveTheSameBits` over its
      batches, each a family of fixtures sharing a table and an exit layout. (Re-dated
      from 2026-09-12: the hand-typed file and batch counts are gone, F-TK-03.)
- [x] 2026-09-14 — Every check proven non-degenerate once, by mutation runs, each
      restored afterwards: a reference `Isp` raised by 0.1 % at one exit (that fixture
      case red); the frozen-at-chamber fixtures run as shifting flow (every
      frozen-at-chamber fixture red); the kernel given a different chamber temperature
      estimate (every batch's `KernelEqualityTests` test red); area ratios below 1
      accepted by the solver (that fixture case red). Re-dated from 2026-09-12.
      ⚠ 2026-09-14: was the solver's sonic and area-ratio tolerances loosened to `1e-2`
      and `4e-2` (88 of 89 cases red), now `RocketSolver.TightTolerance` loosened, the
      report-tolerance fallback being dead code → HISTORY.md#crit-mutations
- [x] 2026-09-14 — Bits level green:
      `BitSnapshotTests.EveryRocketFixtureGivesTheRecordedBits` over the
      enumerated rocket directory against `Bits.approved.txt`, recorded before any
      code of the decomposition moved (the code of `8e36a27`: the two commits between
      it and the snapshot changed documents only) and unchanged after it. Seen red
      twice on the reference machine, each restored afterwards: the `0.5` of the
      throat's initial pressure estimate (`RocketSolver`, equation 6.15) raised by one
      ulp to `0.5000000000000001` — every one of the enumerated fixtures red; one line
      removed from `Bits.approved.txt` — that fixture red, naming it, with the
      instruction to approve, and the other fixtures green.
- [x] 2026-09-15 — Bits level, the other direction: `BitSnapshotTests.EveryApprovedLineNamesARocketFixture`
      builds its keys from `FixtureFiles.Enumerate("rocket")` (no solve) and fails on
      any key `Harness.ApprovedSnapshot.StaleKeys` reports, naming it. Repair-review
      finding R-Performance.Tests-1: until this fact existed, a deleted or renamed
      fixture left its line in `Bits.approved.txt` untouched and unread, green by
      silence. Seen red once, restored afterwards: a fabricated line
      (`tests/Fixtures/cases/rocket/MUTATION-GHOST-FIXTURE.json`, a zero hash)
      appended to `Bits.approved.txt` turned this fact red naming exactly that key;
      the line removed again, `git hash-object tests/Performance.Tests/Bits.approved.txt`
      unchanged (`5aa32f2bbf679cdd0f47749b0780059ba89faa62`).
- [x] 2026-09-14 — The never-supersonic outcome:
      `SubsonicStationTests.AStationThatNeverLeavesTheSubsonicSideIsNotConverged`
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
      `EnergyEquation`, `AssignedExit`, `FrozenComposition`), each under twenty lines
      of code and nesting at most two, with the two previously inline tolerances (the energy
      equation, the pressure ratio) promoted to `VelocityTolerance` and
      `PressureRatioTolerance` beside the three existing ones and their origin named.
      `InvariantTests` becomes one test per invariant (`TheThroatIsSonic`,
      `EntropyIsConstantAlongTheNozzle`, `VelocityFollowsTheEnergyEquation`,
      `AssignedAreaAndPressureRatiosAreMet`,
      `TheCompositionIsFrozenAfterTheFreezingStation`) over the enumerated
      rocket fixture directory, each a three-line body. Non-degeneracy: with
      `RocketSolver.TightTolerance` loosened from `1e-10` to `1e-2` (the ⚠ above, in
      place of the now-inert `SonicTolerance`/`AreaRatioTolerance`),
      `TheThroatIsSonic` turns red on every fixture and
      `AssignedAreaAndPressureRatiosAreMet` on every fixture that carries an
      exit, while `EntropyIsConstantAlongTheNozzle`,
      `VelocityFollowsTheEnergyEquation` and
      `TheCompositionIsFrozenAfterTheFreezingStation` stay green throughout —
      a sharper proof than the single combined theory gave, because entropy and the
      energy-equation identity are guaranteed by the equilibrium solve itself and the
      frozen copy is bit-exact, none of the three sensitive to the throat/exit
      search's own convergence. `KernelEqualityTests.KernelAndHostGiveTheSameBits`
      is split at its two seams into `Fill` (`RocketBatchBuffers`: allocates and
      uploads one batch) and `AssertSameBits` (the field-by-field comparison), each
      under 60 lines; the kernel given a different chamber temperature estimate still
      turns every batch red. The self-consistency comparisons of
      `AnAreaRatioBelowOneFailsItsStationOnly` name the `SelfConsistency`
      constant instead of an inline `1e-9` (F-TK-10). The hand-typed counts of files,
      flows and batches leave the criteria above; the enumerated directory is the
      list. Every mutation restored afterwards; the Bits level did not move (no
      `src/Performance` file changed for this criterion).

      ⚠ 2026-09-15: was "each under fifteen lines", now under twenty lines of code (the
      longest, `FrozenComposition`, 18) → HISTORY.md#crit-invariants-one-type
- [x] 2026-09-15 — The creation of this node's `RocketBatchViews` in
      `KernelEqualityTests` names its arguments, in the order of the parameters (the
      root's condition on a declared wide constructor, the row of
      `## Shape exceptions`); `ShapeTests.EveryWideConstructorIsCalledWithNamedArguments`
      now covers this on the merged tree: two sites tree-wide, this node's (the
      `RocketBatchViews` construction inside the `RocketBatchBuffers` constructor,
      re-verified in place after the R-Performance.Tests-4 cut below moved it) and
      the execution node's own type of the same name (inside `RocketPipeline.Run`),
      both fully named; the node's bit snapshot unchanged (`Bits.approved.txt` hash
      `5aa32f2bbf679cdd0f47749b0780059ba89faa62`, the fast suite 699/699 green that day).

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

      ⚠ 2026-09-15: was `ChemicalSystem` holding `ElementMoles` (now `Mixture` with
      `ReactantEnthalpy`; `ChemicalSystem` is `Elements`, `Products`) and the flat
      names kept as forwarding properties (now removed: every read site names
      `.Outcome.`, `.System.`, `.Mixture.` or `.Exits.`); 700/700 tests green
      → HISTORY.md#crit-rocket-inputs

- [x] 2026-09-15 — `RocketBatchBuffers` (`KernelEqualityTests.cs`) is built by its own
      constructor, `(Accelerator, SpeciesTable, IReadOnlyList<RocketInputs>)`, 3
      parameters, exactly as `RocketCase` next to it builds one case's buffers in its
      own constructor. The settable `{ get; set; }` properties and the `Fill` method
      that assigned them one by one from outside are gone: `Table`, `Views`,
      `Stations`, `Moles`, `Figures`, `StationStatus` and `Status` are now get-only,
      assigned once, inside the constructor. `AssertSameBits` is unchanged but for its
      caller: it still calls `.GetAsArray1D()` on `buffers.Stations`, `.Moles`,
      `.Figures`, `.StationStatus`, `.Status` itself, exactly as before the cut.
      `ShapeTests.NoTypeSpansMoreThan400Lines` and
      `ShapeTests.NoMethodSpansMoreThan60Lines` both hold for it; its own
      efferent coupling, recorded by `CouplingMeasures` (not limited — the root's
      coupling rule holds only for `src` types), fell to 13.

      This moved four call sites in `RocketCase.cs` from `inputs.ElementMoles` /
      `.ExitValues` / `.ExitKinds` / `.ReactantEnthalpy` to `inputs.Mixture.ElementMoles`
      / `inputs.Exits.Values` / `inputs.Exits.Kinds` / `inputs.Mixture.ReactantEnthalpy`,
      as part of the same commit as the `Mixture`/forwarding-properties cut above,
      which is why `RocketCase`'s own efferent coupling is noted here rather than left
      silent: `CouplingMeasures` puts it at Ce=19 today (`Mixture` and `ExitPlan` newly
      named — direct now, where the removed forwarding properties on `RocketInputs`
      used to hide them from `RocketCase`'s own body), up from Ce=17 at `5d1ccd3`, the
      pre-split source, by the same coupling-walk reasoning. The root limits the
      efferent coupling of the `src` types only, so a test type's figure is recorded,
      not limited.

      Verified: 700/700 tests green, `Bits.approved.txt` hash unchanged
      (`5aa32f2bbf679cdd0f47749b0780059ba89faa62`), protocol lint 0/0.

- [x] 2026-09-27 — The NaN and cv guards (Constraints). The audit's two mutations
      (`Velocity = NaN`; a frozen exit's cv edited in a fixture copy) each turn this
      node red.

      Predicates rewritten to fail on NaN (`!(|a − b| <= tol)` in place of `> tol`):
      `RocketInvariants.SonicThroat`, `ConstantEntropy`, `EnergyEquation` and the two
      branches of `ExitMismatch` (`AssignedExit`'s area-ratio and pressure-ratio
      checks). `SupersonicAreaRatioExits` (finding F3) already used the safe form
      (`!(mach >= 1.0)`) and needed no change; the two bit-exact checks
      (`PressureRatioMatchesTheSolvedPressure`, `FrozenComposition`) compare raw bits
      and were never blind to begin with. A new invariant,
      `RocketInvariants.VelocityEqualsSpecificImpulse`, asserts `Velocity ==
      SpecificImpulse` bit for bit and both finite at every `Ok` station, run as
      `InvariantTests.VelocityEqualsSpecificImpulseAtEveryOkStation` over every rocket
      fixture (`StationFigures.Write`, `src/Performance`, sets both from the same local
      variable, so the identity holds by construction — the fixtures carry no
      `velocity` field of their own to compare with).

      The frozen-station cv skip of `StationComparison.Compare` no longer fires on the
      `frozen` flag alone: `IsFrozenCvDefectSignature` also requires the reference's
      own `cvFrozen`/`cvEquilibrium` at that station to be exactly zero, or exactly the
      freezing station's own recorded value for the same field (`RocketInvariants.
      FreezingStationOf`, made `internal` so both types share the one mapping from
      `FlowModel` to the chamber or throat station index). Anywhere else the field is
      compared like any other.

      Evidence, each mutation applied through a temporary, uncommitted fact
      (`ZzGuardsAuditRedOnce.cs`, deleted before this commit) and seen red, never
      reverted into the tree:
      - every station's `Velocity` set to `double.NaN` on a solved
        `lox-lh2_of4_pc5MPa_frozenAtThroat` case: at least one of `SonicThroat`,
        `ConstantEntropy`, `EnergyEquation`, `AssignedExit`,
        `VelocityEqualsSpecificImpulse` now reports a violation (before this fix, all
        five stayed silent under the old `> tol` form and the bit-exact checks do not
        read `Velocity`);
      - the same fixture's frozen exit `cvFrozen`/`cvEquilibrium` edited in an
        in-memory copy of its reference JSON, from `0`/`0` to `2500`/`9999` (neither
        the defect's zero nor the freezing station's own value):
        `StationComparison.Compare` now reports a mismatch, where the old,
        unconditional `frozen && NotAtFrozenStations.Contains(name)` skip stayed
        silent.

      `APTHERMO_NO_CUDA=1 dotnet test tests/Performance.Tests --filter
      "Category!=LongRunning"`: 1035/1035 (up from 937 by the 98 fixture cases of the
      new invariant's theory), none skipped. `Bits.approved.txt` unchanged
      (`git hash-object`: `5aa32f2bbf679cdd0f47749b0780059ba89faa62`, the same figure
      this node's own 2026-09-15 criterion recorded). The protocol lint: 0 errors, 0
      warnings.
- [x] 2026-09-28 — The second hidden-defect audit's F1/F3/F4/F5/O1/O2 fixes (`627f815`)
      moved one line of `Bits.approved.txt`: `rp1311-example13` (the rocket family;
      `Problems.Tests/Bits.approved.txt` moved the same case's line, recorded there
      too). Investigated at the coordinator's review of 2026-09-28, which found the
      substituted F3/F4 evidence insufficient and asked for this case's own record.

      The oracle (`MassFluxOracle`, `tests/Performance.Tests/SecondAuditFixTests.cs`)
      finds exactly one local maximum of ρu between p/p_c 0.05 and 0.95: the refined
      peak sits at p/p_c 0.6137155045658974, ρu 10625.412618753851 (c* 1946.6794017…,
      matching the throat below to 9 figures). There is no second maximum: the audit's
      "no fixture has two maxima" holds for this case in that narrow sense.

      ⚠ What moves the bits is not F1's "first, not largest, maximum" rule (there is
      only one maximum to choose between) but the mechanism the same fix always runs
      afterwards, unconditionally, for every case (`ThroatSearch.At` → new call
      `UpstreamChokeCheck.Verify`): the ρu curve carries a slope discontinuity at p/p_c
      ≈ 0.6700–0.6705 (a melting-plateau boundary crossed between the chamber and the
      momentum search's own candidate at p/p_c 0.6137), so the candidate's and the
      chamber's condensed fingerprints differ, and `Verify` does not stand the
      candidate as found: it walks to that one boundary via `PhaseBoundaryLocator`,
      finds its low side shares the candidate's own fingerprint (no further boundary
      separates them), and calls `RestoreCandidate`, which re-solves the throat row
      at the candidate's own pressure before accepting it (its own doc comment: "need
      not be the candidate's state even when no earlier choke overrides it"). That
      re-solve is a genuine new floating-point path this case did not take before
      `627f815` (the whole `UpstreamChokeCheck` call is new), and it moves the state's
      last bits without moving the accepted pressure or any physical figure outside
      tolerance.

      ⚠ 2026-09-28, measured field by field: pressure, temperature and `GammaS` are
      bit-identical, the largest relative change of 41 fields is 4.378e-13 (the
      condensed `BeO(b)` mole fraction) → HISTORY.md#crit-example13-measured

      Old throat (before `627f815`, no `UpstreamChokeCheck` in the tree): p/p_c
      1.629424878440996⁻¹ = 0.613715559…, c* matching this node's own pre-`627f815`
      `Bits.approved.txt` line (superseded, not separately re-measured: the case's
      values, not its bits, are what this record is for). New throat (`627f815` and
      after, measured above): p/p_c 0.6137155597356388, c* 1946.679401702188. CEA
      reference (`tests/Fixtures/cases/rocket/rp1311-example13.json`, the throat
      station): pressureRatio (p_c/p_t) 1.629424878440996 (p/p_c 0.613715…, matching
      both), characteristicVelocity 1946.6824094405736. The tree's figure differs from
      the reference by 1.55e-6 relative, inside the fixture tolerance table's own
      `characteristicVelocity` and `pressureRatio` rows (`tests/Fixtures/tolerances.json`:
      relative 1e-4 each), which is what `RocketTests.TheRocketCaseReproducesTheReferenceEndToEnd`
      (Problems.Tests) already checks and was green throughout this investigation.

      A second, structural consequence found while gathering this evidence: the
      `throat` fixture family's own reference (`tests/Fixtures/generate/throat_scan.py`'s
      ternary-refined scan) is not guaranteed to land on the chamber side of a melting
      plateau's edge either — finding F3's own mechanism, in the reference instead of
      the tree — so `beo-h2o-throat_pc15MPa_h-11.06875MJkg`'s reference throat Mach is
      1.011285688885813, past the edge, while the tree's own (correct) throat stays at
      Mach 0.9655657887575096. `StationComparison.IsPlateauEdgeDivergence` (this node)
      skips that one station's field-by-field comparison, guarded on the reference's
      own recorded Mach being at or past 1 (never a blanket exemption), and
      `SecondAuditFixTests.ThePlateauEdgeAcceptsTheChamberSideOnTheAuditsLi2OAndBeOCases`
      checks that fixture's own chamber-side answer directly, without the reference.

      Evidence: `dotnet test tests/Performance.Tests` 1418/1418 green with the guard
      in place; `beo-h2o-throat_pc15MPa_h-11.06875MJkg` shown red once by temporarily
      removing the guard (`IsPlateauEdgeDivergence` forced `false`), 7 field
      mismatches (`cpEquilibrium`, `cvEquilibrium`, `gammaS`, `dlnVdlnT`, `dlnVdlnP`,
      `soundSpeed`, `mach`), all consistent with the reference's own pinned-pair state
      against the tree's single-phase one, none of them a value the tree computed
      wrong. `Bits.approved.txt` and `Problems.Tests/Bits.approved.txt` unchanged by
      this investigation itself (no source line touched outside the temporary,
      reverted probes listed above).

## Taboos

- Do not loosen a tolerance for green; do not exclude a failing case.
- Do not compare a subset of fields chosen by hand.
