# CLAUDE.md

Loader. This repository is run by the document-tree protocol; the protocol and the
tree root are imported below and enter the context of every session.

@AGENTS.md
@BOOT.md

⚠ No claims about the system live here and none may be added (`AGENTS.md`, §2).
Everything about the system lives in the `BOOT.md` and `API.md` of the nodes: a second
source of truth drifts from the first sooner or later, and the tree cannot correct it.

## Before any work

Start procedure: `AGENTS.md`, §10. Read the chain of `BOOT.md` from the task's node up
to the root, the `API.md` of the neighbours listed in that node's `## Dependencies`,
decide the mode (design or coding), run the linter before and after the work.

## Commands

- Protocol lint: `python -X utf8 tools/protocol-lint/protocol_lint.py . --exclude templates --strict`
  (`-X utf8` avoids a cp1252 crash on Windows consoles; `--exclude templates` keeps the
  document templates under `docs/protocol/templates/` from being read as nodes).
- Build: `dotnet build APThermo.sln`
- Tests, fast set: `dotnet test APThermo.sln --filter "Category!=LongRunning&Category!=EndToEnd"`
- Tests, end-to-end set: `dotnet test APThermo.sln --filter "Category=EndToEnd"`
- Tests, full set: `dotnet test APThermo.sln`
- Release GPU remainder: `dotnet test APThermo.sln --configuration Release --no-build -m:1 --filter "(Category=Cuda|Category=BitSnapshot)&FullyQualifiedName!=APThermo.Execution.Tests.CudaTests.TheSweepOf100000CasesOnCudaEqualsTheCpuAcceleratorBitForBitAndIsDeterministic&FullyQualifiedName!=APThermo.Execution.Tests.CudaTests.ThroughputIsRecordedAndNotBelowTheApprovedRatio"`
- Release GPU sweep: `dotnet test tests/Execution.Tests/APThermo.Execution.Tests.csproj --configuration Release --no-build -m:1 --filter "FullyQualifiedName=APThermo.Execution.Tests.CudaTests.TheSweepOf100000CasesOnCudaEqualsTheCpuAcceleratorBitForBitAndIsDeterministic|FullyQualifiedName=APThermo.Execution.Tests.CudaTests.ThroughputIsRecordedAndNotBelowTheApprovedRatio" -- RunConfiguration.TreatNoTestsAsError=true`

## Repository

- Branch `main`, Conventional Commits, MIT license.
- English in every document, identifier, comment and commit message, the protocol kit
  (`AGENTS.md`, `docs/protocol/`, `tools/protocol-lint/`) included.
- Nothing in this repository is secret; nothing here may be published on behalf of the
  owner without being asked.
