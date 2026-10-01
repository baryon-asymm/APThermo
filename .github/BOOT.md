# BOOT.md — .github

## Purpose

The repository's continuous-integration and release configuration and the one tool that
configuration runs: the workflows `ci.yml` and `release.yml`, the composite actions they
share under `actions/`, the scripts under `scripts/`, and `diagnostics/IsaProbe`, the probe
of the runner-diagnostics step (a node of its own). It gives the tree the proof, on every
push and pull request, that a commit builds and passes the fast suite on a Windows and a
Linux runner, and, on a tag, the path from the tested commit to the packages on nuget.org.
It holds no product code and no formula: a step calls the tree's own projects, scripts and
tools. Its contract is the list of workflows, jobs, actions and scripts in [API.md](API.md).

## Invariants

- **Self-hosted runners** never run a pull request's code. GPU jobs trigger only on tags
  and on manual dispatch, and the runners run under an account without administrator
  rights, started for a release rather than kept as services.
  - What a runner must provide, checked by a preflight step that names the missing
    item: git, the .NET SDK of `global.json`, an NVIDIA driver (`nvidia-smi`), libnvvm
    and `libdevice.10.bc` where the execution node's discovery looks, and no
    `APTHERMO_NO_CUDA`. Nothing else is assumed: no PowerShell 7, no Python, no Git
    Bash. A step of a self-hosted job names its shell explicitly, `powershell` on
    Windows and `bash` on Linux.
- **Evidence for workflow changes.** A change under `.github/` runs only on GitHub, so it
  is accepted on a run of the path it changes, on the runner class it targets: a CI
  run for `ci.yml`, a dispatch run for `release.yml`. A review or a linter is not
  enough.
- **No second implementation of a check.** A step calls the tree's own command or script
  (the protocol lint, `regenerate.py`, the harness types) and does not restate what it
  checks; the one comparison the packed tool's example needs is the harness's, referenced.

## Dependencies

- [protocol-lint](../tools/protocol-lint/API.md) — the lint command `ci.yml` runs first.
- [Fixtures](../tests/Fixtures/API.md) — `regenerate.py --check --sample`, the step that
  binds the fixtures to the generator.
- [Harness](../tests/Harness/API.md) — `RunPropertyCut` and `JsonFieldComparison`, which the
  scratch program of the packed tool's example step references.
- [Samples](../samples/Samples/API.md) — its `## Scenarios` table, which the step that runs
  every sample against the package parses.
- [Docs.Tests](../tests/Docs.Tests/API.md) — the approved outputs the samples and the
  packed tool's example are compared with.
- [Problems](../src/Problems/API.md) and [Cli](../src/Cli/API.md) — the two projects the
  workflows pack.

Outside the tree: GitHub Actions with the actions `actions/checkout` 7,
`actions/setup-dotnet` 6, `actions/setup-python` 7, `actions/upload-artifact` 7,
`actions/download-artifact` 8 and `NuGet/login` 1; the hosted runners `windows-latest` and
`ubuntu-latest`; two self-hosted runners of the reference machine (Windows, and Linux under
WSL2); nuget.org with Trusted Publishing; the .NET SDK of `global.json`; Python 3 and the
`cea` package of `tests/Fixtures/generate/requirements.txt` for the fixtures step;
actionlint, whose label `gpu` is declared in `actionlint.yaml`.

## Constraints

A rule below that names the platform constraint or Documentation names the root's
`BOOT.md` (`## Constraints`, `## Delivery`); the rules of the root bind every workflow here,
and nothing is pushed to GitHub or nuget.org without the owner's word.

- **Continuous integration.** GitHub Actions under `.github/workflows`.

  ⚠ 2026-10-01: was `.github` not a node, now it and IsaProbe are → HISTORY.md#isaprobe
  - Every push and pull request, on Windows and Linux hosted runners: the protocol lint,
    the build, the fast suite with `APTHERMO_NO_CUDA=1` and without the bit snapshots
    (`Category!=BitSnapshot`, the ⚠ of 2026-09-18 under the platform constraint), and
    packing both packages. The release's self-hosted jobs on the reference machine run
    the bit snapshots with the CUDA tests. Then the samples run against the fresh
    `APThermo` package from a local feed, the tool installed from that feed runs an
    approved example, and the docs tests run (the ⚠ of 2026-09-17 under Documentation).
    The example's output is compared with the approved record field by field, numbers
    within 1e-9 relative, the Documentation rule of 2026-09-29 (a hosted runner's CPU is
    not the reference machine's). → HISTORY.md#delivery-ci-condensed

    ⚠ 2026-09-30: was the example compared byte for byte, now field by field within 1e-9
    relative → HISTORY.md#ci-field-comparison
  - There is no nightly run (2026-09-17).

  ⚠ 2026-09-17: was a nightly run of the long-running tests, now none (they are CUDA
  tests, run at each release) → HISTORY.md#no-nightly-run
- **Release**, on a tag `v*`, in order:
  1. the hosted matrix;
  2. the CUDA tests, the long-running ones included, and the bit snapshots, on two
     self-hosted runners of the reference machine (Windows, and Linux under WSL2), one
     after the other, since they share one CPU and one GPU and the throughput tripwire
     measures both;
  3. packing;
  4. a push to nuget.org through Trusted Publishing, behind an environment the owner
     approves;
  5. a GitHub release with the notes of `CHANGELOG.md`.
- **Rehearsal before the tag** (2026-09-19). A manual dispatch of the release workflow
  runs steps 1 to 3 on the commit to be released, and never 4 or 5. A tag `v<version>`
  is pushed only on a commit whose dispatch run is green through packing, and the tag
  message names that run. A tag is not moved once pushed; a failure after the tag is
  fixed on a new commit, rehearsed, and released under the next patch version.

  ⚠ 2026-09-19: was a workflow verified by reading (four moved tags), now a green
  dispatch rehearsal before any tag → HISTORY.md#rehearsal-first-release
- `diagnostics/IsaProbe` is a node with a pair of its own. The root's Diagnostics constraint
  covers it, since `Directory.Build.props` applies to it and `DiagnosticsTests` reads `.github`.

## Acceptance criteria

- [x] 2026-10-01 — `.github` and `diagnostics/IsaProbe` are nodes and the protocol lint reads
      them: 0 errors for both (`python -X utf8 tools/protocol-lint/protocol_lint.py .
      --exclude templates`, `Protocol.Tests.LintTests`; the lint's own fact
      `test_dot_github_is_read_as_part_of_the_tree` is the reason it sees them).
- [ ] The comment-only edits of 2026-10-01 to the workflows, actions and scripts, which
      cite the nodes the moved rules now live in, run once on GitHub: a CI run for
      `ci.yml`, a dispatch run for `release.yml` (Invariants, "Evidence for workflow
      changes"). Until then no run of those files is dated after the edit.

## Taboos

- No product code and no formula in a workflow, an action or a script.
- No push to nuget.org outside the `publish` job, which waits for the owner's approval of
  the `release` environment, and no tag moved once pushed.
- No pull request's code on a self-hosted runner, and no assumption of PowerShell 7,
  Python or Git Bash there.
