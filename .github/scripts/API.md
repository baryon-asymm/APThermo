# API.md — .github/scripts

## GPU release command guards ⏳

`test_delivery.py` checks both actual GPU job commands: sequential project
execution, complete CUDA-or-bit selection, Release/no-build, original shells,
dependencies and job limits. Frozen former wiring and deliberate independent
Windows/Linux command mutations must fail. These are local stdlib guards;
actual self-hosted verification is a separate acceptance requirement.

## Contract ✅

Host preflight, release-note extraction and CPU verification orchestration, plus controlled local guard tests.

`preflight-windows.ps1` / `preflight-linux.sh`: run at repository root; exit 0
only when git, the SDK selected by .NET under global.json and nvidia-smi run successfully and
APTHERMO_NO_CUDA is unset. On failure exit nonzero and name each missing item.

`check-release.sh <version> <changelog> <notes-output> [ref-name]`: an optional ref
must equal v<version> literally. Extract the exact `## [<version>]` section,
allowing a date suffix, with nonempty notes; failure exits nonzero.

`test_delivery.py`: standard-library local guard suite on hosted Windows/Linux;
controlled command probes, no GPU, publishing or external mutation.

⚠ 2026-11-14: A23/A34 implementation now enforces the declared refusal contract:
missing commands and every nonzero native exit fail, SDK versions are whole entries,
and CHANGELOG headings are literal strings. Local guards do not prove an actual
self-hosted GitHub preflight run; that evidence remains open in BOOT.md.

Linux preflight parity (2026-11-14): each prerequisite must be discoverable and its
native probe must exit zero; the requested SDK must equal a whole --list-sdks token.
Bash probe tests run on Windows Git Bash and hosted Linux; they are distinct from
the Windows-only PowerShell 5.1 probes.

⚠ 2026-11-14: was "the requested SDK must equal a whole --list-sdks token", now the
SDK is selected by `dotnet --version` in the repository, respecting global.json
rollForward and other resolver policy. Root's real host validly selected 10.0.113
for 10.0.112/latestPatch. Native resolver failure or malformed output still refuses.
Windows uses the first discovered application when PATH contains multiple matches.

`trace_scan.py [--configuration Debug|Release] [--results-dir <path>] [--discover-only]`:
select the checked `TraceGasScanTests` methods from Equilibrium.Tests/API.md,
verify metadata discovery and run on CPU. Persist discovery.log, matrix.json,
trace-gas.trx and execution.log; exit nonzero for missing/duplicate/skipped tests,
failed process, timeout, missing report or inconsistent counters. Discover-only
produces a matrix marked unexecuted and exits after verified discovery.

⚠ 2026-11-14: a failed execution attempt formerly left matrix entries "not-run".
Matrix entries now change to "started" before the execution call; that means an
execution attempt, not proof each method ran. Only an exact successful TRX changes
them to "passed". Nonzero exit, timeout and malformed/missing report retain
"started" with their execution log/error; discover-only retains "not-run".

Before execution, a previous trace-gas.trx is retained byte-for-byte as
trace-gas.previous-<unique-id>.trx. Only the newly emitted current report can verify
this attempt; an old successful report cannot cover a missing new report.

⚠ 2026-11-14: reusing a results directory could previously accept a stale successful
TRX when the new process returned zero without a report. A controlled counterexample
was red; preserving/renaming the old report before execution closes that path.


## Hosted test execution ✅

`hosted_tests.py --lane core|cli|docs --results-dir PATH`: discover and partition all
solution `.Tests.csproj` projects; run the selected lane once with Release/no-build/
-m:1/Category!=LongRunning and per-project TRX; retain manifest/raw logs with source,
commands, states, timing and counters. Exit nonzero on discovery/selection errors,
failed command, missing/empty/skipped/failed/inconsistent TRX; no retries. Core is
all projects except the designated Cli.Tests and Docs.Tests projects. Selection
must be a disjoint exhaustive partition; every selected project executes once.

`test_hosted_tests.py`: bounded stdlib fake-process/TRX and workflow controls; it
never invokes real dotnet test, numerical cases, CUDA, credentials or publishing.
Each runner invocation creates a unique attempt directory, preserving earlier
reports unchanged. Root and attempt manifest.json contain source commit/dirty
state/runner hash, discovery and lane assignments, command vectors and per-project
not-run/started/passed/failed states, UTC and monotonic timings, exit codes and
actual parsed counters. The started record is persisted before process launch;
only a zero exit plus verified fresh nonempty completed TRX can become passed.
Failed projects retain raw logs and report errors and do not prevent the next
selected project from running. Logs are replayed to the workflow console at each
project's completion. This adds no test-process deadline or retry.
