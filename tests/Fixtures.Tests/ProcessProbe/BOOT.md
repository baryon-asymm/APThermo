# BOOT.md — ProcessProbe

## Purpose

Controlled external .NET process for the parent's process-runner guards; no production API.

## Invariants

Each blocking delay is capped at twenty seconds; child readiness waits at most five seconds. Emit supplied fixture bytes
unchanged and write only the caller's ready/PID files. No GPU or network.

## Dependencies

None.

Outside the tree: .NET SDK/runtime inherited from the root.

## Constraints

Commands: echo writes stdout/stderr fixture files, verifies the supplied environment
value and returns the supplied exit code; block writes fixtures and PID then waits;
spawn starts one child inheriting both pipes, waits for its PID, writes parent-ready
and exits. Tree mode instead keeps that parent alive for a bounded delay so the
test can verify whole-tree termination. Child waits twenty seconds at most. Each PID file identifies this probe
only, allowing its parent test to stop it after bounded-runner assertions.

## Acceptance criteria

- [x] 2026-11-14 — Parent `DotnetProcessGuardTests` prove raw bytes/arguments/environment/
  nonzero exits, interrupted process/pipe waits and whole-tree cleanup; eleven cases
  green inside Fixtures.Tests 79/79. Kill-without-descendants mutation: one red/two
  green cases; unbounded readable excerpt mutation: one red case. Build: zero diagnostics.

## Taboos

No product calculation, unbounded wait, shell invocation or public library surface.
