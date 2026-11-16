# BOOT.md — Fixtures.Tests

## Purpose

The node also exercises the shared Harness snapshot/schema guards used by fixture
consumers (`OracleGuardTests`, audit A16/A17/A27). These are infrastructure negative
controls, not additional thermodynamic reference calculations.

The definition of what "`Fixtures` is ready" means: the levels of verification, what
each is checked against, what is covered and what is not. The fixtures are the
reference every numerical node is compared with; this node proves their form,
provenance and coverage, never their values (AGENTS.md §1).

| Level | What it checks | Against what (source of truth) | State |
|---|---|---|---|
| L1 | every committed fixture loads through the loader, names its kind, its script and the pinned package version, and carries the hashes of the committed data files | the directory listing of `cases/`, `generate/requirements.txt`, the SHA-256 of `data/thermo.inp` and `data/trans.inp` computed in the test | ✅ 2026-09-12 |
| L1 | every fixture's `scriptSha256` and `generatorSha256` equal the hashes the test recomputes over the committed generator scripts, and all fixtures carry one `thermoLibSha256` and one `transLibSha256` | the committed files of `generate/`, hashed by the test with the CRLF-to-LF normalization of `generate/BOOT.md` | ✅ 2026-09-27 |
| L1 | the tolerance table loads, every field has a derivation, an unknown field throws, the comparison is absolute plus relative, and every numeric state field the rocket and equilibrium fixtures report has an entry | `tolerances.json` and the fixture files themselves (field names read from them) | ✅ 2026-09-12 |
| L1 | a malformed document is rejected with the file name and the field | documents written to a temporary directory | ✅ 2026-09-12 |
| L2 | regeneration is byte-identical (`regenerate.py --check` exits 0) | the generator environment; a command of the Fixtures node's procedure, run by hand or in CI, not by xunit | manual |

## Invariants

- Process infrastructure controls use the owned ProcessProbe executable, never a
  product calculation. Every process-starting guard is Category=EndToEnd. The probe
  has its own finite self-stop, and tests clean up only the PID written by their own
  probe. Ordinary byte/argument/environment/nonzero-exit cases remain exact.

- A Linux probe is live unless its owned PID is absent or `/proc/<pid>/stat`
  reports zombie/dead state (`Z`, `X`, `x`). Sleeping, stopped and uninterruptible
  processes remain live. Read the state after the final `)` delimiting the command
  name; a missing delimiter/state field and read failures fail verification.
  Other platforms retain `Process.HasExited`. Cancellation of a ready tree proves
  that both observations were live beforehand and both are non-executing afterwards.

⚠ 2026-11-15: the former fresh `Process.GetProcessById(pid).HasExited` observation
could label an already exited Linux zombie as running. Hosted release run
43687839467 failed the live-child assertion; the same assertion reproduced locally.
An owned unreaped child deterministically showed kernel `Z` with HasExited=false;
the new observation distinguishes that state while direct-parent-only kill mutants
still fail both tree controls. The hosted log contains no process-state sample, so
its exact failed instant still requires confirmation by the orchestrator's rerun.

- **Lists are produced by the machine**: kinds by listing `cases/`, compared fields by
  reading the fixtures, the pinned version by reading `requirements.txt`. The one typed
  list is the set of kinds of the case matrix, which is the design decision under test.
- **No fixture is interpreted here.** Whether a value is right is the business of the
  node under test; this node checks form, provenance and coverage.
- **Temporary files only.** Malformed documents are written under the system temporary
  directory and deleted; nothing is written into the working directory.

## Dependencies

[Harness](../Harness/API.md)

- [Fixtures](../Fixtures/API.md) — the loader, the tolerance table and the fixture files under test.

Outside the tree: xunit.

## Constraints

Infrastructure review controls exercise public whole-entry APIs: JSON comparison
walks complete documents, schema validation checks complete schemas/instances, and
process controls remain distinct from any thermodynamic reference calculation.

- Part of the default test command; runs without Python and without the reference
  package.
- The regeneration check is not an xunit test: a test that must be skipped where the
  package is absent is a check nobody sees red (AGENTS.md §13). It is a command in the
  Fixtures node's procedure instead, and its last run is dated in that node's
  acceptance criteria.

### Asynchronous descendant observation

A tree guard observes the fixture-owned PID identities until they are absent or
non-executing, within the remaining original cleanup budget. For a two-second
deadline a controlled clock advances only after fixture readiness and the observation
stopwatch starts when that deadline fires; setup has a separate external watchdog.
For ready-tree cancellation start a cleanup stopwatch immediately before requesting
cancellation and cap it at five seconds. Do not restart the budget after Run returns.
The original outer ten-second ceiling includes cleanup and observation after expiry. Poll only while live
and while time remains, checking terminal state once more at the boundary.
Keep R14's absent/Z/X/x semantics; sleeping/stopped/uninterruptible and PF_EXITING
alone remain live. PID/parent/start-time/state diagnostics concern only owned probe
identities and must be captured before fixture teardown on failure. Never scan or
terminate unrelated machine processes. Preserve all raw bytes and cancellation tokens.
Controlled transitions use finite owned processes and an explicit readiness/release
handshake, not a sleep chosen to make the test pass. A child held live beyond the
remaining budget must fail, well before its unchanged twenty-second self-stop.
Production Harness changes are outside this first remedy unless controlled evidence
shows that its documented attempt/bounded-drain contract is itself violated; escalate
that finding to the root rather than silently changing the contract.

The owned marker is published after its identity sidecar: PID, recorded parent,
UTC start time, and Linux kernel start ticks. Each sample checks that identity;
a mismatched identity is excluded from teardown, which rechecks the identity
immediately before requesting kill.

⚠ 2026-11-15: the former wording said a reused PID "is never targeted for teardown".
The implemented read/recheck predicate excludes observed mismatches and rechecks
immediately before requesting kill; it does not provide a kernel-atomic guarantee.

Windows samples retain one valid Process.SafeHandle before querying HasExited and
StartTime on that same Process, closing the check/reopen race when a child exits
between getters. Handle acquisition errors remain errors unless the framework
specifically reports an absent process; never normalize access denial as exit.
Only fixture-owned processes are queried, with the framework's requested access.
A normal exit between GetProcessById and initial handle acquisition may itself
raise ProcessHasExited/InvalidOperationException. Handle that only around the
SafeHandle acquisition and only when a fresh framework HasExited observation
confirms exit; other InvalidOperationException sites and Win32/access errors stay
failures. Do not mask malformed identity data as process disappearance.
An exception inside the exit filter leaves the original failure propagating;
StartTime and identity parsing are outside that catch.
For the Linux owned `/proc/<pid>/stat` read only, .NET 10 may report disappearance
between open and read as IOException with raw HResult 3 (Linux ESRCH), in addition
to missing file/directory. This specific absence is terminal. Other I/O errors,
permission failures and malformed state remain verification failures; do not infer
absence from a generic File.Exists result or swallow all IOException instances.
Raw errno 3 is not the Windows HRESULT 0x80070003; EIO (5) stays an error.
This handling does not establish the cause of the historical 84e failed instant.

Each guard logs its original clock/budget and final owned states before assertions.
The bounded waiter accepts an already running clock, never starts or restarts it,
and makes a final state read at the boundary. Controlled release occurs after the
first LIVE sample; held and zero-budget controls prove live-state refusal.

⚠ 2026-11-15: immediate post-Run descendant assertions assumed synchronous OS exit.
The recurring 84e hosted failure did not sample the kernel state. Owned handshake
controls show the immediate-observation defect on both platforms, while this remedy
observes the same terminal states within the remaining original budget. No claim
about PF_EXITING or the historical failed instant is inferred from a later pass.

## Acceptance criteria

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

## Taboos

- Do not type an expected value that exists in a fixture or in `requirements.txt`.
- Do not skip a test when a fixture directory is empty: an empty kind is a failure.
- Do not loosen the loader to accept a document missing a field: the field list is the contract.
