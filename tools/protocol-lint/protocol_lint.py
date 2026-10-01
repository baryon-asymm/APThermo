#!/usr/bin/env python3
"""protocol_lint - the half of the protocol's machine checks that needs no compiler.

It checks the tree of documents against AGENTS.md, never the code against the
documents: that every node carries its pair of documents, that AGENTS.md sits only
at the root, that every BOOT.md has the six canonical sections, that dependencies
are declared in the canonical form, that every relative link resolves, that an
API.md carrying code carries a status mark, that a checked acceptance criterion
carries a date, that - textually, as a cheap stand-in for the reflection check -
what a document declares under a tick is at least mentioned by the node's source,
that every BOOT.md is within its AGENTS.md 15 line limit or declares the deviation
(a node whose own specification replaces some of the six sections with a
transcription is measured without those sections' lines), and that every
"-> HISTORY.md#anchor" pointer resolves to an anchor that actually exists.

What it cannot check, and no tooling can: whether a document says the right thing
about the code it correctly names.

Python 3.8+, standard library only, no configuration file:

    python protocol_lint.py <tree-root> [--ext .sh,.ps1] [--exclude vendor] [--strict]

Exit code 0 when there are no errors, 1 when there are (with --strict, warnings
count as errors too), 2 on a usage error.
"""

from __future__ import annotations

import argparse
import os
import re
import sys
from dataclasses import dataclass
from pathlib import Path
from typing import Dict, List, Optional, Sequence, Set, Tuple
from urllib.parse import unquote

BOOT_NAME = "BOOT.md"
API_NAME = "API.md"
AGENTS_NAME = "AGENTS.md"
HISTORY_NAME = "HISTORY.md"

# Canonical and compared letter by letter: the body of a document is written in
# whatever language the project speaks, the headings are not translated.
CANONICAL_SECTIONS: Tuple[str, ...] = (
    "Purpose",
    "Invariants",
    "Dependencies",
    "Constraints",
    "Acceptance criteria",
    "Taboos",
)

DEFAULT_SOURCE_EXTENSIONS: Set[str] = {
    ".c", ".cc", ".clj", ".cpp", ".cs", ".cu", ".cuh", ".cxx", ".dart", ".erl", ".ex",
    ".exs", ".f", ".f90", ".f95", ".for", ".fs", ".go", ".h", ".hh", ".hpp", ".hs",
    ".java", ".jl", ".js", ".jsx", ".kt", ".kts", ".lua", ".m", ".ml", ".mm", ".nim",
    ".php", ".py", ".pyi", ".r", ".rb", ".rs", ".scala", ".sol", ".svelte", ".swift",
    ".ts", ".tsx", ".vb", ".vue", ".zig",
}

# A directory holding one of these is a module boundary even when its own code sits
# in subdirectories, so it is a node.
DEFAULT_MANIFEST_NAMES: Set[str] = {
    "build.gradle", "build.gradle.kts", "Cargo.toml", "CMakeLists.txt", "go.mod",
    "package.json", "pom.xml", "pyproject.toml", "setup.py",
}
DEFAULT_MANIFEST_SUFFIXES: Tuple[str, ...] = (".csproj", ".fsproj", ".vbproj", ".vcxproj")

# A directory whose name starts with a dot is a tool's cache or state and is skipped, with
# one exception: these are committed configuration, and the tree reads them as part of
# itself, nodes included.
DOT_DIRECTORIES_READ: Set[str] = {".github"}

DEFAULT_EXCLUDED_DIRS: Set[str] = {
    "artifacts", "bin", "build", "coverage", "dist", "env", "node_modules", "obj",
    "out", "packages", "target", "TestResults", "vendor", "venv", "__pycache__",
}

# Fences that hold something other than declarations. A block with no language is
# treated as prose on purpose: guessing wrong there costs a false warning on every
# document that shows a shell transcript.
NON_DECLARATION_FENCES: Set[str] = {
    "", "bash", "bat", "cmd", "console", "csv", "diff", "html", "ini", "json", "jsonc",
    "log", "markdown", "md", "mermaid", "plain", "powershell", "ps1", "pwsh", "sh",
    "shell", "text", "toml", "txt", "xml", "yaml", "yml", "zsh",
}

DECLARATION = re.compile(
    r"\b(?:record\s+(?:struct|class)|class|struct|interface|enum|record|trait|protocol"
    r"|type|def|fn|func|function|object|module)\s+([A-Za-z_]\w*)"
)

# Words the pattern above can capture when a language spells a declaration with two
# keywords ("enum class Colour", "public sealed class X"): a keyword is never a name.
NOT_A_NAME: Set[str] = {
    "abstract", "async", "class", "const", "default", "def", "enum", "export", "extends",
    "false", "final", "fn", "func", "function", "implements", "in", "interface",
    "internal", "let", "module", "new", "null", "object", "out", "override", "partial",
    "private", "protected", "protocol", "public", "readonly", "record", "ref", "return",
    "sealed", "self", "static", "struct", "this", "trait", "true", "type", "val", "var",
    "virtual", "where",
}

DATE = re.compile(r"\b(?:19|20)\d{2}-\d{2}-\d{2}\b")
LINK = re.compile(r"!?\[[^\]\n]*\]\(\s*<?([^)\s>]+)>?[^)]*\)")
HEADING = re.compile(r"^ {0,3}(#{1,6})\s+(.*?)\s*#*\s*$")
FENCE = re.compile(r"^ {0,3}(`{3,}|~{3,})\s*([^\s`]*)")
CHECKED_ITEM = re.compile(r"^(\s*)[-*+]\s+\[[xX]\]\s*(.*)$")
SCHEME = re.compile(r"^[A-Za-z][A-Za-z0-9+.\-]*:")

TICK = "✅"       # done: the block under it is compared with the code
HOURGLASS = "⏳"  # planned: the block under it is a sketch

# AGENTS.md 15: the size of a document.
PARENT_LINE_LIMIT = 250    # a node with descendant nodes
LEAF_LINE_LIMIT = 400      # a node with none
DEVIATION_PREFIX = "⚠ Declared deviation, §15:"

# AGENTS.md 15: a node whose §6 deviation names the sections a transcription
# replaces is measured without those sections' lines.
SIX_DEVIATION_MARK = "⚠ Declared deviation, §6:"
REPLACED_BY = re.compile(r"replaced by:\s*(.+?)\.?\s*$")

# AGENTS.md 3.2 (§2, §6, §15): any node may hand its acceptance criteria to a file of
# their own, read only in its own node, and the root's own limit rises to the leaf's
# once it does.
ACCEPTANCE_NAME = "ACCEPTANCE.md"
ACCEPTANCE_LIMIT = 400
ROOT_LIMIT_WITH_ACCEPTANCE = 400
ACCEPTANCE_POINTER = re.compile(r"^→\s*\[ACCEPTANCE\.md\]\(ACCEPTANCE\.md\)\s*$")

# AGENTS.md 15: every HISTORY.md#anchor cited anywhere outside a HISTORY.md, whether
# bare (the citing node's own file, or an ancestor's) or qualified by a node's path
# (that node's own file). An anchor never starts with '<', so the one placeholder
# syntax the kit itself writes, `HISTORY.md#<anchor>`, never matches this pattern -
# there is no separate rule for it (AGENTS.md 13).
CITATION = re.compile(r"(?P<prefix>[\w.\-/]*?)HISTORY\.md#(?P<anchor>[A-Za-z0-9][A-Za-z0-9_\-]*)")

# AGENTS.md 15: a pointer left by a moved correction or table, and the anchor it
# must resolve to inside the node's own HISTORY.md.
HISTORY_ANCHOR = re.compile(r'<a\s+id="([^"]+)"')


@dataclass(frozen=True)
class Finding:
    """One complaint, addressed at a place and at an article of the protocol."""

    level: str      # ERROR | WARN
    article: str    # the article of AGENTS.md the check comes from, or "-"
    where: str      # path relative to the tree root, optionally with :line
    message: str

    def __str__(self) -> str:
        return "{:<5} {:<5} {}: {}".format(self.level, self.article, self.where, self.message)


def error(article: str, where: str, message: str) -> Finding:
    return Finding("ERROR", article, where, message)


def warn(article: str, where: str, message: str) -> Finding:
    return Finding("WARN", article, where, message)


# --------------------------------------------------------------------------- text


def read(path: Path) -> str:
    """The file as text, BOM and line endings normalised away."""
    return path.read_text(encoding="utf-8-sig", errors="replace").replace("\r\n", "\n")


def mask_inline_code(line: str) -> str:
    """The line with every backtick span blanked out, its length preserved.

    The examples in AGENTS.md are written as inline code precisely so that the link
    check does not try to follow them; blanking them here is what makes that work.
    """
    out: List[str] = []
    index, length = 0, len(line)
    while index < length:
        if line[index] != "`":
            out.append(line[index])
            index += 1
            continue

        end_of_ticks = index
        while end_of_ticks < length and line[end_of_ticks] == "`":
            end_of_ticks += 1
        ticks = line[index:end_of_ticks]

        closing = line.find(ticks, end_of_ticks)
        if closing < 0:
            out.append(ticks)
            index = end_of_ticks
            continue

        out.append(" " * (closing + len(ticks) - index))
        index = closing + len(ticks)

    return "".join(out)


def mask_code(text: str) -> List[str]:
    """The document's lines with code - fenced and inline - replaced by blanks.

    Line numbers survive, so a finding can still name the line it came from.
    """
    masked: List[str] = []
    fence: Optional[str] = None

    for line in text.split("\n"):
        opening = FENCE.match(line)
        if fence is None:
            if opening:
                fence = opening.group(1)
                masked.append("")
                continue
            masked.append(mask_inline_code(line))
            continue

        # Inside a fence: only a marker of the same character and at least the same
        # length closes it, which is how a fence can quote another fence.
        if opening and opening.group(1)[0] == fence[0] and len(opening.group(1)) >= len(fence):
            fence = None
        masked.append("")

    return masked


def fenced_blocks(text: str) -> List[Tuple[str, int, List[str]]]:
    """(language, index of the opening line, body lines) for every fenced block."""
    blocks: List[Tuple[str, int, List[str]]] = []
    fence: Optional[str] = None
    language = ""
    start = 0
    body: List[str] = []

    for index, line in enumerate(text.split("\n")):
        opening = FENCE.match(line)
        if fence is None:
            if opening:
                fence, language, start, body = opening.group(1), opening.group(2).lower(), index, []
            continue

        if opening and opening.group(1)[0] == fence[0] and len(opening.group(1)) >= len(fence):
            blocks.append((language, start, body))
            fence = None
            continue

        body.append(line)

    if fence is not None:           # a document that forgot to close its last fence
        blocks.append((language, start, body))
    return blocks


def section_lines(masked: List[str], name: str) -> Optional[List[Tuple[int, str]]]:
    """The lines of the '## name' section, numbered from 0, or None if there is none."""
    inside = False
    collected: List[Tuple[int, str]] = []

    for index, line in enumerate(masked):
        heading = HEADING.match(line)
        if heading:
            level, title = len(heading.group(1)), heading.group(2)
            if inside and level <= 2:
                break
            inside = level == 2 and title == name
            continue
        if inside:
            collected.append((index, line))

    return collected if inside or collected else None


def second_level_headings(masked: List[str]) -> List[str]:
    return [m.group(2) for m in (HEADING.match(line) for line in masked) if m and len(m.group(1)) == 2]


def section_ranges(masked: List[str]) -> Dict[str, Tuple[int, int]]:
    """Level-2 heading name -> (start, end) line indices, 0-based, end exclusive.

    `start` is the heading line itself; `end` is the index of the next heading at
    level 1 or 2, or the end of the document. Used only to slice out the sections a
    §6 deviation names as replaced by a transcription (AGENTS.md 15).
    """
    ranges: Dict[str, Tuple[int, int]] = {}
    name: Optional[str] = None
    start = 0
    for index, line in enumerate(masked):
        heading = HEADING.match(line)
        if heading and len(heading.group(1)) <= 2:
            if name is not None:
                ranges[name] = (start, index)
            name = heading.group(2) if len(heading.group(1)) == 2 else None
            start = index
    if name is not None:
        ranges[name] = (start, len(masked))
    return ranges


# ---------------------------------------------------------------------- the tree


class Tree:
    """The directories under a root, split into nodes and everything else."""

    def __init__(
        self,
        root: Path,
        source_extensions: Set[str],
        excluded_dirs: Set[str],
    ) -> None:
        self.root = root
        self.source_extensions = source_extensions
        self.excluded_dirs = excluded_dirs
        self.directories: List[Path] = []
        self.nodes: List[Path] = []
        self.markdown: List[Path] = []
        self._sources: Dict[Path, List[Path]] = {}
        self._text: Dict[Path, str] = {}
        self._scan()

    def _excluded(self, name: str) -> bool:
        if name in DOT_DIRECTORIES_READ:
            return name in self.excluded_dirs
        return name.startswith(".") or name in self.excluded_dirs or name.endswith(".egg-info")

    def _scan(self) -> None:
        for dirpath, dirnames, filenames in os.walk(self.root):
            dirnames[:] = sorted(name for name in dirnames if not self._excluded(name))
            directory = Path(dirpath)
            self.directories.append(directory)

            sources = [directory / name for name in filenames
                       if Path(name).suffix.lower() in self.source_extensions]
            self._sources[directory] = sources
            self.markdown.extend(directory / name for name in filenames if name.endswith(".md"))

            has_manifest = any(
                name in DEFAULT_MANIFEST_NAMES or name.endswith(DEFAULT_MANIFEST_SUFFIXES)
                for name in filenames
            )
            has_document = BOOT_NAME in filenames or API_NAME in filenames

            # A directory with neither code nor a manifest nor a document is pure
            # grouping (src/, tests/): the tree reads straight through it.
            if directory == self.root or sources or has_manifest or has_document:
                self.nodes.append(directory)

    def relative(self, path: Path) -> str:
        try:
            return path.relative_to(self.root).as_posix() or "."
        except ValueError:
            return path.as_posix()

    def text(self, path: Path) -> str:
        if path not in self._text:
            self._text[path] = read(path)
        return self._text[path]

    def sources_under(self, node: Path) -> List[Path]:
        """Every source file in the node's subtree, its child nodes included."""
        found: List[Path] = []
        for directory, sources in self._sources.items():
            if directory == node or node in directory.parents:
                found.extend(sources)
        return found

    def has_descendant_nodes(self, node: Path) -> bool:
        """Whether some other node of the tree sits under this one (AGENTS.md 1: a
        child node is any descendant, recursively, not only a direct child)."""
        return any(other != node and node in other.parents for other in self.nodes)

    def all_sources(self) -> List[Path]:
        """Every source file the tree scanned, in every node."""
        return [path for sources in self._sources.values() for path in sources]

    def owning_node(self, path: Path) -> Path:
        """The node whose directory is the closest ancestor of `path` (or `path`
        itself, for a node's own BOOT.md/API.md); the root when none is closer."""
        directory = path.parent
        candidates = [node for node in self.nodes if node == directory or node in directory.parents]
        return max(candidates, key=lambda node: len(str(node))) if candidates else self.root

    def ancestor_nodes(self, node: Path) -> List[Path]:
        """`node` and every node above it up to the root, nearest first - the chain a
        bare HISTORY.md#anchor citation is allowed to resolve against (AGENTS.md 15)."""
        node_set = set(self.nodes)
        chain: List[Path] = []
        directory = node
        while True:
            if directory in node_set:
                chain.append(directory)
            if directory == self.root:
                break
            directory = directory.parent
        return chain

    def history_anchors(self, node: Path) -> Optional[Set[str]]:
        """The anchors `node`'s own HISTORY.md defines, or None if it has none."""
        history = node / HISTORY_NAME
        if not history.is_file():
            return None
        return set(HISTORY_ANCHOR.findall(self.text(history)))


# ------------------------------------------------------------------------ checks


def check_pairs(tree: Tree) -> List[Finding]:
    """AGENTS.md 1: a node without its pair of documents is a level nobody described."""
    findings: List[Finding] = []
    for node in tree.nodes:
        for document in (BOOT_NAME, API_NAME):
            if not (node / document).is_file():
                findings.append(error(
                    "1", tree.relative(node / document),
                    "a source directory is a node and a node carries both documents; this one is missing",
                ))
    return findings


def check_agents(tree: Tree) -> List[Finding]:
    """AGENTS.md 2: the protocol exists once, at the root."""
    findings: List[Finding] = []
    if not (tree.root / AGENTS_NAME).is_file():
        findings.append(error("2", AGENTS_NAME, "the tree root carries no AGENTS.md, so it is not a tree root"))
    for directory in tree.directories:
        if directory != tree.root and (directory / AGENTS_NAME).is_file():
            findings.append(error(
                "2", tree.relative(directory / AGENTS_NAME),
                "AGENTS.md belongs only at the root; a second one makes a second protocol",
            ))
    return findings


def check_sections(tree: Tree, boot: Path, masked: List[str]) -> List[Finding]:
    """AGENTS.md 6: the six sections are the condition for leaving design mode."""
    headings = second_level_headings(masked)
    findings: List[Finding] = []
    for name in CANONICAL_SECTIONS:
        if name not in headings:
            findings.append(error(
                "6", tree.relative(boot),
                "no '## {}' section (the six headings are canonical and not translated)".format(name),
            ))
    duplicated = sorted({name for name in headings if name in CANONICAL_SECTIONS and headings.count(name) > 1})
    for name in duplicated:
        findings.append(warn("6", tree.relative(boot), "'## {}' appears more than once".format(name)))
    return findings


def check_dependencies(tree: Tree, node: Path, boot: Path, masked: List[str]) -> List[Finding]:
    """AGENTS.md 6: the section a machine reads to compare with the real edges."""
    lines = section_lines(masked, "Dependencies")
    if lines is None:
        return []                      # already reported by check_sections

    findings: List[Finding] = []
    declares_none = any(line.strip().lstrip("-*+ ").strip().rstrip(".") == "None" for _, line in lines)
    links = [(number, target) for number, line in lines for target in LINK.findall(line)]
    api_links = [(number, target) for number, target in links if target.split("#")[0].endswith(API_NAME)]

    if not api_links and not declares_none:
        findings.append(error(
            "6", "{}:{}".format(tree.relative(boot), lines[0][0] + 1 if lines else 1),
            "declares nothing in canonical form: one link to each neighbour's API.md, "
            "or the single word None; prose alone reads as 'no dependencies declared'",
        ))
    if api_links and declares_none:
        findings.append(error(
            "6", tree.relative(boot),
            "says None and lists neighbours at the same time; one of the two is false",
        ))

    for number, target in api_links:
        resolved = (boot.parent / unquote(target.split("#")[0])).resolve()
        neighbour = resolved.parent
        if neighbour == node:
            findings.append(warn(
                "6", "{}:{}".format(tree.relative(boot), number + 1),
                "declares a dependency on itself",
            ))
        elif node in neighbour.parents or neighbour.parent == node:
            findings.append(warn(
                "6", "{}:{}".format(tree.relative(boot), number + 1),
                "declares a dependency on its own descendant {}; a node owns what is under it, "
                "the section is about neighbours".format(tree.relative(neighbour)),
            ))
    return findings


def check_links(tree: Tree, document: Path, masked: List[str]) -> List[Finding]:
    """A link between documents that resolves to nothing is a reference to nothing."""
    findings: List[Finding] = []
    for number, line in enumerate(masked):
        for target in LINK.findall(line):
            if target.startswith("#") or SCHEME.match(target):
                continue

            path = unquote(target.split("#")[0].split("?")[0])
            if not path:
                continue
            if path.startswith("/"):
                findings.append(warn(
                    "-", "{}:{}".format(tree.relative(document), number + 1),
                    "absolute link {}; between documents of one tree the link is relative".format(target),
                ))
                continue

            resolved = (document.parent / path).resolve()
            if not resolved.exists():
                findings.append(error(
                    "-", "{}:{}".format(tree.relative(document), number + 1),
                    "the link to {} resolves to nothing".format(target),
                ))
    return findings


def check_section_exemption(tree: Tree, boot: Path, text: str, masked: List[str]) -> Tuple[int, List[Finding]]:
    """AGENTS.md 15: a §6 deviation that replaces sections with a transcription is
    not paying to read those sections twice.

    A line carrying `SIX_DEVIATION_MARK` and a `replaced by:` clause names, on that
    same line, the level-2 sections the external source replaces. Each named section
    that actually exists in the document (checked here, not trusted) has its
    non-blank lines excluded from the AGENTS.md 15 count; a named section that does
    not exist is an error, and none of its (nonexistent) lines are excluded.
    """
    lines = text.split("\n")
    findings: List[Finding] = []
    excluded = 0
    ranges: Optional[Dict[str, Tuple[int, int]]] = None

    for number, line in enumerate(lines):
        if SIX_DEVIATION_MARK not in line:
            continue
        match = REPLACED_BY.search(line)
        if not match:
            continue

        if ranges is None:
            ranges = section_ranges(masked)

        for token in match.group(1).split(","):
            name = re.sub(r"^#{1,6}\s*", "", token.strip().strip("`\"'").strip()).strip()
            if not name:
                continue
            bounds = ranges.get(name)
            if bounds is None:
                findings.append(error(
                    "15", "{}:{}".format(tree.relative(boot), number + 1),
                    "names '## {}' as replaced by the transcription, and no such section "
                    "exists in this document".format(name),
                ))
                continue
            start, end = bounds
            excluded += sum(1 for excluded_line in lines[start:end] if excluded_line.strip())

    return excluded, findings


def check_boot_size(
    tree: Tree, node: Path, boot: Path, text: str, excluded: int = 0, root_pointer: bool = False,
) -> List[Finding]:
    """AGENTS.md 15: a BOOT.md this big has stopped being a cheap read.

    Counted are non-blank lines of the file as written, code blocks and table rows
    included: the limit is arithmetic on how often the document is read, not a
    judgement about its prose. A node with a descendant node (AGENTS.md 1: "child
    nodes" are every descendant, recursively) is read on the way to every one of them
    and gets the tighter, 250-line budget; a leaf gets 400. `excluded` is the number
    of non-blank lines a declared §6 deviation's transcription sections account for
    (`check_section_exemption`) and is subtracted before comparing against the limit.
    `root_pointer` is AGENTS.md 3.1's own exemption: the root, and only the root, once
    its own `## Acceptance criteria` is the one line pointing to `ACCEPTANCE.md`, is
    measured against the leaf's 400 lines instead of the parent's 250 - it is the one
    common ancestor the frame binds, but it can still hand over its evidence. A node
    that declares the deviation - a line starting with `DEVIATION_PREFIX` in its own
    BOOT.md - is reported as a warning instead of an error, and named as declared.
    """
    non_blank = sum(1 for line in text.split("\n") if line.strip()) - excluded
    is_root = node == tree.root
    if is_root and root_pointer:
        limit = ROOT_LIMIT_WITH_ACCEPTANCE
        kind = "the root, its acceptance criteria in ACCEPTANCE.md"
    else:
        limit = PARENT_LINE_LIMIT if tree.has_descendant_nodes(node) else LEAF_LINE_LIMIT
        kind = "a node with children" if limit == PARENT_LINE_LIMIT else "a leaf node"
    if non_blank <= limit:
        return []

    message = "{} non-blank lines, over the {}-line limit for {}".format(non_blank, limit, kind)

    deviation = declared_deviation(text)
    if deviation is not None:
        return [warn(
            "15", tree.relative(boot),
            "{} - declared deviation: {}".format(message, deviation),
        )]
    return [error(
        "15", tree.relative(boot),
        "{}; move what is no longer current truth to HISTORY.md, oldest superseded "
        "material first, or declare the deviation (AGENTS.md §12)".format(message),
    )]


def acceptance_pointer_only(lines: Optional[List[Tuple[int, str]]]) -> bool:
    """Whether a `## Acceptance criteria` section (as `section_lines` returns it) holds
    nothing but the one line AGENTS.md 3.1 (§6) prescribes for a node that keeps its
    criteria in `ACCEPTANCE.md`."""
    if not lines:
        return False
    non_blank = [line for _, line in lines if line.strip()]
    return len(non_blank) == 1 and bool(ACCEPTANCE_POINTER.match(non_blank[0].strip()))


def check_acceptance_file(
    tree: Tree, node: Path, boot: Path, has_pointer: bool, acceptance_exists: bool,
) -> List[Finding]:
    """AGENTS.md 3.2 (§6, §15): `ACCEPTANCE.md` stands in any node, leaf or not, and
    only where the BOOT.md it stands beside points to it with the one line the article
    prescribes - the pointer without the file, and the file without the pointer (an
    orphan), are the two errors."""
    findings: List[Finding] = []
    if has_pointer and not acceptance_exists:
        findings.append(error(
            "6", tree.relative(boot),
            "'## Acceptance criteria' points to {}, which does not exist".format(ACCEPTANCE_NAME),
        ))
    if acceptance_exists and not has_pointer:
        findings.append(error(
            "6", tree.relative(node / ACCEPTANCE_NAME),
            "exists, but {}'s '## Acceptance criteria' does not point to it "
            "(AGENTS.md 3.2) - an orphan file, or a stale one".format(BOOT_NAME),
        ))
    return findings


def declared_deviation(text: str) -> Optional[str]:
    """The first line of `text` that starts with `DEVIATION_PREFIX`, stripped, if any."""
    return next(
        (line.strip() for line in text.split("\n") if line.strip().startswith(DEVIATION_PREFIX)),
        None,
    )


def check_acceptance_size(
    tree: Tree, acceptance: Path, text: str, boot_text: str,
) -> List[Finding]:
    """AGENTS.md 3.2 (§15): `ACCEPTANCE.md` is current truth, not history, and is held
    to 400 lines. The deviation §15 names for a `BOOT.md` is granted to it as well: a
    line starting with `DEVIATION_PREFIX` in the node's own BOOT.md (`boot_text`)
    turns the error into a warning that quotes it."""
    non_blank = sum(1 for line in text.split("\n") if line.strip())
    if non_blank <= ACCEPTANCE_LIMIT:
        return []
    message = "{} non-blank lines, over the {}-line limit for {}".format(
        non_blank, ACCEPTANCE_LIMIT, ACCEPTANCE_NAME)
    deviation = declared_deviation(boot_text)
    if deviation is not None:
        return [warn(
            "15", tree.relative(acceptance),
            "{} - declared deviation: {}".format(message, deviation),
        )]
    return [error(
        "15", tree.relative(acceptance),
        "{}; move what is no longer current truth to HISTORY.md, or declare the "
        "deviation in the node's BOOT.md (AGENTS.md §12)".format(message),
    )]


def mask_fences(text: str) -> List[str]:
    """The document's lines with fenced blocks - and only fenced blocks - blanked out.

    Unlike `mask_code`, an inline backtick span is left as written. AGENTS.md 15's
    citation check (`check_history_citations`) reads a citation inside backticks as a
    real reference, not an example the way AGENTS.md's own illustrative links are: the
    kit's own placeholder syntax for an example, `HISTORY.md#<anchor>`, already fails
    to match `CITATION` on its own (an anchor never starts with '<'), so no further
    exemption is needed for it, backticked or not.
    """
    masked: List[str] = []
    fence: Optional[str] = None
    for line in text.split("\n"):
        opening = FENCE.match(line)
        if fence is None:
            if opening:
                fence = opening.group(1)
                masked.append("")
                continue
            masked.append(line)
            continue
        if opening and opening.group(1)[0] == fence[0] and len(opening.group(1)) >= len(fence):
            fence = None
        masked.append("")
    return masked


def check_history_citations(tree: Tree, path: Path, lines: List[str]) -> List[Finding]:
    """AGENTS.md 15: every `HISTORY.md#anchor` citation resolves, wherever it is
    written and however it is written - the gap `protocol_lint` itself had (AGENTS.md
    3.1, Appendix C): the old check read only a BOOT.md's own bare pointers, and only
    outside backticks, so a citation inside backticks, in an API.md, in a test's
    source, or naming a neighbour's node by path, could dangle and never turn red.

    A **qualified** citation names a node's path before `HISTORY.md#anchor`, either
    from the tree root (`tests/Harness/HISTORY.md#anchor`) or, with a leading `../`,
    relative to the citing file's own directory; it must resolve in that named node's
    own HISTORY.md. A **bare** citation (`HISTORY.md#anchor`, no path) must resolve in
    the citing file's own node or one of that node's ancestors (AGENTS.md 15's "Frame
    and evidence": an ancestor's HISTORY.md is inherited on the way up, a neighbour's
    is not) - never in a neighbour or a descendant, which is exactly the mistake this
    check exists to catch (a bare citation of another node's anchor is silently wrong
    until someone deletes the anchor it happened to still find).
    """
    owner = tree.owning_node(path)
    findings: List[Finding] = []
    for number, line in enumerate(lines):
        for match in CITATION.finditer(line):
            prefix, anchor = match.group("prefix"), match.group("anchor")
            if prefix == "":
                chain = tree.ancestor_nodes(owner)
                if any(anchor in (tree.history_anchors(candidate) or ()) for candidate in chain):
                    continue
                where = tree.relative(owner / HISTORY_NAME)
            else:
                if ".." in prefix.split("/") or prefix.startswith("./"):
                    target = (path.parent / prefix).resolve()
                else:
                    target = (tree.root / prefix.rstrip("/")).resolve()
                anchors = tree.history_anchors(target)
                if anchors is not None and anchor in anchors:
                    continue
                where = "{}/{}".format(tree.relative(target), HISTORY_NAME)
            findings.append(error(
                "15", "{}:{}".format(tree.relative(path), number + 1),
                "cites HISTORY.md#{}, which no anchor in {} defines".format(anchor, where),
            ))
    return findings


def check_status_marks(tree: Tree, api: Path, text: str, masked: List[str]) -> List[Finding]:
    """AGENTS.md 7: a contract with code in it says whether the code exists."""
    if not fenced_blocks(text):
        return []
    if any(TICK in line or HOURGLASS in line for line in masked):
        return []
    return [warn(
        "7", tree.relative(api),
        "carries code blocks and no status mark; a document without a mark is read as "
        "entirely implemented, which is a promise nobody made on purpose",
    )]


def check_acceptance_dates(
    tree: Tree, boot: Path, masked: List[str], whole_file: bool = False,
) -> List[Finding]:
    """AGENTS.md 6: a tick carries the day its evidence was obtained.

    A BOOT.md is read in its `## Acceptance criteria` section; an ACCEPTANCE.md
    (`whole_file`, AGENTS.md 3.2) holds criteria throughout, so the whole file is read
    whether or not it carries the section heading."""
    lines = list(enumerate(masked)) if whole_file else section_lines(masked, "Acceptance criteria")
    if not lines:
        return []

    findings: List[Finding] = []
    index = 0
    while index < len(lines):
        number, line = lines[index]
        item = CHECKED_ITEM.match(line)
        if not item:
            index += 1
            continue

        indent = len(item.group(1))
        text = [item.group(2)]
        index += 1
        # The item runs on while the lines are blank or indented deeper than its bullet.
        while index < len(lines):
            _, following = lines[index]
            if following.strip() and len(following) - len(following.lstrip()) <= indent:
                break
            text.append(following)
            index += 1

        if not DATE.search(" ".join(text)):
            findings.append(warn(
                "6", "{}:{}".format(tree.relative(boot), number + 1),
                "a checked criterion with no date: a tick carries the day the evidence was "
                "obtained and where the evidence lives",
            ))
    return findings


def check_implemented_declarations(tree: Tree, node: Path, api: Path, text: str) -> List[Finding]:
    """AGENTS.md 7, textually: what is declared under a tick is at least mentioned by the code.

    The real check needs reflection and is written per stack. This one only reads
    files, so it cannot see that a member is missing from a type that exists - but it
    does catch the failure that matters while a tree is young: a tick put on a node
    before the node was written.
    """
    marks: Dict[int, str] = {}
    for number, line in enumerate(mask_code(text)):
        if HOURGLASS in line:
            marks[number] = HOURGLASS
        elif TICK in line:
            marks[number] = TICK

    def implemented(block_start: int) -> bool:
        preceding = [number for number in marks if number < block_start]
        return marks[max(preceding)] == TICK if preceding else True

    declared: Dict[str, int] = {}
    for language, start, body in fenced_blocks(text):
        if language in NON_DECLARATION_FENCES or not implemented(start):
            continue
        for line in body:
            stripped = re.sub(r"//.*$", "", line)
            for name in DECLARATION.findall(stripped):
                if name not in NOT_A_NAME:
                    declared.setdefault(name, start + 1)

    if not declared:
        return []

    sources = tree.sources_under(node)
    corpus = "\n".join(tree.text(path) for path in sources)
    findings: List[Finding] = []
    for name, line in sorted(declared.items()):
        if not re.search(r"\b{}\b".format(re.escape(name)), corpus):
            findings.append(warn(
                "7", "{}:{}".format(tree.relative(api), line),
                "declares {} under a tick, and no source file of this node mentions it - "
                "either the mark is ahead of the code, or the code is elsewhere".format(name),
            ))
    return findings


# ------------------------------------------------------------------------- entry


def lint(
    root: Path,
    extra_extensions: Sequence[str] = (),
    extra_excluded: Sequence[str] = (),
    heuristics: bool = True,
) -> List[Finding]:
    """Every complaint about the tree at root, ordered by place.

    The AGENTS.md 15 line-count check and the HISTORY.md citation check both run
    unconditionally: a project adopts the protocol's size limit by bringing its nodes
    inside it or by declaring the deviation, not by a flag that lets the checker
    itself stay silent about a known backlog (`tools/protocol-lint/BOOT.md`,
    "Constraints").
    """
    tree = Tree(
        root=root.resolve(),
        source_extensions=DEFAULT_SOURCE_EXTENSIONS | {e.lower() for e in extra_extensions},
        excluded_dirs=DEFAULT_EXCLUDED_DIRS | set(extra_excluded),
    )

    findings: List[Finding] = []
    findings += check_pairs(tree)
    findings += check_agents(tree)

    for node in tree.nodes:
        boot = node / BOOT_NAME
        has_pointer = False
        if boot.is_file():
            text = tree.text(boot)
            masked = mask_code(text)
            findings += check_sections(tree, boot, masked)
            findings += check_dependencies(tree, node, boot, masked)
            findings += check_acceptance_dates(tree, boot, masked)
            has_pointer = acceptance_pointer_only(section_lines(masked, "Acceptance criteria"))
            excluded, exemption_findings = check_section_exemption(tree, boot, text, masked)
            findings += exemption_findings
            findings += check_boot_size(tree, node, boot, text, excluded, has_pointer)

        acceptance = node / ACCEPTANCE_NAME
        acceptance_exists = acceptance.is_file()
        findings += check_acceptance_file(tree, node, boot, has_pointer, acceptance_exists)
        if acceptance_exists:
            acceptance_text = tree.text(acceptance)
            acceptance_masked = mask_code(acceptance_text)
            findings += check_acceptance_dates(tree, acceptance, acceptance_masked, whole_file=True)
            boot_text = tree.text(boot) if boot.is_file() else ""
            findings += check_acceptance_size(tree, acceptance, acceptance_text, boot_text)

        api = node / API_NAME
        if api.is_file():
            text = tree.text(api)
            findings += check_status_marks(tree, api, text, mask_code(text))
            if heuristics:
                findings += check_implemented_declarations(tree, node, api, text)

    for document in tree.markdown:
        text = tree.text(document)
        findings += check_links(tree, document, mask_code(text))
        if document.name != HISTORY_NAME:
            findings += check_history_citations(tree, document, mask_fences(text))

    for source in tree.all_sources():
        relative = tree.relative(source)
        if relative.startswith("src/") or relative.startswith("tests/"):
            findings += check_history_citations(tree, source, tree.text(source).split("\n"))

    return sorted(findings, key=lambda finding: (finding.where, finding.article, finding.level, finding.message))


def main(argv: Optional[Sequence[str]] = None) -> int:
    parser = argparse.ArgumentParser(
        prog="protocol_lint",
        description="Check a BOOT.md/API.md tree against AGENTS.md. The documents are checked "
                    "against the protocol, not the code against the documents.",
    )
    parser.add_argument("root", help="the root of the tree - the directory holding AGENTS.md")
    parser.add_argument("--ext", default="", help="extra source extensions, comma separated (.sh,.ps1)")
    parser.add_argument("--exclude", default="", help="extra directory names to skip, comma separated")
    parser.add_argument("--strict", action="store_true", help="warnings fail too")
    parser.add_argument("--no-heuristics", action="store_true",
                        help="skip the textual check of declarations under a tick")
    parser.add_argument("--list-nodes", action="store_true", help="print the nodes found and stop")
    arguments = parser.parse_args(argv)

    root = Path(arguments.root)
    if not root.is_dir():
        print("protocol_lint: {} is not a directory".format(root), file=sys.stderr)
        return 2

    split = lambda value: [part.strip() for part in value.split(",") if part.strip()]  # noqa: E731

    if arguments.list_nodes:
        tree = Tree(
            root=root.resolve(),
            source_extensions=DEFAULT_SOURCE_EXTENSIONS | {e.lower() for e in split(arguments.ext)},
            excluded_dirs=DEFAULT_EXCLUDED_DIRS | set(split(arguments.exclude)),
        )
        for node in sorted(tree.nodes):
            print(tree.relative(node))
        return 0

    findings = lint(root, split(arguments.ext), split(arguments.exclude), not arguments.no_heuristics)
    for finding in findings:
        print(finding)

    errors = sum(1 for finding in findings if finding.level == "ERROR")
    warnings = len(findings) - errors
    print("protocol_lint: {} errors, {} warnings".format(errors, warnings))
    return 1 if errors or (arguments.strict and warnings) else 0


if __name__ == "__main__":
    sys.exit(main())
