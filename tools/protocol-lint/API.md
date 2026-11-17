# API.md — protocol-lint

The node exposes the linter command/module and a document-history command.
Everything else is internal and may change.

## Command line ✅

```console
$ python protocol_lint.py <tree-root> [--ext .sh,.ps1] [--exclude vendor]
                          [--source-root <path> ...] [--strict] [--no-heuristics] [--list-nodes]
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
| `--source-root` | additionally scan an existing owned subtree below an excluded directory; repeatable, must remain inside the tree |
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

`ACCEPTANCE.md` (`AGENTS.md` 3.2) is checked where it stands, in any node, a leaf
included:

- it must stand beside a `BOOT.md` whose `## Acceptance criteria` is the one line
  `→ [ACCEPTANCE.md](ACCEPTANCE.md)`, and that pointer must resolve to a file: a
  pointer without a file and a file without a pointer are each an `ERROR`;
- a ticked criterion in it carries a date, a `WARN` like the one in a `BOOT.md`; the
  whole file is read, with or without a `## Acceptance criteria` heading;
- it is held to 400 non-blank lines: an `ERROR` over it, or, when the node's own
  `BOOT.md` carries a line starting `⚠ Declared deviation, §15:`, a `WARN` quoting that
  line;
- a root `BOOT.md` with the pointer is measured against 400 lines instead of 250; the
  pointer changes no other node's limit.

  ⚠ 2026-10-01: stood "it may stand only in a node with children: in a leaf it is an
  `ERROR`" and "with no deviation available", and the dates were read in the section
  only; now as above (`AGENTS.md` 3.2).

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
def lint(root, extra_extensions=(), extra_excluded=(), heuristics=True, source_roots=()): ...
def main(argv=None): ...

class Finding:                 # level: ERROR | WARN, article, where, message
    ...
```

`lint` returns the list of findings ordered by location; `main` returns the exit code.
Neither writes to disk. For a project wiring the linter into its own test set the entry
point is `lint`: findings are easier to assert on than to parse from the text output.

## Document history command ✅

```console
$ python -X utf8 tools/protocol-lint/history_guard.py <repo> <base> <branch> <node> [<node> ...]
$ python -X utf8 tools/protocol-lint/history_guard.py --criteria-only <repo> <before> <after>
$ python -X utf8 tools/protocol-lint/history_guard.py --help
```

The first form checks a document move: lost/stray text, append-only history,
anchors and current criteria. The second checks the changed acceptance documents
between two commits or trees; code changes are permitted. Each old checkbox and
its associated evidence must remain in a current acceptance section or correctly
linked `ACCEPTANCE.md`, with multiplicity preserved. A copy in `HISTORY.md` does
not satisfy that obligation. Dated completion/re-verification and a documented
revision with a concrete successor are allowed; merely adding a date or a vague
replacement is not. Missing criteria or malformed pointers print `GUARD:` lines
and exit 1, otherwise the command exits 0. Semantic adequacy of the replacement
and evidence remains the reviewer's responsibility.

`--help` prints both checking forms and exits successfully without opening a repository.
⚠ 2026-11-17: the option formerly raised `ValueError` while unpacking positional arguments.

⚠ 2026-11-14: the old form checks accepted date-shaped invalid dates, directory
link targets and extra masked content beside an acceptance pointer (audit A24).
Those exact physical/calendar checks now apply. The separately tested history
matcher supplies merge-guard's current-criterion check (audit A20).

## What this node does not do

- it does not check the code against the documents: those are the reflection checks,
  written for the project's stack (`AGENTS.md`, §13);
- it does not check whether the document tells the truth;
- it does not edit documents or code.

## Source discovery ✅

Default source extensions include `.sh` and `.ps1`; build manifests include
`.props`/`.targets` and action.yml/action.yaml. Workflow .yml/.yaml files directly
under .github/workflows are source. Other YAML is data/configuration unless `--ext`
selects it. Existing source, manifest and document-pair discovery remains unchanged.

⚠ 2026-11-14: shell/MSBuild/action/workflow nodes were previously omitted by default;
A22 extends discovery while keeping arbitrary YAML data distinct.
