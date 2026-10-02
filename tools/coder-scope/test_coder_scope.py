#!/usr/bin/env python3
"""Self-test of coder_scope.py: hook inputs fed through the real command-line interface.

A fake repository is built under a temporary directory: a few nodes, a worktree copy of
them under .claude/worktrees/agent-x and the scope file .claude/scopes/agent-x.json.
Run: python -X utf8 tools/coder-scope/test_coder_scope.py -v
"""

import json
import os
import shutil
import subprocess
import sys
import tempfile
import unittest

HERE = os.path.dirname(os.path.abspath(__file__))
SCRIPT = os.path.join(HERE, "coder_scope.py")
sys.path.insert(0, HERE)
import coder_scope  # noqa: E402

NODE_FILES = {
    "AGENTS.md": "protocol\n",
    "CLAUDE.md": "loader\n",
    "BOOT.md": "root boot\n",
    "API.md": "root api\n",
    "ACCEPTANCE.md": "root evidence\n",
    "APThermo.sln": "sln\n",
    "Directory.Build.props": "<Project/>\n",
    "src/BOOT.md": "src boot\n",
    "src/ACCEPTANCE.md": "src evidence\n",
    "src/Alpha/BOOT.md": "alpha boot\n",
    "src/Alpha/API.md": "alpha api\n",
    "src/Alpha/ACCEPTANCE.md": "alpha evidence\n",
    "src/Alpha/Alpha.cs": "class Alpha {}\n",
    "src/Beta/BOOT.md": "beta boot\n",
    "src/Beta/API.md": "beta api\n",
    "src/Beta/Beta.cs": "class Beta {}\n",
    "tests/Alpha.Tests/BOOT.md": "tests boot\n",
    "tests/Alpha.Tests/API.md": "tests api\n",
    "tests/Alpha.Tests/AlphaTests.cs": "class AlphaTests {}\n",
    "tools/protocol-lint/protocol_lint.py": "print('lint')\n",
}


def write_file(path, text):
    """Create a file, and its directories, with LF line endings."""
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8", newline="\n") as handle:
        handle.write(text)


class HookCase(unittest.TestCase):
    """Base class: a fake repository, a worktree, a scope, and the helpers to call the hook."""

    @classmethod
    def setUpClass(cls):
        cls.root = os.path.realpath(tempfile.mkdtemp(prefix="coder-scope-test-"))
        cls.repo = os.path.join(cls.root, "repo")
        cls.wt = os.path.join(cls.repo, ".claude", "worktrees", "agent-x")
        cls.scratch = os.path.join(cls.root, "scratch")
        for relative, text in NODE_FILES.items():
            write_file(os.path.join(cls.repo, relative), text)
            write_file(os.path.join(cls.wt, relative), text)
        write_file(os.path.join(cls.scratch, "VERDICT.md"), "verdict\n")
        write_file(os.path.join(cls.scratch, "OTHER.md"), "other\n")
        cls.scope = {
            "nodes": ["src/Alpha", "tests/Alpha.Tests"],
            "write": ["src/Alpha/.*", "tests/Alpha\\.Tests/.*"],
            "read": [os.path.join(cls.scratch, "VERDICT.md").replace("\\", "/"),
                     "tools/protocol-lint/protocol_lint.py"],
            "task": "Split Alpha into children",
        }
        cls.write_scope("agent-x", cls.scope)

    @classmethod
    def tearDownClass(cls):
        shutil.rmtree(cls.root, ignore_errors=True)

    @classmethod
    def write_scope(cls, name, data):
        """Publish a scope file for a worktree name."""
        text = data if isinstance(data, str) else json.dumps(data)
        write_file(os.path.join(cls.repo, ".claude", "scopes", name + ".json"), text)

    def wpath(self, relative):
        """Return an absolute path inside the worktree."""
        return os.path.join(self.wt, *relative.split("/"))

    def call(self, tool, tool_input, agent="sonnet-coder", cwd=None, args=()):
        """Run the script on one hook input; return (exit code, stdout, stderr)."""
        hook = {"session_id": "s", "transcript_path": "t", "cwd": cwd or self.wt,
                "tool_name": tool, "tool_input": tool_input, "tool_use_id": "u",
                "permission_mode": "default"}
        if agent is not None:
            hook["agent_id"] = "a1"
            hook["agent_type"] = agent
        return self.run_raw(json.dumps(hook), args)

    def run_raw(self, text, args=()):
        """Run the script with raw standard input."""
        done = subprocess.run([sys.executable, "-X", "utf8", SCRIPT, "--repo", self.repo] + list(args),
                              input=text.encode("utf-8"), stdout=subprocess.PIPE,
                              stderr=subprocess.PIPE, check=False)
        return done.returncode, done.stdout.decode("utf-8"), done.stderr.decode("utf-8")

    def assertAllowed(self, tool, tool_input, **kwargs):
        """The call is allowed: exit 0 and nothing printed."""
        code, out, err = self.call(tool, tool_input, **kwargs)
        self.assertEqual((code, out, err), (0, "", ""), "expected allowed: %s %r" % (tool, tool_input))

    def assertRefused(self, tool, tool_input, *needles, **kwargs):
        """The call is refused with the decision JSON; the reason contains every needle."""
        code, out, _ = self.call(tool, tool_input, **kwargs)
        self.assertEqual(code, 0)
        self.assertTrue(out, "expected a refusal: %s %r" % (tool, tool_input))
        decision = json.loads(out)["hookSpecificOutput"]
        self.assertEqual(decision["hookEventName"], "PreToolUse")
        self.assertEqual(decision["permissionDecision"], "deny")
        reason = decision["permissionDecisionReason"]
        self.assertTrue(reason.startswith("coder-scope: "), reason)
        for needle in needles:
            self.assertIn(needle, reason)
        return reason


class WhoIsJudgedTests(HookCase):
    """Only coders are judged."""

    def test_orchestrator_call_without_agent_type_is_allowed_unread(self):
        """A call without agent_type is allowed, prints nothing, even outside any worktree."""
        self.assertAllowed("Read", {"file_path": self.wpath("src/Beta/Beta.cs")}, agent=None)
        self.assertAllowed("Bash", {"command": "cat /etc/passwd"}, agent=None, cwd=self.repo)

    def test_other_agent_types_are_allowed(self):
        """Reviewers and arbiters are never refused."""
        self.assertAllowed("Read", {"file_path": self.wpath("src/Beta/Beta.cs")},
                           agent="clean-code-reviewer")

    def test_coder_types_option_changes_who_is_judged(self):
        """--coder-types names the judged agent types."""
        foreign = {"file_path": self.wpath("src/Beta/Beta.cs")}
        self.assertAllowed("Read", foreign, args=["--coder-types", "other-coder"])
        self.assertRefused("Read", foreign, agent="other-coder", args=["--coder-types", "other-coder"])


class ReadTests(HookCase):
    """The read set of a coder."""

    def test_own_node_is_readable(self):
        """Every file under a granted node, its ACCEPTANCE.md included."""
        for relative in ("src/Alpha/Alpha.cs", "src/Alpha/BOOT.md", "src/Alpha/ACCEPTANCE.md",
                         "tests/Alpha.Tests/AlphaTests.cs"):
            self.assertAllowed("Read", {"file_path": self.wpath(relative)})

    def test_ancestor_boot_is_readable(self):
        """The BOOT.md of every ancestor of a granted node, up to the root."""
        self.assertAllowed("Read", {"file_path": self.wpath("BOOT.md")})
        self.assertAllowed("Read", {"file_path": self.wpath("src/BOOT.md")})

    def test_neighbour_api_and_protocol_files_are_readable(self):
        """Any API.md, AGENTS.md, CLAUDE.md and the build files."""
        for relative in ("src/Beta/API.md", "API.md", "AGENTS.md", "CLAUDE.md", "APThermo.sln",
                         "Directory.Build.props"):
            self.assertAllowed("Read", {"file_path": self.wpath(relative)})

    def test_extra_read_paths_are_readable(self):
        """An absolute extra path and a repository-relative one; a sibling of the first is not."""
        self.assertAllowed("Read", {"file_path": os.path.join(self.scratch, "VERDICT.md")})
        self.assertAllowed("Read", {"file_path": self.wpath("tools/protocol-lint/protocol_lint.py")})
        self.assertRefused("Read", {"file_path": os.path.join(self.scratch, "OTHER.md")},
                           "outside the worktree")

    def test_neighbour_source_is_refused(self):
        """A neighbour's code and its BOOT.md are outside the read set, AGENTS.md section 3."""
        reason = self.assertRefused("Read", {"file_path": self.wpath("src/Beta/Beta.cs")},
                                    "AGENTS.md section 3", "src/Beta/Beta.cs", "escalate")
        self.assertIn("Split Alpha into children", reason)
        self.assertRefused("Read", {"file_path": self.wpath("src/Beta/BOOT.md")}, "src/Beta/BOOT.md")

    def test_ancestor_acceptance_is_refused(self):
        """An ancestor's ACCEPTANCE.md is that ancestor's evidence, not frame."""
        self.assertRefused("Read", {"file_path": self.wpath("ACCEPTANCE.md")}, "read set")
        self.assertRefused("Read", {"file_path": self.wpath("src/ACCEPTANCE.md")}, "read set")

    def test_scope_directory_is_refused(self):
        """The scope files of the main checkout are out of reach, for every tool."""
        scope_file = os.path.join(self.repo, ".claude", "scopes", "agent-x.json")
        self.assertRefused("Read", {"file_path": scope_file}, "scope files")
        self.assertRefused("Grep", {"pattern": "nodes", "path": os.path.dirname(scope_file)}, "scope files")
        self.assertRefused("Write", {"file_path": scope_file}, "scope files")
        self.assertRefused("Bash", {"command": "cat " + scope_file.replace("\\", "/")}, "scope files")

    def test_main_checkout_is_outside_the_worktree(self):
        """The same file of the main checkout is refused: it is not the worktree."""
        self.assertRefused("Read", {"file_path": os.path.join(self.repo, "src", "Alpha", "Alpha.cs")},
                           "outside the worktree")

    def test_dotdot_escape_of_the_worktree_is_refused(self):
        """A path through .. out of the worktree is judged by where it lands."""
        escape = "src/Alpha/../../../../../src/Alpha/Alpha.cs"
        self.assertRefused("Read", {"file_path": self.wpath(escape)}, "outside the worktree")

    def test_dotdot_escape_of_the_node_is_refused(self):
        """A path through .. that leaves the granted node for a neighbour is refused."""
        self.assertRefused("Read", {"file_path": self.wpath("src/Alpha/../Beta/Beta.cs")}, "read set")
        self.assertRefused("Read", {"file_path": "src/Alpha/../Beta/Beta.cs"}, "read set")

    def test_relative_path_resolves_against_cwd(self):
        """A relative path is resolved against the hook input's cwd."""
        self.assertAllowed("Read", {"file_path": "Alpha.cs"}, cwd=self.wpath("src/Alpha"))
        self.assertRefused("Read", {"file_path": "Beta.cs"}, cwd=self.wpath("src/Beta"), )

    @unittest.skipUnless(os.name == "nt", "drive letters and backslashes are Windows paths")
    def test_windows_path_with_backslashes_and_forward_slashes(self):
        """The same path in both spellings gets the same verdict."""
        for foreign, own in (("src\\Beta\\Beta.cs", "src\\Alpha\\Alpha.cs"),):
            back_foreign = self.wt + "\\" + foreign
            back_own = self.wt + "\\" + own
            self.assertRefused("Read", {"file_path": back_foreign}, "read set")
            self.assertRefused("Read", {"file_path": back_foreign.replace("\\", "/")}, "read set")
            self.assertAllowed("Read", {"file_path": back_own})
            self.assertAllowed("Read", {"file_path": back_own.replace("\\", "/")})
        self.assertIn(":", self.wt)

    @unittest.skipUnless(os.name == "nt", "case-insensitive comparison is the Windows rule")
    def test_paths_compare_case_insensitively_on_windows(self):
        """A different case of a granted path is the same path; of a foreign one too."""
        self.assertAllowed("Read", {"file_path": self.wpath("SRC/ALPHA/NotYetThere.cs")})
        self.assertAllowed("Write", {"file_path": self.wpath("SRC/ALPHA/NotYetThere.cs")})
        self.assertRefused("Read", {"file_path": self.wpath("SRC/BETA/NotYetThere.cs")}, "read set")
        shouting = self.wt.upper()
        self.assertAllowed("Read", {"file_path": shouting + "/src/Alpha/Alpha.cs"}, cwd=shouting)


class WriteTests(HookCase):
    """The write set: the scope's patterns, no default."""

    def test_write_inside_the_patterns_is_allowed(self):
        """Write, Edit and NotebookEdit inside a pattern."""
        self.assertAllowed("Write", {"file_path": self.wpath("src/Alpha/New.cs")})
        self.assertAllowed("Edit", {"file_path": self.wpath("src/Alpha/BOOT.md")})
        self.assertAllowed("Write", {"file_path": self.wpath("tests/Alpha.Tests/Data/x.txt")})
        self.assertAllowed("NotebookEdit", {"notebook_path": self.wpath("src/Alpha/n.ipynb")})

    def test_write_outside_the_patterns_is_refused(self):
        """A neighbour, the protocol file, a path that only looks like a granted one."""
        for relative in ("src/Beta/Beta.cs", "AGENTS.md", "BOOT.md", "tests/AlphaXTests/x.txt"):
            self.assertRefused("Write", {"file_path": self.wpath(relative)}, "write set", relative)
        self.assertRefused("NotebookEdit", {"notebook_path": self.wpath("src/Beta/n.ipynb")}, "write set")

    def test_write_outside_the_worktree_is_refused(self):
        """The main checkout and the scratch directory are not writable."""
        self.assertRefused("Write", {"file_path": os.path.join(self.repo, "src", "Alpha", "x.cs")},
                           "outside the worktree")
        self.assertRefused("Write", {"file_path": os.path.join(self.scratch, "VERDICT.md")},
                           "outside the worktree")

    def test_dotdot_escape_of_the_granted_node_is_refused(self):
        """src/Alpha/../Beta/x.cs lands in a neighbour and matches no pattern."""
        self.assertRefused("Write", {"file_path": self.wpath("src/Alpha/../Beta/x.cs")}, "write set")
        self.assertRefused("Edit", {"file_path": "src/Alpha/../../AGENTS.md"}, "write set")

    def test_empty_write_list_refuses_every_write(self):
        """There is no default write set."""
        self.write_scope("agent-empty", dict(self.scope, write=[]))
        self.addCleanup(os.remove, os.path.join(self.repo, ".claude", "scopes", "agent-empty.json"))
        cwd = os.path.join(self.repo, ".claude", "worktrees", "agent-empty")
        os.makedirs(cwd)
        self.addCleanup(shutil.rmtree, cwd, True)
        self.assertRefused("Write", {"file_path": os.path.join(cwd, "src", "Alpha", "x.cs")},
                           "write set", cwd=cwd)


class SearchTests(HookCase):
    """Glob and Grep."""

    def test_grep_of_a_granted_node_is_allowed(self):
        """A search root under a granted node, and a single readable file."""
        self.assertAllowed("Grep", {"pattern": "class", "path": self.wpath("src/Alpha")})
        self.assertAllowed("Grep", {"pattern": "api", "path": self.wpath("src/Beta/API.md")})

    def test_grep_of_a_neighbour_or_the_whole_worktree_is_refused(self):
        """Contents of foreign code are read-set violations, with or without a path."""
        self.assertRefused("Grep", {"pattern": "class", "path": self.wpath("src/Beta")}, "read set")
        self.assertRefused("Grep", {"pattern": "class"}, "read set")

    def test_glob_lists_names_inside_the_worktree_only(self):
        """Names are not content; a directory outside the worktree is refused."""
        self.assertAllowed("Glob", {"pattern": "src/*/API.md"})
        self.assertAllowed("Glob", {"pattern": "**/*.cs", "path": self.wpath("src")})
        self.assertRefused("Glob", {"pattern": "**/*.cs", "path": self.repo}, "outside the worktree")
        self.assertRefused("Glob", {"pattern": "../../../../../src/**/*.cs"}, "outside the worktree")


class ShellTests(HookCase):
    """Bash and PowerShell, judged by the paths of the command text."""

    def test_cat_of_own_node_is_allowed_and_of_a_foreign_source_refused(self):
        """The case the audit of 2026-09-29 found."""
        self.assertAllowed("Bash", {"command": "cat src/Alpha/Alpha.cs"})
        self.assertRefused("Bash", {"command": "cat src/Beta/Beta.cs"}, "read set", "heuristic")
        self.assertRefused("Bash", {"command": "cd src/Beta && cat Beta.cs"}, "read set")
        self.assertRefused("Bash", {"command": "head -5 " + self.wpath("src/Beta/Beta.cs").replace("\\", "/")},
                           "read set")

    def test_git_dash_c_and_cd_outside_the_worktree_are_refused(self):
        """A directory change out of the worktree is judged by that directory."""
        main = self.repo.replace("\\", "/")
        self.assertRefused("Bash", {"command": "git -C %s status" % main}, "directory change outside")
        self.assertRefused("Bash", {"command": "cd %s && ls" % main}, "directory change outside")
        self.assertRefused("PowerShell", {"command": "Set-Location '%s'; Get-ChildItem" % self.repo},
                           "directory change outside")
        self.assertRefused("Bash", {"command": "cd .. && cat BOOT.md"}, "directory change outside",
                           cwd=self.wt)

    def test_changing_into_the_worktree_and_ordinary_commands_are_allowed(self):
        """The commands of the working rules: cd into the worktree, build, git, the linter."""
        wt = self.wt.replace("\\", "/")
        for command in ("cd %s && git status" % wt,
                        "cd %s && dotnet build APThermo.sln" % wt,
                        "git commit -m \"feat(alpha): split a/b and </x>\" 2>/dev/null",
                        "git add src/Alpha/Alpha.cs tests/Alpha.Tests/AlphaTests.cs",
                        "dotnet build /p:Configuration=Release -c Release APThermo.sln",
                        "python -X utf8 tools/protocol-lint/protocol_lint.py . --exclude templates",
                        "git -C %s log --oneline -3" % wt,
                        "grep -rn \"</param>\" src/Alpha | head -5"):
            self.assertAllowed("Bash", {"command": command})

    def test_shell_redirection_is_judged_as_a_write(self):
        """A redirect target outside the write patterns is refused, inside is allowed."""
        self.assertAllowed("Bash", {"command": "echo hi > src/Alpha/out.txt"})
        self.assertRefused("Bash", {"command": "echo hi > src/Beta/out.txt"}, "write set")
        self.assertRefused("Bash", {"command": "echo hi >> AGENTS.md"}, "write set")

    def test_heredoc_body_is_not_read_as_paths(self):
        """The text of a here-document is not a list of paths; its redirect target still is."""
        body = "cat > src/Alpha/x.md <<'EOF'\nsee src/Beta/Beta.cs and /etc/passwd\nEOF"
        self.assertAllowed("Bash", {"command": body})
        self.assertRefused("Bash", {"command": body.replace("src/Alpha/x.md", "src/Beta/x.md")},
                           "write set")

    def test_powershell_here_string_body_is_not_read_as_paths(self):
        """The text of a PowerShell here-string is data."""
        command = "Set-Content src/Alpha/x.md @'" + chr(10) + "don't read src/Beta/Beta.cs" + chr(10) + "'@"
        self.assertAllowed("PowerShell", {"command": command})

    def test_powershell_backslash_paths(self):
        """PowerShell: backslash paths are judged like slash paths."""
        self.assertRefused("PowerShell", {"command": "Get-Content src\\Beta\\Beta.cs"}, "read set")
        self.assertAllowed("PowerShell", {"command": "Get-Content src\\Alpha\\Alpha.cs"})

    def test_wildcards_are_judged_by_their_literal_directory(self):
        """src/*/API.md is allowed; src/Beta/*.cs and src/*/*.cs are not."""
        self.assertAllowed("Bash", {"command": "cat src/*/API.md"})
        self.assertAllowed("Bash", {"command": "cat src/Alpha/*.cs"})
        self.assertRefused("Bash", {"command": "cat src/Beta/*.cs"}, "read set")
        self.assertRefused("Bash", {"command": "cat src/*/*.cs"}, "read set")

    def test_path_built_from_a_variable_cannot_be_judged(self):
        """A path that needs the shell to expand it is refused."""
        self.assertRefused("Bash", {"command": "cat $HOME/x/y.cs"}, "variable")

    def test_git_revision_path_is_judged_by_its_path(self):
        """git show REV:path reads a file."""
        self.assertAllowed("Bash", {"command": "git show HEAD:src/Alpha/Alpha.cs"})
        self.assertRefused("Bash", {"command": "git show main:src/Beta/Beta.cs"}, "read set")

    def test_empty_command_is_refused(self):
        """A shell call without text cannot be judged."""
        self.assertRefused("Bash", {"command": ""}, "no command text")


class FailClosedTests(HookCase):
    """A coder's call that cannot be judged is refused, never allowed."""

    def setUp(self):
        self.published = []

    def make_worktree(self, name):
        """Create an empty worktree directory, removed after the test."""
        path = os.path.join(self.repo, ".claude", "worktrees", name)
        os.makedirs(path)
        self.addCleanup(shutil.rmtree, path, True)
        return path

    def publish(self, name, data):
        """Publish a scope file, removed after the test."""
        self.write_scope(name, data)
        path = os.path.join(self.repo, ".claude", "scopes", name + ".json")
        if path not in self.published:
            self.published.append(path)
            self.addCleanup(os.remove, path)

    def test_missing_scope_file_is_refused_with_the_retry_reason(self):
        """Until the orchestrator publishes the scope, every coder call says retry."""
        cwd = self.make_worktree("agent-nosuch")
        self.assertRefused("Read", {"file_path": os.path.join(cwd, "AGENTS.md")},
                           "scope not yet published", "retry", cwd=cwd)

    def test_unreadable_scope_file_is_refused(self):
        """Not JSON, a missing list, a bad regular expression."""
        cwd = self.make_worktree("agent-bad")
        for text in ("{not json", json.dumps({"nodes": ["src/Alpha"]}),
                     json.dumps({"nodes": ["src/Alpha"], "write": ["("], "read": []})):
            self.publish("agent-bad", text)
            self.assertRefused("Read", {"file_path": os.path.join(cwd, "AGENTS.md")},
                               "scope file unreadable", cwd=cwd)

    def test_wildcard_write_pattern_and_root_node_are_refused(self):
        """A scope that would let a coder write everything, or read everything, is refused."""
        cwd = self.make_worktree("agent-wide")
        readable = {"file_path": os.path.join(cwd, "AGENTS.md")}
        self.publish("agent-wide", dict(self.scope, write=[".*"]))
        self.assertRefused("Read", readable, "wildcard", cwd=cwd)
        self.publish("agent-wide", dict(self.scope, write=["src/Alpha/.*", ".*\\.md"]))
        self.assertRefused("Read", readable, "wildcard", cwd=cwd)
        self.publish("agent-wide", dict(self.scope, nodes=["."]))
        self.assertRefused("Read", readable, "root or leaves it", cwd=cwd)
        self.publish("agent-wide", dict(self.scope, nodes=["src/../.."]))
        self.assertRefused("Read", readable, "root or leaves it", cwd=cwd)

    def test_cwd_outside_the_worktrees_is_refused(self):
        """A coder whose cwd is the main checkout or elsewhere cannot be keyed to a scope."""
        self.assertRefused("Read", {"file_path": os.path.join(self.repo, "AGENTS.md")},
                           "working directory outside", cwd=self.repo)
        self.assertRefused("Read", {"file_path": os.path.join(self.root, "x")},
                           "working directory outside", cwd=self.root)

    def test_unknown_tool_is_refused_and_pathless_tools_are_allowed(self):
        """A tool the hook does not know is refused for a coder; TodoWrite and the hand-back pass."""
        self.assertRefused("WebFetch", {"url": "https://example.com"}, "not known to the hook")
        for tool in ("TodoWrite", "ToolSearch", "SendMessage", "SubagentHandback"):
            self.assertAllowed(tool, {"message": "x"})

    def test_tool_without_a_path_is_refused(self):
        """A Read whose input names no path cannot be judged."""
        self.assertRefused("Read", {}, "names no path")
        self.assertRefused("Write", {"file_path": ""}, "names no path")

    @unittest.skipUnless(os.name == "nt", "case-insensitive comparison is the Windows rule")
    def test_scope_written_in_another_case_still_matches_on_windows(self):
        """Nodes and write patterns spelled in capitals judge the same files."""
        cwd = self.make_worktree("agent-case")
        write_file(os.path.join(cwd, "src", "Alpha", "Alpha.cs"), "class Alpha {}\n")
        self.publish("agent-case", dict(self.scope, nodes=["SRC/ALPHA"], write=["SRC/ALPHA/.*"]))
        self.assertAllowed("Read", {"file_path": os.path.join(cwd, "src", "Alpha", "Alpha.cs")}, cwd=cwd)
        self.assertAllowed("Write", {"file_path": os.path.join(cwd, "src", "Alpha", "New.cs")}, cwd=cwd)

    def test_unexpected_failure_is_a_refusal_not_an_allowance(self):
        """A tool input the hook did not foresee must not slip through as allowed."""
        self.assertRefused("Grep", {"pattern": "x", "path": 5}, "internal error")

    def test_invalid_input_exits_with_two(self):
        """Unreadable standard input is an invocation error, exit 2, the reason on stderr."""
        code, out, err = self.run_raw("not json")
        self.assertEqual((code, out), (2, ""))
        self.assertIn("invocation error", err)
        self.assertEqual(self.run_raw("[1]")[0], 2)
        self.assertEqual(self.run_raw("{}", ["--bogus"])[0], 2)

    def test_hook_never_writes(self):
        """After a refusal and an allowed call the scope directory holds only the published files."""
        before = sorted(os.listdir(os.path.join(self.repo, ".claude", "scopes")))
        self.call("Read", {"file_path": self.wpath("src/Beta/Beta.cs")})
        self.call("Read", {"file_path": self.wpath("src/Alpha/Alpha.cs")})
        self.assertEqual(sorted(os.listdir(os.path.join(self.repo, ".claude", "scopes"))), before)


class FunctionTests(unittest.TestCase):
    """Direct calls of the pure parts."""

    def test_tokenize_splits_on_operators_and_keeps_backslashes(self):
        """Segments, redirect targets and quoted words."""
        segments = coder_scope.tokenize("cd C:\\a\\b && echo 'x y' > out.txt | tee z")
        self.assertEqual(segments[0], [("cd", "arg"), ("C:\\a\\b", "arg")])
        self.assertEqual(segments[1], [("echo", "arg"), ("x y", "arg"), ("out.txt", "write")])
        self.assertEqual(segments[2], [("tee", "arg"), ("z", "arg")])

    def test_wildcard_split(self):
        """The literal prefix and the rest."""
        self.assertEqual(coder_scope.wildcard_split("src/*/API.md"), ("src", ["*", "API.md"]))
        self.assertEqual(coder_scope.wildcard_split("src/Alpha/x.cs"), ("src/Alpha/x.cs", []))
        self.assertEqual(coder_scope.wildcard_split("*.cs"), ("", ["*.cs"]))

    def test_relative_to_is_a_path_boundary(self):
        """A sibling that shares a prefix is not inside."""
        root = os.path.join(os.sep, "r", "wt")
        self.assertIsNone(coder_scope.relative_to(root, root + "2"))
        self.assertEqual(coder_scope.relative_to(root, root), "")
        self.assertEqual(coder_scope.relative_to(root, os.path.join(root, "a", "b")), "a/b")

    def test_decide_allows_a_non_coder_without_touching_the_disk(self):
        """decide() returns None for an input without agent_type, whatever else it holds."""
        self.assertIsNone(coder_scope.decide({"tool_name": "Read"}, "/nonexistent", ("sonnet-coder",)))


if __name__ == "__main__":
    unittest.main()
