#!/usr/bin/env python3
"""Proof that every check of protocol_lint is non-degenerate.

The protocol asks each check to be seen red once: a check nobody has watched fail is
indistinguishable from a missing one. Each test here builds a conforming tree, breaks
exactly the one thing a check guards, and asserts that this check - and, where it
matters, only this check - complains. Two tests do the opposite and guard against
false alarms: a grouping directory is not a node, and a link inside a code block is
not a link.

    python -m unittest discover -s checks
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
    """The templates next to this checker have to satisfy the checker."""

    TEMPLATES = Path(__file__).resolve().parent.parent / "templates"

    @unittest.skipUnless(TEMPLATES.is_dir(), "templates are not shipped beside the checks")
    def test_every_boot_template_carries_the_six_sections(self) -> None:
        for template in sorted(self.TEMPLATES.rglob("BOOT.md")):
            headings = lint.second_level_headings(lint.mask_code(lint.read(template)))
            for section in lint.CANONICAL_SECTIONS:
                self.assertIn(section, headings, "{} has no '## {}'".format(template.name, section))


if __name__ == "__main__":
    unittest.main()
