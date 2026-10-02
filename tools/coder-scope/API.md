# API.md — coder-scope

The node exposes a hook command, a scope file format and the registration the owner adds
to the settings. Everything else is internal and may change.

## The hook command ✅

```console
$ python -X utf8 <repo>/tools/coder-scope/coder_scope.py [--repo <repo>] [--coder-types sonnet-coder]
```

Reads one `PreToolUse` hook input (JSON) from standard input. An allowed call: exit 0,
nothing printed. A refused call: exit 0 and, on standard output,

```json
{"hookSpecificOutput": {"hookEventName": "PreToolUse", "permissionDecision": "deny",
  "permissionDecisionReason": "coder-scope: <rule>: <path>. <what to do instead>"}}
```

`--repo` defaults to the repository that holds the script; `--coder-types` (comma-
separated) defaults to `sonnet-coder`. An invocation error (unreadable input) exits 2, which
Claude Code treats as a refusal with the error text.

## The scope file ✅

`<repo>/.claude/scopes/<worktree-name>.json`, written by the orchestrator after launching
the coder (the worktree name is `agent-<id>` for a harness worktree):

```json
{
  "nodes": ["src/Equilibrium", "tests/Equilibrium.Tests"],
  "write": ["src/Equilibrium/.*", "tests/Equilibrium\\.Tests/.*"],
  "read": ["C:/Users/<u>/AppData/Local/Temp/claude/<...>/scratchpad/ARBITER-EQUILIBRIUM.md"],
  "task": "Split Equilibrium into three children"
}
```

| Field | Meaning |
|---|---|
| `nodes` | the granted nodes: every file under them is readable; their ancestors' `BOOT.md` too |
| `write` | patterns (`re.fullmatch`, repository-relative, forward slashes) of the writable paths |
| `read` | extra readable paths, absolute or repository-relative, exact files or directories |
| `task` | free text, quoted in refusals |

The hook refuses every coder call, naming the file, when the scope file is not JSON, lacks
one of the three lists, holds a `write` pattern that is not a regular expression or that
matches `AGENTS.md`, `BOOT.md` or `.claude/settings.local.json` (a wildcard pattern), or
names as a node the repository root or a path that leaves it.

## The registration (the owner's step) ✅

In `.claude/settings.local.json` of the main checkout (or the user settings), with the
absolute path of this checkout:

```json
{
  "hooks": {
    "PreToolUse": [
      { "matcher": "*",
        "hooks": [ { "type": "command",
                     "command": "python -X utf8 C:/Projects/AerospacePropellantThermodynamics/tools/coder-scope/coder_scope.py" } ] }
    ]
  }
}
```

Removing the entry disables the hook; nothing else depends on it.
