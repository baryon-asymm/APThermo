# ACCEPTANCE.md — Fixtures.Tests

- [x] `OwnerPreStartTests` proves actual pre-connect bootstrap cancellation and public
  timeout, a controlled unconnected process with both cancellation modes and a
  possibly partial START guard on Windows and Linux; the baseline leak and write-order
  mutants must fail, preserving the original token/bytes. Before START the target
  never starts; the ambiguous delivered-START control instead starts the target and
  verifies termination of the owned scope. Windows Release and native Linux c0
  controls passed on 2026-11-16; acceptance of the follow-up source remains pending.
  The actual bootstrap is held before managed startup by the per-process .NET diagnostic
  suspend option and is verified unconnected. A separate finite ProcessProbe substitutes
  for a non-connecting owner to check exact fixture bytes; the two controls are distinct.
  (2026-11-17: Follow-up `779eeace`, five cases passed on each OS in CI 44048478640 and
  Release 44048539081. See `fix-evidence-20261117/astra/hosted-windows-prestart-779.json`
  and `hosted-native-cases-779.json` for exact identities. The earlier baseline leak and
  START-order mutant retain their original evidence.)

⚠ 2026-11-17: the former criterion was: `OwnerPreStartTests` proves actual pre-connect bootstrap cancellation and public
timeout, a controlled unconnected process with both cancellation modes and a
possibly partial START guard on Windows and Linux; the baseline leak and write-order
mutants must fail while the target never starts and the original token/bytes remain.
Windows Release passed 5/5 on 2026-11-16; native Linux and integrated acceptance remain pending.
The actual bootstrap is held before managed startup by the per-process .NET diagnostic
suspend option and is verified unconnected. A separate finite ProcessProbe substitutes
for a non-connecting owner to check exact fixture bytes; the two controls are distinct.
The ambiguous START-write control starts the target, and native Linux c0 evidence was
obtained; neither establishes acceptance of the later source.

- [x] 2026-11-16 — `DotnetProcessDirectoryTests` exercises a real child holding the
  private directory as CWD, controlled release after the actual Windows sharing
  error, refusal while held through a bounded test deadline, a non-sharing I/O
  error, and fail-closed missing-root classification. Windows Release process
  controls 37/37, then 39/39 including late-release and zero-budget. The prior
  implementation failed late-release while zero passed; sharing-code and absent-root
  mutations fail their controls. Linux remains separate acceptance evidence.

- [x] R18 local cleanup controls preserve primary timeout/cancellation and raw
  evidence under documented partial tree-kill failures. (2026-11-15:
  `DotnetCleanupFailureTests`, frozen old filters 2 red/3 positive on Windows
  and Linux, corrected controls 5/5; full Fixtures 197/197 on each OS.
  Evidence: `SCRATCH/audit/secondary-policy/old-filter-red/`,
  `linux-old-filter-red/`, `cleanup-fixed/`, `windows-fixtures-r18/` and
  `linux-fixtures-r18/`, with TRX and source hashes retained.)

- [x] R18: controlled partial tree-kill AggregateException on the actual cleanup
  path preserves primary timeout/cancellation, raw arrays, excerpts and caller
  token within the existing cleanup budget. Frozen prior filtering is red;
  normal cleanup and the full process-fixture controls pass on Windows/Linux.
  (2026-11-15: exact source `f21ac451c286`. Partial tree-kill controls preserve primary
  timeout/cancellation identity.
  CI `43742270400`, Trace `43742270272`, Release `43742272942`.
  Source-bound reports, controls and artifact inventory:
  external `fix-evidence/github-f21a/final-proof-index-f21a-v1.json`.)

- [x] 2026-11-15 — Local asynchronous-observation controls: immediate frozen waiter
  is red only for release-after-first-LIVE on each OS (three positives); final bounded
  waiter passes release, held, spent and already-completed controls. Terminal-assertion
  mutations reject held/spent on each OS before their twenty-second self-stop.
  Isolated parent-only kill mutations fail the real deadline and cancellation tree
  guards on each OS. Full Fixtures: 192/192 Windows and Linux, sixteen process guards.
  Evidence: `SCRATCH/audit/process-tree-completion/` — frozen-diagnostic-controls.json,
  windows-immediate-diagnostic-red-v2.trx, linux-immediate-diagnostic-red.trx,
  windows-held-live-refused-red-v2.trx, linux-held-live-refused-red.trx,
  windows-kill-direct-mutant-v2.trx, linux-kill-direct-mutant.trx,
  windows-fixtures-final-v2.trx, linux-fixtures-final-v2.trx and verification-summary.json.
  The actual hosted acceptance remains the separate unchecked criterion below.

- [x] Explain and guard the recurrent hosted tree-cleanup observation without
  pretending that the historical failed instant was sampled. Freeze a controlled
  immediate-observation red/positive pair before the remedy; prove bounded owned
  live-to-terminal transition and budget-exhausted live refusal. Parent-only kill
  mutants must still fail the real deadline and cancellation tree guards on both
  Windows and Linux. Keep the two/five/ten/twenty-second limits, raw bytes and caller
  tokens unchanged. All targeted controls, full Fixtures and actual hosted CI on
  both OS must pass with diagnostic state/identity evidence retained.
  (2026-11-15: exact source `f21ac451c286`. Owned-tree PID/start-time and terminal-state
  controls pass all cohorts.
  CI `43742270400`, Trace `43742270272`, Release `43742272942`.
  Source-bound reports, controls and artifact inventory:
  external `fix-evidence/github-f21a/final-proof-index-f21a-v1.json`.)


- [x] 2026-11-15 — Hosted-Linux process observation repair: owned zombie/live/stopped
  controls are old-red/new-green; both Windows and Linux direct-parent-only kill
  mutations fail deadline and cancellation tree controls. The twelve process guards
  pass within the full Fixtures.Tests suite, 188/188 on each platform. Evidence:
  `SCRATCH/audit/process-tree-ci/linux-old-observer-control-red.json`,
  `linux-owned-states-final.json`, `linux-kill-direct-mutant.trx`,
  `windows-kill-direct-mutant.trx`, `linux-fixtures-final.trx`,
  `windows-fixtures-rebuilt-final-v2.trx` in that same evidence directory.

- [x] 2026-11-14 — T14: `DotnetProcessGuardTests` eleven controls pass; old unbounded
  forwarding implementation had seven failures and two positive byte/exit controls.
  Whole-tree kill mutation fails the live-child case; unbounded diagnostic excerpt
  mutation fails the large-output case. Full Fixtures.Tests: 79/79. Evidence:
  `SCRATCH/audit/process-red/process-red.trx`, `process-fixtures/process-fixtures.trx`,
  `process-kill-mutant/process-kill-mutant.trx`, `process-excerpt-mutant/process-excerpt-mutant.trx`.

- [x] 2026-11-14: A16/A17/A27, `OracleGuardTests` exercises infrastructure,
      not thermodynamics: initially 18 red and 5 green controls on the old helpers,
      plus a separate red multiple-marker filename case. All 24 controls and the
      full Fixtures.Tests suite pass (66 tests).

- [x] 2026-09-12 — L1 loader: `FixtureLoadingTests` (`EveryFixtureOfAKindLoads`
      for every kind directory, `TheKindsPresentAreThoseOfTheCaseMatrix`,
      `EveryFixtureIsTiedToTheCommittedDataFiles`,
      `EveryFixtureNamesThePinnedPackage`, `EveryFixtureNamesTheScriptThatWroteIt`).
- [x] 2026-09-12 — L1 tolerance table: `ToleranceTableTests`
      (`TheTableLoadsWithADerivationForEveryField`, `UnknownFieldsAreReportedByName`,
      `MatchesAddsTheAbsoluteAndTheRelativePart`,
      `EveryStateFieldOfTheFixturesHasATolerance` for `rocket`, `tp`, `hp`, `sp`).
- [x] 2026-09-12 — L1 malformed documents: `MalformedFixtureTests` (`ACompleteDocumentLoads`,
      `AMissingFieldNamesTheFileAndTheField`, `AMissingProvenanceFieldIsNamed`,
      `AKindThatDoesNotMatchItsDirectoryIsRejected`,
      `TextThatIsNotJsonIsRejectedWithTheFileName`,
      `AToleranceWithoutADerivationIsRejected`, `ANegativeToleranceIsRejected`).
- [x] 2026-09-12 — Every check proven non-degenerate once (AGENTS.md §13): the
      `dataThermoSha256` of `cases/constants/R.json` altered turned
      `EveryFixtureIsTiedToTheCommittedDataFiles` red with the file named; the
      `gammaS` entry removed from `tolerances.json` turned
      `EveryStateFieldOfTheFixturesHasATolerance` red for `rocket`, `tp`, `hp` and
      `sp` with the field named. Mutations reverted; `regenerate.py --check` confirmed the
      fixtures unchanged afterwards.
- [x] 2026-09-15 — `EveryStateFieldOfTheFixturesHasATolerance`'s nesting fixed:
      the numeric-property check, four levels deep inside the case loop, the state loop
      and the property loop, moved to `ComparedFieldsOf`, a single filter-and-project
      expression with no nested control flow of its own; the outer test calls it once
      per state and now nests no deeper than 2, measured by the protocol tests node's
      `ShapeMeasures.Nesting` (`EveryStateFieldOfTheFixturesHasATolerance` 2,
      `ComparedFieldsOf` 0). Verified: build clean, 0 warnings; `dotnet test
      tests/Fixtures.Tests`: 26 of 26 green; `protocol_lint` 0 errors, 0 warnings.

- [x] 2026-09-27 — The provenance ties every fixture to the committed generator (the
      guards audit's F7, `tests/Fixtures/ACCEPTANCE.md`'s criterion of the same date):
      `EveryFixturesScriptSha256MatchesItsCommittedScript`,
      `EveryFixturesGeneratorSha256MatchesTheCommittedGenerator` and
      `AllFixturesCarryOneThermoLibSha256AndOneTransLibSha256` (`FixtureLoadingTests.cs`),
      each shown red once against a mutation (a fixture copy with `scriptSha256`
      replaced by 64 zeros; a comment added to `common.py`, left unregenerated),
      reverted before this tick, the evidence recorded in `tests/Fixtures/ACCEPTANCE.md`.
      `dotnet test tests/Fixtures.Tests`: 33/33.

- [x] 2026-10-03 — The `seeded` kind (`tests/Fixtures/ACCEPTANCE.md`, the criterion of the same
      date): `FixtureLoadingTests.TheKindsPresentAreThoseOfTheCaseMatrix` lists it,
      `ToleranceTableTests.EveryStateFieldOfTheFixturesHasATolerance` covers its fields, and
      `MalformedFixtureTests` (`ACompleteSeededDocumentLoads`, `AMalformedSeededDocumentIsRejected`
      over a missing seed, a seed of another kind, a seed without a temperature, and both or neither
      of the enthalpy and the entropy) proves the loader's field checks, seen red once with the
      kind test of `CeaFixtures.RequireSeed` mutated (5 red, reverted). `dotnet test tests/Fixtures.Tests`:
      42/42.
