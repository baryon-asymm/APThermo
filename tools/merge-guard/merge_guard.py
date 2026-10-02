#!/usr/bin/env python3
"""merge-guard: the orchestrator's acceptance gate for a coder's branch.

One command instead of a sequence typed by hand. It checks what the orchestrator
otherwise checks by reading (paths touched, approved records, the documents' moved text,
files both sides changed), merges the branch into a throwaway worktree and runs the
linter, the build and the tests there, and only after all of it is green merges the
branch into the current branch, with the merged tree proven equal to the tree tested.

    python -X utf8 tools/merge-guard/merge_guard.py <branch> --scope <regex> [...]

The contract is API.md, the rules are BOOT.md; both sit next to this file. The standard
library only, Python 3.8+. Nothing here pushes, fetches, tags or calls a network service.
"""

from __future__ import annotations

import argparse
import datetime
import json
import os
import re
import shutil
import stat
import subprocess
import sys
import tempfile
from dataclasses import dataclass
from typing import Dict, List, Optional, Pattern, Sequence, Set, Tuple

PROTECTED_TARGET = "main"
APPROVED_MARKER = ".approved."
LOCK_NAME = "gpu.lock"
LOCK_HOURS = 2
DETAIL_LIMIT = 40
DOCUMENT_NAMES = ("BOOT.md", "HISTORY.md", "ACCEPTANCE.md")
POINTER_MARKERS = ("HISTORY.md#", "ACCEPTANCE.md](ACCEPTANCE.md)", "/BOOT.md")
BARE_CITATION = re.compile(r"(?<![\w./-])HISTORY\.md#([A-Za-z0-9][A-Za-z0-9_-]*)")
EXPLICIT_ANCHOR = re.compile(r'<a\s+id="([^"]+)"')
HEADING = re.compile(r"^#+ (.+)$", re.MULTILINE)
TEST_SUMMARY = re.compile(r"^\s*(?:Passed|Failed)!\s+-\s+Failed:\s*(\d+),\s*Passed:\s*(\d+)", re.MULTILINE)
WARNING_COUNT = re.compile(r"(\d+) Warning\(s\)")
ERROR_COUNT = re.compile(r"(\d+) Error\(s\)")


class GuardFailure(Exception):
    """A check that is red: `what` is the one `GUARD:` line, `details` the lines above it."""

    def __init__(self, what: str, details: Sequence[str] = ()) -> None:
        super().__init__(what)
        self.what = what
        self.details = list(details)


class InvocationError(Exception):
    """The command line is wrong (exit code 2), not the branch."""


@dataclass(frozen=True)
class Command:
    """One entry of the command table (API.md, the command table)."""

    name: str
    argv: List[str]
    env: Dict[str, str]
    requires: Optional[str]
    green_when_output_contains: Optional[str]


@dataclass
class Options:
    """The parsed command line."""

    branch: str
    scope: List[Pattern[str]]
    approve: List[Pattern[str]]
    overlap: List[Pattern[str]]
    doc_nodes: List[str]
    checks_only: bool
    cuda: bool
    merge_message: Optional[str]
    commands: Optional[List[Command]]


@dataclass
class Context:
    """What the preconditions established and every later check reads."""

    repo: str
    options: Options
    target: str
    target_head: str
    base: str
    changed: List[str]


# --------------------------------------------------------------------------- git


def git(repo: str, *args: str, check: bool = True) -> "subprocess.CompletedProcess[str]":
    """Run git against `repo` with an explicit -C; the process working directory never changes."""
    result = subprocess.run(
        ["git", "-C", repo] + list(args),
        capture_output=True, text=True, encoding="utf-8", errors="replace",
    )
    if check and result.returncode != 0:
        detail = result.stderr.strip() or result.stdout.strip()
        raise GuardFailure("git {} failed: {}".format(" ".join(args[:2]), detail))
    return result


def git_text(repo: str, *args: str) -> str:
    """Stdout of a git command that must succeed, without its trailing newline."""
    return git(repo, *args).stdout.rstrip("\n")


def git_names(repo: str, *args: str) -> List[str]:
    """The NUL-separated names a `-z` git command prints."""
    return [name for name in git(repo, *args).stdout.split("\0") if name]


def merge_in_progress(repo: str) -> bool:
    """True while the checkout `repo` holds an unfinished merge."""
    return git(repo, "rev-parse", "-q", "--verify", "MERGE_HEAD", check=False).returncode == 0


def same_directory(left: str, right: str) -> bool:
    """Whether two paths name one directory, spelled however the platform spells it."""
    return os.path.normcase(os.path.realpath(left)) == os.path.normcase(os.path.realpath(right))


def matches_any(patterns: Sequence[Pattern[str]], path: str) -> bool:
    """`re.search` of every pattern against a repository-relative path with forward slashes."""
    return any(pattern.search(path) for pattern in patterns)


def capped(lines: Sequence[str]) -> List[str]:
    """At most DETAIL_LIMIT detail lines, the rest counted."""
    shown = list(lines[:DETAIL_LIMIT])
    if len(lines) > DETAIL_LIMIT:
        shown.append("... and {} more".format(len(lines) - DETAIL_LIMIT))
    return shown


# ------------------------------------------------------------------ preconditions


def coder_worktree_path(repo: str, branch: str) -> Optional[str]:
    """The path `git worktree list` shows for `branch`, or None."""
    listing = git_text(repo, "worktree", "list", "--porcelain")
    for block in listing.split("\n\n"):
        fields = block.splitlines()
        if "branch refs/heads/" + branch in fields:
            return next(line[len("worktree "):] for line in fields if line.startswith("worktree "))
    return None


def check_preconditions(repo: str, options: Options) -> Tuple[Context, str]:
    """Check 1: the target is not main and is clean, the branch exists with commits to merge."""
    target = git(repo, "symbolic-ref", "--quiet", "--short", "HEAD", check=False).stdout.strip()
    if not target:
        raise GuardFailure("the main checkout is on a detached HEAD: there is no target branch")
    if target == PROTECTED_TARGET:
        raise GuardFailure("the target is main: the owner merges main, not the guard")
    dirty = git_text(repo, "status", "--porcelain", "--untracked-files=no")
    if dirty or merge_in_progress(repo):
        raise GuardFailure("the main checkout has tracked changes or a merge in progress", capped(dirty.splitlines()))
    branch_ref = "refs/heads/" + options.branch
    if git(repo, "rev-parse", "--verify", "--quiet", branch_ref + "^{commit}", check=False).returncode != 0:
        raise GuardFailure("branch {} does not exist".format(options.branch))
    base = git(repo, "merge-base", "HEAD", branch_ref, check=False).stdout.strip()
    if not base:
        raise GuardFailure("branch {} shares no history with {}".format(options.branch, target))
    commits = int(git_text(repo, "rev-list", "--count", "{}..{}".format(base, branch_ref)))
    if commits < 1:
        raise GuardFailure("branch {} has no commit over the merge base: an empty walk".format(options.branch))
    check_coder_worktree(repo, options.branch)
    changed = git_names(repo, "diff", "--name-only", "-z", "--no-renames", base, branch_ref)
    context = Context(repo, options, target, git_text(repo, "rev-parse", "HEAD"), base, changed)
    return context, "preconditions: target {}, merge base {}, {} coder commits".format(target, base[:7], commits)


def check_coder_worktree(repo: str, branch: str) -> None:
    """The coder's worktree, when there is one, has no change: it is read, never written."""
    path = coder_worktree_path(repo, branch)
    toplevel = git_text(repo, "rev-parse", "--show-toplevel")
    if path is None or same_directory(path, toplevel) or not os.path.isdir(path):
        return
    changes = git_text(path, "status", "--porcelain")
    if changes:
        raise GuardFailure("the coder's worktree {} has changes".format(path), capped(changes.splitlines()))


# ------------------------------------------------------------------ scope, records


def check_scope(ctx: Context) -> str:
    """Check 2: every path the branch changed matches a scope pattern."""
    if not ctx.changed:
        raise GuardFailure("the branch changed no path since the merge base: an empty walk")
    outside = [path for path in ctx.changed if not matches_any(ctx.options.scope, path)]
    if outside:
        raise GuardFailure("scope: {} of {} paths outside the scope".format(len(outside), len(ctx.changed)),
                           capped(["outside the scope: " + path for path in outside]))
    return "scope: {} paths, all inside the scope".format(len(ctx.changed))


def is_approved_record(path: str) -> bool:
    """An approved record is a file whose name contains `.approved.` (`.txt`, `.json`, `.csv` and the like)."""
    return APPROVED_MARKER in path.split("/")[-1]


def check_approved_records(ctx: Context) -> str:
    """Check 3: a changed approved record must match an --approve pattern; every one is listed."""
    options = ctx.options
    tree = git_names(ctx.repo, "ls-tree", "-r", "--name-only", "-z", "refs/heads/" + options.branch)
    records = [path for path in tree if is_approved_record(path)]
    unmatched = [p.pattern for p in options.approve if not any(p.search(r) for r in records)]
    if unmatched:
        raise GuardFailure("approved records: no file of the tree matches {}: an empty walk".format(unmatched[0]),
                           ["pattern matches no approved record: " + pattern for pattern in unmatched])
    changed = [path for path in ctx.changed if is_approved_record(path)]
    if not changed:
        return "approved records: none changed"
    listing, refused = [], []
    for path in changed:
        added, removed = line_counts(ctx, path)
        approved = matches_any(options.approve, path)
        listing.append("{}: +{} -{} ({})".format(path, added, removed, "approved" if approved else "NOT APPROVED"))
        if not approved:
            refused.append(path)
    if refused:
        raise GuardFailure("approved records: {} changed outside --approve".format(len(refused)), listing)
    return "approved records: {} changed, all approved\n      ".format(len(changed)) + "\n      ".join(listing)


def line_counts(ctx: Context, path: str) -> Tuple[int, int]:
    """Added and removed line counts of one path between the merge base and the branch."""
    text = git_text(ctx.repo, "diff", "--numstat", "--no-renames", ctx.base, "refs/heads/" + ctx.options.branch, "--", path)
    fields = text.split("\t")
    if len(fields) < 3 or not fields[0].isdigit() or not fields[1].isdigit():
        return 0, 0
    return int(fields[0]), int(fields[1])


# -------------------------------------------------------------------- documents


def normalize_line(line: str) -> str:
    """A document line without indentation, blockquote markers and repeated spaces."""
    line = line.strip()
    while line.startswith(">"):
        line = line[1:].strip()
    return re.sub(r"\s+", " ", line)


def node_prefix(node: str) -> str:
    """The repository-relative directory prefix of a node name (`.` is the root)."""
    name = node.strip().strip("/\\").replace("\\", "/")
    return "" if name in ("", ".") else name + "/"


def parse_hunks(diff_text: str) -> Tuple[List[str], List[str]]:
    """Removed and added lines of a one-file `--unified=0` diff.

    Only lines after the first hunk header count: a removed line that itself begins with
    two dashes prints as `---...` and must not be mistaken for the file header.
    """
    removed: List[str] = []
    added: List[str] = []
    in_hunk = False
    for line in diff_text.split("\n"):
        if line.startswith("@@"):
            in_hunk = True
        elif in_hunk and line.startswith("-"):
            removed.append(line[1:])
        elif in_hunk and line.startswith("+"):
            added.append(line[1:])
    return removed, added


def diff_lines(ctx: Context, path: str) -> Tuple[List[str], List[str]]:
    """Removed and added lines of `path` between the merge base and the branch."""
    text = git_text(ctx.repo, "diff", "--unified=0", "--no-color", "--no-ext-diff", "--no-renames",
                    ctx.base, "refs/heads/" + ctx.options.branch, "--", path)
    return parse_hunks(text)


def show_file(ctx: Context, revision: str, path: str) -> str:
    """The text of `path` at a revision, or the empty string when it is not there."""
    result = git(ctx.repo, "show", "{}:{}".format(revision, path), check=False)
    return result.stdout if result.returncode == 0 else ""


def file_exists(ctx: Context, revision: str, path: str) -> bool:
    """Whether `path` exists at a revision."""
    return git(ctx.repo, "cat-file", "-e", "{}:{}".format(revision, path), check=False).returncode == 0


def paragraphs(text: str) -> List[List[str]]:
    """Blank-line separated blocks of lines."""
    blocks: List[List[str]] = []
    block: List[str] = []
    for line in text.split("\n"):
        if line.strip():
            block.append(line)
        elif block:
            blocks.append(block)
            block = []
    if block:
        blocks.append(block)
    return blocks


def pointer_lines(text: str) -> Set[str]:
    """Normalized lines of every paragraph that carries a pointer."""
    found: Set[str] = set()
    for block in paragraphs(text):
        if any(marker in line for line in block for marker in POINTER_MARKERS):
            found |= {normalize_line(line) for line in block}
    return found


def defined_anchors(history_text: str) -> Set[str]:
    """Explicit `<a id>` anchors and heading slugs of a HISTORY.md text."""
    slugs = {re.sub(r"[^a-z0-9 -]", "", heading.lower()).replace(" ", "-") for heading in HEADING.findall(history_text)}
    return set(EXPLICIT_ANCHOR.findall(history_text)) | slugs


def ancestor_prefixes(prefix: str) -> List[str]:
    """`a/b/` gives `a/b/`, `a/`, and the root's empty prefix."""
    parts = [part for part in prefix.split("/") if part]
    return ["/".join(parts[:k]) + "/" for k in range(len(parts), 0, -1)] + [""]


@dataclass
class NodeFindings:
    """What the moved-text rules found in one node."""

    removed: int
    lost: List[str]
    stray: List[str]
    unresolved: List[str]


def node_findings(ctx: Context, prefix: str, gained: Set[str], moved_down: Set[str]) -> NodeFindings:
    """The moved-text rules of `history_guard.py` over one node's BOOT.md."""
    boot = prefix + "BOOT.md"
    removed, added = diff_lines(ctx, boot)
    lost = [line for line in removed if line.strip() and normalize_line(line) not in gained]
    new_text = show_file(ctx, "refs/heads/" + ctx.options.branch, boot)
    allowed = pointer_lines(new_text) | moved_down
    stray: List[str] = []
    if file_exists(ctx, ctx.base, boot):
        stray = [line for line in added
                 if line.strip() and not line.lstrip().startswith("#") and normalize_line(line) not in allowed]
    history = "".join(show_file(ctx, "refs/heads/" + ctx.options.branch, up + "HISTORY.md")
                      for up in ancestor_prefixes(prefix))
    cited_in = new_text + show_file(ctx, "refs/heads/" + ctx.options.branch, prefix + "ACCEPTANCE.md")
    known = defined_anchors(history)
    unresolved = sorted(anchor for anchor in set(BARE_CITATION.findall(cited_in)) if anchor not in known)
    return NodeFindings(len([line for line in removed if line.strip()]), lost, stray, unresolved)


def check_history_append_only(ctx: Context) -> List[str]:
    """A line removed from any HISTORY.md of the tree, named node or not, is a failure."""
    problems: List[str] = []
    for path in ctx.changed:
        if path.split("/")[-1] != "HISTORY.md":
            continue
        removed, _ = diff_lines(ctx, path)
        problems += ["{}: removed line: {}".format(path, line.strip()[:120]) for line in removed if line.strip()]
    return problems


def check_documents(ctx: Context) -> str:
    """Check 4: the moved-text rules over the named nodes, plus HISTORY.md append-only."""
    if not ctx.options.doc_nodes:
        return "documents: skipped (no --doc-nodes)"
    prefixes: List[str] = []
    for node in ctx.options.doc_nodes:
        if node_prefix(node) not in prefixes:
            prefixes.append(node_prefix(node))
    for prefix in prefixes:
        if not file_exists(ctx, "refs/heads/" + ctx.options.branch, prefix + "BOOT.md"):
            raise GuardFailure("documents: node {} has no BOOT.md: an empty walk".format(prefix.rstrip("/") or "."))
    gained: Set[str] = set()
    moved_down: Set[str] = set()
    for prefix in prefixes:
        for name in DOCUMENT_NAMES:
            removed, added = diff_lines(ctx, prefix + name)
            gained |= {normalize_line(line) for line in added if line.strip()}
            if name == "BOOT.md":
                moved_down |= {normalize_line(line) for line in removed if line.strip()}
    results = [node_findings(ctx, prefix, gained, moved_down) for prefix in prefixes]
    return summarize_documents(prefixes, results, check_history_append_only(ctx))


def summarize_documents(prefixes: Sequence[str], results: Sequence[NodeFindings], rewritten: Sequence[str]) -> str:
    """Raise the one failure the node findings add up to, or return the green line."""
    details: List[str] = []
    for prefix, found in zip(prefixes, results):
        name = prefix.rstrip("/") or "."
        details += ["{}: lost line: {}".format(name, line.strip()[:120]) for line in found.lost]
        details += ["{}: line added outside a pointer paragraph: {}".format(name, line.strip()[:120]) for line in found.stray]
        details += ["{}: unresolved anchor HISTORY.md#{}".format(name, anchor) for anchor in found.unresolved]
    details += list(rewritten)
    counts = [sum(len(f.lost) for f in results), sum(len(f.stray) for f in results),
              sum(len(f.unresolved) for f in results), len(rewritten)]
    if details:
        raise GuardFailure("documents: {} lost, {} stray, {} unresolved, {} HISTORY.md lines removed".format(*counts),
                           capped(details))
    return "documents: {} nodes, {} lines moved, 0 lost, 0 stray, 0 unresolved, HISTORY.md append-only".format(
        len(prefixes), sum(f.removed for f in results))


def check_overlap(ctx: Context) -> str:
    """Check 5: a file changed on both sides since the merge base must match --overlap."""
    target_changed = set(git_names(ctx.repo, "diff", "--name-only", "-z", "--no-renames", ctx.base, ctx.target_head))
    both = sorted(path for path in ctx.changed if path in target_changed)
    refused = [path for path in both if not matches_any(ctx.options.overlap, path)]
    if refused:
        raise GuardFailure("overlap: {} files changed on both sides".format(len(refused)),
                           capped(["changed on both sides: " + path for path in refused]))
    return "overlap: none" if not both else "overlap: {} files, all allowed".format(len(both))


# ------------------------------------------------------------------ trial worktree


def make_writable(function, path: str, _excinfo) -> None:  # type: ignore[no-untyped-def]
    """`shutil.rmtree` error handler: git marks object files read-only on Windows."""
    os.chmod(path, stat.S_IWRITE)
    function(path)


class TrialWorktree:
    """The guard's own detached worktree in the system temp directory."""

    def __init__(self, repo: str) -> None:
        self.repo = repo
        self.parent: Optional[str] = None
        self.path = ""

    def create(self, target_head: str) -> None:
        """Add the detached worktree at the target's head."""
        self.parent = tempfile.mkdtemp(prefix="merge-guard-trial-")
        self.path = os.path.join(self.parent, "trial")
        git(self.repo, "worktree", "add", "--detach", self.path, target_head)

    def merge(self, branch: str) -> str:
        """`merge --no-ff --no-commit` of the branch; the tree it produced, or a conflict failure."""
        result = git(self.path, "merge", "--no-ff", "--no-commit", "refs/heads/" + branch, check=False)
        if result.returncode != 0:
            conflicts = git_names(self.path, "diff", "--name-only", "-z", "--diff-filter=U")
            what = "trial merge: conflict in {} files".format(len(conflicts)) if conflicts else \
                "trial merge failed: " + (result.stderr.strip() or result.stdout.strip())
            raise GuardFailure(what, ["conflict: " + name for name in conflicts])
        return git_text(self.path, "write-tree")

    def remove(self) -> None:
        """Remove the worktree and its parent directory; safe to call twice."""
        if self.parent is None:
            return
        if os.path.isdir(self.path):
            # --force is for this worktree only, the one the guard created (BOOT.md, Invariants):
            # the trial merge leaves it dirty and git refuses to remove a dirty worktree otherwise.
            git(self.repo, "worktree", "remove", "--force", self.path, check=False)
        if os.path.isdir(self.path):
            shutil.rmtree(self.path, onerror=make_writable)
        git(self.repo, "worktree", "prune", check=False)
        shutil.rmtree(self.parent, ignore_errors=True)
        self.parent = None


# ----------------------------------------------------------------------- commands


def default_commands() -> List[Command]:
    """The command table of API.md; `python` is the interpreter running the guard."""
    python = [sys.executable, "-X", "utf8"]
    no_warnings = "0 Warning(s)"
    return [
        Command("lint", python + ["tools/protocol-lint/protocol_lint.py", ".", "--exclude", "templates", "--strict"],
                {}, None, None),
        Command("build", ["dotnet", "build", "APThermo.sln"], {}, None, no_warnings),
        Command("fast suite", ["dotnet", "test", "APThermo.sln", "--no-build", "--filter", "Category!=LongRunning"],
                {"APTHERMO_NO_CUDA": "1"}, None, None),
        Command("cuda build", ["dotnet", "build", "APThermo.sln", "-c", "Release"], {}, "cuda", no_warnings),
        Command("cuda proofs", ["dotnet", "test", "APThermo.sln", "-c", "Release", "--no-build",
                                "--filter", "Category=Cuda|Category=BitSnapshot"], {}, "cuda", None),
        Command("execution", ["dotnet", "test", "tests/Execution.Tests", "-c", "Release", "--no-build"], {}, "cuda", None),
    ]


def load_commands(path: str) -> List[Command]:
    """The `--commands` JSON file: an array of {name, argv, env, requires, green_when_output_contains}."""
    try:
        with open(path, encoding="utf-8") as handle:
            entries = json.load(handle)
    except (OSError, ValueError) as error:
        raise InvocationError("--commands {}: {}".format(path, error))
    if not isinstance(entries, list) or not entries:
        raise InvocationError("--commands {}: expected a non-empty JSON array".format(path))
    return [parse_command(entry, path) for entry in entries]


def parse_command(entry: object, path: str) -> Command:
    """One validated entry of the `--commands` file."""
    if not isinstance(entry, dict):
        raise InvocationError("--commands {}: every entry must be an object".format(path))
    argv, env = entry.get("argv"), entry.get("env") or {}
    needle, requires = entry.get("green_when_output_contains"), entry.get("requires")
    valid = (isinstance(entry.get("name"), str) and isinstance(argv, list) and argv
             and all(isinstance(item, str) for item in argv) and isinstance(env, dict)
             and requires in ("cuda", None) and (needle is None or isinstance(needle, str)))
    if not valid:
        raise InvocationError("--commands {}: malformed entry {!r}".format(path, entry.get("name")))
    return Command(entry["name"], argv, {str(k): str(v) for k, v in env.items()}, requires, needle)


def summarize_output(output: str) -> str:
    """One line for a command: the test totals, the build's counts, or the last line."""
    totals = TEST_SUMMARY.findall(output)
    if totals:
        return "{} assemblies, {} passed, {} failed".format(
            len(totals), sum(int(passed) for _, passed in totals), sum(int(failed) for failed, _ in totals))
    warnings, errors = WARNING_COUNT.findall(output), ERROR_COUNT.findall(output)
    if warnings and errors:
        return "{} Warning(s), {} Error(s)".format(warnings[-1], errors[-1])
    lines = [line.strip() for line in output.splitlines() if line.strip()]
    return lines[-1] if lines else "(no output)"


def output_is_green(command: Command, returncode: int, output: str) -> bool:
    """Exit 0 and, when the entry asks for it, its marker in the output.

    The marker does not count when a digit precedes it: `10 Warning(s)` holds the text
    `0 Warning(s)` and is not a clean build.
    """
    if returncode != 0:
        return False
    needle = command.green_when_output_contains
    return needle is None or re.search(r"(?<!\d)" + re.escape(needle), output) is not None


def run_command(command: Command, cwd: str, log: str) -> Tuple[bool, str]:
    """Run one command in the trial worktree, append its full output to the log."""
    environment = dict(os.environ)
    environment.update(command.env)
    try:
        result = subprocess.run(command.argv, cwd=cwd, env=environment, capture_output=True,
                                text=True, encoding="utf-8", errors="replace")
        returncode, output = result.returncode, result.stdout + result.stderr
    except OSError as error:
        returncode, output = 127, "cannot run {}: {}".format(command.argv[0], error)
    with open(log, "a", encoding="utf-8", newline="\n") as handle:
        handle.write("=== {}: {} (exit {})\n{}\n".format(command.name, " ".join(command.argv), returncode, output))
    return output_is_green(command, returncode, output), summarize_output(output)


# ----------------------------------------------------------------------- GPU lock


def utc_now() -> datetime.datetime:
    """The current time, timezone-aware."""
    return datetime.datetime.now(datetime.timezone.utc)


def parse_time(text: str) -> datetime.datetime:
    """An ISO-8601 time; one without an offset is read as UTC."""
    moment = datetime.datetime.fromisoformat(text.replace("Z", "+00:00"))
    return moment if moment.tzinfo else moment.replace(tzinfo=datetime.timezone.utc)


class GpuLock:
    """`gpu.lock` in the system temp directory: {"owner": ..., "until": ISO-8601} (BOOT.md, GPU)."""

    def __init__(self, owner: str) -> None:
        self.path = os.path.join(tempfile.gettempdir(), LOCK_NAME)
        self.owner = owner
        self.held = False

    def acquire(self) -> None:
        """Take the lock, replacing an expired one; fail when another owner holds a live one."""
        until = (utc_now() + datetime.timedelta(hours=LOCK_HOURS)).replace(microsecond=0).isoformat()
        payload = json.dumps({"owner": self.owner, "until": until})
        for _ in range(3):
            try:
                descriptor = os.open(self.path, os.O_CREAT | os.O_EXCL | os.O_WRONLY)
            except FileExistsError:
                self.clear_if_stale()
                continue
            with os.fdopen(descriptor, "w", encoding="utf-8") as handle:
                handle.write(payload)
            self.held = True
            return
        raise GuardFailure("GPU lock {} could not be taken".format(self.path))

    def clear_if_stale(self) -> None:
        """Delete the lock file when it is expired; fail loudly when it is live or unreadable."""
        try:
            with open(self.path, encoding="utf-8") as handle:
                data = json.load(handle)
            owner, until = str(data["owner"]), str(data["until"])
            live = parse_time(until) > utc_now()
        except FileNotFoundError:
            return
        except (OSError, ValueError, KeyError, TypeError) as error:
            raise GuardFailure("GPU lock {} is unreadable ({}): delete it by hand".format(self.path, error))
        if live and owner != self.owner:
            raise GuardFailure("GPU busy: {} until {}".format(owner, until))
        try:
            os.remove(self.path)
        except FileNotFoundError:
            pass

    def release(self) -> None:
        """Remove the lock if it is still this guard's own."""
        if not self.held:
            return
        self.held = False
        try:
            with open(self.path, encoding="utf-8") as handle:
                mine = json.load(handle).get("owner") == self.owner
            if mine:
                os.remove(self.path)
        except (OSError, ValueError, AttributeError):
            pass


# ------------------------------------------------------------------- final merge


def merge_into_checkout(ctx: Context, trial_tree: str, message_file: str) -> str:
    """Check 8: merge in the main checkout, prove the tree equals the trial's, commit.

    Every failure after the merge started aborts it; the checkout is never left in a merge state.
    """
    repo = ctx.repo
    try:
        result = git(repo, "merge", "--no-ff", "--no-commit", "refs/heads/" + ctx.options.branch, check=False)
        if result.returncode != 0:
            raise GuardFailure("merge into {} failed: {}".format(ctx.target, (result.stderr or result.stdout).strip()))
        tree = git_text(repo, "write-tree")
        if tree != trial_tree:
            raise GuardFailure("merged tree {} differs from the trial tree {}: merge aborted".format(tree[:7], trial_tree[:7]))
        git(repo, "commit", "-F", message_file)
        return "merged: {}, tree equal to the trial tree".format(git_text(repo, "rev-parse", "--short", "HEAD"))
    finally:
        if merge_in_progress(repo):
            git(repo, "merge", "--abort", check=False)


# ----------------------------------------------------------------------- driver


class Reporter:
    """The guard's output: `ok` lines as the checks pass, one `GUARD:` line on the first failure."""

    @staticmethod
    def ok(line: str) -> None:
        """A green check."""
        print("ok    " + line, flush=True)

    @staticmethod
    def failure(failure: GuardFailure) -> None:
        """The detail lines, then the one `GUARD:` line."""
        for detail in failure.details:
            print("      " + detail, flush=True)
        print("GUARD: " + failure.what, flush=True)


def run_trial(ctx: Context, trial: TrialWorktree, lock: GpuLock, log: str) -> str:
    """Checks 6 and 7: the trial merge and the commands; the trial tree."""
    trial.create(ctx.target_head)
    tree = trial.merge(ctx.options.branch)
    Reporter.ok("trial merge: no conflict (worktree removed on exit)")
    ran = 0
    for command in ctx.options.commands or default_commands():
        if command.requires == "cuda":
            if not ctx.options.cuda:
                continue
            if not lock.held:
                lock.acquire()
        green, summary = run_command(command, trial.path, log)
        if not green:
            raise GuardFailure("{} failed: {} (log: {})".format(command.name, summary, log))
        Reporter.ok("{}: {}".format(command.name, summary))
        ran += 1
    if ran == 0:
        raise GuardFailure("no command ran: an empty walk")
    return tree


def run_guard(options: Options, repo: str) -> int:
    """All checks in the order of API.md; the exit code."""
    lock = GpuLock("merge-guard pid {} ({})".format(os.getpid(), options.branch))
    trial = TrialWorktree(repo)
    stamp = utc_now().strftime("%Y%m%d-%H%M%S")
    log = os.path.join(tempfile.gettempdir(), "merge-guard-{}.log".format(stamp))
    try:
        ctx, line = check_preconditions(repo, options)
        Reporter.ok(line)
        for check in (check_scope, check_approved_records, check_documents, check_overlap):
            Reporter.ok(check(ctx))
        if options.checks_only:
            print("merge-guard: green (static checks only)", flush=True)
            return 0
        tree = run_trial(ctx, trial, lock, log)
        if options.merge_message:
            Reporter.ok(merge_into_checkout(ctx, tree, options.merge_message))
        print("merge-guard: green (log: {})".format(log), flush=True)
        return 0
    except GuardFailure as failure:
        Reporter.failure(failure)
        return 1
    finally:
        trial.remove()
        lock.release()


def compile_patterns(parser: argparse.ArgumentParser, patterns: Optional[Sequence[str]]) -> List[Pattern[str]]:
    """Compile --scope-like patterns; an invalid regular expression is an invocation error."""
    compiled = []
    for text in patterns or []:
        try:
            compiled.append(re.compile(text))
        except re.error as error:
            parser.error("invalid regular expression {!r}: {}".format(text, error))
    return compiled


def parse_arguments(argv: Optional[Sequence[str]]) -> Options:
    """The command line of API.md; argparse exits with code 2 on an invocation error."""
    parser = argparse.ArgumentParser(prog="merge_guard.py", description=__doc__.split("\n")[0])
    parser.add_argument("branch", help="the coder's branch, merged into the current branch")
    for flag in ("--scope", "--approve", "--overlap"):
        parser.add_argument(flag, action="extend", nargs="+", metavar="REGEX")
    parser.add_argument("--doc-nodes", action="extend", nargs="+", metavar="NODE", default=[])
    parser.add_argument("--checks-only", action="store_true")
    parser.add_argument("--cuda", action="store_true")
    parser.add_argument("--merge", metavar="MESSAGE_FILE")
    parser.add_argument("--commands", metavar="JSON_FILE")
    args = parser.parse_args(argv)
    if not args.scope:
        parser.error("at least one --scope is required: the scope is the orchestrator's statement about one task")
    if args.checks_only and (args.merge or args.cuda):
        parser.error("--checks-only runs no trial: it cannot be combined with --merge or --cuda")
    message = os.path.abspath(args.merge) if args.merge else None
    if message and not os.path.isfile(message):
        parser.error("--merge: {} is not a file".format(args.merge))
    try:
        commands = load_commands(os.path.abspath(args.commands)) if args.commands else None
    except InvocationError as error:
        parser.error(str(error))
    return Options(args.branch, compile_patterns(parser, args.scope), compile_patterns(parser, args.approve),
                   compile_patterns(parser, args.overlap), args.doc_nodes, args.checks_only, args.cuda,
                   message, commands)


def main(argv: Optional[Sequence[str]] = None) -> int:
    """Entry point: the repository is the current directory, which is never changed."""
    return run_guard(parse_arguments(argv), os.getcwd())


if __name__ == "__main__":
    sys.exit(main())
