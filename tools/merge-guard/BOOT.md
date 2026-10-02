# BOOT.md — merge-guard

## Purpose

The orchestrator's acceptance gate for a coder's branch, one command instead of a
sequence typed by hand. It checks what the orchestrator otherwise checks by reading
(the paths the branch touched, the approved records, the documents' moved text, the
files both sides changed), then merges the branch into a throwaway worktree and runs
the linter, the build and the test sets there, and only after all of it is green
merges the branch into the current branch, with the merged tree proven equal to the
tree that was tested.

It exists because the gate was a list in the orchestrator's memory and scratch files:
a step skipped once is a step the list did not hold (the guard of the AGENTS.md 3.2
migration, `history_guard.py`, lived in a session scratchpad, and a merge-guard shell
script of 2026-09-17 was carried to the sibling tree and lost here). Decided by the
orchestrator on 2026-10-02 under the owner's word of that day to finish the 3.2 phase
and start the next one autonomously (the item agreed on 2026-09-29: coder scope
enforced by a hook and a guarded merge); an orchestrator decision, not the owner's.

The guard does not replace the line-by-line reading of a branch's `BOOT.md` diffs:
whether a condensed rule kept its meaning is not something a script can tell.

## Invariants

- **Standard library of Python 3.8+ only**, like the protocol linter: it runs on the
  reference machine as it is, Windows and WSL2.
- **Nothing leaves the machine.** The guard never pushes, fetches, tags or calls any
  network service.
- **The coder's worktree is read, never written**, and the main checkout is never left
  in a merge state: every failure after a merge started aborts it (`git merge
  --abort`). No `reset --hard`, no `--force` except on the removal of its own trial
  worktree, no deletion of a branch or of a worktree it did not create.

  ⚠ 2026-10-02: was "no `--force`" without exception, now one: `git worktree remove
  --force` of the trial worktree only. The trial merge and the build leave that
  worktree dirty and git refuses to remove a dirty worktree otherwise; nothing but the
  guard's own worktree is ever named in that call.
- **What was tested is what is merged.** The final merge is made with `--no-commit`,
  its tree (`git write-tree`) is compared with the tree of the trial merge, and it is
  committed only when the two are equal; otherwise it is aborted.
- **The trial worktree is the guard's own**: created detached in the system temp
  directory, removed on every exit path, success or failure.
- **Every check fails loudly on an empty walk**: a branch with no commit over the
  merge base, an approved-record pattern that matches no file of the tree, or a
  documents check over a node that has no `BOOT.md` is a failure, not a pass.
- **The first failing check stops the run** with one line `GUARD: <what>`, exit code
  1; the checks run in the order of `API.md`, cheapest first.

## Dependencies

None.

Outside the tree: Python 3.8+; git 2.30+ (worktrees, `merge --no-commit`); for the
trial commands the .NET SDK of `global.json`; for `--cuda` the CUDA prerequisites the
root `BOOT.md` names. The commands the trial runs are this tree's (`API.md`, the
command table): the build of `APThermo.sln`, the fast suite with `APTHERMO_NO_CUDA=1`
and `Category!=LongRunning`, and, with `--cuda`, the reference-machine proofs.

## Constraints

- **The checks and their order** are `API.md`'s; a new check goes into both documents
  and the self-test in the same change.
- **Scope.** Every path the branch changed since the merge base matches one of the
  `--scope` patterns (Python regular expressions, matched with `re.search` against the
  repository-relative path with forward slashes). There is no default scope: a call
  without `--scope` is an invocation error.
- **Approved records.** A file whose name contains `.approved.` (the bit snapshots of
  both platforms, the throughput figures, `PublicSurface.approved.txt`,
  `TreeContract.approved.txt`, and the approved outputs, which are also `.approved.json`,
  `.approved.csv` and the like: `tests/Docs.Tests/approved/`, `tests/Cli.Tests/documents/`)
  may change only when it matches an `--approve` pattern; every changed approved record is listed with its added
  and removed line counts, approved or not, so the reviewer sees the move.
- **Documents.** With `--doc-nodes`, the moved-text check of the 3.2 migration runs
  over the named nodes (the rules of `history_guard.py`, kept): every non-blank line
  removed from a named node's `BOOT.md` reappears verbatim (ignoring indentation,
  blockquote markers and repeated spaces) among the lines that any named node's
  `BOOT.md`, `HISTORY.md` or `ACCEPTANCE.md` gained; a line added to an existing
  `BOOT.md` stands in a paragraph carrying a pointer (`HISTORY.md#`,
  `→ [ACCEPTANCE.md](ACCEPTANCE.md)`, a link to a `/BOOT.md`), is a heading, or came
  verbatim from a named node's `BOOT.md`; every bare `HISTORY.md#<anchor>` in a
  named node's `BOOT.md` or `ACCEPTANCE.md` is defined in the `HISTORY.md` of that node
  or of an ancestor. New against `history_guard.py`: **`HISTORY.md` is append-only** —
  a non-blank line removed from any `HISTORY.md` of the tree is a failure, named node
  or not. The base is always the merge base, never the head of the target branch (a
  two-dot diff against a moving head reported other nodes as out of scope).

  ⚠ 2026-10-02: was "from another named node's `BOOT.md`", now any named node's, the
  node's own included, as `history_guard.py` always did: a line moved within one file
  is the same text, not a stray one. Two further facts of the port: a defined anchor is
  an `<a id="…">` or a heading's slug (the linter, run in the trial, accepts the first
  only and has the last word), and the diff is read by hunks, because a removed line
  that itself begins with two dashes prints as `---…` and `history_guard.py` skipped it
  as a file header, losing the line unseen.
- **Overlap.** A file changed both on the target since the merge base and on the
  branch must match an `--overlap` pattern.
- **Trial.** The branch is merged with `--no-ff --no-commit` into a detached worktree
  at the target's head; a conflict is a failure that names the files. The linter runs
  with `--strict`; the build must report 0 warnings and 0 errors; each test command
  must exit 0; a marker a table entry asks for (`0 Warning(s)`) does not count when a
  digit precedes it, so `10 Warning(s)` is not a clean build. The guard prints one summary line per command (the `Passed!`/`Failed!`
  lines of `dotnet test`, the linter's last line, the build's counts) and writes the
  full output to a log file whose path it prints.
- **GPU.** `--cuda` takes a lock before its first CUDA command: a file `gpu.lock` in the
  system temp directory holding a JSON object `{"owner": …, "until": ISO-8601}`. A lock
  of another owner whose `until` is in the future fails the run as `GPU busy: <owner>
  until <time>`; an expired lock is replaced. The guard removes its own lock on every
  exit path; a lock file it cannot read fails the run and is left alone. The lock is the machine-readable half of the GPU agreement between the
  sessions of this machine; the messages between them stay.
- **Target.** The guard refuses to merge into `main` (the owner merges `main`), and
  refuses when the main checkout has tracked changes or the coder's worktree (found
  through `git worktree list`) has any change.
- Code shape: the root's limits apply in spirit (a function at most 60 lines, nesting
  at most 3); the protocol tests node measures only C#, so this node holds itself to
  them by review.

## Acceptance criteria

- [x] The self-test `python -X utf8 tools/merge-guard/test_merge_guard.py` passes and
      builds its repositories in the system temp directory, never in this tree; its
      command table is replaced by stub commands (`--commands`), so it needs no .NET
      SDK and runs in under a minute (2026-10-02, `test_merge_guard.py`: 61 tests, none
      skipped, 46 s on the reference machine; every guard run is a subprocess whose
      `TMP` is a private directory).

      ⚠ 2026-10-02: was "so it runs in seconds", now under a minute: 61 tests each
      start the guard and 20 to 30 git processes on Windows, and 46 s is what that costs.
- [x] Every check of `API.md` is shown red by a named self-test, and a green branch is
      shown merged with the merged tree equal to the trial tree: scope, approved record,
      lost document line, stray document line, unresolved anchor, removed `HISTORY.md`
      line, overlap, conflict, failing command, GPU lock of another owner, dirty main
      checkout, dirty coder worktree, merge into `main`, empty walk (2026-10-02,
      `test_merge_guard.py`, one test per name: `test_a_path_outside_the_scope_fails_and_is_named`,
      `test_a_changed_approved_record_outside_approve_fails_and_is_listed`,
      `test_a_changed_approved_json_outside_approve_fails`, `test_a_lost_line_fails`,
      `test_a_stray_line_fails`, `test_an_unresolved_anchor_fails`,
      `test_a_removed_line_of_a_named_nodes_history_fails`,
      `test_a_removed_line_of_an_unnamed_nodes_history_fails`,
      `test_a_file_changed_on_both_sides_fails_without_an_overlap_pattern`,
      `test_a_conflict_fails_names_the_files_and_leaves_nothing_behind`,
      `test_a_failing_command_stops_the_run_and_leaves_nothing_behind`,
      `test_a_live_lock_of_another_owner_fails_the_run`,
      `test_a_dirty_main_checkout_is_refused`, `test_a_dirty_coder_worktree_is_refused`,
      `test_merging_into_main_is_refused`, and the empty walks
      `test_a_branch_with_no_commit_over_the_merge_base_is_an_empty_walk`,
      `test_a_branch_whose_commits_change_no_path_is_an_empty_walk`,
      `test_an_approve_pattern_that_matches_no_record_is_an_empty_walk`,
      `test_a_named_node_without_boot_is_an_empty_walk`,
      `test_a_table_that_runs_nothing_is_an_empty_walk`; the green merge:
      `test_a_green_branch_is_merged_with_the_tree_equal_to_the_trial_tree`). Each check
      was also seen red by a mutation applied to a scratch copy of the module and not
      committed (2026-10-02, 36 mutations, each turning at least one test red).
- [x] Every exit path removes the trial worktree and the guard's own GPU lock, and
      leaves the main checkout without a merge in progress: shown by self-tests that fail
      at the conflict, at a failing command and at the tree comparison (2026-10-02,
      `test_a_conflict_fails_names_the_files_and_leaves_nothing_behind`,
      `test_a_failing_command_stops_the_run_and_leaves_nothing_behind`,
      `test_the_lock_is_removed_after_a_failing_cuda_command`,
      `test_a_target_that_moved_after_the_trial_aborts_the_merge`, each ending in
      `assert_left_clean`: no trial directory or worktree, no `gpu.lock`, no merge in
      progress).
- [ ] One real use on this tree: a coder's branch accepted through the guard, its
      output kept with the run's date in the orchestrator's report.

## Taboos

- No push, fetch, tag or network call.
- No write to the coder's worktree, no `reset --hard`, no `--force` (but for the
  removal of the guard's own trial worktree, Invariants), no deletion of anything the
  guard did not create.
- No default scope and no "allow everything" pattern in the command table: the scope is
  the orchestrator's statement about one task.
- No third-party package.
