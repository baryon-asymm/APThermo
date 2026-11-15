# BOOT.md — .github/scripts

## Purpose

Host preflight and release-note extraction, plus controlled local guard tests.

## Invariants

Missing tools, failed native commands and mismatched literal version headings fail; release inputs are data.

## Dependencies

None.

Outside the tree: host shells and GitHub Actions versions inherited from the ancestor.

## Constraints

The CPU trace runner derives scan methods from the neighbour's checked API declaration,
requires exact nonempty discovery under Category=LongRunning, and then runs exactly
that filter with APTHERMO_NO_CUDA=1. It records discovery, a generated category/job
matrix and TRX. Successful execution requires the exact discovered methods passed,
no duplicates/skips and matching nonzero TRX counters. A discover-only mode never
claims execution. The workflow builds beforehand; runner commands are argument lists,
with bounded discovery/execution times and no shell interpolation.

Linux preflight has the same native-status and global.json SDK-resolution obligation as Windows.
Bash guards use controlled functions for all three host tools and explicit missing
command discovery, so no test reaches a real NVIDIA command on either test host.

Windows preflight runs under Windows PowerShell 5.1, without Python or Git Bash. Bash release scripts run on hosted Linux. Local tests may use Python and controlled fake commands; never call a GPU or publish.

### Hosted test runner

`hosted_tests.py --lane core|cli|docs --results-dir PATH` is the single implementation
of hosted project selection/execution, rooted from __file__. Discover solution
projects with `dotnet sln APThermo.sln list`; select names ending `.Tests.csproj`,
validate nonempty/unique/contained paths, require CLI/Docs designated projects,
compute all three assignments and prove exact disjoint union. CLI/Docs lanes each
run their named project; core runs every remaining project sequentially.
For each selected project invoke dotnet test Release/no-build/-m:1 with the exact
Category!=LongRunning filter and TRX logger into a distinct project directory.
Record discovered/assigned/selected projects, full command vectors, UTC/monotonic
start/finish, elapsed, exit status and source identity; mark execution started
before invoking, never call a failed/not-executed record passed. Continue remaining
selected projects after a failure but return nonzero overall. Preserve console/raw
execution logs and parse nonempty actual TRX: failed/skipped/inconsistent counters
or missing reports fail. Never retry, filter away a case or change a timeout.
Use only stdlib/Python and declared .NET tools, no extra service/package. Unit
controls inject bounded fake tool/process/TRX fixtures, including a newly added
core project, empty/duplicate/outside/missing discovery, wrong assignment, failed
command, absent/failed/skipped/inconsistent TRX and positive normal cases. Guards
must read actual workflow wiring/runner commands; remove-a-lane/filter mutations
must be red. No numerical/GPU/credential/publication operations in local guards.

## Acceptance criteria

- [x] 2026-11-15 — R15 local controls: `test_hosted_tests.py` passes 15 tests;
  frozen old workflows fail selection guards, and six frozen runner mutations
  fail for lost filter/serialization/start persistence/exit/partition/TRX checks.
  Metadata-only discovery contains 24 solution projects and 11 test projects in
  a disjoint exhaustive generated partition; no real local test matrix ran.
  Evidence: `SCRATCH/audit/hosted-test-lanes/hosted-green.txt`,
  `mutation-results.json`, `actual-discovery-partition.json` in the same directory.


- [ ] R15 runner/guard controls and deliberate wiring/discovery/result mutations
  prove exact project coverage and honest started/failed/complete states; actual
  hosted artifacts confirm all generated assignments and real passed TRX.


- [x] 2026-11-14 — A39 fresh-report control: `test_trace_scan.py` ten tests pass.
  A stale successful TRX plus a new zero-exit/no-report attempt was first accepted
  (red control), then refused while preserving the old report's bytes under a
  unique previous-report name. Evidence: `SCRATCH/audit/trace-stale-report-red.txt`,
  `trace-stale-report-green.txt`.

- [x] 2026-11-14 — A39 matrix truth: `test_trace_scan.py` nine tests pass; nine failure
  subcases were red before started-state persistence (nonzero exit, timeout and
  invalid/missing/skipped reports). Execution attempts retain started; only verified
  exact TRX becomes passed. Discover-only remains not-run. Evidence:
  `SCRATCH/audit/trace-started-red.txt`, `trace-started-green.txt`.

- [x] 2026-11-14 — Trace runner guards (`test_trace_scan.py`, eight tests) reject empty,
  missing, duplicate and extra discovery; nonzero process status; absent, skipped,
  failed or inconsistent TRX. Positive synthetic reports generate a passed matrix;
  actual metadata discovery generates a not-run matrix. No numerical scan was run
  locally. Discovery/TRX bypass mutations are red (four/seven subcases).

- [x] 2026-11-14 — Linux native parity and SDK-resolution guards (`LinuxPreflightGuards`)
  pass valid/roll-forward prerequisites and refuse missing/nonzero-native tools plus
  malformed resolver output. Windows also accepts multiple git executables by using
  the first PATH application. `test_delivery.py`: 20/20 on Windows (Release 7,
  Bash preflight 6, Windows PowerShell 5.1 preflight 7); Linux hosted execution runs
  the first two groups only. Five resolver/multiple-path controls were red before
  this correction (`SCRATCH/audit/sdk-resolver-red.txt`, `sdk-resolver-green.txt`).

- [x] A23/A34: missing/native-failing tools refuse, a valid preflight passes, and release notes match the exact version heading. (2026-11-14, `test_delivery.py`).

  ⚠ 2026-11-14: was "2026-11-14 — A23/A34: missing/native-failing tools refuse, a valid preflight passes, and release notes match the exact version heading. Evidence: `test_delivery.py`, Windows PowerShell 5.1 controlled .cmd probes plus Bash literal-version cases; 11 guards pass together, eight failures on frozen pre-fix sources." The preservation guard rejected that free-form completion because its added prose was not a bounded dated citation. The original requirement is unchanged above, with a dated test citation; the measurements remain recorded in this note.
- [x] Actual release dispatch on the Windows/Linux self-hosted runner classes proves
  the preflight path on GitHub; local controlled probes are narrower evidence.
  (2026-11-15: Release dispatch `43691442012` at exact `0f7ecb6f4fff` passes both
  preflight/CUDA paths, Windows job `132747447148` and Linux job `132748505091`;
  explicit matching shells and actual runner labels are retained in the final proof index.
  External audit index: `fix-evidence/github-0f7/final-proof-index-0f7-v1.json`.)

## Taboos

Do not infer success from a stale LASTEXITCODE or interpolate untrusted text into shell source.

⚠ 2026-11-14: Windows preflight formerly invoked nvidia-smi without discovery and
ignored git's native exit. A missing command inherited the prior successful exit
under PowerShell 5.1. Command discovery and each native result now gate success.
The SDK check formerly accepted a substring (the requested version plus an extra
suffix); it now compares a whole --list-sdks entry. CHANGELOG matching formerly used
an unescaped version regex; literal environment-fed heading matching replaces it.

⚠ 2026-11-14: Linux preflight also masked git failure inside an echo substitution and
matched SDKs with an unescaped substring regex. Separate native-status checks and
literal whole-token SDK matching now enforce the existing prerequisite contract.

⚠ 2026-11-14: the initial exact installed SDK check incorrectly rejected the valid
10.0.112/latestPatch policy when .NET selected 10.0.113. Root's real-host check also
found two git executables, whose Source values were mistakenly joined for invocation.
Both hosts now ask `dotnet --version` in the repository to apply its own global.json
policy; a failed resolver or malformed output fails preflight. Windows selects one
application by PATH order. Earlier controlled tests had not covered these host cases.
The initial Bash parity proof (four cases, two red on old source) is narrower historic
evidence; the expanded resolver/multiple-path controls supersede its SDK requirement.
