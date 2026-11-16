# HISTORY.md — Fixtures.Tests

<a id="linux-terminal-state"></a>

## 2026-11-16 — from "## Invariants" — Linux terminal state

⚠ 2026-11-15: the former fresh `Process.GetProcessById(pid).HasExited` observation
could label an already exited Linux zombie as running. Hosted release run
43687839467 failed the live-child assertion; the same assertion reproduced locally.
An owned unreaped child deterministically showed kernel `Z` with HasExited=false;
the new observation distinguishes that state while direct-parent-only kill mutants
still fail both tree controls. The hosted log contains no process-state sample, so
its exact failed instant still requires confirmation by the orchestrator's rerun.
