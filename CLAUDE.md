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

- Protocol lint: `python -X utf8 tools/protocol-lint/protocol_lint.py . --exclude templates`
  (`-X utf8` avoids a cp1252 crash on Windows consoles; `--exclude templates` keeps the
  document templates under `docs/protocol/templates/` from being read as nodes).
- Build: `dotnet build AerospacePropellantThermodynamics.sln`
- Tests, fast set: `dotnet test AerospacePropellantThermodynamics.sln --filter "Category!=LongRunning"`
- Tests, full set: `dotnet test AerospacePropellantThermodynamics.sln`

## Repository

- Branch `main`, Conventional Commits, MIT license.
- English in every document, identifier, comment and commit message, the protocol kit
  (`AGENTS.md`, `docs/protocol/`, `tools/protocol-lint/`) included.
- Nothing in this repository is secret; nothing here may be published on behalf of the
  owner without being asked.
