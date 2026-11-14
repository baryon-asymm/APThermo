# API.md — .github/workflows

## Contract ✅

GitHub event and job orchestration for hosted verification and approved releases.

`ci.yml`: hosted Windows/Linux verification for pushes, PRs and manual dispatch.
`release.yml`: hosted verification, sequential self-hosted CUDA jobs, packing;
only tag pushes may reach approved publishing/release creation.

## CPU scan lane ✅

`trace-gas.yml`: hosted CPU scan verification on affected pushes/PRs and manual
dispatch, with discovery and execution artifacts. See the ancestor's delivery policy.

⚠ 2026-11-14: release shell ref interpolation is replaced by quoted environment
values. Hosted matrices execute strict protocol lint and `scripts/test_delivery.py`.
Windows runs the PowerShell 5.1 probes; both hosts run Bash release guards.

Affecting changes include src, Equilibrium.Tests, fixture/harness code, shared build
files and .github delivery code. Both push and PR select that lane on hosted Windows
and Linux; manual dispatch is available after the workflow reaches the default branch.
Evidence upload runs even on failure and refuses an empty artifact selection.
