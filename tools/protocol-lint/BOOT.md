# BOOT.md — protocol-lint

## Purpose

The language-independent half of the protocol's machine checks (`AGENTS.md`, §13). The
node checks **the document tree against the protocol**, not the code against the
documents: everything for which reading files is enough lives here; everything that
needs reflection over a build is written for the specific stack and lives in the
project's tests node.

The node is placed in the tree as an ordinary node (for example `tools/protocol-lint/`)
and is run before and after work in any node.

## Invariants

- **Standard library of Python 3.8+ only.** The protocol check must run where nothing
  has been installed yet, including on the first commit of a new repository, before a
  package manager has been chosen.
- **Not a single change on disk.** The linter only reads: a check that edits what it
  checks hides the divergence instead of showing it.
- **Every check is named after the article of the protocol** it is based on. A check
  without an article is the linter author's personal opinion about someone else's project.
- **The split between errors and warnings is substantive**: an error is a violation of
  an article, a warning is something that can be legitimate (a textual heuristic, an
  obsolete form). By default only errors fail the run; `--strict` makes warnings fail
  it too.
- **Links and headings inside code do not count**: the examples in `AGENTS.md` are
  written in backticks precisely so that they remain examples.

## Dependencies

None.

Outside the tree: Python 3.8+ (standard library), `unittest` for the self-test.

## Constraints

- The node knows none of the project's programming languages: the list of source
  extensions is a parameter, not knowledge.
- A false positive costs more than a miss: the linter runs on every commit, and noise
  in it devalues the real findings. That is why the textual heuristic (names under ✅)
  yields a warning, not an error, and is switched off by a flag.
- Output is one line per finding, with path and line number: it is read by a human in
  a terminal and by an agent in a tool's output.

## Acceptance criteria

- [x] Every check is proven non-degenerate: exactly one breakage is applied to a
      conformant tree and the check turns red — 27 tests
      (2026-09-12, `test_protocol_lint.py`).
- [x] Two guard tests against false positives: a grouping directory is not counted as
      a node, a link inside a code block is not resolved
      (2026-09-12, `test_a_grouping_directory_is_not_a_node`,
      `test_links_inside_code_are_not_followed`).
- [x] Verified on a real tree, not only on a fixture: a tree of 52 nodes (the
      PastyPropellant reconstruction) and the tree of the port, both parsed in full,
      and every finding explained (2026-09-12).
- [ ] The self-test is not run automatically by anything except a manual
      `python -m unittest`: in a project it has to be wired into the project's own test set.
- [ ] The checks that need reflection are not implemented here and cannot be: that is
      a separate node for the project's stack (`AGENTS.md`, §13).

## Taboos

- Do not check the content of documents: whether what is written is true, the machine
  does not know and cannot know.
- Do not add checks without an article of the protocol. A new check needs the article
  first, then the code.
- Do not introduce external dependencies or configuration files: the parameters are flags.
- Do not repair the tree automatically. The linter reports; a human or an agent decides.
