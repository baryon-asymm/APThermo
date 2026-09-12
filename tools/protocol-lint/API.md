# API.md — protocol-lint

The node exposes a command line and one Python module. Everything else is internal
and may change.

## Command line ✅

```console
$ python protocol_lint.py <tree-root> [--ext .sh,.ps1] [--exclude vendor]
                          [--strict] [--no-heuristics] [--list-nodes]
ERROR 1     src/Orders/API.md: a source directory is a node and a node carries both documents; this one is missing
WARN  7     src/Orders/API.md:12: declares Ghost under a tick, and no source file of this node mentions it
protocol_lint: 1 errors, 1 warnings
```

| Argument | Meaning |
|---|---|
| `<tree-root>` | the tree root: the directory holding `AGENTS.md` |
| `--ext` | additional source extensions, comma-separated |
| `--exclude` | additional directory names to skip |
| `--strict` | warnings also produce a non-zero exit code |
| `--no-heuristics` | skip the textual check of names under ✅ |
| `--list-nodes` | print the nodes found and exit |

Exit code: `0` — no errors, `1` — there are errors (with `--strict`, warnings too),
`2` — invocation error. Directories starting with a dot and the usual build
directories are always skipped.

## Python module ✅

```python
def lint(root, extra_extensions=(), extra_excluded=(), heuristics=True): ...
def main(argv=None): ...

class Finding:                 # level: ERROR | WARN, article, where, message
    ...
```

`lint` returns the list of findings ordered by location; `main` returns the exit code.
Neither writes to disk. For a project wiring the linter into its own test set the entry
point is `lint`: findings are easier to assert on than to parse from the text output.

## What this node does not do

- it does not check the code against the documents: those are the reflection checks,
  written for the project's stack (`AGENTS.md`, §13);
- it does not check whether the document tells the truth;
- it does not edit documents or code.
