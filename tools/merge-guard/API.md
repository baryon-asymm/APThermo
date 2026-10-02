# API.md — merge-guard

The node exposes one command line, run from the main checkout. Everything else is
internal and may change.

## Command line ⏳

```console
$ python -X utf8 tools/merge-guard/merge_guard.py <branch> --scope <regex> [--scope <regex> ...]
                 [--approve <regex> ...] [--overlap <regex> ...] [--doc-nodes <node> ...]
                 [--checks-only] [--cuda] [--merge <message-file>] [--commands <json-file>]
ok    preconditions: target protocol-3.1, merge base 34c0be8, 2 coder commits
ok    scope: 29 paths, all inside the scope
ok    approved records: none changed
ok    documents: 4 nodes, 260 lines moved, 0 lost, 0 stray, 0 unresolved, HISTORY.md append-only
ok    overlap: none
ok    trial merge: no conflict (worktree removed on exit)
ok    lint: protocol_lint: 0 errors, 0 warnings
ok    build: 0 Warning(s), 0 Error(s)
ok    fast suite: 11 assemblies, 5442 passed, 0 failed
ok    merged: 9f3e2a1, tree equal to the trial tree
merge-guard: green (log: <temp>/merge-guard-20261002-0612.log)
```

| Argument | Meaning |
|---|---|
| `<branch>` | the coder's branch, merged into the current branch of the main checkout |
| `--scope` | a pattern every path the branch changed must match; at least one is required |
| `--approve` | a pattern of approved records (`*.approved.txt`) the branch may change |
| `--overlap` | a pattern of files allowed to have changed on both sides since the merge base |
| `--doc-nodes` | the nodes whose documents the moved-text check reads (`.` for the root) |
| `--checks-only` | run the static checks (preconditions to overlap) and stop: no trial, no merge |
| `--cuda` | add the CUDA proofs of the reference machine to the trial, behind the GPU lock |
| `--merge` | after a green trial, merge with this file as the commit message; without it the guard stops after the trial |
| `--commands` | replace the command table with the one in a JSON file (the self-test's stubs) |

Exit code: `0` — every check green (and the merge made, with `--merge`); `1` — a check
failed, printed as one line `GUARD: <what>`; `2` — invocation error.

## The checks, in order ⏳

1. **Preconditions**: the current branch is not `main`; the main checkout has no
   tracked change; the branch exists and has at least one commit over the merge base;
   the coder's worktree, if `git worktree list` shows one for the branch, has no change.
2. **Scope** (`--scope`): every path changed between the merge base and the branch.
3. **Approved records** (`--approve`): every changed `*.approved.txt`, listed with its
   line counts; one outside the patterns fails. A pattern matching no file of the tree
   fails as an empty walk.
4. **Documents** (`--doc-nodes`, skipped without it): the moved-text rules of
   `BOOT.md`, `## Constraints`, plus the append-only rule over every `HISTORY.md`.
5. **Overlap** (`--overlap`): files changed on both sides since the merge base.
6. **Trial merge**: a detached worktree at the target's head in the system temp
   directory, `git merge --no-ff --no-commit <branch>`.
7. **Commands**, in the trial worktree, in the order of the command table.
8. **Merge** (`--merge`): `git merge --no-ff --no-commit` in the main checkout, the
   tree compared with the trial's, then `git commit -F <message-file>`; on a different
   tree, `git merge --abort` and a failure.

## The command table ⏳

| Name | Command (run in the trial worktree) | Green when |
|---|---|---|
| lint | `python -X utf8 tools/protocol-lint/protocol_lint.py . --exclude templates --strict` | exit 0 |
| build | `dotnet build APThermo.sln` | exit 0 and `0 Warning(s)` |
| fast suite | `dotnet test APThermo.sln --no-build --filter "Category!=LongRunning"` with `APTHERMO_NO_CUDA=1` | exit 0 |
| cuda build (`--cuda`) | `dotnet build APThermo.sln -c Release` | exit 0 and `0 Warning(s)` |
| cuda proofs (`--cuda`) | `dotnet test APThermo.sln -c Release --no-build --filter "Category=Cuda\|Category=BitSnapshot"` | exit 0 |
| execution (`--cuda`) | `dotnet test tests/Execution.Tests -c Release --no-build` | exit 0 |

The `--commands` file is a JSON array of objects `{"name", "argv", "env", "requires":
"cuda"|null, "green_when_output_contains": string|null}`, replacing the table whole.
