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

- [ ] Both OS branches invoke their matching script, with explicit shells; release dispatch on both target runner classes remains required.

## Taboos

Do not silently skip a missing prerequisite or install host software.
