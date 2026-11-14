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

- [ ] A23/A34: missing/native-failing tools refuse, a valid preflight passes, and release notes match the exact version heading.

## Taboos

Do not infer success from a stale LASTEXITCODE or interpolate untrusted text into shell source.
