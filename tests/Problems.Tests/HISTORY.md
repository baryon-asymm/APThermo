# HISTORY.md — Problems.Tests

Append-only store of what `BOOT.md` no longer needs to state as current truth
(`AGENTS.md`, §15). Newest entry first. Every entry names the date it was moved, the
`BOOT.md` section it came from, and carries the original text in full, unedited. The
start procedure (`AGENTS.md`, §10) does not read this file; it is reached only by
following a dated pointer left in `BOOT.md` at the place the text used to stand.

---

<a id="criterion-nan-cv-guards"></a>

## 2026-10-01 — from "## Acceptance criteria", "the NaN and cv guards" — condensed wording, with the mutation evidence

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the original date is
2026-09-27. What stays at the pointer is the current rule. The text as it stood:

> - [x] 2026-09-27 — The NaN and cv guards: a NaN mole fraction on one side, and a
>       frozen exit's cv edited in a fixture copy, each red.
>
>       `StationEquality.RelativeDifferences`'s mole-fraction loop no longer lets
>       `Math.Max(p, q)` hide a NaN behind the trace floor: `Math.Max` returns NaN when
>       either argument is NaN (.NET's documented behaviour), which made
>       `Math.Max(p, q) >= moleFractionFloor` false and skipped the pair before `Close`
>       ever ran. The floor test now also fires when either side is NaN
>       (`double.IsNaN(p) || double.IsNaN(q) || Math.Max(p, q) >= moleFractionFloor`),
>       so `Close`'s own NaN-safe form (already `!(<=)`-shaped) is reached. `BitDifferences`
>       was never blind (`Bits.Same` compares raw bits) and needed no change.
>
>       `ReferenceComparison`'s frozen-station cv skip is keyed on the reference's
>       defect signature the same way `Performance.Tests`' `StationComparison` now is
>       (that node's BOOT.md, the same finding): `StationCaveats` carries an added
>       `FreezingStationReference` (the reference's own chamber or throat station,
>       `RocketTests.FreezingStationReferenceOf`, from the case's `FlowModel`), and
>       `IsFrozenCvDefectSignature` requires the reference's `cvFrozen`/`cvEquilibrium`
>       at the frozen station to be exactly zero or exactly that station's own recorded
>       value before skipping; `EquilibriumTests`' two callers pass `Frozen: false`
>       always (tp/hp/sp problems have no frozen station), so they carry no freezing
>       reference and are unaffected.
>
>       Evidence, each mutation applied through a temporary, uncommitted fact
>       (`ZzGuardsAuditRedOnce.cs`, deleted before this commit) and seen red, never
>       reverted into the tree:
>       - two minimal `Station` records differing only in one mole fraction, `NaN`
>         against `0.5`, both far below `moleFractionFloor`:
>         `StationEquality.RelativeDifferences` now reports the pair, where the old
>         `Math.Max` guard stayed silent;
>       - `lox-lh2_of4_pc5MPa_frozenAtThroat` solved, its frozen exit's
>         `cvFrozen`/`cvEquilibrium` edited in an in-memory copy of the reference JSON
>         from `0`/`0` to `2500`/`9999`: `ReferenceComparison.Compare` now reports a
>         mismatch, where the old, unconditional `caveats.Frozen &&
>         NotAtFrozenStations.Contains(name)` skip stayed silent.
>
>       `APTHERMO_NO_CUDA=1 dotnet test tests/Problems.Tests --filter
>       "Category!=LongRunning"`: 1191/1191, none skipped, the same count as before this
>       fix (no fixture-enumerated theory added here). `Bits.approved.txt` unchanged
>       (`git hash-object`: `5663fb464dfd1aab6792f177de7b70546306a231`). The protocol
>       lint: 0 errors, 0 warnings.

---

<a id="invariants-bits-platform"></a>

## 2026-10-01 — from "## Invariants", "The bits are a tripwire, not a contract" — the platform files of 2026-09-17 and the BitSnapshot trait of 2026-09-19

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the original date is
2026-09-19. What stays at the pointer is the current rule. The text as it stood:

>   ⚠ 2026-09-17: this bullet assumed one snapshot file. The root's platform constraint
>   now keeps a Windows and a Linux record, since the CPU accelerator's `System.Math`
>   calls the platform's C runtime and the two do not round the last bit alike;
>   `ApprovedPath` resolves through `Harness.ApprovedSnapshot.ApprovedPathFor`
>   (`tests/Harness/API.md`), which picks `Bits.approved.txt` or `Bits.linux.approved.txt`
>   for the running platform, so this node's own code names no platform. The first Linux
>   run (2026-09-17, WSL2 Ubuntu 24.04, `f67b1a9` plus this task's harness change) did not
>   reproduce the Windows bits, within the tolerance the root BOOT.md records for the
>   difference; `Bits.linux.approved.txt` was approved from that run.
>
>   ⚠ 2026-09-19: the root BOOT.md's platform constraint (⚠ 2026-09-18, the declared
>   deviation from "every test runs on both platforms") holds that the bits are a
>   record of the reference machine, not of the platform alone: the first release run
>   failed on a hosted Windows runner with one rocket case of this node's own snapshot
>   changed in its last bits, every CEA tolerance test green. `EveryFixtureGivesTheRecordedBits`
>   now carries `[Trait("Category", "BitSnapshot")]`, so it runs in every local run
>   (`CLAUDE.md`'s fast set) and in the release's self-hosted jobs
>   (`.github/workflows/release.yml`'s `cuda-windows` and `cuda-linux`, filter
>   `Category=Cuda|Category=BitSnapshot`), and is filtered out of the hosted fast suite
>   (`ci.yml`; `release.yml`'s `matrix` job; filter
>   `Category!=LongRunning&Category!=BitSnapshot`), where the reference comparison of
>   the front door's own tolerance tests holds correctness instead.

---

<a id="criterion-field-dump"></a>

## 2026-10-01 — from "## Acceptance criteria", "the Bits level's per-case field dump" — condensed wording, with the red-once record of the dump

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the original date is
2026-09-18. What stays at the pointer is the current rule. The text as it stood:

> - [x] 2026-09-18 — The Bits level's per-case field dump (bits-diagnostics task): `Record`
>       passes its `BitHash`'s `Fields` to `ApprovedSnapshot.Problem`
>       (`tests/Harness/BOOT.md`, "a field dump is a caller's opt-in"), so a fixture that
>       disagrees with `Bits.approved.txt` also gets its own
>       `Bits.actual.<sanitized path>.fields.txt`, every hashed field as a round-trip
>       double or an int/bool, one per line, in the order `HashOf` adds them. Shown red
>       once and the dump inspected: the last hex digit of
>       `tests/Fixtures/cases/rocket/lox-lh2_of4_pc5MPa_frozenAtThroat.json`'s line in
>       `Bits.approved.txt` changed from `3` to `0` (the exact fixture the release run of
>       2026-09-17 flagged, `SCRATCH/nondeterminism-report.txt`), `dotnet test
>       tests/Problems.Tests -c Release --filter FullyQualifiedName~BitSnapshotTests` red
>       on that one key, and
>       `Bits.actual.tests_Fixtures_cases_rocket_lox-lh2_of4_pc5MPa_frozenAtThroat.json.fields.txt`
>       written beside `Bits.actual.txt` with the case's 173 fields; copied out as
>       `SCRATCH/bits-diag/reference-lox-lh2_of4_pc5MPa_frozenAtThroat.txt` before the
>       approved line was restored byte for byte (`git diff` empty) and the test green
>       again, 1/1. No approved file moved by this criterion.

---

<a id="constraints-writes"></a>

## 2026-10-01 — from "## Constraints" — the dates of the writes into the working directory

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the original date is
2026-09-18. What stays at the pointer is the current rule. The text as it stood:

> - Paths from the repository root; the only writes into the working directory are the
>   `Bits.actual.txt` of a failed bit comparison, next to the approved file and
>   git-ignored (2026-09-14; until that day the node wrote nothing), and, since
>   2026-09-18 (the bits-diagnostics task), one `Bits.actual.<sanitized fixture path>.fields.txt`
>   per differing fixture, beside it and git-ignored too (`tests/Harness/BOOT.md`, "a
>   field dump is a caller's opt-in").

---

<a id="criterion-support-code"></a>

## 2026-10-01 — from "## Acceptance criteria", "the support code in shape" — condensed wording, with the recorded mutations and the plateau facts' red-once record

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the original date is
2026-09-14. What stays at the pointer is the current rule. The text as it stood:

> - [x] 2026-09-14 — The support code in shape (the review's F-TF-01, F-TF-09, F-TF-10,
>       F-TF-11, F-TF-14): `Comparison` becomes `SpeciesList` (the names with the gas
>       count, `SpeciesList.cs`), `ReferenceCaveats` (the caveat facts of a station and
>       their field sets, each citing the fixtures node, `ReferenceCaveats.cs`),
>       `ReferenceComparison` (one station against one fixture station in six parameters
>       — `reference, station, species, label, tolerances, caveats` — split into
>       `TransportMismatches`, `StateAndPerformanceMismatches` and
>       `MoleFractionMismatches`, the messages byte for byte as before,
>       `ReferenceComparison.cs`) and `StationEquality` (bit and relative equality of two
>       stations of the tree's own code, `StationEquality.cs`); the union test becomes
>       three facts:
>       `RocketTests.RocketProblemsOverSeveralMixturesAreOneBatchOverTheUnionOfElements`,
>       `EquilibriumProblemsOverSeveralMixturesAreOneBatchOverTheUnionOfElements`
>       and `RejectionTests.ABatchOverMismatchedMixturesOrProblemCountsIsRejected`;
>       every tolerance of a comparison with the tree's own code a named constant with its
>       origin (eight added: `MoleFractionSumTolerance`, `MassBitRoundingTolerance`,
>       `ReportedMassPrintTolerance`, `FormulaMassRoundingTolerance`,
>       `MassOfSummationTolerance`, `ScaledMassSummationTolerance`,
>       `TransitionBoundTolerance`, `OwnCodeIsentropeTolerance`, each with an origin
>       comment, across ten former bare-literal sites); no method over 60 lines or nested
>       deeper than 3 (unchanged by `dotnet build`, run clean every commit).
>
>       The recorded mutations "the defect signature disabled"
>       (`FixtureCases.DefectiveStationsOf`'s `TraceEliminations > 0` check disabled) and
>       "mole fractions taken over the gaseous phase" (`StationFactory.Create`'s
>       fractions loop narrowed from `speciesCount` to `table.GasCount`) red again after
>       the split, both on `TheRocketCaseReproducesTheReferenceEndToEnd`: the
>       first over the three `lox-lh2_of4_*` and three `lox-lh2_of5_*` shifting-flow
>       cases with transport (the reacting-conductivity and Prandtl skip no longer
>       guarded, the tree's figures compared against the reference's own documented
>       defect and found to disagree with it, as the skip exists to catch); the second
>       over all ten `ap-htpb-al*` cases (every condensed species "not in the table",
>       `Species` and `MoleFractions` collapsed to the gas phase). Each reverted; the
>       fast suite green unchanged after (1111/1111 `Problems.Tests`).
>
>       Each plateau fact of `SplitRecordTests` seen red once, none previously recorded
>       (the level table's ⚠ of 2026-09-14 above): the cut-species collapsing in
>       `StationFactory.SpeciesNames` disabled (always appended instead of collapsed) —
>       `ACutSpeciesReportsOneEntryUnderItsDatabaseName` red, `ALN(L)` counted
>       twice, not once; the state record's pressure doubled on its way into the
>       equilibrium problem in `StateRecords.ToEquilibriumProblems` (its enthalpy tried
>       first: passing `null` instead of `record.Enthalpy` left the fact green, because
>       `ProblemValidation.Equilibrium` defaults an unset assigned-enthalpy target to the
>       mixture's own enthalpy, which for a state record is `record.Enthalpy` again — a
>       finding in itself, recorded so the same non-mutation is not retried) —
>       `AnEnthalpyInsideTheALNGapSolvesThroughTheFrontDoor` red, T =
>       2770.839051872195 K against the 2700 K cut, no longer pinned;
>       `TransitionBoundTolerance` tightened from 0.01 to 0 —
>       `ASweepAcrossTheAluminaPlateauStaysOnTheIsentropeByEitherPath` red, a
>       pinned station at 2327.000012414645 K against the exact bound 2327, the residual
>       the 0.01 K tolerance exists to absorb. Each reverted; `dotnet test` on
>       `Problems.Tests` after every revert: 1111/1111, `Bits.approved.txt` unmoved
>       throughout (`git diff` empty against `8f8263c` and in the working tree).

---

<a id="criterion-contract-facts"></a>

## 2026-10-01 — from "## Acceptance criteria", "the contract facts of 2026-09-14" — condensed wording, with the red-once mutations of each fact

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the original date is
2026-09-14. What stays at the pointer is the current rule. The text as it stood:

> - [x] 2026-09-14 — The contract facts of 2026-09-14 (the level table's second new row):
>       `RocketTests.AStateRecordWithExitsEqualsItsCaseThroughTheBatchOverMixtures`
>       (bit for bit, transport figures included);
>       `RejectionTests.AStateRecordThatBreaksARuleOfItsShapeIsRefusedWithItsIndex`
>       (one case per rule: no target, two targets, exits without an enthalpy, a flow
>       without exits, a record with exits given to `SolveStates`, one without given to
>       `SolveRocketStates`, a negative abundance, a duplicated symbol; each a
>       `StateRecordException` whose `Index` is the record's
>       and whose `Reason` names the rule);
>       `RocketTests.ABatchMixingTransportAndNoneEqualsEachProblemSolvedAlone`
>       (bit for bit, and `TransportStatus` null where none was asked) with
>       `CasesAreGroupedByExitLayoutAndTransportFlag` over the runner's internal
>       grouping, which is where the narrowed pass is visible;
>       `RocketTests.ARatioAndPressureProductAsOneBatchEqualsItsCasesSolvedOneByOne`,
>       the sweep's fact on the batch over mixtures, replacing
>       `A_sweep_equals_its_cases_solved_one_by_one`;
>       `RejectionTests.EveryPublicMethodOfADisposedSolverThrows` with
>       `TheDisposalFactsCoverEveryPublicMethodOfTheSolver` (the list of methods
>       from reflection); `RejectionTests.TheToleranceRuleIsTheOneCreateApplies`
>       (`IsValidMassTolerance` false exactly where `Create` refuses). Each seen red once,
>       reverted after: the kind check — `StateRecords.Validate`'s
>       `record.HasExits != expectsExits` — removed, both rows of
>       `AStateRecordThatBreaksARuleOfItsShapeIsRefusedWithItsIndex` for the
>       two routing reasons red ("no exception was thrown"), the other six rows
>       unaffected; the grouping by the transport flag removed — `RocketRunner.Solve`'s
>       per-layout `GroupBy(k => cases[k].Problem.Transport)` collapsed to one flag per
>       exit-layout group (`groups[key].Any(...)`) — both
>       `ABatchMixingTransportAndNoneEqualsEachProblemSolvedAlone` and
>       `CasesAreGroupedByExitLayoutAndTransportFlag` red, transport figures
>       attached to a station whose own case never asked; one method's disposal guard
>       removed — `CandidateSpeciesFor`'s `ThrowIfDisposed()` taken out,
>       `EveryPublicMethodOfADisposedSolverThrows` red for that entry ("no
>       exception was thrown"). The same removal tried first on `SolveRocketStates`
>       stayed green: its own guard is masked by the engine's disposal check reached
>       through `RocketRunner.Solve`, so the fact the criterion asks for — every public
>       method throws — still held; recorded here so a guard whose own removal is
>       unobservable is not mistaken for one never tried. `dotnet test` on
>       `Problems.Tests` after every revert: 1111/1111, `Bits.approved.txt` and
>       `PublicSurface.approved.txt` unmoved.

---

<a id="criterion-bits-level"></a>

## 2026-10-01 — from "## Acceptance criteria", "the Bits level" — condensed wording, with the repeated checks and the reverse direction

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the original date is
2026-09-14. What stays at the pointer is the current rule. The text as it stood:

> - [x] 2026-09-14 — Bits level green: `BitSnapshotTests.EveryFixtureGivesTheRecordedBits`
>       over the enumerated rocket, tp, hp and sp directories against `Bits.approved.txt`
>       (213 fixtures), recorded before any code of the front door's decomposition moved
>       (on a tree whose numerical nodes are bit for bit as at `8e36a27`, by their own
>       Bits levels) and confirmed unchanged after every step of it:
>       `git diff 8f8263c HEAD -- tests/Problems.Tests/Bits.approved.txt` and the
>       working-tree diff both empty, checked repeatedly through steps 2 to 4 and last
>       after the contract commit's collaborator-DRY pass and the mutation testing
>       below; the test itself green in every fast-suite run of this node (1111/1111 the
>       last time). Seen red once, at the snapshot's own commit (`8f8263c`, 2026-09-14):
>       a reactant enthalpy per kilogram perturbed by a relative 1e-9 turned every one of
>       the 213 fixtures red; a fixture line removed from `Bits.approved.txt` turned only
>       that fixture red, naming it as missing. Both reverted before the commit.
>
>       2026-09-15 (the clean-code repair's R-Problems.Tests-1): the reverse direction
>       closed, the gap the Harness `BOOT.md` already recorded — an approved line no
>       enumerated fixture produces now fails the test too, naming the stale key
>       (`ApprovedSnapshot.StaleKeys`, over the same keys the fact enumerates). Seen red
>       once: a fabricated line
>       (`tests/Fixtures/cases/rocket/__mutation-stale-key-does-not-exist.json`, a fake
>       hash) appended to `Bits.approved.txt` turned the fact red naming exactly that key
>       as stale ("recorded in Bits.approved.txt but no enumerated fixture produced it");
>       reverted, green again (`dotnet test tests/Problems.Tests --filter
>       FullyQualifiedName~BitSnapshotTests`, 1/1 before, red with the fabricated line,
>       1/1 after the revert). `Bits.approved.txt` itself unchanged (213 lines).

---

<a id="criterion-l0-nine-facts"></a>

## 2026-10-01 — from "## Acceptance criteria", "L0 green" — the count of facts over a list of eight

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the original date is
2026-09-14. What stays at the pointer is the current rule. The text as it stood:

>   ⚠ 2026-09-14: stood "nine facts" over a list of eight; a number repeating the length
>   of a list, dropped (the clean-code review's F-TF-16).

---

<a id="invariants-declared-duplication"></a>

## 2026-10-01 — from "## Invariants", "a declared duplication" — the declared duplication of the union-batch tolerances, resolved the same day

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the original date is
2026-09-14. What stays at the pointer is the current rule. The text as it stood:

> - ⚠ 2026-09-14, a declared duplication (`AGENTS.md` §12): the comparison of a case in
>   a union batch that reorders its elements used the mole-fraction floor (1e-8) and the
>   polish-threshold tier (1e-9) of the GPU/CPU table, copied from the execution tests
>   node, whose code this node may not read. Resolved the same day (the review's
>   F-TF-05): `RocketTests`' own `ReorderedElementsTolerance` and `MoleFractionFloor`
>   constants are gone; the two facts that used them
>   (`RocketProblemsOverSeveralMixturesAreOneBatchOverTheUnionOfElements`,
>   `EquilibriumProblemsOverSeveralMixturesAreOneBatchOverTheUnionOfElements`)
>   now read `polishThresholdRelative` and `moleFractionFloor` from the fixtures node's
>   tolerance table, the same two entries the execution tests node's own table also
>   stopped duplicating.

---

<a id="invariants-trace-threshold"></a>

## 2026-10-01 — from "## Invariants", "No expected value is typed into a test" — the history of the trace threshold's home

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the original date is
2026-09-14. What stays at the pointer is the current rule. The text as it stood:

> - No expected value is typed into a test: everything comes from the fixture files or
>   from another solve of the same code; `b_i` and `h_0` are the fixture's. The trace
>   threshold of the mole-fraction comparison is the fixtures node's
>   `ToleranceTable.MoleFractionField` (2026-09-14: it first stood as
>   `Comparison.TracePrintThreshold`, 5e-6 retyped under a comment naming the table, the
>   review's F-TF-11; then as this node's own `ReferenceCaveats.TracePrintThreshold`
>   reading the table's `moleFraction` entry and a selection line typed at the one call
>   site; the architecture review's F-AR-03 found the same selection line typed here and
>   in `Equilibrium.Tests` and `Performance.Tests` alike, and moved it to the fixtures
>   node once for all three).

---

<a id="purpose-plateau-row"></a>

## 2026-10-01 — from "## Purpose" — the missing plateau row of the level table

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the original date is
2026-09-14. What stays at the pointer is the current rule. The text as it stood:

> ⚠ 2026-09-14: the plateau row was missing. `SplitRecordTests` came with the
> melting-plateau commit (`8e36a27`), whose criteria in the `Problems` node cite its three
> facts while this node's table, criteria and mutations did not name them; found by the
> clean-code review (F-TF-04). The evidence keeps the day it was obtained; the row and the
> criterion below carry the day they were written.

---
