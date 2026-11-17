# HISTORY.md — Fixtures.Tests

<a id="bounded-terminal-2026-11-15"></a>

## 2026-11-17 — from "## Invariants" — bounded terminal observation

⚠ 2026-11-15: immediate post-Run descendant assertions assumed synchronous OS exit.
The recurring 84e hosted failure did not sample the kernel state. Owned handshake
controls show the immediate-observation defect on both platforms, while this remedy
observes the same terminal states within the remaining original budget. No claim
about PF_EXITING or the historical failed instant is inferred from a later pass.

<a id="pid-recheck-2026-11-15"></a>

## 2026-11-17 — from "## Invariants" — PID recheck

⚠ 2026-11-15: the former wording said a reused PID "is never targeted for teardown".
The implemented read/recheck predicate excludes observed mismatches and rechecks
immediately before requesting kill; it does not provide a kernel-atomic guarantee.

<a id="linux-terminal-state"></a>

## 2026-11-16 — from "## Invariants" — Linux terminal state

⚠ 2026-11-15: the former fresh `Process.GetProcessById(pid).HasExited` observation
could label an already exited Linux zombie as running. Hosted release run
43687839467 failed the live-child assertion; the same assertion reproduced locally.
An owned unreaped child deterministically showed kernel `Z` with HasExited=false;
the new observation distinguishes that state while direct-parent-only kill mutants
still fail both tree controls. The hosted log contains no process-state sample, so
its exact failed instant still requires confirmation by the orchestrator's rerun.
