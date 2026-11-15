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

## Fixture Python environment ✅

CI selects CPython 3.13 x64. On hosted Windows it installs the existing pinned CEA
and NumPy requirements binary-only in a fresh venv, imports cea.lib.libcea in
isolated mode, reports interpreter/package/native-module provenance, then runs the
unchanged mandatory regenerate.py --check --sample binding comparison. Missing
wheels and native-import failures stop the job; no source build or skipped binding.

⚠ 2026-11-15: floating 3.x selected 3.15.2 without a CEA 3.3.4 cp315 wheel; the sdist
built but libcea failed to load before comparison (hosted job 132731873556).
The published cp313 wheel installs/imports locally on 3.13.15 with NumPy 2.5.3 and
empty PATH, and the unchanged sample compares 38 fixtures. Local evidence does
not replace the required later hosted Windows CI run.

## Hosted test lanes ✅

CI/release hosted jobs partition solution test projects into core/cli/docs per OS,
through scripts/hosted_tests.py. Every lane persists project-selection/command/
monotonic-execution/TRX evidence. Core owns packing, samples, installed CLI and
Windows fixture binding; all hosted release lanes precede CUDA. Existing thirty-
minute release-job and subprocess limits remain unchanged. See scripts/API.md.
