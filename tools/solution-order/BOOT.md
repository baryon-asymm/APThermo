# BOOT.md — solution test ordering

## Purpose

Protect the solution-level build barrier used by `dotnet test APThermo.sln`.
The root `Directory.Solution.targets` ensures a complete solution Build finishes
before the generated solution project dispatches VSTest, within the same command.

## Invariants

The root target runs before solution `VSTest`, depends on solution `Build`, and is
conditional on `VSTestNoBuild` not being true. Failed Build prevents all VSTest
dispatch. An explicit `--no-build` must not run the barrier. The Build dependency
does not invoke VSTest, and ordinary solution Build does not invoke test execution.
Project references, selected tests, configuration and native failures are preserved.
The barrier starts no separate process and does not alter individual project builds.

## Dependencies

[root](../../API.md) — root solution and its solution-level target file.

Outside the tree: Python 3.8+ standard library and the .NET SDK selected by `global.json`.

## Constraints

`test_solution_order.py` copies the actual root target file to a fresh temporary
two-project solution with synthetic Build and VSTest targets. Copy root `global.json`
and verify the selected SDK agrees with the repository. Native `dotnet test`
controls check the barrier, deliberate build failure, explicit `--no-build`, and
ordinary `dotnet build`. Each call has a finite timeout. The existing EndToEnd tool
runner discovers and runs this file inside its owned process scope. Test subprocesses
disable MSBuild node reuse through their own environment, without changing the caller's.
Frozen controls must turn red if the hook or its Build dependency is removed or if
the no-build condition is removed. No actual numerical or CUDA work is done here.
This synthetic solution proves sequencing, not the production test population:
the latter is checked separately from generated real discovery/TRX and build logs.

## Acceptance criteria

- [x] The synthetic solution's positive and frozen negative controls prove ordering,
  build failure propagation, no recursion, and `--no-build` preservation.
  (2026-11-17: `test_solution_order.py` and source-frozen `workers/solution-order/sdk-pinned/controls.json`.)
- [x] Real solution clean/incremental builds and selected populations are accepted separately.
  (2026-11-17: Debug `Rebuild` and the following incremental build succeeded for the full
  solution at `d7cc333a`. The Windows software run passed 7,818 fast cases and 121 end-to-end
  cases. One optional CORE-MATH case retained its earlier skip. Against the same local paths
  at `779eeace`, the project, test ID, name and outcome comparison retained every original
  result and added 22 passing cases. The owner-held `closure-opus55-5f-20261117/root/`
  contains `rebuild.json`, `incremental.json`, their binary logs, and
  `population-check-independent-final.json`.)

## Taboos

- Do not suppress failures, alter selectors, or weaken a build or timing threshold.
- Do not start actual CUDA, invoke publication, or mutate the repository in controls.
