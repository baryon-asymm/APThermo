# API.md — Protocol.Tests

The node exposes nothing outward. Its contract points upward: what the tree may
consider guaranteed about the agreement between its documents and its code.

## What this node guarantees

| Claim | Confirmed by | State |
|---|---|---|
| the tree passes the language-independent linter without errors | Lint level | ⏳ |
| no public surface changes without the snapshot moving in the same commit | Surface level | ⏳ |
| every exported type is named in its node's `API.md` | Coverage level | ⏳ |
| every declaration under ✅ exists | Declarations level | ⏳ |
| declared dependencies match the real ones, in both directions, from signatures and method bodies | Dependencies level | ⏳ |

What it does not guarantee: that a document tells the truth about the code it
names correctly (`AGENTS.md` §13).

## What the tests rely on

- `RepositoryPaths`: the tree root from `[CallerFilePath]`.
- `PublicSurface.approved.txt` in this node, one section per assembly.
- The linter invoked as `python -X utf8 tools/protocol-lint/protocol_lint.py . --exclude templates`
  from the tree root, with Python found on the path; absence of Python is a failure.
