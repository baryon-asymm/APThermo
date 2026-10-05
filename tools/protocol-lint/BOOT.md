# BOOT.md — protocol-lint

## Purpose

The language-independent half of the protocol's machine checks (`AGENTS.md`, §13). The
node checks **the document tree against the protocol**, not the code against the
documents: everything for which reading files is enough lives here; everything that
needs reflection over a build is written for the specific stack and lives in the
project's tests node.

The node is placed in the tree as an ordinary node (for example `tools/protocol-lint/`)
and is run before and after work in any node. Since 2026-10-01 its checks are those of
`AGENTS.md` 3.2, §13's table, adopted word for word by the owner's decision.

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
  obsolete form, a declared deviation). By default only errors fail the run; `--strict`
  makes warnings fail it too.
- **Links and headings inside code do not count**: the examples in `AGENTS.md` are
  written in backticks precisely so that they remain examples. The one exception is a
  citation of a `HISTORY.md` anchor, which is read inside backticks too (`AGENTS.md` §13
  and §15); the placeholder `HISTORY.md#<anchor>` is no citation, since an anchor never
  starts with `<`.
- **The size check (`AGENTS.md` §15) counts, it does not read.** Whether a BOOT.md is
  over its line limit is arithmetic on the file as written; whether what is left in it
  is still current truth is not something this node can tell, so a node's own
  `⚠ Declared deviation, §15:` line is taken on trust, the same way `## Dependencies`'
  `None` is. The same trust extends to a node's own `⚠ Declared deviation, §6: …
  replaced by: ## Section, …` line: this node checks that each named section actually
  exists before excluding its lines, but not that the node genuinely cannot be
  self-sufficient without it.
- **A citation is checked, not trusted.** `→ HISTORY.md#<anchor>` is the one place the
  size machinery writes something a reader is expected to follow; an anchor that does
  not exist defeats the pointer's only purpose, so this is an error, unconditionally,
  not gated behind anything, and it is checked wherever it is written: in a `BOOT.md`, an
  `API.md` or an `ACCEPTANCE.md`, in a comment of the code under `src/` and `tests/`, in
  backticks or out of them.
- **`ACCEPTANCE.md` is current truth, not history** (`AGENTS.md` 3.2). Any node, leaf
  or not, may hold its `## Acceptance criteria` body there instead of in `BOOT.md`,
  leaving the one line `→ [ACCEPTANCE.md](ACCEPTANCE.md)` behind; this node checks
  that the file exists exactly when the pointer does, and that its dates (read over the
  whole file, with or without a section heading) and its own 400-line limit hold, the
  same way `BOOT.md`'s do, the limit with the node's `⚠ Declared deviation, §15:` line
  taken on trust as for a `BOOT.md`. The pointer exempts no node's `BOOT.md` from its
  limit except the root's, which rises from 250 to 400, since it is then measured for
  frame alone, not for evidence too (`AGENTS.md` §15).

  ⚠ 2026-10-01: was "only a node with children may hold it, never a leaf's; its limit
  has no deviation, and the dates are read in its `## Acceptance criteria` section
  only", now any node, a deviation available, the whole file read for dates
  (`AGENTS.md` 3.2, Appendix D). On one tree eight leaves stood at 396–400 lines while
  their criteria grew; on the other, splitting the criteria out brought five of seven
  leaves inside their limit; and an undated tick in a file without the section heading
  had passed unseen.

## Dependencies

None.

Outside the tree: Python 3.8+ (standard library), `unittest` for the self-test.

## Constraints

- The node knows none of the project's programming languages: the list of source
  extensions is a parameter, not knowledge. The one place the code is read for a
  citation, outside the node documents, is the files under `src/` and `tests/` that the
  scan already treats as source.
- A false positive costs more than a miss: the linter runs on every commit, and noise
  in it devalues the real findings. That is why the textual heuristic (names under ✅)
  yields a warning, not an error, and is switched off by a flag.
- Output is one line per finding, with path and line number: it is read by a human in
  a terminal and by an agent in a tool's output.
- The size check and the citation check run unconditionally: a flag a caller can
  forget is not a check, and a known backlog is declared by the nodes that carry it
  (`AGENTS.md` §12, §13), not by a switch that lets the linter stay silent about it.
- The `--exclude` option and the built-in exclusion of dot directories and build
  directories apply to every check alike, the new ones included; the tree's own command
  (`CLAUDE.md`) passes `--exclude templates` so that the document templates are not read
  as nodes. One dot directory is read as part of the tree, `.github` (2026-10-01): it holds
  committed configuration and the tool projects of that configuration, which are nodes
  like any directory with a manifest (`AGENTS.md` §1); every other dot directory stays out.

  ⚠ 2026-10-01: stood "the built-in exclusion of dot directories", all of them. The
  directory `.github` held a C# project with no pair of documents and the linter could
  not see it, which the root `BOOT.md` had to declare as a deviation of `AGENTS.md` §1
  (the guards audit of 2026-09-26); with `.github` read, the deviation lifts.
- The checks are those of the `AGENTS.md` it is run against (3.2 since 2026-10-01).
  A later revision of the protocol changes this node in the same step, and the two
  are never allowed to disagree about a limit or a canonical line.

  ⚠ 2026-10-01: before this date the node implemented `AGENTS.md` 3 only: no size
  limit, no `ACCEPTANCE.md`, and no check of a `HISTORY.md` anchor. The owner adopted
  3.1 verbatim; the node's checks were taken over from the tool of the tree where 3.1
  was lived through, and extended here with four facts that tool's tests lacked
  (`ACCEPTANCE.md` over its limit, the parent limit kept below the root with the
  pointer, a citation inside a fenced block, a qualified citation of an anchor the
  named node lacks), plus one for `--exclude`, which no fact guarded before.

## Acceptance criteria

- [x] Every check is proven non-degenerate: exactly one breakage is applied to a
      conformant tree and the check turns red — 68 tests, none skipped
      (2026-10-01, `test_protocol_lint.py`, run as
      `python -X utf8 tools/protocol-lint/test_protocol_lint.py`; 27 tests on
      2026-09-12, before the checks of `AGENTS.md` 3.1, and 63 before 3.2).
- [x] Two guard tests against false positives: a grouping directory is not counted as
      a node, a link inside a code block is not resolved
      (2026-09-12, `test_a_grouping_directory_is_not_a_node`,
      `test_links_inside_code_are_not_followed`).
- [x] Verified on a real tree, not only on a fixture: a tree of 52 nodes (the
      PastyPropellant reconstruction) and the tree of the port, both parsed in full,
      and every finding explained (2026-09-12). Re-verified on this repository's tree
      on 2026-10-01: sixteen §15 errors for the nodes still over their limits, no
      other finding.
- [x] The AGENTS.md §15 size check is seen red once per mutation, each applied alone
      to a scratch copy of the module and not committed (2026-10-01,
      `test_protocol_lint.py`):
      - the leaf limit raised turns red `test_a_leaf_boot_over_its_limit_is_an_error`,
        `test_the_size_check_runs_by_default`, `test_exit_codes_reflect_the_size_check`,
        `test_a_declared_deviation_downgrades_the_finding_to_a_warning` and
        `test_a_section_exemption_does_not_hide_a_genuine_overflow`;
      - the parent limit raised turns red `test_a_parent_boot_uses_the_tighter_limit` and
        `test_a_node_below_the_root_keeps_the_parent_limit_with_the_pointer`;
      - the downgrade of a declared deviation removed turns red
        `test_a_declared_deviation_downgrades_the_finding_to_a_warning`;
      - the within-limit fixtures `test_a_leaf_boot_within_its_limit_is_clean` and
        `test_a_leaf_boot_is_not_held_to_the_parent_limit` guard the other direction.
- [x] The §6 section exemption is seen red once: removing the exclusion arithmetic
      turns red `test_a_named_section_is_excluded_from_the_count`, removing the
      existence check turns red `test_a_named_section_that_does_not_exist_is_an_error`,
      and `test_a_section_exemption_does_not_hide_a_genuine_overflow` holds that the
      exemption narrows an overflow and does not hide one (2026-10-01).
- [x] `ACCEPTANCE.md` (AGENTS.md 3.2, §6, §15), in any node, is seen red once per
      mutation (2026-10-01, `test_protocol_lint.py`):
      - the pointer-without-file check removed turns red
        `test_a_pointer_with_no_acceptance_file_is_an_error`;
      - the orphan check removed turns red `test_an_orphan_acceptance_file_is_an_error`;
      - the date check of the file removed turns red
        `test_an_undated_tick_in_acceptance_md_is_a_warning`;
      - the date check reading only the section turns red
        `test_an_undated_tick_in_a_headingless_acceptance_md_is_a_warning`;
      - the size check of the file removed turns red
        `test_acceptance_md_over_400_lines_is_an_error`,
        `test_acceptance_md_in_a_leaf_over_400_lines_is_an_error` and
        `test_a_declared_deviation_downgrades_an_acceptance_overflow_to_a_warning`;
      - the deviation of the file removed turns red
        `test_a_declared_deviation_downgrades_an_acceptance_overflow_to_a_warning`;
      - the pointer exempting a `BOOT.md` below the root from its limit turns red
        `test_a_leaf_with_the_pointer_keeps_the_leaf_limit` and
        `test_a_node_below_the_root_keeps_the_parent_limit_with_the_pointer`;
      - the root's raised limit set back to 250 turns red
        `test_a_root_boot_over_400_lines_with_the_pointer_is_an_error` and
        `test_a_root_boot_at_exactly_400_lines_with_the_pointer_is_clean`, and the raise
        applied to every parent turns red
        `test_a_node_below_the_root_keeps_the_parent_limit_with_the_pointer`;
      - on the module as it stood before 3.2 (the leaf error, no deviation for the file,
        dates read in the section only) `test_acceptance_md_in_a_leaf_is_clean`,
        `test_a_leaf_moving_its_criteria_out_comes_inside_its_limit`,
        `test_a_declared_deviation_downgrades_an_acceptance_overflow_to_a_warning` and
        `test_an_undated_tick_in_a_headingless_acceptance_md_is_a_warning` are red, and
        of the 63 tests that stood only the one it replaced, the leaf error, is red;
      - `test_a_pointer_that_resolves_to_a_real_file_is_clean` and
        `test_acceptance_md_in_a_leaf_is_clean` guard the false positive.

      ⚠ 2026-10-01: stood "only in a node with children", with a leaf an `ERROR`
      (`test_acceptance_md_in_a_leaf_is_an_error`), no deviation for the file's size
      and dates read in the section only: now any node, per `AGENTS.md` 3.2.
- [x] The `HISTORY.md` citation check is seen red once per mutation
      (2026-10-01, `test_protocol_lint.py`):
      - the check removed for documents turns red
        `test_a_pointer_to_a_missing_anchor_is_an_error`,
        `test_a_pointer_with_no_history_file_at_all_is_an_error`,
        `test_a_backticked_dangling_citation_in_api_md_is_an_error` and
        `test_a_bare_citation_of_a_neighbours_anchor_is_an_error`;
      - the check removed for code turns red
        `test_history_citations_are_checked_in_code_under_src_and_tests`;
      - a bare citation resolved against every node instead of the chain turns red
        `test_a_bare_citation_of_a_neighbours_anchor_is_an_error`;
      - a qualified citation resolved always turns red
        `test_a_qualified_citation_of_an_anchor_the_named_node_lacks_is_an_error`;
      - the exemption of a `HISTORY.md` removed turns red
        `test_a_citation_inside_a_history_md_itself_is_not_checked`;
      - fenced blocks left unmasked turns red
        `test_a_citation_inside_a_fenced_block_is_not_checked`;
      - the false-positive fixtures `test_a_pointer_that_resolves_is_not_flagged`,
        `test_a_backticked_placeholder_citation_is_not_flagged`,
        `test_a_bare_citation_resolves_via_an_ancestor`,
        `test_a_qualified_citation_of_a_neighbour_resolves` and
        `test_a_relative_qualified_citation_resolves` hold the other direction.
- [x] `--exclude` keeps working as before (2026-10-01,
      `test_an_extra_excluded_directory_is_not_a_node`; ignoring the option turns it red).
- [x] `.github` is read as part of the tree and every other dot directory stays skipped
      (2026-10-01): with the exception emptied `test_dot_github_is_read_as_part_of_the_tree`
      turns red, with the dot rule removed `test_every_other_dot_directory_stays_skipped`
      and `test_excluded_directories_are_not_nodes` turn red.
- [x] The self-test is wired into the project's own test set (2026-10-05):
      `Protocol.Tests.ToolSelfTestTests.EverySelfTestExitsZero(script:
      "tools/protocol-lint/test_protocol_lint.py")` runs it as a process in the end-to-end
      set (`Category=EndToEnd`), where a non-zero exit fails the fact and an empty walk
      fails `TheWalkFindsTheSelfTests`. Seen red in a scratch copy with one canonical
      section name changed in the copy of the linter: the self-test exits 1.

      ⚠ 2026-10-05: was "not run automatically by anything except a manual run", now run by
      `Protocol.Tests` since 2026-10-02 (end-to-end set since 2026-10-03). The old wording came
      with the kit and was never true of this tree after the walk of `tools/*/test_*.py`
      was written; it was found stale, not fixed, on 2026-10-05.
- [x] The document templates of the tree satisfy the checker: every `BOOT.md` under
      `docs/protocol/templates/` carries the six canonical sections, and the test fails,
      not skips, when that directory holds no BOOT template, since an empty walk proves
      nothing (2026-10-01,
      `ShippedTemplatesTest.test_every_boot_template_carries_the_six_sections`, which
      locates the directory from the repository root). Seen red twice in a scratch copy:
      with `## Taboos` renamed in the node template, and with the three templates
      removed.

      ⚠ 2026-10-01: the test looked for the templates in `tools/templates`, a directory
      this tree does not have, and was skipped (`unittest.skipUnless`) for as long as it
      existed, which `AGENTS.md` §13 forbids for a check. Found when the checks of 3.1 were
      taken over; the owner decided that the test reads `docs/protocol/templates/`.
- [x] The checks that need reflection are not implemented here and cannot be: they are a
      separate node for the project's stack (`AGENTS.md`, §13), in this tree
      [Protocol.Tests](../../tests/Protocol.Tests/API.md) (2026-10-05): the public surface
      (`SurfaceTests`), exported types against `API.md` (`CoverageTests`), declarations under ✅
      (`DeclarationTests`) and the declared dependencies against the real ones (`DependencyTests`),
      all green in `dotnet test tests/Protocol.Tests`.

      ⚠ 2026-10-05: was an open box, "not implemented here", which is a limitation of this
      node by design and not a gap to close; reformulated as satisfied by the neighbouring
      node that implements the four reflection checks, and ticked.

## Taboos

- Do not check the content of documents: whether what is written is true, the machine
  does not know and cannot know.
- Do not add checks without an article of the protocol. A new check needs the article
  first, then the code.
- Do not introduce external dependencies or configuration files: the parameters are flags.
- Do not repair the tree automatically. The linter reports; a human or an agent decides.
- Do not add a flag that silences a check of the table of `AGENTS.md` §13: a node
  declares a deviation in its own `BOOT.md`, the linter does not look away.
