# BOOT.md — ProcessOwner

## Purpose

Establish owned process scope before a target starts and keep its owner available for
bounded cleanup after that target exits. This is test infrastructure, not product code.

## Invariants

- The target starts only after ownership is established; no shell reinterprets arguments.
- Its exit code, stdin, stdout, stderr, working folder and environment retain their semantics.
- Ownership covers ordinary descendants remaining in the Windows job or Linux group.
  A deliberate escape or destruction of the owner is outside this guarantee.
- Failure to establish ownership is explicit and occurs before the target executes.

## Dependencies

None.

Outside the tree: .NET 10 runtime and the Windows or Linux native process APIs. No new
required command-line tool, analyzer suppression or `partial` declaration.

## Constraints

The parent's owned-process design is binding. The launcher is a standalone BCL program:
it must not reference the Harness assembly that builds and invokes it. Bootstrap
status/control reports the actual target PID for preserved diagnostics and uses one
`CurrentUserOnly` duplex named pipe with a random name and
non-inheritable endpoints, distinct from the target's inherited stdout and stderr. On Linux the live
bootstrap anchors the session/group until released; on Windows the parent owns the job
handle and the bootstrap joins it before launching the target. Close unneeded control,
job and standard-stream handles so children do not accidentally retain them. The
bootstrap alone owns its redirected stdout/stderr: Linux identifies matching writable
close-on-exec duplicates by pipe identity, checks the identity again before closing,
and does not use the runtime standard output or error after closure. No other bootstrap
code concurrently closes or reassigns those descriptors. The original deadline includes
bootstrap startup and both output streams; bounded cleanup
uses at most the existing five seconds. Implement only after a native interop probe has
compiled with the project's analyzers on both supported OSes. The exact argv, framed
control protocol, target exit/owner lifetime and cancellation rules are in API.md.

## Acceptance criteria

- [ ] Real target/descendant controls on both operating systems prove exit-code and
  byte-exact output preservation, actual target PID, completion, timeout, cancellation, failure to join,
  cleanup after target exit even when a live descendant closed both streams, expected
  owner death after cleanup, a checked empty-job/no-live-group postcondition, and no
  accidental handle inheritance, including four simultaneous distinct channels. An
  owner crash after RELEASE but before group kill cannot report success. Compiling mutations
  reproduce the old leak and fail; host-specific evidence identifies source and OS.

## Taboos

- No global mutable process state, unbounded waits, hidden retries, policy relaxation,
  or promise to terminate descendants outside the owned job/group.
