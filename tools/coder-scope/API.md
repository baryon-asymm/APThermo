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

Shell-path judgment treats existing bare directory names as paths. Git content reads
through `grep`, `diff`, `blame`, `archive` or `format-patch` require one or more explicit
paths following `--`, each within the read set; unscoped, empty, foreign and
pathspec-magic selections are refused. `cat-file` supports metadata `-t/-s` only.

History commands `log`, `reflog` (optionally `reflog show`), `show` and `whatchanged`
use an explicit option grammar: known metadata is allowed; patch modes and content
filters (`-S`, `-G`, pickaxe/find-object) require the same checked output paths.
Supported patch modes include `-p`, `-u`, `-U`/`-U3`, `--unified`/`--unified=3`, `-c`, `--cc`,
`--remerge-diff` and `--diff-merges=...`. Unknown history options are refused.
`-L` selectors and output-widening `--full-diff`/`--pickaxe-all` are unsupported and
refused, even with readable trailing paths; the widening guard also covers the
content-command list above. `show REV:path` checks each selected file from the worktree
root, including when the shell's cwd is nested; `./` and `../` blob selectors are
unsupported and refused. Metadata-only `show` accepts HEAD or no revision, rather
than an arbitrary object that could be a blob.

Known metadata options are:

- Output: `--oneline`, `--raw`, `--stat`, `--shortstat`, `--numstat`, `--name-only`,
  `--name-status`, `-s`, `--no-patch`, `--decorate`, `--no-decorate`, `--graph`,
  `--abbrev-commit`, `--no-abbrev-commit`, `--relative-date`, `--no-color`, `--color`,
  `--pretty`, `--abbrev`.
- Traversal: `--all`, `--branches`, `--tags`, `--remotes`, `--reverse`, `--date-order`,
  `--author-date-order`, `--topo-order`, `--first-parent`, `--no-merges`, `--merges`,
  `--boundary`, `--parents`, `--children`, `--left-right`, `--cherry`, `--cherry-pick`,
  `--cherry-mark`, `--walk-reflogs`, `-g`, `--no-walk`, `--do-walk`, `--follow`.
- Matching: `--no-renames`, `--no-ext-diff`, `--no-textconv`, `--all-match`,
  `--invert-grep`, `-i`, `--regexp-ignore-case`, `--extended-regexp`, `--fixed-strings`,
  `--perl-regexp`.
- Value options: `-n`, `--max-count`, `--skip`, `--since`, `--after`, `--until`,
  `--before`, `--author`, `--committer`, `--grep`, `--grep-reflog`, `--date`,
  `--encoding`, with a separate value or `=value`;
  digit-only `-N` and `-nN` counts are accepted too.
- Optional attached values: `--stat=...`, `--decorate=...`, `--branches=...`,
  `--tags=...`, `--remotes=...`, `--no-walk=...`, `--color=...`, `--pretty=...`,
  `--abbrev=...`. A bare optional-value option does not consume the next token.
- Required attached value: `--format=...`; bare `--format` is unsupported.

Recognized option values are consumed as values, not mistaken for subcommands or
file paths. Content options remain content-sensitive even if a later `--no-patch`
suppresses display; the classifier conservatively requires their checked paths.
The supported global prefixes are one `-C` and `--no-pager`/`--paginate`;
configuration overrides and other unparsed prefixes are refused. Other Git operations
retain the general shell-token policy; configured aliases are not expanded.
`-C` is recognized only before the subcommand. `git` and `git.exe` use the same `-C`
directory basis, local to Git's arguments. The following shell segment, the executable
path and input/output redirections retain the shell's original cwd. `< file` is a read
checked separately from Git's arguments; `>`/`>>` targets are checked as writes.
Explicit redirection targets are paths even when they are new bare basenames;
`/dev/null`, `nul` and `$null` retain their null-device exemptions.
Here-document contents and delimiters are data, not input-file paths.

`find` judges explicit roots, including `.` and `..`, or the current directory when no
root is stated. Its nested execution/confirmation actions (`-exec`, `-execdir`, `-ok`,
`-okdir`) are refused as unjudgeable; scoped name searches remain usable. Permitted
stdin pipelines and neighbour API reads retain their existing read-set policy.
The shell parser is heuristic and is not an operating-system isolation boundary.

⚠ 2026-11-17: bare directory tokens and implicit Git selections were not judged as reads.
⚠ 2026-11-17: the former content-command list missed exports, content-filtered history
and `find` roots/actions; real-CLI controls of the archived review strings exposed
those omissions. The checked command families above replace that incomplete list.
⚠ 2026-11-17: the previous patch-form promise exceeded the implementation, which
recognized only `-p`/`--patch*`, omitted reflog and trusted paths under `--full-diff`.
Frozen CLI controls also exposed an unchecked embedded `-L` selector. The bounded
history grammar and explicit refusals above replace that metadata-by-absence rule.
The additional frozen directory-basis controls corrected cwd-relative blob judgment
and the former `git.exe -C` mismatch and persistence into subsequent shell commands;
these selectors now use the bases stated above.
⚠ 2026-11-17: the first grammar incorrectly consumed the next token for optional
`--pretty`, `--abbrev`, `-U` and `--unified` values, hiding `-p` or `--full-diff`.
Actual Git probes established the attached-value forms above; `-U 3` names revision
`3`, not a context value. The correction also limits `-C` to the actual global prefix
and preserves shell redirections as independent read/write tokens. Frozen candidate
controls were red before these corrections; the owner-held v3 receipts retain them.
⚠ 2026-11-17: output redirection still used path guessing and missed a new bare
basename. The frozen `NEW_OUTPUT` root/owned-node pair exposed the difference;
explicit input and output targets now use the same direct path resolution.

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
