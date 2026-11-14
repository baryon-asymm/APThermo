# BOOT.md — shared test build

## Purpose

Owns `Directory.Build.props`, the common test-project build policy. Verification
responsibilities and evidence stay in the individual test nodes; this node supplies
their shared manifest rather than a second inventory of their facts.

## Invariants

- Root build, diagnostics and test-budget constraints apply to every child.
- Test SDK, xUnit and runner versions come from the root package-version manifest.
- Projects that explicitly set `IsTestProject=false` stay plain libraries.
- This manifest adds no numerical implementation or assertion.

## Dependencies

None.

Outside the tree: Microsoft.NET.Test.Sdk 17.14.1, xunit 2.9.3 and
xunit.runner.visualstudio 3.1.4, pinned by `Directory.Packages.props`.

## Constraints

Import the ancestor build properties before adding the test defaults. Child projects
inherit the test SDK, runner and xUnit using only when classified as test projects.
No diagnostic is suppressed and no child may relax the root's requirements.

## Acceptance criteria

- [x] Project evaluation preserves test discovery and the plain-library opt-outs;
  solution build and affected test discovery provide the evidence.
  (2026-11-15: both c0df Release solution builds report 0 warnings/errors and actual
  output manifests.  both full CPU cohorts discover/execute all 11 test assemblies
  with 7,611 passing cases. Plain fixture/harness/process-probe opt-outs remain.
  external `final-candidate/c0df7b146ffd-windows-build/result.json`,
  `final-candidate/c0df7b146ffd-linux-build/result.json`,
  `final-candidate/c0df7b146ffd-windows-cpu/result.json` and
  `final-candidate/c0df7b146ffd-linux-cpu/result.json` record that inventory.)
- [x] Protocol discovery recognizes this build-manifest directory and its documents.
  (2026-11-15: c0df strict protocol lint is 0 errors/0 warnings and current discovery
  controls pass. External `c0df7b146ffd-windows-tools-revalidated-parser-v2/result.json`
  validates the unchanged original process logs (10 exits 0, 281 methods).  it is
  metadata revalidation, not another execution. Original adapter exit 3 is retained.)

## Taboos

- No duplicated root compiler or package-version settings.
- No readiness claims for a child whose own verification has not run.
- No project-specific exceptions in this shared manifest.
