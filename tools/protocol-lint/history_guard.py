"""Guard for moving document text without loss (AGENTS.md §15, a node split or condensed).

Usage: python history_guard.py <repo> <base-commit> <branch> <node> [<node> ...]
       python history_guard.py --criteria-only <repo> <target> <merged-tree>

The second form checks affected acceptance documents across the merged tree. It permits
code changes, completing a criterion with dated evidence and documented §6 revisions.

<base-commit> is the merge base of the branch with the integration branch
(`git merge-base <integration> <branch>`), never the moving head of the integration
branch: a diff against a moving head pulls other nodes' changes in as "out of scope".
<node> is a node path from the repository root (`.` for the root); pass every node of the
task - the parent and each child a rule moved into.

Rules, for every node passed:
  - lost: text removed from its BOOT.md or ACCEPTANCE.md reappears verbatim among the lines
    gained by the BOOT.md, HISTORY.md or ACCEPTANCE.md of any node passed (leading spaces,
    blockquote markers and repeated whitespace ignored), so a rule moved down into the child
    that owns it is found. The external file's title and the pointer to it are structural
    when that file is folded back into BOOT.md;
  - live criteria: each checkbox and its associated evidence must remain together, as
    many times as before, in a current acceptance section or a linked ACCEPTANCE.md
    among the named nodes. Archiving a former measurement marked with ⚠ is supported;
  - stray: every line added to a BOOT.md that existed at base belongs to a paragraph that
    carries a pointer ("HISTORY.md#", "-> [ACCEPTANCE.md](ACCEPTANCE.md)" or a link to a
    "/BOOT.md"), is a heading, or came verbatim from another BOOT.md of the task; a BOOT.md
    that did not exist at base (a new child) is not checked for stray lines;
  - append-only: no non-blank line is removed from its HISTORY.md;
  - anchors: every bare `HISTORY.md#<anchor>` cited in its BOOT.md or ACCEPTANCE.md is
    defined (an `<a id="...">` or a heading slug) in the HISTORY.md of the node, an
    ancestor or the root; qualified citations (`../HISTORY.md#x`, `a/b/HISTORY.md#x`) are
    left to the protocol linter;
  - out of scope: any changed file other than the BOOT.md, HISTORY.md and ACCEPTANCE.md of
    the nodes passed. Code files moved by a node split land here by design: a human reads
    them.

The guard answers one question: was anything lost. Known weaknesses:
  - ordinary prose lines are compared as a set, so a short common line ("- None.", "---") "reappears"
    anywhere;
  - a pointer paragraph may be reworded freely - only a human checks that a condensed
    wording kept every condition;
  - a rule added on purpose during a move (e.g. one restored from the criteria) is reported
    as stray, exactly as expected: a human reads it, or it goes in a separate commit;
  - append-only is checked for the nodes passed only; a removal from another node's
    HISTORY.md shows up merely as "out of scope".

Exit code 0 when nothing fails; otherwise every failure is printed as "GUARD: ...".
Tests: test_history_guard.py beside this file (standard library and git only).
"""
import re
import subprocess
import sys
from collections import defaultdict
from functools import lru_cache
from typing import NamedTuple


def git(repo, *args):
    return subprocess.run(["git", "-C", repo, *args], capture_output=True, text=True, encoding="utf-8", check=True).stdout


def norm(line):
    line = line.strip()
    while line.startswith(">"):
        line = line[1:].strip()
    return re.sub(r"\s+", " ", line)


@lru_cache(maxsize=None)
def diff_lines(repo, base, branch, path):
    out = git(repo, "--literal-pathspecs", "diff", "--no-renames", "--unified=0", "--no-color",
              f"{base}..{branch}", "--", path)
    removed, added = [], []
    in_hunk = False  # file headers ("--- a/x", "+++ b/x") come only before the first hunk;
    for line in out.splitlines():  # a removed line "---" or "-- x" inside a hunk is content
        if line.startswith("@@"):
            in_hunk = True
            continue
        if not in_hunk:
            continue
        if line.startswith("-"):
            removed.append(line[1:])
        elif line.startswith("+"):
            added.append(line[1:])
    return removed, added


@lru_cache(maxsize=None)
def blob(repo, branch, path):
    try:
        return git(repo, "show", f"{branch}:{path}")
    except subprocess.CalledProcessError:
        if git(repo, "--literal-pathspecs", "ls-tree", "-z", branch, "--", path):
            raise
        return None


def show(repo, branch, path):
    return blob(repo, branch, path) or ""


def paragraphs(text):
    block = []
    for line in text.splitlines():
        if line.strip():
            block.append(line)
        elif block:
            yield block
            block = []
    if block:
        yield block


POINTER = re.compile(r"^→\s*\[ACCEPTANCE\.md\]\(ACCEPTANCE\.md\)$")
HEADING = re.compile(r"^ {0,3}(#{1,6})\s+(.+?)\s*#*\s*$")
CHECKBOX = re.compile(r"^\s*(?:[-*+]|[0-9]{1,9}[.)])\s+\[([ xX])\](?:\s+|$)")
DATE = re.compile(r"\b\d{4}-\d{2}-\d{2}\b")
EVIDENCE = re.compile(DATE.pattern + r"\s*[,;:—–-]\s*(.+)")
DATE_PREFIX = re.compile("^" + DATE.pattern + r"\s*[:—–-]\s*")
PLACEHOLDER = re.compile(r"(?:placeholder|todo|tbd|n[/.]a|to be (?:defined|determined)|<[^<>]+>|\.{3}|…)[.!]?",
                         re.IGNORECASE)
NAMED_REFERENCE = re.compile(r'`[^`]+`|\[[^\[\]]+\]\([^()\s]+\)|"[^"]+"')
REFERENCE_ID = re.compile(r"[\w./:#@+\\-]+")
FENCE = re.compile(r"^ {0,3}(`{3,}|~{3,})(.*)$")
COMMENT_OR_CODE = re.compile(r"(`+)(?!`).*?\1(?!`)|<!--")


def visible_text(line, comment):
    """Drop HTML comments while preserving inline code that illustrates their syntax."""
    visible = []
    while line:
        if comment:
            _, end, line = line.partition("-->")
            comment = not end
        else:
            match = COMMENT_OR_CODE.search(line)
            if not match:
                visible.append(line)
                break
            comment = match[0] == "<!--"
            visible.append(line[:match.start() if comment else match.end()])
            line = line[match.end():]
    return "".join(visible), comment


def live_lines(text):
    """Classify document lines: fenced examples supply evidence but cannot declare a criterion."""
    fence = ""
    comment = False
    for line in text.lstrip("\ufeff").splitlines():
        marker = FENCE.match(line)
        if fence:
            if marker and marker[1].startswith(fence) and not marker[2].strip(" \t"):
                fence = ""
            yield line, False
        elif marker and not comment and (marker[1][0] == "~" or "`" not in marker[2]):
            fence = marker[1]
            yield line, False
        else:
            # Block comments in the shipped templates can contain sample checkboxes.
            line, comment = visible_text(line, comment)
            yield line, True


def acceptance_section(boot, physical=False):
    inside = False
    for raw, (line, markup) in zip(boot.lstrip("\ufeff").splitlines(), live_lines(boot)):
        heading = HEADING.match(line) if markup else None
        if heading and len(heading[1]) <= 2:
            inside = heading[1] == "##" and heading[2] == "Acceptance criteria"
        elif inside:
            if physical:
                yield raw, markup and raw.strip() == line.strip()
            else:
                yield line, markup


class Criterion(NamedTuple):
    path: str
    text: str
    note: str


def criteria(path, lines):
    """Keep each item's wrapped evidence and its adjacent correction note distinct."""
    item, notes = [], []
    blank = False
    indent = 0
    for line, markup in lines:
        stripped = line.strip()
        if not stripped:
            blank = True
            continue
        level = len(line) - len(line.lstrip())
        checkbox = markup and CHECKBOX.match(line)
        next_block = level <= indent and (blank or re.match(r"(?:[-*+]\s|>)", stripped))
        if item and markup and stripped.startswith("⚠"):
            notes.append(stripped)
            blank = False
            continue
        if checkbox or next_block or (markup and HEADING.match(line)):
            if item:
                yield Criterion(path, " ".join(item), " ".join(notes))
            item, notes = [], []
        if checkbox:
            indent = level
        if checkbox or item:
            (notes if notes else item).append(re.sub(r"\s+", " ", stripped))
        blank = False
    if item:
        yield Criterion(path, " ".join(item), " ".join(notes))


def current_criteria(repo, revision, prefixes):
    entries, errors = [], []
    for prefix in prefixes:
        boot = prefix + "BOOT.md"
        path = prefix + "ACCEPTANCE.md"
        document = show(repo, revision, boot)
        lines = [(line.strip(), markup) for line, markup in acceptance_section(document, physical=True) if line.strip()]
        has_link = any(markup and POINTER.fullmatch(line) for line, markup in lines)
        pointer = has_link and len(lines) == 1
        external = blob(repo, revision, path)
        if (external is not None and not pointer) or (external is None and has_link):
            errors.append(f"{boot}: {path} requires both a file and the sole canonical acceptance pointer")
        entries.extend(criteria(boot, acceptance_section(document)))
        # Include obligations recorded in an already malformed base; fixing its pointer
        # must not silently discard them. A malformed candidate itself always fails.
        if external is not None:
            entries.extend(criteria(path, live_lines(external)))
    return entries, errors


def citation_body(text):
    """Return the content of one complete citation, with no prose outside its boundaries."""
    text = text.strip()
    if text.endswith("."):
        text = text[:-1].rstrip()
    if re.fullmatch(r"\[[^\[\]]+\]\([^()\s]+\)", text):
        return text
    if not text:
        return ""
    if text[0] == "`":
        return text[1:-1] if re.fullmatch(r"`[^`]+`", text) else ""
    opening = text[0]
    closing = {"(": ")", "[": "]"}.get(opening)
    if not closing:
        return ""
    depth = 0
    for index, char in enumerate(text):
        if char == opening:
            depth += 1
        elif char == closing:
            depth -= 1
            if depth == 0:
                return text[1:index] if index == len(text) - 1 else ""
    return ""


def citation_sources(text):
    """A semicolon starts a separate source or measurement in the same citation."""
    if not re.search(r"[^\W\d_]", text):
        return False
    # Protect semicolons inside named references. Keep free-form measurement
    # descriptions with quoted source names or recorded numeric observations.
    masked = NAMED_REFERENCE.sub("\x00", text)
    if ";" not in masked:
        return True
    return all(REFERENCE_ID.fullmatch(part.strip()) or "\x00" in part or re.search(r"\b\d", part)
               for part in masked.split(";"))


def dated_citation(text):
    body = citation_body(text)
    match = EVIDENCE.fullmatch(body or text.rstrip("."))
    if not match or not citation_sources(match[1]):
        return False
    # Unwrapped evidence has no closing delimiter. Accept a reference, not an
    # unbounded sentence that could qualify the original requirement.
    return bool(body or citation_body(match[1]) or REFERENCE_ID.fullmatch(match[1]))


def recorded_citation(text):
    """Does the original item already end in a dated citation or a date-first reference?"""
    for date in DATE.finditer(text):
        start = date.start()
        if start and text[start - 1] in "([`":
            start -= 1
        if dated_citation(text[start:]):
            return True
    return bool(DATE_PREFIX.match(text) and any(
        citation_sources(citation_body(text[m.start():])) for m in re.finditer(r"(?<=\s)[(\[`]", text)))


def completed_or_reverified(old, new):
    old_box, new_box = CHECKBOX.match(old.text), CHECKBOX.match(new.text)
    if new_box[1].lower() != "x":
        return False
    previous = old.text[old_box.end():]
    current = new.text[new_box.end():]
    if not previous:
        return False
    if previous == current:
        return recorded_citation(current)
    # Some established trees date the item before its claim and cite the test
    # afterwards, e.g. "2026-09-17: <original wording> (`tests/Random.Tests`)."
    dated = DATE_PREFIX.match(current)
    if dated and current[dated.end():].startswith(previous + " "):
        citation = current[dated.end() + len(previous):].strip()
        return citation_sources(citation_body(citation))
    # Keep the full original wording and any old proof intact. Accept dated proof
    # before or after it. Ordinary prose appended to weaken a requirement is not proof.
    if current.startswith(previous + " "):
        evidence = current[len(previous):].strip()
    elif current.endswith(" " + previous):
        evidence = current[:-len(previous)].strip()
    else:
        return False
    return dated_citation(evidence)


def documented_revision(old, new):
    if old.path.rpartition("/")[0] != new.path.rpartition("/")[0]:
        return False
    successor = new.text[CHECKBOX.match(new.text).end():].strip()
    # The BOOT templates use angle brackets for instructions awaiting replacement.
    # A dated note or citation attached to a template cannot supply its missing claim.
    claim = DATE_PREFIX.sub("", successor, count=1).strip("*_` ")
    if not claim or dated_citation(claim):
        return False
    marker = PLACEHOLDER.match(claim)
    if marker:
        rest = claim[marker.end():].strip()
        if not rest or dated_citation(rest) or citation_body(rest):
            return False
    if not new.note or new.note == old.note or not DATE.search(new.note):
        return False
    # An adjacent dated §6/§8 note identifies the old wording and gives a reason.
    # Checked entries can separate their previous dated proof from the quoted wording.
    prior = old.text[CHECKBOX.match(old.text).end():]
    wording = re.split(r"\s+\(\d{4}-\d{2}-\d{2}\b", prior, maxsplit=1)[0]
    for quoted in (prior, wording):
        if quoted and quoted in new.note:
            explanation = DATE.sub("", new.note.replace(quoted, "", 1))
            if re.search(r"[^\W\d_]", explanation):
                return True
    return False


def match_entries(before, after, matches):
    """Consume each destination only once. Return unmatched items in the old snapshot."""
    missing = []
    for old in before:
        for i, new in enumerate(after):
            if matches(old, new):
                after.pop(i)
                break
        else:
            missing.append(old)
    return missing


def check_criteria(repo, base, branch, prefixes, allow_updates):
    before, _ = current_criteria(repo, base, prefixes)
    after, errors = current_criteria(repo, branch, prefixes)
    # Match local occurrences first, then transfers. Existing copies at other nodes
    # cannot cover a deletion even when their text and evidence happen to be identical.
    missing = match_entries(before, after, lambda a, b: a.path == b.path and a.text == b.text)
    missing = match_entries(missing, after, lambda a, b: a.text == b.text)
    if allow_updates:
        missing = match_entries(missing, after, completed_or_reverified)
        missing = match_entries(missing, after, documented_revision)
    return missing, errors


def main():
    arguments = sys.argv[1:]
    if arguments[:1] == ["--criteria-only"]:
        repo, base, branch = arguments[1:]
        paths = git(repo, "diff", "--no-renames", "--name-only", "-z", base, branch, "--",
                    ":(glob)**/BOOT.md", ":(glob)**/ACCEPTANCE.md")
        prefixes = sorted({p[:p.rfind("/") + 1] for p in paths.split("\0") if p})
        missing, failures = check_criteria(repo, base, branch, prefixes, allow_updates=True)
        failures.extend(f"{item.path}: lost active criterion or evidence: {item.text}" for item in missing)
        for failure in failures:
            print("GUARD:", failure)
        return int(bool(failures))

    repo, base, branch, *nodes = arguments
    nodes = [n.strip("/").replace("\\", "/") for n in nodes]
    prefix = lambda n: "" if n in (".", "") else n + "/"
    allowed = {prefix(n) + f for n in nodes for f in ("BOOT.md", "HISTORY.md", "ACCEPTANCE.md")}
    changed = [p for p in git(repo, "diff", "--no-renames", "--name-only", "-z", f"{base}..{branch}").split("\0") if p]
    failures = [f"out of scope: {p}" for p in changed if p not in allowed]
    missing_items = defaultdict(list)
    missing, pointer_errors = check_criteria(repo, base, branch, [prefix(n) for n in nodes], allow_updates=False)
    failures.extend(pointer_errors)
    for item in missing:
        missing_items[item.path].append(item.text)

    # A line removed from one node's BOOT.md may reappear in any allowed node's documents: a rule moved down
    # into the child that owns it lands in that child's BOOT.md.
    gained = set()
    for n in nodes:
        for other in ("HISTORY.md", "ACCEPTANCE.md", "BOOT.md"):
            _, more = diff_lines(repo, base, branch, prefix(n) + other)
            gained |= {norm(l) for l in more if l.strip()}
    removed_anywhere = set()
    removed_from_acceptance = set()
    for n in nodes:
        gone, _ = diff_lines(repo, base, branch, prefix(n) + "BOOT.md")
        removed_anywhere |= {norm(l) for l in gone if l.strip()}
        gone, _ = diff_lines(repo, base, branch, prefix(n) + "ACCEPTANCE.md")
        removed_from_acceptance.update(norm(line) for line in gone if line.strip())

    for node in nodes:
        boot = prefix(node) + "BOOT.md"
        acceptance = prefix(node) + "ACCEPTANCE.md"
        removed, added = diff_lines(repo, base, branch, boot)
        new_boot = show(repo, branch, boot)
        external = show(repo, branch, acceptance)
        lost = [l for l in removed if l.strip() and norm(l) not in gained
                and not (not external and POINTER.fullmatch(l.strip()))]
        detached_removed, _ = diff_lines(repo, base, branch, acceptance)
        # The file's own H1 and canonical section heading retire with its wrapper.
        retired_headers = set()
        if not external:
            first = show(repo, base, acceptance).lstrip().splitlines()[:1]
            retired_headers.update(line for line in first if line.startswith("# "))
            retired_headers.add("## Acceptance criteria")
        external_lost = [line for line in detached_removed
                         if line.strip() and line not in retired_headers and norm(line) not in gained]
        pointer_lines = set()
        for block in paragraphs(new_boot):
            if any("HISTORY.md#" in l or "ACCEPTANCE.md](ACCEPTANCE.md)" in l or "/BOOT.md" in l for l in block):
                pointer_lines |= {norm(l) for l in block}
        # Lines a rule moved down from the parent brings into this BOOT.md, verbatim, are not stray.
        pointer_lines |= removed_anywhere
        pointer_lines.update(norm(line) for line, _ in acceptance_section(new_boot)
                             if norm(line) in removed_from_acceptance)
        existed = bool(show(repo, base, boot).strip())
        stray = [] if not existed else [l for l in added if l.strip() and norm(l) not in pointer_lines and not l.lstrip().startswith("#")
                 and norm(l) not in {norm(r) for r in removed}]
        history = ""
        parts = node.split("/")
        for k in range(len(parts), 0, -1):
            if parts[:k] != ["."]:
                history += show(repo, branch, "/".join(parts[:k]) + "/HISTORY.md")
        history += show(repo, branch, "HISTORY.md")
        defined = set(re.findall(r'<a id="([^"]+)"></a>', history)) | {
            re.sub(r"[^a-z0-9 -]", "", h.lower()).replace(" ", "-") for h in re.findall(r"^#+ (.+)$", history, re.M)}
        cited = set(re.findall(r"(?<![/\w.])HISTORY\.md#([A-Za-z0-9_-]+)", new_boot + show(repo, branch, prefix(node) + "ACCEPTANCE.md")))
        missing = sorted(a for a in cited if a not in defined and a != "anchor")
        count = sum(1 for l in new_boot.splitlines() if l.strip())
        print(f"{node}: BOOT.md {count} non-blank lines; lost lines {len(lost) + len(external_lost)}, "
              f"lost active criteria {len(missing_items[boot]) + len(missing_items[acceptance])}, "
              f"stray additions {len(stray)}, unresolved anchors {len(missing)}")
        for path in (boot, acceptance):
            failures.extend(f"{path}: lost active criterion or evidence: {entry}" for entry in missing_items[path])
        failures += [f"{node}: lost line: {l.strip()[:120]}" for l in lost]
        failures += [f"{acceptance}: lost line: {l.strip()[:120]}" for l in external_lost]
        failures += [f"{node}: line added outside a pointer paragraph: {l.strip()[:120]}" for l in stray]
        failures += [f"{node}: unresolved anchor HISTORY.md#{a}" for a in missing]
        history_removed, _ = diff_lines(repo, base, branch, prefix(node) + "HISTORY.md")
        failures += [f"{node}: line removed from append-only HISTORY.md: {l.strip()[:120]}"
                     for l in history_removed if l.strip()]

    for f in failures:
        print("GUARD:", f)
    return int(bool(failures))


if __name__ == "__main__":
    sys.exit(main())
