#!/usr/bin/env python3
"""Proof that every check of protocol_lint is non-degenerate.

The protocol asks each check to be seen red once: a check nobody has watched fail is
indistinguishable from a missing one. Each test here builds a conforming tree, breaks
exactly the one thing a check guards, and asserts that this check - and, where it
matters, only this check - complains. Two tests do the opposite and guard against
false alarms: a grouping directory is not a node, and a link inside a code block is
not a link.

    python -X utf8 tools/protocol-lint/test_protocol_lint.py
"""

from __future__ import annotations

import contextlib
import io
import sys
import tempfile
import unittest
from pathlib import Path
from typing import List, Optional

sys.path.insert(0, str(Path(__file__).resolve().parent))

import protocol_lint as lint  # noqa: E402

BOOT = """# BOOT.md - {name}

## Purpose

{name} does one thing, and this line says which.

## Invariants

- It keeps doing it.

## Dependencies

{dependencies}

## Constraints

Inherited from the node above.

## Acceptance criteria

- [x] It does the thing (2026-09-12, `ItDoesTheThing`).

## Taboos

- Doing anything else.
"""

API_WITH_TICK = """# API.md - a

What `a` offers outward.

## Thing ✅

```python
class Thing:
    pass
```
"""

API_WITH_HOURGLASS = """# API.md - b

What `b` will offer outward.

## Other ⏳

```python
class Other:
    pass
```
"""

ROOT_API = """# API.md - root

The system as a whole.

## Children

- [a](./a/API.md) - does one thing.
- [b](./b/API.md) - will do another.
"""


class ProtocolLintTest(unittest.TestCase):
    """Each test mutates the conforming tree built in setUp and reads the findings."""

    def setUp(self) -> None:
        directory = tempfile.TemporaryDirectory()
        self.addCleanup(directory.cleanup)
        self.root = Path(directory.name).resolve()

        self.write("AGENTS.md", "# AGENTS.md\n\nThe protocol.\n")
        self.write("BOOT.md", BOOT.format(name="root", dependencies="None."))
        self.write("API.md", ROOT_API)

        self.write("a/BOOT.md", BOOT.format(name="a", dependencies="- [b](../b/API.md) - the other half."))
        self.write("a/API.md", API_WITH_TICK)
        self.write("a/thing.py", "class Thing:\n    pass\n")

        self.write("b/BOOT.md", BOOT.format(name="b", dependencies="None"))
        self.write("b/API.md", API_WITH_HOURGLASS)
        self.write("b/other.py", "VALUE = 1\n")

    # ------------------------------------------------------------------ helpers

    def write(self, relative: str, text: str) -> Path:
        path = self.root / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(text, encoding="utf-8")
        return path

    def findings(self, **keywords) -> List[lint.Finding]:
        return lint.lint(self.root, **keywords)

    def assertFinding(
        self,
        level: str,
        article: str,
        where: str,
        needle: str = "",
        findings: Optional[List[lint.Finding]] = None,
    ) -> None:
        found = findings if findings is not None else self.findings()
        matching = [
            finding for finding in found
            if finding.level == level
            and finding.article == article
            and finding.where.startswith(where)
            and needle in finding.message
        ]
        self.assertTrue(
            matching,
            "expected a {} on {} ({}), got:\n{}".format(
                level, where, article, "\n".join(str(f) for f in found) or "nothing"),
        )

    def assertNoFinding(self, needle: str, findings: Optional[List[lint.Finding]] = None) -> None:
        found = findings if findings is not None else self.findings()
        offending = [finding for finding in found if needle in str(finding)]
        self.assertFalse(offending, "unexpected:\n{}".format("\n".join(str(f) for f in offending)))

    # -------------------------------------------------------------- the baseline

    def test_a_conforming_tree_is_clean(self) -> None:
        self.assertEqual([], self.findings(), "the fixture itself must satisfy the protocol")

    # ------------------------------------------------------------- 1: the pair

    def test_a_node_without_its_api_is_an_error(self) -> None:
        (self.root / "a" / "API.md").unlink()
        self.assertFinding("ERROR", "1", "a/API.md")

    def test_a_source_directory_without_documents_is_an_error(self) -> None:
        self.write("c/module.py", "VALUE = 2\n")
        self.assertFinding("ERROR", "1", "c/BOOT.md")
        self.assertFinding("ERROR", "1", "c/API.md")

    def test_a_manifest_alone_makes_a_directory_a_node(self) -> None:
        self.write("service/pyproject.toml", "[project]\nname = 'service'\n")
        self.assertFinding("ERROR", "1", "service/BOOT.md")

    def test_a_grouping_directory_is_not_a_node(self) -> None:
        """A directory with neither code nor manifest nor documents is read through."""
        self.write("src/c/BOOT.md", BOOT.format(name="c", dependencies="None"))
        self.write("src/c/API.md", "# API.md - c\n\nNothing yet.\n")
        self.write("src/c/code.py", "VALUE = 3\n")
        self.assertNoFinding("src/BOOT.md")
        self.assertNoFinding("src/API.md")

    def test_excluded_directories_are_not_nodes(self) -> None:
        self.write("node_modules/left-pad/index.js", "module.exports = 1;\n")
        self.write("obj/Debug/Generated.cs", "class Generated { }\n")
        self.write(".venv/lib/site.py", "VALUE = 4\n")
        self.assertEqual([], self.findings())

    def test_dot_github_is_read_as_part_of_the_tree(self) -> None:
        """Committed configuration is a node when it holds code: its own and its children's
        pairs are required, and its documents are checked like any other."""
        self.write(".github/diagnostics/Probe/probe.py", "VALUE = 6\n")
        self.write(".github/BOOT.md", "# BOOT.md - github\n\n## Purpose\n\nCI.\n")
        self.write(".github/API.md", "# API.md - github\n\nNothing.\n")
        findings = self.findings()
        self.assertFinding("ERROR", "1", ".github/diagnostics/Probe/BOOT.md", findings=findings)
        self.assertFinding("ERROR", "1", ".github/diagnostics/Probe/API.md", findings=findings)
        self.assertFinding("ERROR", "6", ".github/BOOT.md", "Invariants", findings=findings)

    def test_every_other_dot_directory_stays_skipped(self) -> None:
        self.write(".claude/worktrees/x/module.py", "VALUE = 7\n")
        self.write(".git/hooks/hook.py", "VALUE = 8\n")
        self.write(".venv-fixtures/lib/site.py", "VALUE = 9\n")
        self.assertEqual([], self.findings())

    # ---------------------------------------------------------- 2: the protocol

    def test_agents_below_the_root_is_an_error(self) -> None:
        self.write("a/AGENTS.md", "# AGENTS.md\n")
        self.assertFinding("ERROR", "2", "a/AGENTS.md")

    def test_a_root_without_agents_is_an_error(self) -> None:
        (self.root / "AGENTS.md").unlink()
        self.assertFinding("ERROR", "2", "AGENTS.md")

    # --------------------------------------------------------- 6: the sections

    def test_a_translated_section_heading_is_an_error(self) -> None:
        boot = self.root / "a" / "BOOT.md"
        boot.write_text(boot.read_text(encoding="utf-8").replace("## Taboos", "## Tabous"), encoding="utf-8")
        self.assertFinding("ERROR", "6", "a/BOOT.md", "Taboos")

    def test_a_heading_inside_a_code_block_does_not_count(self) -> None:
        boot = self.root / "a" / "BOOT.md"
        boot.write_text(
            boot.read_text(encoding="utf-8").replace("## Taboos", "```markdown\n## Taboos\n```\n## Tabous"),
            encoding="utf-8",
        )
        self.assertFinding("ERROR", "6", "a/BOOT.md", "Taboos")

    # ------------------------------------------------------ 6: the dependencies

    def test_dependencies_in_prose_only_is_an_error(self) -> None:
        self.write("a/BOOT.md", BOOT.format(name="a", dependencies="Uses b for the other half."))
        self.assertFinding("ERROR", "6", "a/BOOT.md", "canonical form")

    def test_none_together_with_a_neighbour_is_an_error(self) -> None:
        self.write("a/BOOT.md", BOOT.format(name="a", dependencies="None\n\n- [b](../b/API.md) - the other half."))
        self.assertFinding("ERROR", "6", "a/BOOT.md", "None")

    def test_a_dependency_on_a_descendant_is_a_warning(self) -> None:
        self.write("BOOT.md", BOOT.format(name="root", dependencies="- [a](./a/API.md) - its child."))
        self.assertFinding("WARN", "6", "BOOT.md", "descendant")

    def test_a_dependency_on_a_neighbour_is_not_flagged(self) -> None:
        self.assertNoFinding("descendant")

    def test_external_dependencies_in_prose_are_allowed_beside_the_links(self) -> None:
        self.write("a/BOOT.md", BOOT.format(
            name="a",
            dependencies="- [b](../b/API.md) - the other half.\n\nOutside the tree: pytest 8.3.",
        ))
        self.assertEqual([], self.findings())

    # ---------------------------------------------------------------- the links

    def test_a_link_that_resolves_to_nothing_is_an_error(self) -> None:
        self.write("a/API.md", API_WITH_TICK + "\nSee [c](../c/API.md).\n")
        self.assertFinding("ERROR", "-", "a/API.md", "resolves to nothing")

    def test_links_inside_code_are_not_followed(self) -> None:
        """The examples in AGENTS.md are written as code so that they stay examples."""
        self.write("a/API.md", API_WITH_TICK + """
Written inline: `[Neighbour](../Neighbour/API.md)`.

```markdown
- [Another](../Another/API.md) - a sketch of a link.
```
""")
        self.assertNoFinding("resolves to nothing")

    # ------------------------------------------------------------ 7: the marks

    def test_an_api_with_code_and_no_mark_is_a_warning(self) -> None:
        self.write("b/API.md", "# API.md - b\n\n## Other\n\n```python\nclass Other:\n    pass\n```\n")
        self.assertFinding("WARN", "7", "b/API.md", "status mark")

    def test_a_declaration_under_a_tick_that_no_source_mentions_is_a_warning(self) -> None:
        self.write("a/API.md", API_WITH_TICK.replace("class Thing:", "class Ghost:"))
        self.assertFinding("WARN", "7", "a/API.md", "Ghost")

    def test_a_declaration_under_an_hourglass_is_not_checked(self) -> None:
        """b declares Other, which its code never mentions - and that is legitimate."""
        self.assertNoFinding("Other")

    def test_the_textual_check_can_be_switched_off(self) -> None:
        self.write("a/API.md", API_WITH_TICK.replace("class Thing:", "class Ghost:"))
        self.assertNoFinding("Ghost", findings=self.findings(heuristics=False))

    # --------------------------------------------------------- 6: the criteria

    def test_a_checked_criterion_without_a_date_is_a_warning(self) -> None:
        boot = self.root / "b" / "BOOT.md"
        boot.write_text(
            boot.read_text(encoding="utf-8").replace("(2026-09-12, `ItDoesTheThing`)", "(proven)"),
            encoding="utf-8",
        )
        self.assertFinding("WARN", "6", "b/BOOT.md", "no date")

    def test_a_date_on_a_continuation_line_counts(self) -> None:
        boot = self.root / "b" / "BOOT.md"
        boot.write_text(
            boot.read_text(encoding="utf-8").replace(
                "- [x] It does the thing (2026-09-12, `ItDoesTheThing`).",
                "- [x] It does the thing\n      (2026-09-12, `ItDoesTheThing`).",
            ),
            encoding="utf-8",
        )
        self.assertNoFinding("no date")

    def test_an_unchecked_criterion_needs_no_date(self) -> None:
        boot = self.root / "b" / "BOOT.md"
        boot.write_text(
            boot.read_text(encoding="utf-8").replace("- [x] It does the thing (2026-09-12, `ItDoesTheThing`).",
                                                     "- [ ] It will do the thing."),
            encoding="utf-8",
        )
        self.assertNoFinding("no date")

    # ------------------------------------------------------------- 15: the size

    def filler(self, count: int) -> str:
        return "\n".join(
            "Filler line {} of prose that pads this document out for the size check.".format(i)
            for i in range(count)
        )

    def test_the_size_check_runs_by_default(self) -> None:
        """719 lines, `b` a leaf: over the 400-line limit, and there is no --size flag
        left to gate it - the check runs unconditionally."""
        self.write("b/BOOT.md", BOOT.format(name="b", dependencies="None") + "\n" + self.filler(410))
        self.assertFinding("ERROR", "15", "b/BOOT.md", "400-line limit")

    def test_a_leaf_boot_over_its_limit_is_an_error(self) -> None:
        self.write("b/BOOT.md", BOOT.format(name="b", dependencies="None") + "\n" + self.filler(410))
        self.assertFinding("ERROR", "15", "b/BOOT.md", "400-line limit")

    def test_a_leaf_boot_within_its_limit_is_clean(self) -> None:
        self.assertEqual([], [f for f in self.findings() if f.article == "15"])

    def test_a_parent_boot_uses_the_tighter_limit(self) -> None:
        """root has children (a, b): 260 filler lines clear 250 but not 400."""
        self.write("BOOT.md", BOOT.format(name="root", dependencies="None.") + "\n" + self.filler(260))
        self.assertFinding("ERROR", "15", "BOOT.md", "250-line limit")

    def test_a_leaf_boot_is_not_held_to_the_parent_limit(self) -> None:
        """b is a leaf: 260 filler lines (over 250, under 400) must not fire there."""
        self.write("b/BOOT.md", BOOT.format(name="b", dependencies="None") + "\n" + self.filler(260))
        self.assertEqual([], [f for f in self.findings() if f.article == "15"])

    def test_a_declared_deviation_downgrades_the_finding_to_a_warning(self) -> None:
        self.write(
            "b/BOOT.md",
            BOOT.format(name="b", dependencies="None")
            + "\n⚠ Declared deviation, §15: over limit until the migration lands, lifts 2026-10-01.\n\n"
            + self.filler(410),
        )
        findings = self.findings()
        self.assertFinding("WARN", "15", "b/BOOT.md", "declared deviation", findings=findings)
        self.assertEqual([], [f for f in findings if f.level == "ERROR" and f.article == "15"])

    def test_exit_codes_reflect_the_size_check(self) -> None:
        self.write("b/BOOT.md", BOOT.format(name="b", dependencies="None") + "\n" + self.filler(410))
        with contextlib.redirect_stdout(io.StringIO()):
            self.assertEqual(1, lint.main([str(self.root)]))

    # ------------------------------------------------- 15: the §6 section exemption

    def test_a_named_section_is_excluded_from_the_count(self) -> None:
        """`b` is a leaf: 410 filler lines alone would be over the 400-line limit, but
        they sit entirely inside a section a §6 deviation names as replaced."""
        self.write(
            "b/BOOT.md",
            BOOT.format(name="b", dependencies="None")
            + "\n⚠ Declared deviation, §6: the specification is an external source, "
              "replaced by: ## Transcription.\n\n## Transcription\n\n"
            + self.filler(410),
        )
        self.assertEqual([], [f for f in self.findings() if f.article == "15"])

    def test_a_named_section_that_does_not_exist_is_an_error(self) -> None:
        self.write(
            "b/BOOT.md",
            BOOT.format(name="b", dependencies="None")
            + "\n⚠ Declared deviation, §6: the specification is an external source, "
              "replaced by: ## Nonexistent.\n",
        )
        self.assertFinding("ERROR", "15", "b/BOOT.md", "Nonexistent")

    def test_a_section_exemption_does_not_hide_a_genuine_overflow(self) -> None:
        """The excluded section's own lines do not count, but the rest of the document
        still does: 410 filler lines outside the named section still overflow."""
        self.write(
            "b/BOOT.md",
            BOOT.format(name="b", dependencies="None")
            + "\n⚠ Declared deviation, §6: the specification is an external source, "
              "replaced by: ## Transcription.\n\n## Transcription\n\nA short section.\n\n"
              "## Extra\n\n"
            + self.filler(410),
        )
        self.assertFinding("ERROR", "15", "b/BOOT.md", "400-line limit")

    # ---------------------------------------------------- 15: the HISTORY.md pointer

    def test_a_pointer_to_a_missing_anchor_is_an_error(self) -> None:
        self.write(
            "b/BOOT.md",
            BOOT.format(name="b", dependencies="None")
            + "\n⚠ 2026-09-12: was the old wording, now this one → HISTORY.md#ghost-anchor\n",
        )
        self.assertFinding("ERROR", "15", "b/BOOT.md", "ghost-anchor")

    def test_a_pointer_that_resolves_is_not_flagged(self) -> None:
        self.write(
            "b/BOOT.md",
            BOOT.format(name="b", dependencies="None")
            + "\n⚠ 2026-09-12: was the old wording, now this one → HISTORY.md#real-anchor\n",
        )
        self.write(
            "b/HISTORY.md",
            '# HISTORY.md - b\n\n<a id="real-anchor"></a>\n\n## 2026-09-12 - moved\n\n> The old wording.\n',
        )
        self.assertNoFinding("real-anchor")

    def test_a_pointer_with_no_history_file_at_all_is_an_error(self) -> None:
        self.write(
            "b/BOOT.md",
            BOOT.format(name="b", dependencies="None")
            + "\n⚠ 2026-09-12: was the old wording, now this one → HISTORY.md#real-anchor\n",
        )
        self.assertFinding("ERROR", "15", "b/BOOT.md", "real-anchor")

    # --------------------------------------------------- 3.1: ACCEPTANCE.md, the file

    def root_boot_with_pointer(self, extra: str = "") -> str:
        return BOOT.format(name="root", dependencies="None.").replace(
            "- [x] It does the thing (2026-09-12, `ItDoesTheThing`).",
            "→ [ACCEPTANCE.md](ACCEPTANCE.md)",
        ) + extra

    ACCEPTANCE = (
        "# ACCEPTANCE.md - root\n\n"
        "## Acceptance criteria\n\n"
        "- [x] It does the thing (2026-09-12, `ItDoesTheThing`).\n"
    )

    def test_a_pointer_with_no_acceptance_file_is_an_error(self) -> None:
        self.write("BOOT.md", self.root_boot_with_pointer())
        self.assertFinding("ERROR", "6", "BOOT.md", "does not exist")

    def test_a_pointer_that_resolves_to_a_real_file_is_clean(self) -> None:
        self.write("BOOT.md", self.root_boot_with_pointer())
        self.write("ACCEPTANCE.md", self.ACCEPTANCE)
        self.assertEqual([], self.findings())

    def test_an_orphan_acceptance_file_is_an_error(self) -> None:
        """`ACCEPTANCE.md` exists, but BOOT.md's own section was never turned into
        the one-line pointer: the file is either stale or was never wired in."""
        self.write("ACCEPTANCE.md", self.ACCEPTANCE)
        self.assertFinding("ERROR", "6", "ACCEPTANCE.md", "does not point to it")

    def leaf_boot_with_pointer(self, extra: str = "") -> str:
        """The leaf `b`'s BOOT.md with its criteria handed to ACCEPTANCE.md."""
        return BOOT.format(name="b", dependencies="None").replace(
            "- [x] It does the thing (2026-09-12, `ItDoesTheThing`).",
            "→ [ACCEPTANCE.md](ACCEPTANCE.md)",
        ) + extra

    def criteria(self, count: int) -> str:
        return "\n".join(
            "- [x] Criterion {} holds (2026-09-12, `Criterion{}Test`).".format(i, i)
            for i in range(count)
        )

    def test_acceptance_md_in_a_leaf_is_clean(self) -> None:
        """AGENTS.md 3.2: any node may keep the file, a leaf too."""
        self.write("b/BOOT.md", self.leaf_boot_with_pointer())
        self.write("b/ACCEPTANCE.md", "# ACCEPTANCE.md - b\n\n## Acceptance criteria\n\n"
                                       "- [x] It does the thing (2026-09-12, `ItDoesTheThing`).\n")
        self.assertEqual([], self.findings())

    def test_a_leaf_moving_its_criteria_out_comes_inside_its_limit(self) -> None:
        """300 lines of rules and 200 of criteria: over the leaf's 400 inline, inside it
        once the criteria stand in ACCEPTANCE.md."""
        rules = "\n" + self.filler(300)
        self.write(
            "b/BOOT.md",
            BOOT.format(name="b", dependencies="None").replace(
                "- [x] It does the thing (2026-09-12, `ItDoesTheThing`).", self.criteria(200),
            ) + rules,
        )
        self.assertFinding("ERROR", "15", "b/BOOT.md", "400-line limit")

        self.write("b/BOOT.md", self.leaf_boot_with_pointer(rules))
        self.write("b/ACCEPTANCE.md", "# ACCEPTANCE.md - b\n\n## Acceptance criteria\n\n"
                                       + self.criteria(200) + "\n")
        self.assertEqual([], self.findings())

    def test_a_declared_deviation_downgrades_an_acceptance_overflow_to_a_warning(self) -> None:
        deviation = "⚠ Declared deviation, §15: the criteria are one table nobody may cut."
        self.write("b/BOOT.md", self.leaf_boot_with_pointer("\n" + deviation + "\n"))
        self.write("b/ACCEPTANCE.md", "# ACCEPTANCE.md - b\n\n" + self.filler(410) + "\n")
        findings = self.findings()
        self.assertFinding("WARN", "15", "b/ACCEPTANCE.md", "declared deviation", findings)
        self.assertFinding("WARN", "15", "b/ACCEPTANCE.md", "nobody may cut", findings)
        self.assertEqual([], [f for f in findings if f.level == "ERROR"])

    def test_a_leaf_with_the_pointer_keeps_the_leaf_limit(self) -> None:
        """The pointer exempts only the root: a leaf is measured against 400 lines with
        it as without it, and not against 250."""
        self.write("b/BOOT.md", self.leaf_boot_with_pointer("\n" + self.filler(300)))
        self.write("b/ACCEPTANCE.md", "# ACCEPTANCE.md - b\n\n## Acceptance criteria\n\n"
                                       "- [x] It does the thing (2026-09-12, `ItDoesTheThing`).\n")
        self.assertEqual([], self.findings())

        self.write("b/BOOT.md", self.leaf_boot_with_pointer("\n" + self.filler(420)))
        self.assertFinding("ERROR", "15", "b/BOOT.md", "400-line limit")

    def test_acceptance_md_in_a_leaf_over_400_lines_is_an_error(self) -> None:
        self.write("b/BOOT.md", self.leaf_boot_with_pointer())
        self.write("b/ACCEPTANCE.md", "# ACCEPTANCE.md - b\n\n" + self.filler(410) + "\n")
        self.assertFinding("ERROR", "15", "b/ACCEPTANCE.md", "400-line limit")

    def test_an_undated_tick_in_acceptance_md_is_a_warning(self) -> None:
        self.write("BOOT.md", self.root_boot_with_pointer())
        self.write(
            "ACCEPTANCE.md",
            "# ACCEPTANCE.md - root\n\n## Acceptance criteria\n\n"
            "- [x] It does the thing (`ItDoesTheThing`).\n",
        )
        self.assertFinding("WARN", "6", "ACCEPTANCE.md", "no date")

    def test_an_undated_tick_in_a_headingless_acceptance_md_is_a_warning(self) -> None:
        """The file holds criteria throughout, so its ticks are read whether or not it
        carries the section heading."""
        self.write("BOOT.md", self.root_boot_with_pointer())
        self.write(
            "ACCEPTANCE.md",
            "# ACCEPTANCE.md - root\n\n- [x] It does the thing (`ItDoesTheThing`).\n",
        )
        self.assertFinding("WARN", "6", "ACCEPTANCE.md", "no date")

    def test_a_root_boot_over_400_lines_with_the_pointer_is_an_error(self) -> None:
        """With its criteria in ACCEPTANCE.md the root is measured against the leaf's
        400 lines, not the 250 a node with children otherwise gets - but it is still
        measured."""
        self.write("BOOT.md", self.root_boot_with_pointer("\n" + self.filler(420)))
        self.write("ACCEPTANCE.md", self.ACCEPTANCE)
        self.assertFinding("ERROR", "15", "BOOT.md", "400-line limit")

    def test_a_root_boot_at_exactly_400_lines_with_the_pointer_is_clean(self) -> None:
        base = self.root_boot_with_pointer()
        base_count = sum(1 for line in base.split("\n") if line.strip())
        self.write("BOOT.md", base + "\n" + self.filler(400 - base_count))
        self.write("ACCEPTANCE.md", self.ACCEPTANCE)
        self.assertEqual([], [f for f in self.findings() if f.article == "15" and f.where.startswith("BOOT.md")])

    # ---------------------------------------------- 3.1/15: citations everywhere

    def test_a_backticked_dangling_citation_in_api_md_is_an_error(self) -> None:
        """The old pointer check read only a BOOT.md's own bare citations, and only
        outside backticks; this one is inside an API.md and inside backticks."""
        self.write("a/API.md", API_WITH_TICK + "\nSee `HISTORY.md#ghost` for the reasoning.\n")
        self.assertFinding("ERROR", "15", "a/API.md", "ghost")

    def test_a_backticked_placeholder_citation_is_not_flagged(self) -> None:
        """The kit's own example syntax, `HISTORY.md#<anchor>`, is not a citation: an
        anchor never starts with '<', so it never matches in the first place."""
        self.write("a/API.md", API_WITH_TICK + "\nWritten as `HISTORY.md#<anchor>` in an example.\n")
        self.assertNoFinding("<anchor>")

    def test_a_citation_inside_a_history_md_itself_is_not_checked(self) -> None:
        self.write("b/HISTORY.md", "# HISTORY.md - b\n\nSee HISTORY.md#nowhere for older context.\n")
        self.assertNoFinding("nowhere")

    def test_a_bare_citation_of_a_neighbours_anchor_is_an_error(self) -> None:
        """The mistake the check exists to catch: a bare citation happens to name an
        anchor that exists, but in a neighbour's HISTORY.md, not this node's own or an
        ancestor's."""
        self.write("b/HISTORY.md", '# HISTORY.md - b\n\n<a id="neighbour-only"></a>\n\n## Old\n\n> Text.\n')
        self.write("a/API.md", API_WITH_TICK + "\nSee HISTORY.md#neighbour-only for the reasoning.\n")
        self.assertFinding("ERROR", "15", "a/API.md", "neighbour-only")

    def test_a_bare_citation_resolves_via_an_ancestor(self) -> None:
        self.write("HISTORY.md", '# HISTORY.md - root\n\n<a id="shared-with-children"></a>\n\n## Old\n\n> Text.\n')
        self.write("a/API.md", API_WITH_TICK + "\nSee HISTORY.md#shared-with-children for the reasoning.\n")
        self.assertNoFinding("shared-with-children")

    def test_a_qualified_citation_of_a_neighbour_resolves(self) -> None:
        self.write("b/HISTORY.md", '# HISTORY.md - b\n\n<a id="shared-anchor"></a>\n\n## Old\n\n> Text.\n')
        self.write("a/API.md", API_WITH_TICK + "\nSee `b/HISTORY.md#shared-anchor` for the reasoning.\n")
        self.assertNoFinding("shared-anchor")

    def test_a_relative_qualified_citation_resolves(self) -> None:
        self.write("b/HISTORY.md", '# HISTORY.md - b\n\n<a id="relative-anchor"></a>\n\n## Old\n\n> Text.\n')
        self.write("a/API.md", API_WITH_TICK + "\nSee `../b/HISTORY.md#relative-anchor` for the reasoning.\n")
        self.assertNoFinding("relative-anchor")

    def test_history_citations_are_checked_in_code_under_src_and_tests(self) -> None:
        self.write("tests/Demo/BOOT.md", BOOT.format(name="Demo", dependencies="None"))
        self.write("tests/Demo/API.md", "# API.md - Demo\n\nNothing yet.\n")
        self.write("tests/Demo/Demo.cs", "// see HISTORY.md#missing-in-code\n")
        self.assertFinding("ERROR", "15", "tests/Demo/Demo.cs", "missing-in-code")

    def test_history_citations_outside_src_and_tests_are_not_checked_in_code(self) -> None:
        self.write("a/extra.py", "# see HISTORY.md#not-checked-here\n")
        self.assertNoFinding("not-checked-here")

    def test_acceptance_md_over_400_lines_is_an_error(self) -> None:
        self.write("BOOT.md", self.root_boot_with_pointer())
        self.write("ACCEPTANCE.md", self.ACCEPTANCE + "\n" + self.filler(410))
        self.assertFinding("ERROR", "15", "ACCEPTANCE.md", "400-line limit")

    def test_a_node_below_the_root_keeps_the_parent_limit_with_the_pointer(self) -> None:
        """Only the root's limit rises with the pointer: `a` has a child, points to its
        own ACCEPTANCE.md and still gets 250 lines, not 400."""
        self.write("a/c/BOOT.md", BOOT.format(name="c", dependencies="None"))
        self.write("a/c/API.md", "# API.md - c\n\nNothing yet.\n")
        self.write(
            "a/BOOT.md",
            BOOT.format(name="a", dependencies="- [b](../b/API.md) - the other half.").replace(
                "- [x] It does the thing (2026-09-12, `ItDoesTheThing`).",
                "→ [ACCEPTANCE.md](ACCEPTANCE.md)",
            ) + "\n" + self.filler(260),
        )
        self.write("a/ACCEPTANCE.md", self.ACCEPTANCE)
        self.assertFinding("ERROR", "15", "a/BOOT.md", "250-line limit")

    def test_a_citation_inside_a_fenced_block_is_not_checked(self) -> None:
        self.write("a/API.md", API_WITH_TICK + "\n```text\nHISTORY.md#only-in-an-example\n```\n")
        self.assertNoFinding("only-in-an-example")

    def test_an_extra_excluded_directory_is_not_a_node(self) -> None:
        """`--exclude` names directories to skip beyond the built-in ones: without it
        the directory below is a node missing its pair, with it nothing is found."""
        self.write("templates/sample.py", "VALUE = 5\n")
        self.assertFinding("ERROR", "1", "templates")
        self.assertEqual([], self.findings(extra_excluded=["templates"]))

    def test_a_qualified_citation_of_an_anchor_the_named_node_lacks_is_an_error(self) -> None:
        self.write("b/HISTORY.md", '# HISTORY.md - b\n\n<a id="present"></a>\n\n## Old\n\n> Text.\n')
        self.write("a/API.md", API_WITH_TICK + "\nSee `b/HISTORY.md#absent` for the reasoning.\n")
        self.assertFinding("ERROR", "15", "a/API.md", "absent")

    # --------------------------------------------------------------- the driver

    def test_exit_codes(self) -> None:
        output = io.StringIO()
        with contextlib.redirect_stdout(output):
            self.assertEqual(0, lint.main([str(self.root)]))
        self.assertIn("0 errors, 0 warnings", output.getvalue())

        (self.root / "a" / "API.md").unlink()
        with contextlib.redirect_stdout(io.StringIO()):
            self.assertEqual(1, lint.main([str(self.root)]))

    def test_strict_makes_a_warning_fail(self) -> None:
        self.write("BOOT.md", BOOT.format(name="root", dependencies="- [a](./a/API.md) - its child."))
        with contextlib.redirect_stdout(io.StringIO()):
            self.assertEqual(0, lint.main([str(self.root)]))
            self.assertEqual(1, lint.main([str(self.root), "--strict"]))


class ShippedTemplatesTest(unittest.TestCase):
    """The document templates of this tree have to satisfy the checker."""

    TEMPLATES = Path(__file__).resolve().parents[2] / "docs" / "protocol" / "templates"

    def test_every_boot_template_carries_the_six_sections(self) -> None:
        templates = sorted(self.TEMPLATES.rglob("BOOT.md"))
        self.assertTrue(templates, "no BOOT template under {}: an empty walk proves nothing".format(self.TEMPLATES))
        for template in templates:
            headings = lint.second_level_headings(lint.mask_code(lint.read(template)))
            for section in lint.CANONICAL_SECTIONS:
                self.assertIn(section, headings, "{} has no '## {}'".format(template, section))


if __name__ == "__main__":
    unittest.main()
