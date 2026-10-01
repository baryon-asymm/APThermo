# API.md — protocol-lint

The node exposes a command line and one Python module. Everything else is internal
and may change.

## Command line ✅

```console
$ python protocol_lint.py <tree-root> [--ext .sh,.ps1] [--exclude vendor]
                          [--strict] [--no-heuristics] [--list-nodes]
ERROR 15    src/Orders/BOOT.md: 512 non-blank lines, over the 400-line limit for a leaf node; move what is no longer current truth to HISTORY.md, oldest superseded material first, or declare the deviation (AGENTS.md §12)
ERROR 15    src/Orders/BOOT.md:37: points to HISTORY.md#ghost-anchor, which no anchor in src/Orders/HISTORY.md defines
WARN  7     src/Orders/API.md:12: declares Ghost under a tick, and no source file of this node mentions it
protocol_lint: 2 errors, 1 warnings
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
`2` — invocation error. Directories starting with a dot, except `.github`, and the usual
build directories are always skipped; `.github` is read as part of the tree.

The `AGENTS.md` §15 line-limit check and the `HISTORY.md` citation check run on every
invocation, unconditionally: there is no flag to silence either (`BOOT.md`,
"Constraints"). The size check adds one finding per `BOOT.md` over its limit: an `ERROR`
naming the line count and the limit (250 for a node with children, 400 for a leaf), or,
when the node's own `BOOT.md` carries a line starting `⚠ Declared deviation, §15:`, a
`WARN` that quotes that line so the exemption stays visible in the output, not only in
the document. A node inside its limit produces no finding either way.

A node whose own `BOOT.md` carries a line `⚠ Declared deviation, §6: … replaced by:
## Section, ## Other section` (`AGENTS.md` §15's exemption for a transcription node) is
measured with those named sections' non-blank lines excluded from the count, once each
is confirmed to exist as a real `##` heading in the same document; naming a section
that does not exist is an `ERROR` on its own, independent of whether the document is
over its limit.

`ACCEPTANCE.md` (`AGENTS.md` 3.1) is checked where it stands:

- it must stand beside a `BOOT.md` whose `## Acceptance criteria` is the one line
  `→ [ACCEPTANCE.md](ACCEPTANCE.md)`, and that pointer must resolve to a file: a
  pointer without a file and a file without a pointer are each an `ERROR`;
- it may stand only in a node with children: in a leaf it is an `ERROR`;
- a ticked criterion in it carries a date, a `WARN` like the one in a `BOOT.md`;
- it is held to 400 non-blank lines, an `ERROR` over it, with no deviation available;
- a root `BOOT.md` with the pointer is measured against 400 lines instead of 250.

Independent of the size check, every `HISTORY.md#<anchor>` citation is resolved,
wherever it is written: a `BOOT.md`, an `API.md`, `ACCEPTANCE.md` or any other document
of the tree, or a comment in the code under `src/` or `tests/`, inside backticks or out
of them (a citation inside a `HISTORY.md` itself is not checked, and neither is one in a
fenced block of a document). A **bare** citation is resolved against the citing file's
own node or one of that node's ancestors; a **qualified** one (a path from the tree root,
or a `../`-relative path, before `HISTORY.md#<anchor>`) against the node it names. A
missing anchor, a missing `HISTORY.md` entirely, and a bare citation of a neighbour's
anchor are all an `ERROR` naming the anchor. The one string this never resolves is the
placeholder written as an example, an anchor starting with `<`: it is no citation to
begin with.

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
