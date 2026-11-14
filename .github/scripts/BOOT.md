# BOOT.md — .github/scripts

## Purpose

Host preflight and release-note extraction, plus controlled local guard tests.

## Invariants

Missing tools, failed native commands and mismatched literal version headings fail; release inputs are data.

## Dependencies

None.

Outside the tree: host shells and GitHub Actions versions inherited from the ancestor.

## Constraints

Linux preflight has the same native-status and global.json SDK-resolution obligation as Windows.
Bash guards use controlled functions for all three host tools and explicit missing
command discovery, so no test reaches a real NVIDIA command on either test host.

Windows preflight runs under Windows PowerShell 5.1, without Python or Git Bash. Bash release scripts run on hosted Linux. Local tests may use Python and controlled fake commands; never call a GPU or publish.

## Acceptance criteria

- [x] 2026-11-14 — Linux native parity and SDK-resolution guards (`LinuxPreflightGuards`)
  pass valid/roll-forward prerequisites and refuse missing/nonzero-native tools plus
  malformed resolver output. Windows also accepts multiple git executables by using
  the first PATH application. `test_delivery.py`: 20/20 on Windows (Release 7,
  Bash preflight 6, Windows PowerShell 5.1 preflight 7); Linux hosted execution runs
  the first two groups only. Five resolver/multiple-path controls were red before
  this correction (`SCRATCH/audit/sdk-resolver-red.txt`, `sdk-resolver-green.txt`).

- [x] A23/A34: missing/native-failing tools refuse, a valid preflight passes, and release notes match the exact version heading. (2026-11-14, `test_delivery.py`).

  ⚠ 2026-11-14: was "2026-11-14 — A23/A34: missing/native-failing tools refuse, a valid preflight passes, and release notes match the exact version heading. Evidence: `test_delivery.py`, Windows PowerShell 5.1 controlled .cmd probes plus Bash literal-version cases; 11 guards pass together, eight failures on frozen pre-fix sources." The preservation guard rejected that free-form completion because its added prose was not a bounded dated citation. The original requirement is unchanged above, with a dated test citation; the measurements remain recorded in this note.
- [ ] Actual release dispatch on the Windows/Linux self-hosted runner classes proves
  the preflight path on GitHub; local controlled probes are narrower evidence.

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
