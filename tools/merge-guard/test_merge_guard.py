#!/usr/bin/env python3
"""Proof that every check of merge_guard is non-degenerate.

Each test builds a scratch git repository under the system temp directory (never in this
tree), breaks exactly the one thing a check guards and asserts that this check, and only
this check, stops the run with one `GUARD:` line. The trial commands are replaced by stubs
(`--commands`), so the whole file runs without the .NET SDK. The guard runs as a
subprocess with TMP, TEMP and TMPDIR pointing at a private directory: that is where its
trial worktree, its log and its `gpu.lock` live, so a test can see what it left behind.

    python -X utf8 tools/merge-guard/test_merge_guard.py -v
"""

from __future__ import annotations

import json
import os
import re
import shutil
import stat
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path
from typing import Dict, List, NamedTuple, Optional

sys.path.insert(0, str(Path(__file__).resolve().parent))

import merge_guard as guard  # noqa: E402

GUARD_SCRIPT = str(Path(__file__).resolve().parent / "merge_guard.py")
DOCUMENT_SCOPE = r"^(node|other)/|^(BOOT|HISTORY)\.md$"
MERGE_MESSAGE = "feat(app): merge the coder's branch\n\nCo-Authored-By: Test <test@example.com>\n"

BOOT_TEXT = """# BOOT.md - node

## Purpose

Purpose line.

## Constraints

- rule kept
- row alpha
- row beta
-- double dash line
"""

HISTORY_TEXT = """# HISTORY.md

<a id="old"></a>
old entry text
"""

CHILD_BOOT_TEXT = """# BOOT.md - child

## Purpose

Child purpose line.
"""

SEED_FILES = {
    "README.md": "readme\n",
    "app/code.txt": "one\ntwo\n",
    "app/shared.txt": "a\nb\nc\nd\ne\n",
    "app/Bits.approved.txt": "bits1\nbits2\n",
    "app/Output.approved.json": "{\n  \"x\": 1\n}\n",
    "app/set.approved.d/notes.txt": "not a record\n",
    "BOOT.md": "# root\n\nRoot line.\n",
    "HISTORY.md": "# HISTORY.md\n\n<a id=\"rootold\"></a>\nroot entry\n",
    "node/BOOT.md": BOOT_TEXT,
    "node/HISTORY.md": HISTORY_TEXT,
    "node/sub/BOOT.md": CHILD_BOOT_TEXT,
    "other/BOOT.md": BOOT_TEXT,
    "other/HISTORY.md": HISTORY_TEXT,
}

TEMPLATE_ROOT: Optional[str] = None


class Run(NamedTuple):
    """One run of the guard."""

    code: int
    out: str


def make_writable(function, path, _excinfo):  # type: ignore[no-untyped-def]
    """`shutil.rmtree` error handler: git marks object files read-only on Windows."""
    os.chmod(path, stat.S_IWRITE)
    function(path)


def extended(path: str) -> str:
    """The path in the form Windows accepts beyond 260 characters; unchanged elsewhere."""
    return "\\\\?\\" + os.path.abspath(path) if os.name == "nt" else path


def remove_tree(path: str) -> None:
    """Remove a scratch directory, read-only files and very long paths included."""
    shutil.rmtree(extended(path), onerror=make_writable)


def read_text(path: str) -> str:
    """The text of a file, closed again."""
    with open(path, encoding="utf-8") as handle:
        return handle.read()


def read_json(path: str) -> Dict[str, str]:
    """A JSON object from a file, closed again."""
    with open(path, encoding="utf-8") as handle:
        return json.load(handle)  # type: ignore[no-any-return]


def git(repo: str, *args: str, check: bool = True) -> str:
    """Run git in a scratch repository, with an identity and no signing or line-ending magic."""
    result = subprocess.run(["git", "-C", repo, "-c", "core.longpaths=true"] + list(args), capture_output=True, text=True, encoding="utf-8")
    if check and result.returncode != 0:
        raise AssertionError("git {} failed: {}".format(" ".join(args), result.stderr))
    return result.stdout.strip()


def write(repo: str, files: Dict[str, str]) -> None:
    """Write files under `repo`, LF line endings."""
    for name, text in files.items():
        path = extended(str(Path(repo) / name))
        os.makedirs(os.path.dirname(path), exist_ok=True)
        with open(path, "w", encoding="utf-8", newline="\n") as handle:
            handle.write(text)


def setUpModule() -> None:  # noqa: N802
    """Build the seeded repository once; every test copies it."""
    global TEMPLATE_ROOT
    TEMPLATE_ROOT = tempfile.mkdtemp(prefix="merge-guard-template-")
    repo = os.path.join(TEMPLATE_ROOT, "repo")
    os.mkdir(repo)
    git(repo, "init", "-q", "-b", "main")
    for key, value in (("user.name", "Test"), ("user.email", "test@example.com"),
                       ("commit.gpgsign", "false"), ("core.autocrlf", "false")):
        git(repo, "config", key, value)
    write(repo, SEED_FILES)
    git(repo, "add", "-A")
    git(repo, "commit", "-q", "-m", "seed")
    git(repo, "checkout", "-q", "-b", "integration")


def tearDownModule() -> None:  # noqa: N802
    """Remove the seeded repository."""
    if TEMPLATE_ROOT:
        remove_tree(TEMPLATE_ROOT)


class GuardCase(unittest.TestCase):
    """A scratch copy of the seeded repository, checked out on the target branch `integration`."""

    def setUp(self) -> None:
        self.work = tempfile.mkdtemp(prefix="merge-guard-test-")
        self.addCleanup(remove_tree, self.work)
        self.repo = os.path.join(self.work, "repo")
        self.scratch_tmp = os.path.join(self.work, "tmp")
        os.mkdir(self.scratch_tmp)
        assert TEMPLATE_ROOT is not None
        shutil.copytree(os.path.join(TEMPLATE_ROOT, "repo"), self.repo)
        self.environment = dict(os.environ, TMP=self.scratch_tmp, TEMP=self.scratch_tmp, TMPDIR=self.scratch_tmp)
        self.environment["PYTHONUTF8"] = "1"

    # --- building scenarios

    def coder_commit(self, files: Dict[str, str], message: str = "coder work", branch: str = "coder") -> None:
        """Commit files on a coder branch cut from the seed, then return to `integration`."""
        if git(self.repo, "rev-parse", "--verify", "--quiet", "refs/heads/" + branch, check=False) == "":
            git(self.repo, "branch", branch, "main")
        git(self.repo, "checkout", "-q", branch)
        self.commit(files, message)
        git(self.repo, "checkout", "-q", "integration")

    def commit(self, files: Dict[str, str], message: str = "work") -> None:
        """Commit files on the checked-out branch."""
        write(self.repo, files)
        git(self.repo, "add", "-A")
        git(self.repo, "commit", "-q", "--allow-empty", "-m", message)

    def target_commit(self, files: Dict[str, str]) -> None:
        """Commit files on `integration`, after the coder's branch was cut."""
        self.commit(files, "target work")

    def edit(self, name: str, old: str, new: str) -> str:
        """The text of a seeded file with `old` replaced by `new`."""
        text = SEED_FILES[name]
        self.assertIn(old, text)
        return text.replace(old, new)

    def stubs(self, *entries: Dict[str, object]) -> str:
        """Write a `--commands` file; return its path."""
        path = os.path.join(self.work, "commands.json")
        with open(path, "w", encoding="utf-8", newline="\n") as handle:
            json.dump(list(entries), handle)
        return path

    @staticmethod
    def stub(name: str, code: str = "print('ok')", **fields: object) -> Dict[str, object]:
        """One command-table entry that runs a Python one-liner."""
        return dict({"name": name, "argv": [sys.executable, "-c", code]}, **fields)

    def green_stub(self) -> str:
        """A command table whose one command passes with a dotnet-test-like line."""
        return self.stubs(self.stub("stub", "print('Passed!  - Failed:     0, Passed:     3, Skipped:     0, Total:     3')"))

    # --- running the guard

    def run_guard(self, *arguments: str) -> Run:
        """Run the guard in the scratch repository."""
        result = subprocess.run([sys.executable, "-X", "utf8", GUARD_SCRIPT] + list(arguments), cwd=self.repo,
                                env=self.environment, capture_output=True, text=True, encoding="utf-8")
        return Run(result.returncode, result.stdout + result.stderr)

    def run_green(self, *arguments: str) -> Run:
        """Run with the default branch, the scope `^app/` and the green stub, plus `arguments`."""
        return self.run_guard("coder", "--scope", "^app/", "--commands", self.green_stub(), *arguments)

    # --- assertions

    def assert_red(self, run: Run, fragment: str) -> None:
        """Exit code 1 with exactly one `GUARD:` line, which holds `fragment`."""
        self.assertEqual(1, run.code, run.out)
        lines = [line for line in run.out.splitlines() if line.startswith("GUARD:")]
        self.assertEqual(1, len(lines), run.out)
        self.assertIn(fragment, lines[0], run.out)

    def assert_invocation_error(self, run: Run) -> None:
        """Exit code 2 and no GUARD line."""
        self.assertEqual(2, run.code, run.out)
        self.assertNotIn("GUARD:", run.out)

    def assert_left_clean(self) -> None:
        """No trial worktree or directory, no gpu.lock, no merge in progress in the main checkout."""
        leftovers = [name for name in os.listdir(self.scratch_tmp) if name.startswith("merge-guard-trial")]
        self.assertEqual([], leftovers)
        self.assertNotIn("merge-guard-trial", git(self.repo, "worktree", "list", "--porcelain"))
        self.assertFalse(os.path.exists(os.path.join(self.scratch_tmp, "gpu.lock")))
        self.assertEqual("", git(self.repo, "rev-parse", "-q", "--verify", "MERGE_HEAD", check=False))
        self.assertEqual("", git(self.repo, "status", "--porcelain", "--untracked-files=no"))

    def head(self) -> str:
        """The commit the main checkout stands on."""
        return git(self.repo, "rev-parse", "HEAD")

    def log_path(self, run: Run) -> str:
        """The log file the run printed."""
        match = re.search(r"\(log: (.+)\)", run.out)
        self.assertIsNotNone(match, run.out)
        assert match is not None
        return match.group(1)


class InvocationTests(GuardCase):
    """Exit code 2: the command line is wrong, not the branch."""

    def test_a_call_without_scope_is_an_invocation_error(self) -> None:
        """There is no default scope."""
        self.coder_commit({"app/new.txt": "x\n"})
        self.assert_invocation_error(self.run_guard("coder", "--checks-only"))

    def test_an_invalid_regular_expression_is_an_invocation_error(self) -> None:
        """A pattern that does not compile is refused before any check."""
        self.coder_commit({"app/new.txt": "x\n"})
        self.assert_invocation_error(self.run_guard("coder", "--scope", "(", "--checks-only"))

    def test_merge_with_a_missing_message_file_is_an_invocation_error(self) -> None:
        """The commit message file must exist before anything starts."""
        self.coder_commit({"app/new.txt": "x\n"})
        self.assert_invocation_error(self.run_green("--merge", os.path.join(self.work, "missing.txt")))

    def test_checks_only_with_merge_is_an_invocation_error(self) -> None:
        """A run that stops before the trial cannot merge."""
        self.coder_commit({"app/new.txt": "x\n"})
        message = os.path.join(self.work, "message.txt")
        write(self.work, {"message.txt": MERGE_MESSAGE})
        self.assert_invocation_error(self.run_guard("coder", "--scope", "^app/", "--checks-only", "--merge", message))

    def test_a_malformed_commands_file_is_an_invocation_error(self) -> None:
        """An entry without argv, and an empty table, are refused."""
        self.coder_commit({"app/new.txt": "x\n"})
        bad = self.stubs({"name": "no argv"})
        self.assert_invocation_error(self.run_guard("coder", "--scope", "^app/", "--commands", bad))
        empty = self.stubs()
        self.assert_invocation_error(self.run_guard("coder", "--scope", "^app/", "--commands", empty))


class PreconditionTests(GuardCase):
    """Check 1."""

    def test_merging_into_main_is_refused(self) -> None:
        """The owner merges main."""
        self.coder_commit({"app/new.txt": "x\n"})
        git(self.repo, "checkout", "-q", "main")
        self.assert_red(self.run_green(), "target is main")

    def test_a_dirty_main_checkout_is_refused(self) -> None:
        """A tracked change in the main checkout stops the run."""
        self.coder_commit({"app/new.txt": "x\n"})
        write(self.repo, {"README.md": "changed\n"})
        self.assert_red(self.run_green(), "tracked changes")

    def test_an_untracked_file_in_the_main_checkout_is_not_a_change(self) -> None:
        """Only tracked changes count: local tool state stays untracked."""
        self.coder_commit({"app/new.txt": "x\n"})
        write(self.repo, {".claude/state.json": "{}\n"})
        run = self.run_green("--checks-only")
        self.assertEqual(0, run.code, run.out)

    def test_a_missing_branch_is_refused(self) -> None:
        """The branch must exist."""
        self.assert_red(self.run_guard("nowhere", "--scope", "^app/", "--checks-only"), "does not exist")

    def test_a_branch_with_no_commit_over_the_merge_base_is_an_empty_walk(self) -> None:
        """Nothing to merge is a failure, not a pass."""
        git(self.repo, "branch", "idle", "main")
        self.assert_red(self.run_guard("idle", "--scope", "^app/", "--checks-only"), "no commit over the merge base")

    def test_a_dirty_coder_worktree_is_refused(self) -> None:
        """The coder's worktree, found through git worktree list, must have no change."""
        self.coder_commit({"app/new.txt": "x\n"})
        worktree = os.path.join(self.work, "coder-worktree")
        git(self.repo, "worktree", "add", "-q", worktree, "coder")
        write(worktree, {"app/new.txt": "edited after the commit\n"})
        self.assert_red(self.run_green("--checks-only"), "coder's worktree")

    def test_a_clean_coder_worktree_passes_and_is_left_untouched(self) -> None:
        """A clean coder worktree is read, never written."""
        self.coder_commit({"app/new.txt": "x\n"})
        worktree = os.path.join(self.work, "coder-worktree")
        git(self.repo, "worktree", "add", "-q", worktree, "coder")
        before = git(worktree, "rev-parse", "HEAD")
        run = self.run_green()
        self.assertEqual(0, run.code, run.out)
        self.assertEqual(before, git(worktree, "rev-parse", "HEAD"))
        self.assertEqual("", git(worktree, "status", "--porcelain"))


class ScopeTests(GuardCase):
    """Check 2."""

    def test_a_path_outside_the_scope_fails_and_is_named(self) -> None:
        """One stray path is enough, and it is named."""
        self.coder_commit({"app/new.txt": "x\n", "tools/stray.txt": "y\n"})
        run = self.run_green("--checks-only")
        self.assert_red(run, "scope: 1 of 2 paths outside the scope")
        self.assertIn("outside the scope: tools/stray.txt", run.out)

    def test_every_pattern_is_a_search_over_the_whole_path(self) -> None:
        """Two patterns together cover two directories."""
        self.coder_commit({"app/new.txt": "x\n", "tools/other.txt": "y\n"})
        run = self.run_guard("coder", "--scope", "^app/", "^tools/", "--checks-only")
        self.assertEqual(0, run.code, run.out)
        self.assertIn("scope: 2 paths, all inside the scope", run.out)

    def test_a_branch_whose_commits_change_no_path_is_an_empty_walk(self) -> None:
        """A commit without a change leaves nothing to check."""
        git(self.repo, "branch", "hollow", "main")
        git(self.repo, "checkout", "-q", "hollow")
        git(self.repo, "commit", "-q", "--allow-empty", "-m", "nothing")
        git(self.repo, "checkout", "-q", "integration")
        self.assert_red(self.run_guard("hollow", "--scope", "^app/", "--checks-only"), "changed no path")


class ApprovedRecordTests(GuardCase):
    """Check 3."""

    def test_a_changed_approved_record_outside_approve_fails_and_is_listed(self) -> None:
        """The record is listed with its line counts even though it fails."""
        self.coder_commit({"app/Bits.approved.txt": "bits1\nbits2\nbits3\n"})
        run = self.run_green("--checks-only")
        self.assert_red(run, "approved records: 1 changed outside --approve")
        self.assertIn("app/Bits.approved.txt: +1 -0 (NOT APPROVED)", run.out)

    def test_a_changed_approved_json_outside_approve_fails(self) -> None:
        """An approved record is any name containing `.approved.`, not only `.approved.txt`."""
        self.coder_commit({"app/Output.approved.json": "{\n  \"x\": 2\n}\n"})
        run = self.run_green("--checks-only")
        self.assert_red(run, "approved records: 1 changed outside --approve")
        self.assertIn("app/Output.approved.json", run.out)

    def test_a_changed_approved_record_inside_approve_passes_and_is_listed(self) -> None:
        """An approved change is listed too, so the reviewer sees the move."""
        self.coder_commit({"app/Bits.approved.txt": "bits1\nbits2\nbits3\n", "app/Output.approved.json": "{}\n"})
        run = self.run_green("--checks-only", "--approve", r"Bits\.approved\.txt$", r"Output\.approved\.json$")
        self.assertEqual(0, run.code, run.out)
        self.assertIn("app/Bits.approved.txt: +1 -0 (approved)", run.out)
        self.assertIn("app/Output.approved.json: +1 -3 (approved)", run.out)

    def test_an_approve_pattern_that_matches_no_record_is_an_empty_walk(self) -> None:
        """A pattern that no record of the tree matches guards nothing."""
        self.coder_commit({"app/new.txt": "x\n"})
        self.assert_red(self.run_green("--checks-only", "--approve", "NoSuchRecord"), "an empty walk")

    def test_a_file_with_approved_only_in_its_directory_is_no_record(self) -> None:
        """The name of the file decides, not the name of its directory."""
        self.coder_commit({"app/set.approved.d/notes.txt": "edited\n"})
        run = self.run_green("--checks-only")
        self.assertEqual(0, run.code, run.out)
        self.assertIn("approved records: none changed", run.out)


class DocumentTests(GuardCase):
    """Check 4."""

    def documents(self, *extra: str, nodes: Optional[List[str]] = None) -> Run:
        """Static checks with the document check over `nodes` (default: node), scope everything."""
        selected = nodes if nodes is not None else ["node"]
        return self.run_guard("coder", "--scope", DOCUMENT_SCOPE, "--checks-only", "--doc-nodes", *selected, *extra)

    def condensed(self) -> Dict[str, str]:
        """node/BOOT.md and node/HISTORY.md after the rows moved out, as a condensation does."""
        boot = self.edit("node/BOOT.md", "- row alpha\n- row beta\n", "") + "\n⚠ 2026-10-02: rows moved → HISTORY.md#rows\n"
        history = SEED_FILES["node/HISTORY.md"].replace(
            "\n<a id=\"old\">", "\n<a id=\"rows\"></a>\n- row alpha\n- row beta\n\n<a id=\"old\">")
        return {"node/BOOT.md": boot, "node/HISTORY.md": history}

    def test_a_condensation_that_moves_every_line_is_green(self) -> None:
        """Removed lines reappear in HISTORY.md, the pointer paragraph is the only addition."""
        self.coder_commit(self.condensed())
        run = self.documents()
        self.assertEqual(0, run.code, run.out)
        self.assertIn("documents: 1 nodes, 2 lines moved, 0 lost, 0 stray, 0 unresolved, HISTORY.md append-only", run.out)

    def test_a_lost_line_fails(self) -> None:
        """A line removed from BOOT.md that reappears nowhere is lost."""
        self.coder_commit({"node/BOOT.md": self.edit("node/BOOT.md", "- row alpha\n", "")})
        run = self.documents()
        self.assert_red(run, "1 lost")
        self.assertIn("lost line: - row alpha", run.out)

    def test_a_removed_line_that_starts_with_two_dashes_is_still_lost(self) -> None:
        """git prints such a line as `---...`, which is not a file header."""
        self.coder_commit({"node/BOOT.md": self.edit("node/BOOT.md", "-- double dash line\n", "")})
        run = self.documents()
        self.assert_red(run, "1 lost")
        self.assertIn("-- double dash line", run.out)

    def test_a_stray_line_fails(self) -> None:
        """A line added outside a paragraph that carries a pointer is stray."""
        self.coder_commit({"node/BOOT.md": self.edit("node/BOOT.md", "- row beta\n", "- row beta\n- a rule nobody pointed to\n")})
        run = self.documents()
        self.assert_red(run, "1 stray")
        self.assertIn("a rule nobody pointed to", run.out)

    def test_a_new_heading_is_not_a_stray_line(self) -> None:
        """A heading kept in place is allowed."""
        self.coder_commit({"node/BOOT.md": SEED_FILES["node/BOOT.md"] + "\n## Taboos\n"})
        run = self.documents()
        self.assertEqual(0, run.code, run.out)

    def test_an_unresolved_anchor_fails(self) -> None:
        """A pointer to an anchor that no HISTORY.md in the chain defines."""
        text = SEED_FILES["node/BOOT.md"] + "\n⚠ 2026-10-02: moved → HISTORY.md#missing\n"
        self.coder_commit({"node/BOOT.md": text})
        run = self.documents()
        self.assert_red(run, "1 unresolved")
        self.assertIn("unresolved anchor HISTORY.md#missing", run.out)

    def test_an_anchor_of_an_ancestors_history_resolves(self) -> None:
        """A child cites the anchor its parent's HISTORY.md defines."""
        text = SEED_FILES["node/sub/BOOT.md"] + "\n⚠ 2026-10-02: moved → HISTORY.md#old\n"
        self.coder_commit({"node/sub/BOOT.md": text})
        run = self.documents(nodes=["node/sub"])
        self.assertEqual(0, run.code, run.out)

    def test_an_anchor_of_the_root_history_resolves_for_the_root_node(self) -> None:
        """`.` names the root, whose HISTORY.md is the chain's last link."""
        self.coder_commit({"BOOT.md": SEED_FILES["BOOT.md"] + "\n⚠ 2026-10-02: moved → HISTORY.md#rootold\n"})
        run = self.documents(nodes=["."])
        self.assertEqual(0, run.code, run.out)

    def test_a_line_moved_down_into_another_named_node_is_neither_lost_nor_stray(self) -> None:
        """A rule that moved into the child that owns it is the same text in another BOOT.md."""
        self.coder_commit({"node/BOOT.md": self.edit("node/BOOT.md", "- row alpha\n", ""),
                           "node/sub/BOOT.md": SEED_FILES["node/sub/BOOT.md"] + "\n- row alpha\n"})
        run = self.documents(nodes=["node", "node/sub"])
        self.assertEqual(0, run.code, run.out)
        self.assertIn("2 nodes", run.out)

    def test_a_removed_line_of_a_named_nodes_history_fails(self) -> None:
        """HISTORY.md is append-only."""
        self.coder_commit({"node/HISTORY.md": self.edit("node/HISTORY.md", "old entry text\n", "")})
        run = self.documents()
        self.assert_red(run, "1 HISTORY.md lines removed")
        self.assertIn("node/HISTORY.md: removed line: old entry text", run.out)

    def test_a_removed_line_of_an_unnamed_nodes_history_fails(self) -> None:
        """Named node or not."""
        self.coder_commit({"other/HISTORY.md": self.edit("other/HISTORY.md", "old entry text\n", "")})
        run = self.documents()
        self.assert_red(run, "1 HISTORY.md lines removed")
        self.assertIn("other/HISTORY.md", run.out)

    def test_a_named_node_without_boot_is_an_empty_walk(self) -> None:
        """A documents check over a node that has no BOOT.md is a failure."""
        self.coder_commit({"node/extra.txt": "x\n"})
        self.assert_red(self.documents(nodes=["nowhere"]), "has no BOOT.md")

    def test_documents_are_skipped_without_doc_nodes(self) -> None:
        """Without --doc-nodes the check does not run, a lost line included."""
        self.coder_commit({"node/BOOT.md": self.edit("node/BOOT.md", "- row alpha\n", "")})
        run = self.run_guard("coder", "--scope", DOCUMENT_SCOPE, "--checks-only")
        self.assertEqual(0, run.code, run.out)
        self.assertIn("documents: skipped", run.out)


class OverlapTests(GuardCase):
    """Check 5."""

    def diverge(self, branch_edit: str = "e", target_edit: str = "a") -> None:
        """Both sides edit app/shared.txt, on different lines unless asked."""
        text = SEED_FILES["app/shared.txt"]
        self.coder_commit({"app/shared.txt": text.replace(branch_edit + "\n", branch_edit + "-coder\n")})
        self.target_commit({"app/shared.txt": text.replace(target_edit + "\n", target_edit + "-target\n")})

    def test_a_file_changed_on_both_sides_fails_without_an_overlap_pattern(self) -> None:
        """Both sides touched the file since the merge base."""
        self.diverge()
        run = self.run_green("--checks-only")
        self.assert_red(run, "overlap: 1 files changed on both sides")
        self.assertIn("changed on both sides: app/shared.txt", run.out)

    def test_a_file_changed_on_both_sides_passes_when_allowed(self) -> None:
        """The pattern names the file the orchestrator expects both sides to touch."""
        self.diverge()
        run = self.run_green("--checks-only", "--overlap", r"^app/shared\.txt$")
        self.assertEqual(0, run.code, run.out)
        self.assertIn("overlap: 1 files, all allowed", run.out)

    def test_the_base_is_the_merge_base_not_the_moving_head(self) -> None:
        """A path only the target changed is not an overlap, and not a coder path either."""
        self.coder_commit({"app/new.txt": "x\n"})
        self.target_commit({"README.md": "target readme\n"})
        run = self.run_green("--checks-only")
        self.assertEqual(0, run.code, run.out)
        self.assertIn("scope: 1 paths", run.out)
        self.assertIn("overlap: none", run.out)


class TrialTests(GuardCase):
    """Checks 6 and 7, and what they leave behind."""

    def test_a_conflict_fails_names_the_files_and_leaves_nothing_behind(self) -> None:
        """Both sides edit the same line; the merge is aborted with the worktree."""
        text = SEED_FILES["app/shared.txt"]
        self.coder_commit({"app/shared.txt": text.replace("c\n", "c-coder\n")})
        self.target_commit({"app/shared.txt": text.replace("c\n", "c-target\n")})
        before = self.head()
        run = self.run_green("--overlap", r"^app/shared\.txt$")
        self.assert_red(run, "trial merge: conflict in 1 files")
        self.assertIn("conflict: app/shared.txt", run.out)
        self.assertEqual(before, self.head())
        self.assert_left_clean()

    def test_a_failing_command_stops_the_run_and_leaves_nothing_behind(self) -> None:
        """The first red command is the last one run; its output is in the log."""
        self.coder_commit({"app/new.txt": "x\n"})
        marker = os.path.join(self.work, "second-ran.txt")
        commands = self.stubs(
            self.stub("lint", "import sys; print('protocol_lint: 1 errors, 0 warnings'); sys.exit(1)"),
            self.stub("build", "open({!r}, 'w').write('ran')".format(marker)))
        run = self.run_guard("coder", "--scope", "^app/", "--commands", commands)
        self.assert_red(run, "lint failed: protocol_lint: 1 errors, 0 warnings")
        self.assertFalse(os.path.exists(marker))
        self.assert_left_clean()

    def test_a_command_without_its_green_marker_fails(self) -> None:
        """Exit 0 is not enough when the entry asks for a marker in the output."""
        self.coder_commit({"app/new.txt": "x\n"})
        commands = self.stubs(self.stub("build", "print('Build succeeded.')", green_when_output_contains="0 Warning(s)"))
        self.assert_red(self.run_guard("coder", "--scope", "^app/", "--commands", commands), "build failed")

    def test_ten_warnings_are_not_a_clean_build(self) -> None:
        """`10 Warning(s)` contains the text `0 Warning(s)` and must not satisfy the marker."""
        self.coder_commit({"app/new.txt": "x\n"})
        commands = self.stubs(self.stub("build", "print('    10 Warning(s)'); print('    0 Error(s)')",
                                        green_when_output_contains="0 Warning(s)"))
        self.assert_red(self.run_guard("coder", "--scope", "^app/", "--commands", commands), "build failed")

    def test_a_clean_build_marker_passes_and_the_counts_are_summarised(self) -> None:
        """The marker present, the summary line carries the counts."""
        self.coder_commit({"app/new.txt": "x\n"})
        commands = self.stubs(self.stub("build", "print('    0 Warning(s)'); print('    0 Error(s)')",
                                        green_when_output_contains="0 Warning(s)"))
        run = self.run_guard("coder", "--scope", "^app/", "--commands", commands)
        self.assertEqual(0, run.code, run.out)
        self.assertIn("ok    build: 0 Warning(s), 0 Error(s)", run.out)

    def test_a_command_that_cannot_be_started_fails(self) -> None:
        """A missing executable is a red command, not a crash."""
        self.coder_commit({"app/new.txt": "x\n"})
        commands = self.stubs({"name": "ghost", "argv": ["no-such-program-merge-guard"]})
        self.assert_red(self.run_guard("coder", "--scope", "^app/", "--commands", commands), "ghost failed")
        self.assert_left_clean()

    def test_commands_run_in_the_trial_worktree_with_the_branch_merged(self) -> None:
        """The stub sees the coder's file and the target's file, in a directory that is not the checkout."""
        self.coder_commit({"app/coder.txt": "c\n"})
        self.target_commit({"README.md": "target readme\n"})
        code = ("import os; print('CWD=' + os.getcwd()); "
                "print('CODER=' + str(os.path.exists('app/coder.txt'))); "
                "print('TARGET=' + open('README.md').read().strip())")
        run = self.run_guard("coder", "--scope", "^app/", "--commands", self.stubs(self.stub("probe", code)))
        self.assertEqual(0, run.code, run.out)
        log = read_text(self.log_path(run))
        self.assertIn("CODER=True", log)
        self.assertIn("TARGET=target readme", log)
        cwd = re.search(r"CWD=(.+)", log)
        assert cwd is not None
        self.assertNotEqual(os.path.realpath(self.repo), os.path.realpath(cwd.group(1).strip()))
        self.assertFalse(os.path.exists(os.path.join(self.repo, "app", "coder.txt")))

    def test_a_path_beyond_the_windows_limit_is_checked_out_in_the_trial(self) -> None:
        """The trial's git calls run with core.longpaths: a deep file is no checkout failure."""
        deep = "/".join(["app"] + ["d" * 40] * 6 + ["file.txt"])
        self.coder_commit({deep: "deep\n"})
        run = self.run_green()
        self.assertEqual(0, run.code, run.out)
        self.assert_left_clean()

    def test_without_merge_the_run_stops_after_the_trial(self) -> None:
        """A green trial changes nothing in the main checkout."""
        self.coder_commit({"app/new.txt": "x\n"})
        before = self.head()
        run = self.run_green()
        self.assertEqual(0, run.code, run.out)
        self.assertIn("ok    stub: 1 assemblies, 3 passed, 0 failed", run.out)
        self.assertNotIn("merged:", run.out)
        self.assertEqual(before, self.head())
        self.assert_left_clean()

    def test_checks_only_runs_no_command_and_creates_no_worktree(self) -> None:
        """The static checks stop before the trial."""
        self.coder_commit({"app/new.txt": "x\n"})
        marker = os.path.join(self.work, "ran.txt")
        commands = self.stubs(self.stub("probe", "open({!r}, 'w').write('ran')".format(marker)))
        run = self.run_guard("coder", "--scope", "^app/", "--checks-only", "--commands", commands)
        self.assertEqual(0, run.code, run.out)
        self.assertFalse(os.path.exists(marker))
        self.assertNotIn("trial merge", run.out)
        self.assertEqual([], os.listdir(self.scratch_tmp))

    def test_a_table_that_runs_nothing_is_an_empty_walk(self) -> None:
        """Only CUDA entries, and no --cuda: nothing was proved."""
        self.coder_commit({"app/new.txt": "x\n"})
        commands = self.stubs(self.stub("cuda only", requires="cuda"))
        self.assert_red(self.run_guard("coder", "--scope", "^app/", "--commands", commands), "no command ran")
        self.assert_left_clean()


class GpuLockTests(GuardCase):
    """The GPU lock of `--cuda`."""

    LOCK = "gpu.lock"

    def lock_path(self) -> str:
        """Where the guard keeps the lock."""
        return os.path.join(self.scratch_tmp, self.LOCK)

    def cuda_commands(self) -> str:
        """A plain command, then one that needs CUDA and prints the lock it runs under."""
        probe = ("import json, os; print('LOCKOWNER=' + json.load(open(os.path.join("
                 "os.environ['TMP'], 'gpu.lock')))['owner'])")
        return self.stubs(self.stub("plain"), self.stub("cuda probe", probe, requires="cuda"))

    def test_a_live_lock_of_another_owner_fails_the_run(self) -> None:
        """`GPU busy: <owner> until <time>`, and the other owner's lock is left alone."""
        self.coder_commit({"app/new.txt": "x\n"})
        until = "2999-01-01T00:00:00+00:00"
        with open(self.lock_path(), "w", encoding="utf-8") as handle:
            json.dump({"owner": "another session", "until": until}, handle)
        run = self.run_guard("coder", "--scope", "^app/", "--cuda", "--commands", self.cuda_commands())
        self.assert_red(run, "GPU busy: another session until " + until)
        self.assertIn("ok    plain", run.out)
        self.assertTrue(os.path.exists(self.lock_path()))
        self.assertEqual("another session", read_json(self.lock_path())["owner"])

    def test_an_expired_lock_is_replaced_and_the_guards_own_is_removed(self) -> None:
        """The command runs under the guard's lock; afterwards no lock is left."""
        self.coder_commit({"app/new.txt": "x\n"})
        with open(self.lock_path(), "w", encoding="utf-8") as handle:
            json.dump({"owner": "gone session", "until": "2000-01-01T00:00:00Z"}, handle)
        run = self.run_guard("coder", "--scope", "^app/", "--cuda", "--commands", self.cuda_commands())
        self.assertEqual(0, run.code, run.out)
        self.assertIn("LOCKOWNER=merge-guard pid", read_text(self.log_path(run)))
        self.assert_left_clean()

    def test_the_lock_is_removed_after_a_failing_cuda_command(self) -> None:
        """Every exit path releases the lock."""
        self.coder_commit({"app/new.txt": "x\n"})
        commands = self.stubs(self.stub("cuda proofs", "import sys; sys.exit(3)", requires="cuda"))
        run = self.run_guard("coder", "--scope", "^app/", "--cuda", "--commands", commands)
        self.assert_red(run, "cuda proofs failed")
        self.assert_left_clean()

    def test_without_cuda_no_lock_is_taken_and_cuda_entries_are_skipped(self) -> None:
        """The lock belongs to the CUDA commands alone."""
        self.coder_commit({"app/new.txt": "x\n"})
        marker = os.path.join(self.work, "cuda-ran.txt")
        commands = self.stubs(
            self.stub("plain"),
            self.stub("cuda probe", "open({!r}, 'w').write('ran')".format(marker), requires="cuda"))
        with open(self.lock_path(), "w", encoding="utf-8") as handle:
            json.dump({"owner": "another session", "until": "2999-01-01T00:00:00+00:00"}, handle)
        run = self.run_guard("coder", "--scope", "^app/", "--commands", commands)
        self.assertEqual(0, run.code, run.out)
        self.assertFalse(os.path.exists(marker))
        self.assertEqual("another session", read_json(self.lock_path())["owner"])

    def test_an_unreadable_lock_fails_loudly_and_is_left_alone(self) -> None:
        """The guard does not clobber a file it cannot read."""
        self.coder_commit({"app/new.txt": "x\n"})
        with open(self.lock_path(), "w", encoding="utf-8") as handle:
            handle.write("not json")
        run = self.run_guard("coder", "--scope", "^app/", "--cuda", "--commands", self.cuda_commands())
        self.assert_red(run, "unreadable")
        self.assertEqual("not json", read_text(self.lock_path()))


class MergeTests(GuardCase):
    """Check 8."""

    def message(self) -> str:
        """The commit message file."""
        write(self.work, {"message.txt": MERGE_MESSAGE})
        return os.path.join(self.work, "message.txt")

    def test_a_green_branch_is_merged_with_the_tree_equal_to_the_trial_tree(self) -> None:
        """The stub prints the tree of the trial; the merge commit has the same tree."""
        self.coder_commit({"app/new.txt": "x\n"})
        self.target_commit({"README.md": "target readme\n"})
        code = "import subprocess; print('TRIAL=' + subprocess.run(['git', 'write-tree'], capture_output=True, text=True).stdout.strip())"
        commands = self.stubs(self.stub("probe", code))
        run = self.run_guard("coder", "--scope", "^app/", "--commands", commands, "--merge", self.message())
        self.assertEqual(0, run.code, run.out)
        self.assertIn("tree equal to the trial tree", run.out)
        trial = re.search(r"TRIAL=([0-9a-f]+)", read_text(self.log_path(run)))
        assert trial is not None
        self.assertEqual(trial.group(1), git(self.repo, "rev-parse", "HEAD^{tree}"))
        self.assertEqual(2, len(git(self.repo, "rev-list", "--parents", "-n", "1", "HEAD").split()) - 1)
        self.assertEqual(MERGE_MESSAGE.strip(), git(self.repo, "log", "-1", "--format=%B").strip())
        self.assertTrue(os.path.exists(os.path.join(self.repo, "app", "new.txt")))
        self.assert_left_clean()

    def test_the_coder_branch_is_not_deleted_by_the_merge(self) -> None:
        """No deletion of anything the guard did not create."""
        self.coder_commit({"app/new.txt": "x\n"})
        run = self.run_green("--merge", self.message())
        self.assertEqual(0, run.code, run.out)
        self.assertNotEqual("", git(self.repo, "rev-parse", "--verify", "--quiet", "refs/heads/coder"))

    def test_a_target_that_moved_after_the_trial_aborts_the_merge(self) -> None:
        """The tree of the final merge differs from the trial's: aborted, nothing committed by the guard."""
        self.coder_commit({"app/new.txt": "x\n"})
        moving = (
            "import subprocess\n"
            "def git(*a): subprocess.run(['git', '-C', {repo!r}] + list(a), check=True, capture_output=True)\n"
            "open({late!r}, 'w').write('late\\n')\n"
            "git('add', 'late.txt'); git('commit', '-q', '-m', 'late target work')\n").format(
                repo=self.repo, late=os.path.join(self.repo, "late.txt"))
        commands = self.stubs(self.stub("mover", moving), self.stub("cuda probe", requires="cuda"))
        run = self.run_guard("coder", "--scope", "^app/", "--cuda", "--commands", commands, "--merge", self.message())
        self.assert_red(run, "differs from the trial tree")
        self.assertIn("merge aborted", run.out)
        self.assertEqual("late target work", git(self.repo, "log", "-1", "--format=%s"))
        self.assertFalse(os.path.exists(os.path.join(self.repo, "app", "new.txt")))
        self.assert_left_clean()

    def test_no_merge_is_made_when_a_check_is_red(self) -> None:
        """A red static check stops before any merge, with a message file given."""
        self.coder_commit({"tools/stray.txt": "x\n"})
        before = self.head()
        run = self.run_green("--merge", self.message())
        self.assert_red(run, "scope")
        self.assertEqual(before, self.head())
        self.assert_left_clean()


class SummaryTests(unittest.TestCase):
    """The one-line summaries of a command's output."""

    def test_test_totals_are_summed_over_assemblies(self) -> None:
        """Passed! and Failed! lines of dotnet test, one per assembly."""
        output = ("Passed!  - Failed:     0, Passed:    10, Skipped:     0, Total:    10, Duration: 1 s - A.dll (net10.0)\n"
                  "Failed!  - Failed:     2, Passed:     5, Skipped:     0, Total:     7, Duration: 1 s - B.dll (net10.0)\n")
        self.assertEqual("2 assemblies, 15 passed, 2 failed", guard.summarize_output(output))

    def test_build_counts_are_summarised(self) -> None:
        """The last Warning(s) and Error(s) lines."""
        self.assertEqual("0 Warning(s), 0 Error(s)", guard.summarize_output("Build succeeded.\n    0 Warning(s)\n    0 Error(s)\n"))

    def test_the_last_line_summarises_other_output(self) -> None:
        """The linter's last line."""
        self.assertEqual("protocol_lint: 0 errors", guard.summarize_output("a\nprotocol_lint: 0 errors\n\n"))

    def test_empty_output_is_said_so(self) -> None:
        """No output is not an empty summary."""
        self.assertEqual("(no output)", guard.summarize_output(""))

    def test_hunks_skip_the_file_header_only(self) -> None:
        """A removed `--x` line prints as `---x` after the first hunk header."""
        diff = "diff --git a/f b/f\nindex 1..2\n--- a/f\n+++ b/f\n@@ -1,2 +1 @@\n-keep\n--- gone\n+new\n"
        self.assertEqual((["keep", "-- gone"], ["new"]), guard.parse_hunks(diff))

    def test_the_default_command_table_is_the_one_of_api_md(self) -> None:
        """Names, CUDA gating, the linter's strictness and the CPU-only environment of the fast suite."""
        table = {command.name: command for command in guard.default_commands()}
        self.assertEqual(["lint", "build", "fast suite", "cuda build", "cuda proofs", "execution"], list(table))
        self.assertEqual(["cuda"] * 3, [table[name].requires for name in ("cuda build", "cuda proofs", "execution")])
        self.assertEqual([None] * 3, [table[name].requires for name in ("lint", "build", "fast suite")])
        self.assertEqual("--strict", table["lint"].argv[-1])
        self.assertEqual({"APTHERMO_NO_CUDA": "1"}, table["fast suite"].env)
        self.assertEqual("0 Warning(s)", table["build"].green_when_output_contains)


if __name__ == "__main__":
    unittest.main()
