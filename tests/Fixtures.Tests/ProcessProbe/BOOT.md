# BOOT.md — ProcessProbe

## Purpose

Controlled external .NET process for the parent's process-runner guards; no production API.

## Invariants

Each blocking delay is capped at twenty seconds; child readiness waits at most five seconds. Emit supplied fixture bytes
unchanged and write only the caller's ready/PID/identity files. No GPU or network.

## Dependencies

[ProcessOwner](../../Harness/ProcessOwner/API.md)

⚠ 2026-11-17: this section said `None`; owner-parent mode uses the bootstrap argv
and its Windows job contract, including reflection on `WindowsJob.Create`.

Outside the tree: .NET SDK/runtime inherited from the root.

## Constraints

Commands: echo writes stdout/stderr fixture files, verifies the supplied environment
value and returns the supplied exit code; block writes fixtures and PID then waits;
spawn starts one child inheriting both pipes, waits for its PID, writes parent-ready
and exits. Tree mode instead keeps that parent alive for a bounded delay so the
test can verify whole-tree termination. Child waits twenty seconds at most. Each PID file identifies this probe
only, allowing its parent test to stop it after bounded-runner assertions.

Identity sidecars precede atomically published PID markers. Each records PID, parent
from PROBE_PARENT_ID, UTC start ticks and Linux kernel start ticks. A spawned child
receives its actual fixture parent's PID. Controlled mode stays live until the caller
creates `<ready-file>.release`, with the unchanged twenty-second self-stop. Controlled
mode may append `--channel <nonce>` for its pre-start owner-substitute control. The caller
releases it only after a LIVE observation, so transition controls do not guess timing.

⚠ 2026-11-15: PID-only readiness and fixed blocking were insufficient for the
asynchronous observation controls. Identity metadata and explicit release extend
only this test probe; existing exact bytes, exit code and finite caps remain.

Owner-parent test mode starts the real bootstrap as its own child using its own PID and
birth identity, publishes the child PID atomically, and waits for an explicit release
within its existing finite bound. An optional test-gate argv triple passes only to the
bootstrap; connected-mode parent owns the server and Windows job until exit. Its requested target writes only a private marker.

## Acceptance criteria

- [x] 2026-11-14 — Parent `DotnetProcessGuardTests` prove raw bytes/arguments/environment/
  nonzero exits, interrupted process/pipe waits and whole-tree cleanup; eleven cases
  green inside Fixtures.Tests 79/79. Kill-without-descendants mutation: one red/two
  green cases; unbounded readable excerpt mutation: one red case. Build: zero diagnostics.

## Taboos

No product calculation, unbounded wait, shell invocation or public library surface.
