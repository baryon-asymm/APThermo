# BOOT.md — .github/scripts

## Purpose

Host preflight and release-note extraction, plus controlled local guard tests.

## Invariants

Missing tools, failed native commands and mismatched literal version headings fail; release inputs are data.

## Dependencies

None.

Outside the tree: host shells and GitHub Actions versions inherited from the ancestor.

## Constraints

Windows preflight runs under Windows PowerShell 5.1, without Python or Git Bash. Bash release scripts run on hosted Linux. Local tests may use Python and controlled fake commands; never call a GPU or publish.

## Acceptance criteria

- [x] 2026-11-14 — A23/A34: missing/native-failing tools refuse, a valid preflight passes,
  and release notes match the exact version heading. Evidence: `test_delivery.py`,
  Windows PowerShell 5.1 controlled .cmd probes plus Bash literal-version cases;
  11 guards pass together, eight failures on frozen pre-fix sources.
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
