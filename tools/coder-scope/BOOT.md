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
- **Read set**, as repository-relative paths inside the worktree: `AGENTS.md`,
  `CLAUDE.md`, every `BOOT.md` of an ancestor of a granted node, every `API.md` in the
  tree, every file under a granted node, the build and solution files the build needs
  (`*.sln`, `Directory.Build.*`, `Directory.Packages.props`, `global.json`,
  `.editorconfig`), and the extra paths of the scope file (absolute paths allowed there,
  for a verdict in the orchestrator's scratchpad). Everything else in the worktree, and
  every path outside it, is refused for reading.
- **Write set**: the scope file's `write` patterns, Python regular expressions matched
  with `re.fullmatch` against the repository-relative path; there is no default.
- **Tools without paths** (`TodoWrite`, `ToolSearch`, `SendMessage`, the agent's own
  hand-back) are allowed; a tool not known to the hook is refused for a coder.
- **A missing script.** The registration names the script in the main checkout; while the
  main checkout is on a branch without this node the hook command fails, Claude Code treats
  that as a non-blocking error, and coders run unguarded. The orchestrator keeps this node
  on every branch it checks out once the owner has enabled the hook.
- **The scope file** (`API.md`) is written by the orchestrator right after the launch, when
  the worktree name is known; until it exists every coder call is refused with "scope not
  yet published: retry this call", which the coder does.

## Acceptance criteria

- [ ] The self-test `python -X utf8 tools/coder-scope/test_coder_scope.py` feeds recorded
      hook inputs to the script and checks every decision: an orchestrator call allowed
      unread; a coder's read of its node, of an ancestor's `BOOT.md`, of a neighbour's
      `API.md`, of an extra path allowed; a read of a neighbour's source, of an ancestor's
      `ACCEPTANCE.md`, of `.claude/scopes/` refused; a write inside and outside the
      `write` patterns; a `Bash` `cat` of a foreign source and a `git -C` outside the
      worktree refused; a missing scope file refused with the retry reason.
- [ ] Each rule shown red once by a mutation of the script, recorded with the test that
      turned red.
- [ ] Enabled by the owner and seen working once on a real coder: a refused read of a
      neighbour's source in the coder's transcript, with the reason.

## Taboos

- No edit of any Claude Code settings file by a session of this tree: the registration is
  the owner's act.
- No allow-by-default for a coder, no wildcard write pattern in a scope file.
- No network, no write, no state outside the scope files.
