# Acceptance criteria

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

⚠ 2026-11-14: the old plateau comparison paragraph above described Mach>=1 as
a guard for one fixture, but the code exempted ordinary sonic throats too (A06).
The exemption now names the known BeO fixture and only its seven derivative fields;
temperature, pressure, characteristic velocity and composition remain compared.

- [x] 2026-11-14 — AuditGuardTests corrupts an ordinary throat's temperature,
  GammaS and characteristic velocity and sees each mismatch (old comparator: red
  regression control, because it incorrectly accepted them). Its unchanged case
  is the positive control.
- [x] 2026-11-14 — FigureCheckTests independently enumerates every numeric
  PerformanceFigures property. Removing ThrustCoefficient from FigureChecks
  causes three nonfinite controls to fail; finite figures pass (A11).
- [x] 2026-11-14 — ThroatToleranceTests rejects a later higher maximum when an
  earlier smaller maximum exists; reversal of the ordering predicate is red.
  Existing first-maximum scans use that same position check at grid-cell resolution
  and still refine/check the peak flux (A10).
- [x] 2026-11-16 — `UpstreamExhaustionTests` drives the actual verifier's exhaustion
  dispatch with the physical locator, a controlled initial boundary count and
  synthetic endpoint fingerprints and chamber enthalpy. The
  `Exhausted`-to-`Ok` mutation compiles and yields 1 failed/1 passed; restored and
  rebuilt verifier, walk and bit snapshot selection is 105/105. This tests dispatch,
  not an eight-boundary physical witness; the original eight-boundary walk test stays.
- [x] 2026-11-14 — The former cap test is now accurately named as a locator-end
  check. PhaseBoundaryWalkTests supplies the actual bounded state machine with an
  unresolved chain and with a resolving positive control (A09).
- [x] 2026-11-14 — The tail policy test is named
  TheThroatPolicyConstantsSpecifyTheDecisionThresholdAndTailLength; reading those
  constants is not presented as proof that the tail executed (A40).

- [x] 2026-11-14 — T16: snapshot mismatches write actual named state/figure fields,
  species moles, element multipliers, statuses, iterations and case status. Reflection
  and array traversal preserve the former hash order. The existing rp1311-example13
  mismatch produced a named dump; its cb1f950c... hash is identical with the former
  caller and before the production refactor. The other 100 bit controls pass.
  Evidence `SCRATCH/audit/performance-named-bits.log` and
  `performance-named-example.txt` in audit-performance; no approved file changed.

- [x] 2026-11-14 — Independent BeO sonic known answer: `IndependentBeOThroatTests` freezes the
  external 80-digit raw-NASA equation solution for rp1311-example13 T/p/c*, checks
  the existing polish tolerance, and rejects corrupted measured fields. The bounded
  three-field guard supplements the full fixture/KKT/bit evidence; it replaces none.
  Data and input SHA-256 provenance live in `IndependentBeOThroat.json`.

- [x] 2026-11-16 — The BeO oracle's input SHA follows the regenerated fixture's
  bytes. The prior and current fixture differ only in generator date and SHA; the
  independent 80-digit known answer, data provenance and tolerances are unchanged.
  Its four controls were red on the stale input SHA and pass 4/4 after the link
  update. The owner-held `fix-evidence/fixture-hash-links/` preserves both TRX runs
  and the before/after byte-level comparison.

⚠ 2026-11-16: the A01 source change was committed as `25b95f55`, while the intended
BeO bit-row approval followed separately in `effc89f2`. This did not satisfy the root's
same-commit rule. The historical commits and independent numerical review remain as
they occurred; future intended changes must still meet the rule.

The root authorized exactly the rp1311-example13 Performance bit row after the
independent numerical review of 2026-11-14. Root's pinned-source A01 reversal restores
its original complete outcome and counters; A35, A13 and Performance decomposition
are excluded by the controlled variants. Independent raw fits and sonic balance
solve accept the same BeO(b)/BeO(L) throat: maximum selected primary/sonic relative
error 1.73535e-11, gas stationarity 7.7222e-14, element residual 8.0094e-15, held-phase
potential residuals below 6.5e-15. Both neighboring pressures have lower mass flux.
The two-phase temperature remains the existing effective-range correction near
2851 K. No intermediate swap/exit-guard branch trace was claimed. This is a named
intentional numerical convergence-path change, not a refactoring allowance.
Provenance: external audit `fix-evidence/independent-numerics/REVIEW.md`,
`raw-oracle.json`, `throat-oracle.json`, `raw_oracle.py`, `throat_oracle.py` and
root `fix-evidence/root/cause-*-fields.json`; the guard retains a small frozen answer.

Guard evidence: `IndependentBeOThroatTests` 4/4 CPU positive/negative controls;
a temporary actual throat-temperature corruption in the positive solve made that
known-answer assertion red, then was restored. `SCRATCH/audit/independent-beo-green.trx`
and `independent-beo-corruption-red.trx` in audit-public record both sides.
- [x] 2026-11-14 — Root-approved intentional A01 numerical record, exactly one row:
  `tests/Fixtures/cases/rocket/rp1311-example13.json` in `Bits.approved.txt` changes
  from `76703e3ffd0d51ca479b902495c420114b63ac49d5a9606eb4e0611a63b4e526`
  to `cb1f950c110a137b5317c7f98dc474e5c8c8c9efefab79f35e155dfe9efd44c5`.
  The root's source-reversal proof and independent raw/sonic oracle above justify
  the changed convergence path; the independent guard preserves the established
  `polishThresholdRelative` budget. Hash order, all other approved rows and tolerance
  values are unchanged. Before this one-row update, the generated bit inventory
  yielded 100 passing controls and only this mismatch; after it, the full Performance
  CPU suite is 1494/1494 with no skips. Evidence in audit-public:
  `SCRATCH/audit/performance-bits-before-approved-row.trx`,
  `performance-approved-final-full.trx`, and `example13-approved-row.diff`.

- [x] 2026-11-14 — T13: `SonicTailExecutionTests` compares actual solved throat
  T/p against independent raw-Cp quadrature and a temperature-space sonic balance
  (N2 at 3000 K), calibrated by the analytic constant-Cp argon state (600 K).
  The 256/512-panel reference refinement meets the existing 1e-12 identity budget;
  production comparisons retain `polishThresholdRelative`. A frozen scratch compile
  overlay starts the tail loop at its unchanged limit: only the N2 physical-state
  assertion fails (temperature relative error 1.2183505315732646e-9); the policy
  constant fact and analytic argon control pass. Normal rebuild: all three pass;
  full current Performance 1496/1496 and affected Protocol 18/18, no skips.
  Evidence in audit-tail-test: `SCRATCH/audit/tail-overlay-proof.json`,
  `tail-final-zero-execution/tail-final-zero-execution.trx`,
  `tail-final-restored/tail-final-restored.trx`,
  `performance-tail-current/performance-tail-current.trx`,
  `tail-protocol/tail-protocol.trx`. Numerical production source, existing tolerances
  and approved records are unchanged. This proves sensitivity to bypassing the
  configured tail, not an independent count of its individual steps.
