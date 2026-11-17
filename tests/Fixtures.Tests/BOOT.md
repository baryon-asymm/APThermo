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

- **Private process-directory teardown.** Once owned-process cleanup finishes, delete
  the session's private temporary directory. On Windows only, a sharing-violation
  IOException (0x80070020) means the directory is still in use: retry that exact
  operation while the separate existing five-second cleanup policy remains. A
  Stopwatch bounds the start of each retry, not a fresh budget per attempt; it cannot
  interrupt a synchronous OS deletion or the test callback. At the boundary
  rethrow the original I/O error; other I/O and access errors propagate immediately.
  No process identity, product deadline or GPU budget changes. Real child-CWD
  controls prove the first attempt is blocked and a controlled release succeeds;
  a held child remains a failure at the deadline. On Unix, directory unlink may
  succeed while the child holds its old working directory; that outcome is observed
  explicitly rather than treated as evidence of Windows sharing semantics.

⚠ 2026-11-16: the initial cleanup wording said the stopwatch bounded the whole
synchronous deletion; it bounds retry starts and cannot interrupt an OS call or test
callback. A controlled release after the deadline exposed an extra retry and success.
⚠ 2026-11-16: session teardown previously attempted recursive deletion once; hosted
Windows CI reported three sharing violations after otherwise successful tests. A
real owned child holding the session as CWD reproduces the same error code, but
hosted logs do not identify its holder. The bounded retry does not infer that cause.


- Process infrastructure controls use the owned ProcessProbe executable, never a
  product calculation. Every process-starting guard is Category=EndToEnd. The probe
  has its own finite self-stop, and tests clean up only the PID written by their own
  probe. Ordinary byte/argument/environment/nonzero-exit cases remain exact.
  `DotnetProcessGuardTests` covers runner deadlines and observations;
  `OwnedProcessGuardTests` covers scope, channel and cleanup. Both use one owned
  `DotnetProcessGuardSession` for the same probe and teardown policy.

- A Linux probe is live unless its owned PID is absent or `/proc/<pid>/stat`
  reports zombie/dead state (`Z`, `X`, `x`). Sleeping, stopped and uninterruptible
  processes remain live. Read the state after the final `)` delimiting the command
  name; a missing delimiter/state field and read failures fail verification.
  Other platforms retain `Process.HasExited`. Cancellation of a ready tree proves
  that both observations were live beforehand and both are non-executing afterwards.

⚠ 2026-11-15: was HasExited-only, now Linux terminal states are checked → HISTORY.md#linux-terminal-state

- **Lists are produced by the machine**: kinds by listing `cases/`, compared fields by
  reading the fixtures, the pinned version by reading `requirements.txt`. The one typed
  list is the set of kinds of the case matrix, which is the design decision under test.
- **No fixture is interpreted here.** Whether a value is right is the business of the
  node under test; this node checks form, provenance and coverage.
- **Temporary files only.** Malformed documents are written under the system temporary
  directory and deleted; nothing is written into the working directory.

## Dependencies

[Harness](../Harness/API.md)
[ProcessOwner](../Harness/ProcessOwner/API.md)

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

⚠ 2026-11-15: was never target reused PID, now read/recheck is non-atomic → HISTORY.md#pid-recheck-2026-11-15

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

⚠ 2026-11-15: was immediate exit assertion, now bounded terminal-state observation → HISTORY.md#bounded-terminal-2026-11-15

## Acceptance criteria

→ [ACCEPTANCE.md](ACCEPTANCE.md)

## Taboos

- Do not type an expected value that exists in a fixture or in `requirements.txt`.
- Do not skip a test when a fixture directory is empty: an empty kind is a failure.
- Do not loosen the loader to accept a document missing a field: the field list is the contract.
