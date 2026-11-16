# API.md — Fixtures.Tests

The node exposes no package API. Its assembly participates in protocol reflection;
its contract points upward: it is what the parent may consider proven about `Fixtures`.

## What this node guarantees

| Claim | Confirmed by | State |
|---|---|---|
| every committed fixture loads, names its kind, its script and the pinned package version, and was generated from the committed data files | L1: `FixtureLoadingTests` | ✅ 2026-09-12 |
| the tolerance table loads with a derivation per field and covers every numeric state field the rocket and equilibrium fixtures report | L1: `ToleranceTableTests` | ✅ 2026-09-12 |
| shared snapshot names preserve approved bytes; schema reference siblings and unused branches are checked; tolerance comparisons refuse nonfinite values | `OracleGuardTests` | ✅ 2026-11-14 |
| a malformed fixture or table is rejected with the file name and the field | L1: `MalformedFixtureTests` | ✅ 2026-09-12 |
| a `seeded` document names its tp seed and assigns exactly one of enthalpy and entropy, and the kind is covered by the tolerance table | L1: `MalformedFixtureTests`, `ToleranceTableTests`, `FixtureLoadingTests` | ✅ 2026-10-03 |
| every fixture's `scriptSha256` and `generatorSha256` match the committed generator, and all fixtures carry one `thermoLibSha256` and one `transLibSha256` | L1: `FixtureLoadingTests` | ✅ 2026-09-27 |

## What the tests rely on

- The repository root and the fixture directories come from `RepositoryPaths` and
  `FixtureFiles` of the Fixtures node.
- The kinds are the directories under `cases/`; the compared fields are read from the
  fixture documents; the pinned package version is read from `generate/requirements.txt`.
- Malformed documents are written under the system temporary directory and removed
  when the test class is disposed.

Process infrastructure is checked with the owned finite ProcessProbe executable
(`DotnetProcessGuardTests` and `OwnedProcessGuardTests`, Category=EndToEnd): exact bytes/arguments/environment,
nonzero exits, validation, cancellation, deadline and inherited-pipe/tree cleanup.

⚠ 2026-11-14: the old introduction said nobody referenced a test project; the protocol
inventory does reference this assembly. This is verification, not a consumer API.

## Process liveness evidence ✅

Process-tree controls require both owned processes to be live before cancellation
and non-executing after cleanup. On Linux, a remaining zombie/dead `/proc` record is
an exited process awaiting reaping, while sleeping/stopped processes remain live.
Other platforms use the original Process.HasExited observation. Missing Linux
records mean exit; missing delimiter/state fields and access failures fail verification.
No process/stream deadline, cleanup budget or byte-preservation requirement changes.

⚠ 2026-11-15: the earlier fresh Process.HasExited observation could report a Linux
zombie as live. Owned OS controls demonstrate that false positive and retain live
and stopped positive controls; direct-parent-only kill mutations remain red.

## Asynchronous owned-tree observation ✅

Deadline guards use their original invocation clock through two seconds plus the
existing five-second cleanup budget. Cancellation guards start the five-second
clock before requesting cancellation. Both retain the outer ten-second ceiling,
including state observation; returning from Run never starts another grace period.
An owned child still live at that budget is a failure. S/T/D/PF_EXITING alone remain
live; absent/Z/X/x remain terminal. Only a matching PID/start-time identity qualifies as the owned live process;
a mismatch excludes that replacement from teardown. Failure output includes the captured owned
identity, parent/start/state and original elapsed/budget before test teardown.

⚠ 2026-11-15: immediate descendant assertions assumed tree-kill completion on parent
exit/pipe EOF. Controlled readiness/release tests replace that timing assumption
with bounded state observation. The historical hosted kernel instant stays unknown.

## Partial tree-kill diagnostics ✅

`DotnetCleanupFailureTests` uses one owned finite ProcessProbe and the private
per-call kill seam through the actual Harness completion/cleanup path. Both timeout
and cancellation retain raw fixture bytes, bounded excerpts and caller token; a
simulated documented AggregateException survives as secondary CleanupException
data. The injected kill still terminates the owned real probe. Ordinary cleanup
and normal exit are positive controls; no mutable global hook is introduced.

## Immutable process policy ✅

`DotnetProcessPolicyTests.DefaultsRemainDocumentedAndReadOnly` checks the actual
runner's unchanged five-minute/five-second properties and absence of setters.
The Docs node separately proves that metadata follows this same actual policy.
