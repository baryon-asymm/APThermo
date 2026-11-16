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
- Before connection, the bootstrap observes its launching parent's stable PID and
  birth identity and exits without START if the parent dies or identity is invalid;
  observation errors fail closed, and a successful connection still requires START.

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
Before launch the parent passes its PID and birth identity using the API's exact argv.
Windows retains a non-inheritable query/synchronize handle and validates the creation
FILETIME; Linux retains an `O_DIRECTORY|O_CLOEXEC` `/proc/<pid>` directory handle and
reads `stat` through `openat`, without reopening the numeric PID after validation.
Death, terminal Z/X, identity mismatch or observation error fail closed before START;
never signal the parent. Race the pre-connect channel operation against cancellable
asynchronous liveness observation, without a dedicated thread or a new deadline; a
100 ms observation cadence is not a timeout. Cancel and observe the liveness task and
close every handle on successful connect and every early error. After connection the
existing channel-EOF cleanup governs owner lifetime. Preserve the original caller
and cleanup deadlines, output bytes, cancellation token and non-inheritance guarantees.

⚠ 2026-11-16: pre-connect `ConnectAsync` previously waited without observing whether
the launching parent still existed. A parent crash before connection could leave an
owner indefinitely waiting even after graceful pre-START cleanup had been repaired.

## Acceptance criteria

- [ ] On Windows and Linux, real controls prove wrong birth identity and a parent dead
  before opening fail closed before target launch; a validated parent dying after open
  ends a still-unconnected bootstrap. Exercise connection/death race, a valid START,
  and four concurrent launches without inherited or cross-retained handles. Actual
  compiling negative controls must turn red for missing liveness or accepted mismatch;
  the native primitive probes alone do not prove forced PID reuse. Record source-bound
  evidence on each OS and preserve the existing deadlines and exact output/token rules.

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
