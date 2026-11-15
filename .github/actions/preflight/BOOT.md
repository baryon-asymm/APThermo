# BOOT.md — .github/actions/preflight

## Purpose

Select the supported host preflight script before self-hosted GPU work.

## Invariants

Windows uses powershell (5.1); Linux uses bash; neither assumes a CUDA Toolkit. Script failures fail the action.

## Dependencies

[scripts](../../scripts/API.md)

Outside the tree: host shells and GitHub Actions versions inherited from the ancestor.

## Constraints

Use the checked-out action path to invoke the script and inherit .github host requirements. The action does not provision tools or change the machine.

## Acceptance criteria

- [x] Both OS branches invoke their matching script, with explicit shells; release dispatch on both target runner classes remains required.
  (2026-11-15: Release dispatch `43691442012` at exact `0f7ecb6f4fff` passes both
  preflight/CUDA paths, Windows job `132747447148` and Linux job `132748505091`;
  explicit matching shells and actual runner labels are retained in the final proof index.
  External audit index: `fix-evidence/github-0f7/final-proof-index-0f7-v1.json`.)
  (2026-11-15: exact source `f21ac451c286`. Both native preflights pass on their
  declared runner classes.
  CI `43742270400`, Trace `43742270272`, Release `43742272942`.
  Source-bound reports, controls and artifact inventory:
  external `fix-evidence/github-f21a/final-proof-index-f21a-v1.json`.)

## Taboos

Do not silently skip a missing prerequisite or install host software.
