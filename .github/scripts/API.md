# API.md — .github/scripts

## Contract ✅

Host preflight and release-note extraction, plus controlled local guard tests.

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
