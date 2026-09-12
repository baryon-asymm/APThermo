# BOOT.md — Protocol.Tests

## Purpose

The half of the protocol's machine checks that needs reflection over the build
(`AGENTS.md` §13), written for this stack, plus the wiring of the language-independent
linter into the test set. It guards the tree against the code: the public surface
against its snapshot, exported types against `API.md`, declarations under ✅ against
the assemblies, declared dependencies against the real ones.

| Level | What it checks | Against what (source of truth) | State |
|---|---|---|---|
| Lint | the file half of the protocol | `tools/protocol-lint` run as a process | ⏳ |
| Surface | the public surface of every assembly of the tree equals `PublicSurface.approved.txt` | the approved snapshot | ⏳ |
| Coverage | every exported type is named in the `API.md` of its node (node by directory path) | the documents | ⏳ |
| Declarations | every type and member under ✅ exists | the assemblies | ⏳ |
| Dependencies | `## Dependencies` of every node equals the types used in signatures and method bodies, ancestors allowed, descendants never | the assemblies' IL | ⏳ |

## Invariants

- **Nodes are found by directory path** from the tree root, never by the last segment
  of a namespace; the tree root is found from the test source file.
- **All assemblies of the tree are searched**: `src/*` and `tests/*` projects.
- **Every check has been seen red once** by the mutations named in the acceptance
  criteria, and none is ever marked skipped.
- **Links inside code blocks and backticks are not resolved.**
- The IL walk reads the opcode table from the runtime (`OpCodes`), not from a copy.

## Dependencies

None.

Outside the tree: xunit; Python 3.8+ for the linter process; the reference
implementation of the checks in the protocol kit (`reference/dotnet/`), adapted as
its README requires: English headings, node by path, several assemblies, parent and
children rule, links check removed.

## Constraints

- Part of the default test command.
- The assemblies are loaded from the build output found through the project
  references of this test project; the test project references every node's project
  for that reason only and uses none of their types.
- A missing snapshot is written once and the test fails with instructions; the actual
  snapshot on a mismatch is `PublicSurface.actual.txt` next to the approved one, git-ignored.

## Acceptance criteria

- [ ] Lint wired: the test fails when the linter reports an error (date, test name).
- [ ] Surface, Coverage, Declarations, Dependencies green on the tree (date, test names).
- [ ] Each check shown red once: a public type added and not documented; a member
      renamed in an `API.md` under ✅; a dependency line removed; a public member added
      without updating the snapshot (date, mutation, observed message).

## Taboos

- Do not make a check pass by editing the documents when the check is wrong: fix the check.
- Do not keep a red check; fix or remove it with a declared deviation.
- Do not look up nodes by namespace segment.
