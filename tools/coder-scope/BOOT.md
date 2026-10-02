# BOOT.md — coder-scope

## Purpose

A `PreToolUse` hook that holds a coder subagent to the access rules of `AGENTS.md` §3
and §5 mechanically: a coder reads its own nodes, its ancestors' `BOOT.md`, the
neighbours' `API.md` and the files its task names, and writes only inside the paths the
orchestrator granted for the task. Everything else is refused before the tool runs, with
a reason the coder reads.

It exists because the rule was kept by the coder's discipline alone: a log audit of
2026-09-29 found one of five coders reading a neighbour's code (`src/Data/Species.cs`,
`src/Data/SpeciesDatabase.cs`, `src/Thermo/SpeciesTable.cs`), against §3. The owner
asked on 2026-09-29 that coders be refused reads and writes outside their subtree; the
design is of 2026-10-02, an orchestrator decision under the owner's word of that day to
run the next phase autonomously.

**The hook is enabled only by the owner.** Its registration lives in the owner's Claude
Code settings (`.claude/settings.local.json` or the user settings), which no session of
this tree edits: changing what a session's own tools may do is the owner's act. This node
holds the script, its self-test and the exact registration text (`API.md`).

## Invariants

- **Standard library of Python 3.8+ only**; one process per tool call, no state between
  calls other than the scope files it reads.
- **Only coders are judged.** A call whose hook input carries no `agent_type`, or an
  `agent_type` outside the configured coder types (`sonnet-coder`), is allowed without
  reading anything: the orchestrator, the reviewers and the arbiters are never refused.
- **Fail closed for coders.** A coder's call that cannot be judged (no scope file for its
  worktree, an unreadable scope file, a tool whose paths cannot be found) is refused with
  a reason naming what is missing; a coder's call is never allowed by default.
- **The scope is outside the coder's reach.** Scope files live in the main checkout's
  `.claude/scopes/` (untracked), keyed by the worktree's directory name; a coder can
  neither read nor write that directory, since no scope grants it.
- **The hook never writes**, prints nothing on an allowed call, and on a refused call
  prints the decision JSON of `API.md` with a reason that names the rule (`AGENTS.md`
  §3 or the task's scope), the path, and what the coder should do instead (escalate,
  §11).

## Dependencies

None.

Outside the tree: Python 3.8+; Claude Code's `PreToolUse` hook input (`agent_type`,
`agent_id`, `cwd`, `tool_name`, `tool_input`) and its decision output
(`hookSpecificOutput.permissionDecision` = `deny` with `permissionDecisionReason`), as the
hooks reference documents them (code.claude.com/docs/en/hooks), read 2026-10-02.

## Constraints

- **Which worktree.** The coder's worktree is the hook input's `cwd` when it lies under
  `<repo>/.claude/worktrees/<name>/`; `<name>` keys the scope file
  `<repo>/.claude/scopes/<name>.json`. A coder call whose `cwd` is elsewhere is refused.
- **What a path is.** For `Read`, `Edit`, `Write`, `NotebookEdit`: `file_path` (or
  `notebook_path`). For `Glob` and `Grep`: `path` (the worktree when absent) and, for
  `Glob`, the pattern's literal directory prefix. For `Bash` and `PowerShell`: every token
  of the command that looks like a path (contains `/` or `\`, or names a file of the
  worktree), after resolving it against the worktree; a command that changes directory
  (`cd`, `Set-Location`, `pushd`, `git -C`) to a directory outside the worktree is judged
  by that directory. The shell parse is a heuristic and says so in its refusals; its
  purpose is to stop a coder reading foreign sources by `cat`, not to be a sandbox.
  - A token that follows `>` or `>>` is judged against the write set, every other path
    token against the read set. A token with a wildcard is judged by its literal directory
    (the directory must be under a granted node), except that a pattern ending in
    `API.md` needs only a directory inside the worktree. `REV:path` (`git show`) is
    judged by its path. `/dev/null`, `nul`, URLs, switches (`-x`, `/p:X`) and quoted words
    that contain white space and name no existing path (a commit message) are not paths.
    A path built from a variable (`$HOME/x`, `%X%`) cannot be judged and is refused.
    The bodies of here-documents and PowerShell here-strings are not read as paths.
  - A script run by path (`python tools/protocol-lint/protocol_lint.py`) is a read of that
    path: the orchestrator lists what a coder may run in the scope's `read` extras.
- **Read set**, as repository-relative paths inside the worktree: `AGENTS.md`,
  `CLAUDE.md`, every `BOOT.md` of an ancestor of a granted node, every `API.md` in the
  tree, every file under a granted node, the build and solution files the build needs
  (`*.sln`, `Directory.Build.*`, `Directory.Packages.props`, `global.json`,
  `.editorconfig`), and the extra paths of the scope file (absolute paths allowed there,
  for a verdict in the orchestrator's scratchpad). Everything else in the worktree, and
  every path outside it, is refused for reading; a `Glob`, which lists names and reads no
  content, is the one exception: it needs only a directory inside the worktree. A `Grep`
  reads content: its `path` (the call's `cwd` when absent) must lie under a granted node
  or an extra path, so a `Grep` without `path` is refused and names a node instead.

  ⚠ 2026-10-02: was every path in the worktree outside the read set refused, `Glob`
  included; now `Glob` is the exception. Found while writing the self-test: a coder
  finds a neighbour's `API.md` by `Glob src/*/API.md`, and the directory prefix `src` is
  in no read set; refusing it would refuse the very discovery the rule is for.
- **Write set**: the scope file's `write` patterns, Python regular expressions matched
  with `re.fullmatch` against the repository-relative path; there is no default.
- **Tools without paths** (`TodoWrite`, `ToolSearch`, `SendMessage`, and `SubagentHandback`,
  the agent's own hand-back) are allowed **before anything else is judged**: before the
  `cwd` check and before the scope file is read, so a coder whose worktree is gone, or
  whose scope is unpublished or unreadable, can still hand back its report. A tool not
  known to the hook is refused for a coder. A refusal for a `cwd` outside the worktrees
  tells the coder to stop and hand back its report: the worktree it was given no longer
  exists or was never its own.

  ⚠ 2026-10-02: was the pathless tools allowed after the `cwd` and scope checks, now
  before them. Found on a real coder: resumed after its worktree had been removed (it had
  made no change), its `cwd` was the main checkout, every call was refused, the
  `SubagentHandback` included, and it ended without a report. A coder whose worktree is
  gone is relaunched by the orchestrator, never resumed.
- **A missing script.** The registration names the script in the main checkout; while the
  main checkout is on a branch without this node the hook command fails, Claude Code treats
  that as a non-blocking error, and coders run unguarded. The orchestrator keeps this node
  on every branch it checks out once the owner has enabled the hook.
- **A scope the hook refuses.** A scope file that is unreadable, that has a `write`
  pattern matching `AGENTS.md`, `BOOT.md` or `.claude/settings.local.json` (a wildcard:
  the taboo below), or a node that is the root or leaves it, makes every call of its coder
  refused, with the reason. Any failure of the hook itself while it judges a coder's call
  is a refusal too (fail closed); an unreadable hook input (no JSON object) exits 2.
- **The scope file** (`API.md`) is written by the orchestrator right after the launch, when
  the worktree name is known; until it exists every coder call is refused with "scope not
  yet published: retry this call", which the coder does.

## Acceptance criteria

- [x] 2026-10-02: The self-test `python -X utf8 tools/coder-scope/test_coder_scope.py`
      feeds recorded hook inputs to the script and checks every decision: an orchestrator
      call allowed unread; a coder's read of its node, of an ancestor's `BOOT.md`, of a
      neighbour's `API.md`, of an extra path allowed; a read of a neighbour's source, of an
      ancestor's `ACCEPTANCE.md`, of `.claude/scopes/` refused; a write inside and outside
      the `write` patterns; a `Bash` `cat` of a foreign source and a `git -C` outside the
      worktree refused; a missing scope file refused with the retry reason. Evidence:
      `test_coder_scope.py`, 50 tests through the real command-line interface
      (`ReadTests`, `WriteTests`, `SearchTests`, `ShellTests`, `FailClosedTests`).
- [x] 2026-10-02: Each rule shown red once by a mutation of the script, recorded with the
      test that turned red. Evidence: 32 mutations of `coder_scope.py`, each reverted; the
      first test that turned red, by rule:
      - only coders judged: `test_orchestrator_call_without_agent_type_is_allowed_unread`;
        an agent type outside the list: `test_other_agent_types_are_allowed`
      - fail closed: missing scope `test_missing_scope_file_is_refused_with_the_retry_reason`;
        unreadable scope `test_unreadable_scope_file_is_refused`; cwd outside the worktrees
        `test_cwd_outside_the_worktrees_is_refused`; unknown tool
        `test_unknown_tool_is_refused_and_pathless_tools_are_allowed`; no path in the input
        `test_tool_without_a_path_is_refused`; an unexpected failure
        `test_unexpected_failure_is_a_refusal_not_an_allowance`
      - the scope directory guard `test_scope_directory_is_refused`
      - read set: neighbour source `test_neighbour_source_is_refused`; ancestor
        `ACCEPTANCE.md` `test_ancestor_acceptance_is_refused`; ancestor `BOOT.md` only
        `test_neighbour_source_is_refused`; extra paths `test_extra_read_paths_are_readable`;
        outside the worktree `test_main_checkout_is_outside_the_worktree`; `Grep` directory
        rule `test_grep_of_a_neighbour_or_the_whole_worktree_is_refused`; `Glob` inside the
        worktree `test_glob_lists_names_inside_the_worktree_only`
      - write set: patterns `test_write_outside_the_patterns_is_refused`; outside the
        worktree `test_write_outside_the_worktree_is_refused`; wildcard pattern and root
        node `test_wildcard_write_pattern_and_root_node_are_refused`
      - `..` normalisation `test_dotdot_escape_of_the_worktree_is_refused`; case folding
        `test_scope_written_in_another_case_still_matches_on_windows`
      - shell: path tokens `test_cat_of_own_node_is_allowed_and_of_a_foreign_source_refused`;
        `git -C` and `cd` outside `test_git_dash_c_and_cd_outside_the_worktree_are_refused`;
        redirect as write `test_shell_redirection_is_judged_as_a_write`; here-document
        `test_heredoc_body_is_not_read_as_paths`; here-string
        `test_powershell_here_string_body_is_not_read_as_paths`; variable path
        `test_path_built_from_a_variable_cannot_be_judged`; wildcard directory
        `test_wildcards_are_judged_by_their_literal_directory`; `REV:path`
        `test_git_revision_path_is_judged_by_its_path`; empty command
        `test_empty_command_is_refused`
      - exit 2 on unreadable input `test_invalid_input_exits_with_two`
      - `/x:` switch on every platform (2026-10-02, CI run 37001153109): the old Windows-only
        condition turned `test_a_colon_switch_is_a_switch_and_an_absolute_path_is_still_a_path`
        and `test_changing_into_the_worktree_and_ordinary_commands_are_allowed` red under WSL
        Ubuntu 24.04; green on Windows and Linux with the fix (50 tests, 3 skipped on Linux)
- [x] 2026-10-02 — Enabled by the owner and seen working once on a real coder: a refused read of a
      neighbour's source in the coder's transcript, with the reason. The owner registered the
      hook in `.claude/settings.local.json` the same day, no restart needed; a probe coder
      (scope `tools/coder-scope`) got "scope not yet published" on its first two calls and
      passed on retry, was refused `Read src/Data/Species.cs` and `cat
      src/Thermo/SpeciesTable.cs` (read set, §3) and `Write src/Data/probe.tmp` (write set),
      and was allowed its own node, `src/Data/API.md`, `AGENTS.md` and a write in its node.
- [ ] The pathless tools pass whatever else is wrong: a self-test feeds `SubagentHandback`,
      `SendMessage`, `TodoWrite` and `ToolSearch` with a `cwd` outside the worktrees, with no
      scope file and with an unreadable one, each allowed, while a `Read` in the same three
      states stays refused; the `cwd` refusal's reason tells the coder to hand back its
      report. Shown red once by moving the pathless check back after the `cwd` check.

## Taboos

- No edit of any Claude Code settings file by a session of this tree: the registration is
  the owner's act.
- No allow-by-default for a coder, no wildcard write pattern in a scope file.
- No network, no write, no state outside the scope files.
