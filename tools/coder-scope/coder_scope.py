#!/usr/bin/env python3
"""coder-scope: a PreToolUse hook that holds a coder subagent to its granted nodes.

Reads one hook input (JSON) on standard input. A call of anything but a coder is allowed
unread. A coder's call is judged against the scope file of its worktree
(`<repo>/.claude/scopes/<worktree-name>.json`) and refused, with a reason, when it reads
outside the read set of AGENTS.md section 3 or writes outside the scope's write patterns.
The contract is tools/coder-scope/API.md; the rules are tools/coder-scope/BOOT.md.
"""

import json
import os
import re
import sys

DEFAULT_CODER_TYPES = ("sonnet-coder",)
PATHLESS_TOOLS = frozenset(("TodoWrite", "ToolSearch", "SendMessage", "SubagentHandback"))
SHELL_TOOLS = frozenset(("Bash", "PowerShell"))
CD_WORDS = frozenset(("cd", "chdir", "pushd", "set-location", "push-location", "sl"))
WILDCARDS = "*?[{"
IS_WINDOWS = os.name == "nt"
SPLITTERS = ";|&()`\n"
HEURISTIC = ("The shell check reads the command text and is a heuristic: name the paths "
             "inside your nodes explicitly, or escalate (AGENTS.md section 11).")
WILDCARD_PROBES = ("AGENTS.md", "BOOT.md", ".claude/settings.local.json")


def fold(text):
    """Return text in the comparison form of the platform, with forward slashes."""
    return os.path.normcase(text).replace("\\", "/")


API_NAME = fold("API.md")
BOOT_NAME = fold("BOOT.md")
ROOT_DOCS = frozenset((fold("AGENTS.md"), fold("CLAUDE.md")))
BUILD_FILE = re.compile(r"(.*\.sln|directory\.build\..*|directory\.packages\.props|global\.json|\.editorconfig)",
                        re.IGNORECASE)


class Refusal(Exception):
    """A coder's call that must not run; the message is the reason the coder reads."""


def relative_to(root, path):
    """Return path relative to root, joined with '/', or None when path lies outside root."""
    folded_root = fold(root).rstrip("/")
    folded_path = fold(path)
    if folded_path == folded_root:
        return ""
    if folded_path.startswith(folded_root + "/"):
        return path.replace("\\", "/")[len(folded_root) + 1:]
    return None


def resolve(raw, base):
    """Return the real, normalized absolute path of raw, resolved against base."""
    text = os.path.expanduser(raw)
    if IS_WINDOWS:
        text = re.sub(r"^/([A-Za-z])(/|$)", r"\1:/", text)
    try:
        if not os.path.isabs(text):
            text = os.path.join(base, text)
        return os.path.realpath(text)
    except (ValueError, OSError):
        raise Refusal("coder-scope: unresolvable path: %s. Use a plain path inside your nodes." % raw)


def wildcard_split(text):
    """Split text at its first wildcard segment into (literal prefix, remaining segments)."""
    segments = text.replace("\\", "/").split("/")
    for index, segment in enumerate(segments):
        if any(char in segment for char in WILDCARDS):
            return _join_prefix(text, segments[:index]), segments[index:]
    return text, []


def _join_prefix(text, kept):
    joined = "/".join(kept)
    if not joined and text.replace("\\", "/").startswith("/"):
        return "/"
    return joined


class Scope:
    """The scope file of one worktree and the verdicts it gives."""

    def __init__(self, repo, worktree, data):
        self.repo = repo
        self.worktree = worktree
        self.scopes_dir = os.path.join(repo, ".claude", "scopes")
        self.task = str(data.get("task", ""))
        self.nodes = [clean_node(node) for node in data["nodes"]]
        self.node_keys = [fold(node) for node in self.nodes]
        self.ancestor_keys = ancestor_keys(self.node_keys)
        self.writes = compile_writes(data["write"])
        self.extras = [resolve(path, worktree) for path in data["read"]]

    def refuse(self, rule, path, advice):
        """Raise the refusal of a rule, naming the path and what to do instead."""
        task = ' Your task: "%s".' % self.task if self.task else ""
        raise Refusal("coder-scope: %s: %s. %s.%s" % (rule, self.display(path), advice.rstrip("."), task))

    def display(self, path):
        """Return the worktree-relative form of path when it has one."""
        rel = relative_to(self.worktree, path)
        return path if rel is None else (rel or ".")

    def refuse_outside(self, path, verb):
        """Refuse a path outside the worktree, which no scope grants."""
        self.refuse("%s outside the worktree" % verb, path,
                    "Only the worktree and the extra read paths of your scope are reachable")

    def guard_scopes(self, path):
        """Refuse every path under the scope directory: the scope is out of the coder's reach."""
        if relative_to(self.scopes_dir, path) is not None:
            self.refuse("the scope files are not yours (AGENTS.md section 3)", path,
                        "Do not read or write them")

    def under_node(self, key):
        """Tell whether a folded worktree-relative key lies under a granted node."""
        return any(key == node or key.startswith(node + "/") for node in self.node_keys)

    def readable_file(self, rel):
        """Tell whether a worktree-relative file is in the read set."""
        key = fold(rel)
        directory, _, name = key.rpartition("/")
        if self.under_node(key) or name == API_NAME:
            return True
        if not directory and name in ROOT_DOCS:
            return True
        at_ancestor = directory in self.ancestor_keys
        return at_ancestor and (name == BOOT_NAME or BUILD_FILE.fullmatch(name) is not None)

    def in_extras(self, path):
        """Tell whether path is an extra read path of the scope or lies under one."""
        return any(relative_to(extra, path) is not None for extra in self.extras)

    def check_read(self, path, directory):
        """Allow or refuse a read of a file, or of a directory the tool searches."""
        self.guard_scopes(path)
        if self.in_extras(path):
            return
        rel = relative_to(self.worktree, path)
        if rel is None:
            self.refuse_outside(path, "read")
        allowed = self.under_node(fold(rel)) if directory else self.readable_file(rel)
        if not allowed:
            self.refuse("read set (AGENTS.md section 3)", path,
                        "You may read your nodes %s, the BOOT.md of their ancestors, any API.md, "
                        "AGENTS.md and the build files, not a neighbour's code; if its API.md is "
                        "insufficient, escalate in your report (AGENTS.md section 11)"
                        % ", ".join(self.nodes))

    def check_read_path(self, path):
        """Judge a read of a file or a search of a directory by what path is on disk."""
        self.check_read(path, os.path.isdir(path))

    def check_write(self, path):
        """Allow a write only when the worktree-relative path fullmatches a write pattern."""
        self.guard_scopes(path)
        rel = relative_to(self.worktree, path)
        if rel is None:
            self.refuse_outside(path, "write")
        if not any(pattern.fullmatch(rel) for pattern in self.writes):
            self.refuse("write set (the task's scope)", path,
                        "You may write only the paths matching %s; if the task needs more, "
                        "escalate in your report (AGENTS.md section 11)"
                        % ", ".join("'%s'" % p.pattern for p in self.writes))

    def check_inside(self, path, verb):
        """Allow a name listing anywhere inside the worktree; refuse the rest."""
        self.guard_scopes(path)
        if relative_to(self.worktree, path) is None and not self.in_extras(path):
            self.refuse_outside(path, verb)

    def judge_tool(self, tool, args, cwd):
        """Judge one tool call of the coder; raise Refusal when it must not run."""
        if tool in PATHLESS_TOOLS:
            return
        if tool == "Read":
            self.check_read_path(path_argument(tool, args, "file_path", cwd))
        elif tool in ("Write", "Edit"):
            self.check_write(path_argument(tool, args, "file_path", cwd))
        elif tool == "NotebookEdit":
            self.check_write(path_argument(tool, args, "notebook_path", cwd))
        elif tool == "Glob":
            self.judge_glob(args, cwd)
        elif tool == "Grep":
            self.check_read_path(resolve(args.get("path") or cwd, cwd))
        elif tool in SHELL_TOOLS:
            self.judge_shell(args.get("command"), cwd)
        else:
            raise Refusal("coder-scope: tool not known to the hook: %s. A coder may use the "
                          "tools the hook knows; ask the orchestrator." % tool)

    def judge_glob(self, args, cwd):
        """Judge a Glob: it lists names, so its directory only has to lie inside the worktree."""
        base = resolve(args.get("path") or cwd, cwd)
        self.check_inside(base, "glob")
        prefix, _ = wildcard_split(str(args.get("pattern") or ""))
        if prefix:
            self.check_inside(resolve(prefix, base), "glob")

    def judge_shell(self, command, cwd):
        """Judge a Bash or PowerShell command by the paths its text names (a heuristic)."""
        if not isinstance(command, str) or not command.strip():
            raise Refusal("coder-scope: the shell call has no command text. " + HEURISTIC)
        current = cwd
        try:
            for segment in tokenize(strip_heredocs(command)):
                current = self.judge_segment(segment, current)
        except Refusal as refusal:
            text = str(refusal)
            raise Refusal(text if HEURISTIC in text else text + " " + HEURISTIC)

    def judge_segment(self, segment, cwd):
        """Judge one command of a shell line; return the directory the next one runs in."""
        words = [text for text, _ in segment]
        target = directory_target(words)
        if target is not None:
            cwd = self.enter(target, cwd)
        skipped = target is None
        for text, mode in segment:
            if not skipped and text == target:
                skipped = True
                continue
            self.judge_token(text, mode, cwd)
        return cwd

    def enter(self, target, cwd):
        """Return the directory a cd-like word or `git -C` moves to, refusing one outside."""
        real = resolve(target, cwd)
        if relative_to(self.worktree, real) is None and not self.in_extras(real):
            self.refuse("directory change outside the worktree",
                        real, "Stay in your worktree. " + HEURISTIC)
        return real

    def judge_token(self, text, mode, cwd):
        """Judge one token of a shell command when it looks like a path."""
        candidate = path_candidate(text, cwd)
        if candidate is None:
            return
        prefix, rest = wildcard_split(candidate)
        if mode == "write":
            self.check_write(resolve(candidate, cwd))
        elif rest and fold(rest[-1]) == API_NAME:
            self.check_inside(resolve(prefix or ".", cwd), "read")
        elif rest:
            self.check_read(resolve(prefix or ".", cwd), True)
        else:
            self.check_read_path(resolve(candidate, cwd))


def directory_target(words):
    """Return the directory a cd-like word or `git -C` names in a command, else None."""
    head = words[0].lower()
    if head in CD_WORDS:
        options = [w for w in words[1:] if not w.startswith("-") and w.lower() != "/d"]
        return options[0] if options else "~"
    if head == "git" and "-C" in words[1:-1]:
        return words[words.index("-C") + 1]
    return None


def clean_node(node):
    """Return a granted node as a repository-relative directory, refusing a root or escape."""
    if not isinstance(node, str):
        raise Refusal("coder-scope: scope file unreadable: a node is not a string.")
    text = node.replace("\\", "/").strip("/")
    escapes = ".." in text.split("/") or ":" in text or node.startswith(("/", "\\"))
    if text in ("", ".") or escapes:
        raise Refusal("coder-scope: scope file refused: node '%s' is the root or leaves it." % node)
    return text


def ancestor_keys(node_keys):
    """Return the folded directories that are a granted node or an ancestor of one."""
    keys = {""}
    for key in node_keys:
        parts = key.split("/")
        keys.update("/".join(parts[:count]) for count in range(1, len(parts) + 1))
    return keys


def compile_writes(patterns):
    """Compile the write patterns, refusing a wildcard one that would reach AGENTS.md or settings."""
    flags = re.IGNORECASE if IS_WINDOWS else 0
    compiled = []
    for pattern in patterns:
        try:
            regex = re.compile(pattern, flags)
        except (re.error, TypeError):
            raise Refusal("coder-scope: scope file unreadable: bad write pattern %r." % (pattern,))
        if any(regex.fullmatch(probe) for probe in WILDCARD_PROBES):
            raise Refusal("coder-scope: scope file refused: write pattern '%s' is a wildcard "
                          "(it matches %s)." % (pattern, ", ".join(WILDCARD_PROBES)))
        compiled.append(regex)
    return compiled


def path_argument(tool, args, field, cwd):
    """Return the resolved path a tool names in its input, refusing a call that names none."""
    value = args.get(field)
    if not isinstance(value, str) or not value:
        raise Refusal("coder-scope: %s names no path (%s): it cannot be judged." % (tool, field))
    return resolve(value, cwd)


def strip_heredocs(command):
    """Drop here-document and here-string bodies of a shell line: their text is not paths."""
    command = re.sub(r"<<-?\s*(['\"]?)(\w+)\1[^\n]*\n.*?\n\s*\2[ \t]*(?=\n|\Z)",
                     lambda m: m.group(0).split("\n", 1)[0] + "\n", command, flags=re.DOTALL)
    return re.sub(r"@(['\"])[ \t]*\n.*?\n\1@", "''", command, flags=re.DOTALL)


class _Scanner:
    """Splits shell text into segments of (token, mode) without interpreting escapes."""

    def __init__(self):
        self.segments = []
        self.segment = []
        self.chars = []
        self.started = False
        self.redirect = False

    def flush(self):
        """Close the token being read."""
        if self.started:
            self.segment.append(("".join(self.chars), "write" if self.redirect else "arg"))
            self.redirect = False
        self.chars = []
        self.started = False

    def end_segment(self):
        """Close the command being read."""
        self.flush()
        self.redirect = False
        if self.segment:
            self.segments.append(self.segment)
            self.segment = []

    def feed(self, text):
        """Scan the whole text."""
        quote = None
        for char in text:
            if quote:
                if char == quote:
                    quote = None
                else:
                    self.chars.append(char)
            elif char in "'\"":
                quote = char
                self.started = True
            elif char in SPLITTERS:
                self.end_segment()
            elif char in "<>":
                self.flush()
                self.redirect = char == ">"
            elif char.isspace():
                self.flush()
            else:
                self.chars.append(char)
                self.started = True
        self.end_segment()


def tokenize(command):
    """Return the commands of a shell line, each a list of (token, 'arg' or 'write')."""
    scanner = _Scanner()
    scanner.feed(command)
    return scanner.segments


def path_candidate(text, cwd):
    """Return the path a shell token names, or None when it does not look like one."""
    if text.lower() in ("/dev/null", "nul", "$null", ""):
        return None
    if text.startswith("-"):
        text = text.split("=", 1)[1] if "=" in text else ""
    if not text or re.match(r"[A-Za-z][A-Za-z0-9+.-]*://", text) or re.search(r"[<>|\"]", text):
        return None
    if re.fullmatch(r"/[A-Za-z]+:.*", text) or (IS_WINDOWS and re.fullmatch(r"/[A-Za-z]+(=.*)?", text)):
        return None
    revision = re.fullmatch(r"([^/\\:]{2,}):(.+)", text)
    text = revision.group(2) if revision else text
    named = "/" in text or "\\" in text
    if not named and not (text not in (".", "..") and os.path.isfile(os.path.join(cwd, text))):
        return None
    if "$" in text or "%" in text:
        raise Refusal("coder-scope: path built from a variable: %s. %s" % (text, HEURISTIC))
    if any(char.isspace() for char in text) and not os.path.exists(os.path.join(cwd, text)):
        return None
    return text


def locate_worktree(repo, cwd):
    """Return (name, real directory) of the worktree the hook input's cwd lies in."""
    base = os.path.realpath(os.path.join(repo, ".claude", "worktrees"))
    real_cwd = resolve(cwd, repo)
    rel = relative_to(base, real_cwd)
    if not rel:
        raise Refusal("coder-scope: working directory outside %s: %s. A coder runs in a "
                      "worktree under .claude/worktrees/." % (base, cwd))
    name = rel.split("/")[0]
    return name, os.path.join(base, name)


def load_scope(repo, worktree, name):
    """Read and validate the scope file of a worktree; refuse a call that has none."""
    path = os.path.join(repo, ".claude", "scopes", name + ".json")
    if not os.path.isfile(path):
        raise Refusal("coder-scope: scope not yet published: %s. The orchestrator writes it "
                      "right after the launch: retry this call." % name)
    try:
        with open(path, "r", encoding="utf-8") as handle:
            data = json.load(handle)
        for field in ("nodes", "write", "read"):
            if not isinstance(data[field], list):
                raise TypeError(field)
        return Scope(repo, worktree, data)
    except (OSError, ValueError, KeyError, TypeError, AttributeError):
        raise Refusal("coder-scope: scope file unreadable: %s. It must be JSON with the lists "
                      "nodes, write and read; ask the orchestrator." % name)


def decide(hook, repo, coder_types):
    """Return the refusal reason of a hook input, or None when the call is allowed."""
    agent = hook.get("agent_type")
    if not agent or agent not in coder_types:
        return None
    try:
        cwd = hook.get("cwd")
        if not isinstance(cwd, str) or not cwd:
            raise Refusal("coder-scope: the hook input has no cwd: the worktree is unknown.")
        name, worktree = locate_worktree(repo, cwd)
        scope = load_scope(repo, worktree, name)
        args = hook.get("tool_input")
        scope.judge_tool(hook.get("tool_name"), args if isinstance(args, dict) else {},
                         resolve(cwd, repo))
    except Refusal as refusal:
        return str(refusal)
    except Exception as error:  # a coder's call that cannot be judged is never allowed
        return "coder-scope: internal error, the call cannot be judged: %r." % (error,)
    return None


def parse_arguments(argv):
    """Return (repo, coder types) from the command line, or raise ValueError."""
    here = os.path.dirname(os.path.abspath(__file__))
    repo = os.path.dirname(os.path.dirname(here))
    coder_types = DEFAULT_CODER_TYPES
    index = 0
    while index < len(argv):
        flag = argv[index]
        if flag not in ("--repo", "--coder-types") or index + 1 >= len(argv):
            raise ValueError("unknown or incomplete argument: %s" % flag)
        if flag == "--repo":
            repo = argv[index + 1]
        else:
            coder_types = tuple(part for part in argv[index + 1].split(",") if part)
        index += 2
    return os.path.realpath(repo), coder_types


def main(argv, stdin_bytes, stdout, stderr):
    """Run the hook: return the exit code, print the decision on refusal."""
    try:
        repo, coder_types = parse_arguments(argv)
        hook = json.loads(stdin_bytes.decode("utf-8"))
        if not isinstance(hook, dict):
            raise ValueError("the hook input is not a JSON object")
    except ValueError as error:
        stderr.write("coder-scope: invocation error: %s\n" % error)
        return 2
    reason = decide(hook, repo, coder_types)
    if reason is not None:
        stdout.write(json.dumps({"hookSpecificOutput": {
            "hookEventName": "PreToolUse", "permissionDecision": "deny",
            "permissionDecisionReason": reason}}))
        stdout.write("\n")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:], sys.stdin.buffer.read(), sys.stdout, sys.stderr))
