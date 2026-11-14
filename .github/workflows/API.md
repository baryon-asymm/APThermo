# API.md — .github/workflows

## Contract ✅

GitHub event and job orchestration for hosted verification and approved releases.

`ci.yml`: hosted Windows/Linux verification for pushes, PRs and manual dispatch.
`release.yml`: hosted verification, sequential self-hosted CUDA jobs, packing;
only tag pushes may reach approved publishing/release creation.

## CPU scan lane ⏳

`trace-gas.yml`: hosted CPU scan verification on affected pushes/PRs and manual
dispatch, with discovery and execution artifacts. See the ancestor's delivery policy.
