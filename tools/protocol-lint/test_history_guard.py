"""Tests of history_guard.py: one scenario per rule, each in a throwaway git repository.

Run: python -X utf8 test_history_guard.py   (standard library and git only)
"""
import os
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

GUARD = Path(__file__).with_name("history_guard.py")

PARENT_BOOT = """# BOOT.md - parent

## Purpose

The parent solves the thing.

## Constraints

Rule shared by both children.

Rule that binds only the child.

## Acceptance criteria

- [x] It works (2026-10-01, test_it).
"""

HISTORY = """# HISTORY.md - parent

<a id="old-figure"></a>
## 2026-09-01 - from "Constraints" - an old figure

> The figure was 7.
"""

INLINE = "- [x] It works (2026-10-01, test_it).\n"
POINTER = "→ [ACCEPTANCE.md](ACCEPTANCE.md)\n"
UPPER = "- [x] Correct upper bound\n      (2026-10-01, BoundsUpper).\n"
LOWER = "- [ ] Lower bound.\n"
PARENT = "src/Parent/"


def git(repo, *args):
    subprocess.run(["git", "-C", str(repo), *args], check=True, capture_output=True,
                   text=True, encoding="utf-8")


def write(repo, rel, text):
    path = Path(repo, rel)
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(text.encode("utf-8"))


class Repo:
    """A repository with the parent node at base, and a branch `work` to change."""

    def __init__(self, tmp):
        self.path = Path(tmp)
        git(self.path, "init", "-q", "-b", "main")
        git(self.path, "config", "user.email", "t@example.com")
        git(self.path, "config", "user.name", "t")
        git(self.path, "config", "core.autocrlf", "false")
        write(self.path, "src/Parent/BOOT.md", PARENT_BOOT)
        write(self.path, "src/Parent/HISTORY.md", HISTORY)
        write(self.path, "src/Other/BOOT.md", "# BOOT.md - other\n")
        self.commit("base")
        git(self.path, "checkout", "-q", "-b", "work")

    def commit(self, message):
        git(self.path, "add", "-A")
        git(self.path, "commit", "-q", "-m", message)

    def base(self):
        out = subprocess.run(["git", "-C", str(self.path), "merge-base", "main", "work"],
                             check=True, capture_output=True, text=True)
        return out.stdout.strip()

    def guard(self, *nodes, base=None, criteria_only=False):
        result = subprocess.run(
            [sys.executable, "-X", "utf8", str(GUARD), *(["--criteria-only"] if criteria_only else []), str(self.path),
             base or self.base(), "work", *nodes],
            capture_output=True, text=True, encoding="utf-8")
        return result.returncode, result.stdout + result.stderr


class HistoryGuardTest(unittest.TestCase):
    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.repo = Repo(self._tmp.name)

    def tearDown(self):
        self._tmp.cleanup()

    def start_with_detached_criteria(self, body=LOWER + UPPER):
        write(self.repo.path, PARENT + "BOOT.md", PARENT_BOOT.replace(INLINE, POINTER))
        write(self.repo.path, PARENT + "ACCEPTANCE.md", "# Acceptance\n\n" + body)
        self.repo.commit("detach criteria at base")
        return subprocess.check_output(["git", "-C", str(self.repo.path), "rev-parse", "HEAD"],
                                       text=True).strip()

    def test_detached_criterion_cannot_disappear(self):
        base = self.start_with_detached_criteria()
        write(self.repo.path, PARENT + "ACCEPTANCE.md", "# Acceptance\n\n" + LOWER)
        self.repo.commit("drop upper bound")
        code, out = self.repo.guard("src/Parent", base=base)
        self.assertEqual(code, 1, out)
        self.assertIn("lost active criterion", out)
        self.assertIn("Correct upper bound", out)

    def test_standalone_guard_rejects_noncanonical_pointer_and_archived_criterion(self):
        self.start_with_detached_criteria()
        write(self.repo.path, PARENT + "BOOT.md", PARENT_BOOT.replace(INLINE, POINTER + "Additional prose.\n"))
        self.repo.commit("old malformed pointer")
        git(self.repo.path, "branch", "-f", "main", "HEAD")
        write(self.repo.path, PARENT + "ACCEPTANCE.md", "# Acceptance\n\n" + LOWER)
        write(self.repo.path, PARENT + "HISTORY.md", HISTORY + "\n" + UPPER)
        self.repo.commit("archive active upper bound")
        for args, opts in [(("src/Parent",), {}), ((), {"criteria_only": True})]:
            with self.subTest(mode=opts):
                code, out = self.repo.guard(*args, **opts)
                self.assertEqual(code, 1, out)
                self.assertIn("pointer", out)
                self.assertIn("BoundsUpper", out)

    def test_orphaned_evidence_must_survive_pointer_repair(self):
        self.start_with_detached_criteria()
        write(self.repo.path, PARENT + "BOOT.md", PARENT_BOOT.replace(INLINE, POINTER + "Extra prose.\n"))
        self.repo.commit("old malformed pointer")
        git(self.repo.path, "branch", "-f", "main", "HEAD")
        write(self.repo.path, PARENT + "BOOT.md", PARENT_BOOT.replace(INLINE, POINTER))
        write(self.repo.path, PARENT + "ACCEPTANCE.md", LOWER)
        self.repo.commit("fix pointer but lose a requirement")
        code, out = self.repo.guard(criteria_only=True)
        self.assertEqual(code, 1, out)
        self.assertIn("BoundsUpper", out)

    def test_ordinary_branch_can_close_a_criterion_with_evidence(self):
        base = self.start_with_detached_criteria()
        write(self.repo.path, PARENT + "code.py", "value = 1\n")
        write(self.repo.path, PARENT + "ACCEPTANCE.md", UPPER + "- [x] Lower bound. (2026-11-13, test_lower).\n")
        self.repo.commit("implement lower bound")
        code, out = self.repo.guard(base=base, criteria_only=True)
        self.assertEqual(code, 0, out)

    def test_completion_distinguishes_a_date_in_the_requirement_from_dated_evidence(self):
        pending = "- [ ] Export includes the 2026-11-01 build."
        base = self.start_with_detached_criteria(pending + "\n")
        for suffix, expected in [("", 1), (" (2026-11-13).", 1), (" (2026-11-13, ExportTest).", 0)]:
            with self.subTest(suffix=suffix):
                write(self.repo.path, PARENT + "ACCEPTANCE.md", pending.replace("[ ]", "[x]") + suffix + "\n")
                self.repo.commit("sign off on export " + suffix)
                code, out = self.repo.guard(base=base, criteria_only=True)
                self.assertEqual(code, expected, out)

    def test_completion_can_reuse_a_dated_evidence_reference_already_on_the_item(self):
        pending = "- [ ] Lower bound. (2026-11-13, BoundsTest).\n"
        base = self.start_with_detached_criteria(pending)
        write(self.repo.path, PARENT + "ACCEPTANCE.md", pending.replace("[ ]", "[x]"))
        self.repo.commit("record completion against existing proof")
        code, out = self.repo.guard(base=base, criteria_only=True)
        self.assertEqual(code, 0, out)

    def test_completion_accepts_project_style_date_before_claim_and_citation_after_it(self):
        base = self.start_with_detached_criteria(LOWER)
        for citation, expected in [("", 1), (" (`tests/Bounds.Tests`: `LowerBoundTests`).", 0)]:
            with self.subTest(citation=citation):
                write(self.repo.path, PARENT + "ACCEPTANCE.md", "- [x] 2026-11-13: Lower bound." + citation + "\n")
                self.repo.commit("add evidence in project format " + citation)
                code, out = self.repo.guard(base=base, criteria_only=True)
                self.assertEqual(code, expected, out)

    def test_invalid_fence_opener_cannot_hide_a_deleted_criterion(self):
        example = "The following line contains literal backticks.\n```example `tag`\n"
        base = self.start_with_detached_criteria(example + UPPER)
        write(self.repo.path, PARENT + "ACCEPTANCE.md", example)
        self.repo.commit("drop the upper bound after an invalid example fence")
        code, out = self.repo.guard(base=base, criteria_only=True)
        self.assertEqual(code, 1, out)
        self.assertIn("BoundsUpper", out)

    def test_external_pointer_is_the_only_physical_content_in_its_section(self):
        base = self.start_with_detached_criteria()
        for extra in ("<!-- local note -->\n", "```text\nexample\n```\n"):
            with self.subTest(extra=extra):
                write(self.repo.path, PARENT + "BOOT.md", PARENT_BOOT.replace(INLINE, POINTER + extra))
                self.repo.commit("add another line beside the pointer")
                code, out = self.repo.guard(base=base, criteria_only=True)
                self.assertEqual(code, 1, out)
                self.assertIn("pointer", out)

    def test_ordinary_branch_cannot_weaken_a_criterion_while_ticking_it(self):
        base = self.start_with_detached_criteria()
        write(self.repo.path, PARENT + "ACCEPTANCE.md", UPPER + "- [x] Lower bound may fail. (2026-11-13, test_lower).\n")
        self.repo.commit("silently weaken lower bound")
        code, out = self.repo.guard(base=base, criteria_only=True)
        self.assertEqual(code, 1, out)
        self.assertIn("Lower bound", out)

    def test_completion_citation_must_end_before_any_change_to_the_claim(self):
        base = self.start_with_detached_criteria()
        for claim in ("Lower bound. (2026-11-13, test_lower), except for negative input.",
                      "2026-11-13: Lower bound. (`test_lower`), except for negative input.",
                      "Lower bound. (2026-11-13, test_lower) for positive input (only).",
                      "Lower bound. 2026-11-13, test_lower, except for negative input."):
            with self.subTest(claim=claim):
                write(self.repo.path, PARENT + "ACCEPTANCE.md", UPPER + "- [x] " + claim + "\n")
                self.repo.commit("tick with an exception " + claim)
                code, out = self.repo.guard(base=base, criteria_only=True)
                self.assertEqual(code, 1, out)

    def test_documented_revision_needs_a_successor_even_with_a_dated_reason(self):
        base = self.start_with_detached_criteria()
        for claim, expected in [("Placeholder.", 1), ("TBD", 1), ("<A checkable claim.>", 1),
                                ("Placeholder. (2026-11-13, BoundsTest).", 1),
                                ("2026-11-13: Placeholder. (BoundsTest).", 1),
                                ("Placeholder rows are rejected by the importer.", 0)]:
            with self.subTest(claim=claim):
                write(self.repo.path, PARENT + "ACCEPTANCE.md", UPPER + "- [ ] " + claim + "\n"
                      + "  ⚠ 2026-11-13: «Lower bound.» is obsolete because the importer rejects those rows.\n")
                self.repo.commit("replace criterion with " + claim)
                code, out = self.repo.guard(base=base, criteria_only=True)
                self.assertEqual(code, expected, out)

    def test_a_not_applicable_marker_cannot_replace_a_criterion(self):
        base = self.start_with_detached_criteria()
        for marker in ("N/A.", "n/a", "n.a."):
            with self.subTest(marker=marker):
                write(self.repo.path, PARENT + "ACCEPTANCE.md", UPPER + "- [ ] " + marker + "\n"
                      + "  ⚠ 2026-11-13: «Lower bound.» retired because the importer was replaced.\n")
                self.repo.commit("mark the old criterion " + marker)
                code, out = self.repo.guard(base=base, criteria_only=True)
                self.assertEqual(code, 1, out)

    def test_semicolon_separates_evidence_references(self):
        base = self.start_with_detached_criteria()
        for citations, expected in [("test_lower; negative input excluded", 1),
                                    ("test_lower; test_lower_with_negatives", 0),
                                    ("test_lower; [Bounds on signed input](tests/bounds.md)", 0),
                                    ("test_lower; `tests/Bounds.Tests`: `LowerBoundTests`", 0)]:
            with self.subTest(citations=citations):
                write(self.repo.path, PARENT + "ACCEPTANCE.md", UPPER
                      + "- [x] Lower bound. (2026-11-13, " + citations + ").\n")
                self.repo.commit("cite " + citations)
                code, out = self.repo.guard(base=base, criteria_only=True)
                self.assertEqual(code, expected, out)

    def test_documented_replacement_is_specific_to_one_criterion(self):
        base = self.start_with_detached_criteria()
        new = ('- [ ] Input is clamped at zero.\n'
               '  ⚠ 2026-11-13: «Lower bound.» was inaccurate because zero itself is accepted.\n')
        write(self.repo.path, PARENT + "ACCEPTANCE.md", new + UPPER)
        self.repo.commit("revise lower bound with rationale")
        code, out = self.repo.guard(base=base, criteria_only=True)
        self.assertEqual(code, 0, out)
        write(self.repo.path, PARENT + "ACCEPTANCE.md", new)
        self.repo.commit("remove unrelated upper check")
        code, out = self.repo.guard(base=base, criteria_only=True)
        self.assertEqual(code, 1, out)
        self.assertIn("BoundsUpper", out)

    def test_criterion_and_evidence_stay_together(self):
        base = self.start_with_detached_criteria()
        write(self.repo.path, PARENT + "ACCEPTANCE.md",
              "# Acceptance\n\n- [x] Correct upper bound\n" + LOWER + "      (2026-10-01, BoundsUpper).\n")
        self.repo.commit("move proof to the wrong criterion")
        code, out = self.repo.guard("src/Parent", base=base)
        self.assertEqual(code, 1, out)
        self.assertIn("BoundsUpper", out)

    def test_one_copy_cannot_prove_two_occurrences(self):
        base = self.start_with_detached_criteria(UPPER + UPPER)
        write(self.repo.path, PARENT + "ACCEPTANCE.md", "# Acceptance\n\n" + UPPER)
        self.repo.commit("collapse two occurrences")
        code, out = self.repo.guard("src/Parent", base=base)
        self.assertEqual(code, 1, out)
        self.assertIn("lost active criterion", out)

    def test_inline_criterion_moved_to_history_is_no_longer_active(self):
        write(self.repo.path, PARENT + "BOOT.md", PARENT_BOOT.replace(INLINE, "→ HISTORY.md#old-figure\n"))
        write(self.repo.path, PARENT + "HISTORY.md", HISTORY + "\n> " + INLINE)
        self.repo.commit("only an archived copy remains")
        code, out = self.repo.guard("src/Parent")
        self.assertEqual(code, 1, out)
        self.assertIn("It works", out)

    def test_criterion_may_return_from_detached_file_to_its_leaf(self):
        base = self.start_with_detached_criteria()
        write(self.repo.path, PARENT + "BOOT.md", PARENT_BOOT.replace(INLINE, LOWER + UPPER))
        (self.repo.path / PARENT / "ACCEPTANCE.md").unlink()
        self.repo.commit("move both criteria back into BOOT")
        code, out = self.repo.guard("src/Parent", base=base)
        self.assertEqual(code, 0, out)

    def test_detached_criterion_moves_to_existing_node_under_subheading(self):
        base = self.start_with_detached_criteria()
        write(self.repo.path, PARENT + "ACCEPTANCE.md", "# Acceptance\n\n" + LOWER)
        write(self.repo.path, "src/Other/BOOT.md",
              "# BOOT.md - other\n\n## Acceptance criteria\n\n### Bounds\n\n" + UPPER)
        self.repo.commit("move upper bound and evidence to other node")
        code, out = self.repo.guard("src/Parent", "src/Other", base=base)
        self.assertEqual(code, 0, out)

    def test_inline_criterion_can_move_to_a_linked_acceptance_file(self):
        write(self.repo.path, PARENT + "BOOT.md", PARENT_BOOT.replace(INLINE, POINTER))
        write(self.repo.path, PARENT + "ACCEPTANCE.md", "# Acceptance\n\n" + INLINE)
        self.repo.commit("detach current evidence")
        code, out = self.repo.guard("src/Parent")
        self.assertEqual(code, 0, out)

    def test_examples_and_quotations_do_not_substitute_for_a_criterion(self):
        base = self.start_with_detached_criteria()
        write(self.repo.path, PARENT + "ACCEPTANCE.md", "# Acceptance\n\n" + LOWER
              + "\n```markdown\n" + UPPER + "```\n\n> " + UPPER
              + "\nHidden example <!--\n" + UPPER + "-->\n")
        self.repo.commit("put upper bound only in a sample and quotation")
        code, out = self.repo.guard("src/Parent", base=base)
        self.assertEqual(code, 1, out)
        self.assertIn("lost active criterion", out)

    def test_historical_note_beside_a_criterion_can_be_archived(self):
        old = "      ⚠ 2026-09-01 Former limit was 10.\n      Measured by OldBounds.\n"
        base = self.start_with_detached_criteria(UPPER + "\n" + old)
        write(self.repo.path, PARENT + "ACCEPTANCE.md", "# Acceptance\n\n" + UPPER)
        write(self.repo.path, PARENT + "HISTORY.md", HISTORY + "\n" + old)
        self.repo.commit("archive prior evidence")
        code, out = self.repo.guard("src/Parent", base=base)
        self.assertEqual(code, 0, out)

    def move_rule_to_history(self):
        boot = PARENT_BOOT.replace(
            "Rule shared by both children.\n",
            "⚠ 2026-09-20: was shared, now moved → HISTORY.md#shared-rule\n")
        write(self.repo.path, "src/Parent/BOOT.md", boot)
        history = HISTORY.replace(
            "# HISTORY.md - parent\n",
            "# HISTORY.md - parent\n\n<a id=\"shared-rule\"></a>\n"
            "## 2026-10-02 - from \"Constraints\" - shared rule\n\n"
            "> Rule shared by both children.\n")
        write(self.repo.path, "src/Parent/HISTORY.md", history)

    def test_a_line_moved_to_history_with_a_pointer_is_green(self):
        self.move_rule_to_history()
        self.repo.commit("move")
        code, out = self.repo.guard("src/Parent")
        self.assertEqual(code, 0, out)

    def test_a_line_removed_without_a_copy_is_lost(self):
        write(self.repo.path, "src/Parent/BOOT.md",
              PARENT_BOOT.replace("Rule shared by both children.\n", ""))
        self.repo.commit("drop")
        code, out = self.repo.guard("src/Parent")
        self.assertEqual(code, 1)
        self.assertIn("lost line: Rule shared by both children.", out)

    def test_a_removed_line_that_starts_with_two_dashes_is_still_lost(self):
        write(self.repo.path, "src/Parent/BOOT.md",
              PARENT_BOOT.replace("Rule shared by both children.\n",
                                  "Rule shared by both children.\n\n-- a dashed rule.\n"))
        self.repo.commit("dashed rule at base")
        git(self.repo.path, "branch", "-f", "main", "HEAD")
        write(self.repo.path, "src/Parent/BOOT.md", PARENT_BOOT)
        self.repo.commit("drop the dashed rule")
        code, out = self.repo.guard("src/Parent")
        self.assertEqual(code, 1, out)
        self.assertIn("lost line: -- a dashed rule.", out)

    def test_a_removed_history_separator_breaks_append_only(self):
        write(self.repo.path, "src/Parent/HISTORY.md", HISTORY + "\n---\n")
        self.repo.commit("separator at base")
        git(self.repo.path, "branch", "-f", "main", "HEAD")
        write(self.repo.path, "src/Parent/HISTORY.md", HISTORY)
        self.repo.commit("drop the separator")
        code, out = self.repo.guard("src/Parent")
        self.assertEqual(code, 1, out)
        self.assertIn("line removed from append-only HISTORY.md: ---", out)

    def test_a_line_added_outside_a_pointer_paragraph_is_stray(self):
        write(self.repo.path, "src/Parent/BOOT.md",
              PARENT_BOOT + "\nA brand new rule nobody reviewed.\n")
        self.repo.commit("add")
        code, out = self.repo.guard("src/Parent")
        self.assertEqual(code, 1)
        self.assertIn("line added outside a pointer paragraph: A brand new rule", out)

    def test_an_undefined_anchor_is_red(self):
        write(self.repo.path, "src/Parent/BOOT.md",
              PARENT_BOOT + "\n⚠ 2026-09-21: was x, now y → HISTORY.md#nowhere\n")
        self.repo.commit("cite")
        code, out = self.repo.guard("src/Parent")
        self.assertEqual(code, 1)
        self.assertIn("unresolved anchor HISTORY.md#nowhere", out)

    def test_a_file_outside_the_nodes_is_out_of_scope(self):
        self.move_rule_to_history()
        write(self.repo.path, "src/Other/BOOT.md", "# BOOT.md - other, edited\n")
        self.repo.commit("move and stray edit")
        code, out = self.repo.guard("src/Parent")
        self.assertEqual(code, 1)
        self.assertIn("out of scope: src/Other/BOOT.md", out)

    def test_a_rule_moved_into_a_childs_boot_is_green(self):
        boot = PARENT_BOOT.replace(
            "Rule that binds only the child.\n",
            "The child's own rule lives in [Child](Child/BOOT.md).\n")
        write(self.repo.path, "src/Parent/BOOT.md", boot)
        write(self.repo.path, "src/Parent/Child/BOOT.md",
              "# BOOT.md - child\n\n## Constraints\n\nRule that binds only the child.\n")
        self.repo.commit("move down")
        code, out = self.repo.guard("src/Parent", "src/Parent/Child")
        self.assertEqual(code, 0, out)

    def test_a_new_nodes_boot_is_not_stray(self):
        write(self.repo.path, "src/Parent/Child/BOOT.md",
              "# BOOT.md - child\n\n## Purpose\n\nEverything here is new.\n")
        self.repo.commit("new child")
        code, out = self.repo.guard("src/Parent", "src/Parent/Child")
        self.assertEqual(code, 0, out)

    def test_a_qualified_citation_is_left_to_the_linter(self):
        write(self.repo.path, "src/Parent/BOOT.md",
              PARENT_BOOT + "\nSee the sibling's record → ../Other/HISTORY.md#elsewhere\n")
        self.repo.commit("qualified")
        code, out = self.repo.guard("src/Parent")
        self.assertNotIn("GUARD: src/Parent: unresolved anchor", out)
        self.assertEqual(code, 0, out)

    def test_a_line_removed_from_history_is_red(self):
        write(self.repo.path, "src/Parent/HISTORY.md",
              HISTORY.replace("> The figure was 7.\n", ""))
        self.repo.commit("rewrite history")
        code, out = self.repo.guard("src/Parent")
        self.assertEqual(code, 1)
        self.assertIn("line removed from append-only HISTORY.md: > The figure was 7.", out)

    def test_the_moving_head_as_base_reports_other_nodes_falsely(self):
        self.move_rule_to_history()
        self.repo.commit("move")
        git(self.repo.path, "checkout", "-q", "main")
        write(self.repo.path, "src/Other/BOOT.md", "# BOOT.md - other, moved on main\n")
        self.repo.commit("someone else's node on main")
        git(self.repo.path, "checkout", "-q", "work")
        head = subprocess.run(["git", "-C", str(self.repo.path), "rev-parse", "main"],
                              check=True, capture_output=True, text=True).stdout.strip()
        code, out = self.repo.guard("src/Parent", base=head)
        self.assertEqual(code, 1)
        self.assertIn("out of scope: src/Other/BOOT.md", out)
        code, out = self.repo.guard("src/Parent")
        self.assertEqual(code, 0, out)


if __name__ == "__main__":
    os.environ.setdefault("GIT_CONFIG_NOSYSTEM", "1")
    unittest.main(verbosity=1)
