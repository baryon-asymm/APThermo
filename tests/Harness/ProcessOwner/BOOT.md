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
The parent separately requires safe pre-START force-reap: Windows addresses the
retained actual bootstrap process handle; on Linux the harness probes
`pidfd_send_signal` using its retained `/proc/self` directory handle before any spawn,
then immediately retains the spawned owner's proc-directory. Once the authenticated
bootstrap connects, validate retained birth/stat and exact structured channel nonce
before START. For cancellation before connection, validate before any signal; while
the nonce is unavailable, use bounded rendezvous without signalling. Unavailable capability fails
before spawn; identity mismatch or observation failure never signals. No raw PID or
descendant enumeration fallback is permitted. This capability belongs only to the test
harness; the product Linux package and the separate parent-liveness observation do not
acquire a new kernel requirement.

⚠ 2026-11-16: the parent-liveness design previously stated no new kernel requirement.
That remains true for observation itself, but safe graceful pre-START force-reap now
requires the safe-signal capability in the Linux test harness, failing closed before
launch when unavailable rather than signalling a reusable numeric PID.

⚠ 2026-11-16: pre-connect `ConnectAsync` previously waited without observing whether
the launching parent still existed. A parent crash before connection could leave an
owner indefinitely waiting even after graceful pre-START cleanup had been repaired.

If post-spawn setup fails before owner identity is validated or START is sent, keep the
private channel open for the existing five-second cleanup budget. Accept the real
bootstrap connection and close the channel before START; wait for bootstrap exit and
drain available raw streams. Never signal an unvalidated identity. A bootstrap that
cannot connect or be validated, such as an externally suspended one, yields a secondary
infrastructure failure. The safe-signal adapter supports Linux x64, the root platform.

⚠ 2026-11-16: disposal alone did not stop an unconnected owner after identity or stream
acquisition failure; bounded rendezvous closes that gap without a raw-PID fallback.

A test-only optional `--test-parent-opened <private-marker-path>` bootstrap argument
publishes an atomic marker after the actual parent identity has been validated, before
the ordinary connect/death race. Normal launches omit it and it never reaches target.
Marker failure is fail-closed. A separate real parent starts bootstrap and controls its
own finite exit; observation is not a substitute for that parent-child relationship.

Per-call private test argv, in order after `--test-parent-opened`, may specify
`--test-before-parent-open`, `--test-connect-pending`, `--test-connected`, and
`--test-control-eof`, each followed by its private gate path. A following
`--test-deadline-utc-ticks` supplies the shared positive UTC DateTime-ticks deadline.
The first three gates are before parent Open, after connect and death observation start,
and after connect completion before the final live check, respectively.
Each atomically publishes `<gate>.ready`, asynchronously waits for `<gate>.release`
only until the shared absolute test-preparation deadline supplied in argv, then
continues; timeout and I/O errors fail closed. Test teardown releases every gate. These gates
are absent from ordinary launches, do not reach target and introduce no normal deadline.

An optional `--test-control-eof` gate makes post-START EOF wait under the same test
preparation deadline before normal cleanup; actual TERMINATE stays on the ordinary path.
This test-only seam exposes direct bootstrap killing while its target and child live.
The owning control releases the gate and reaps its own identities in finally. A distinct
real parent owns the connected-mode server and Windows job; its exit closes both.

If owner exit wins setup recovery and the connection was pending at the sample before
server disposal, Linux may report `SocketException` with `OperationAborted`; treat only
that outcome as expected. An error already observed complete at the sample, or another
socket error, remains secondary. The sample and disposal are not atomic: an aborted
connection completing between them is accepted without proof that disposal caused it.

⚠ 2026-11-16: intentional server disposal also produced a native Linux aborted socket
error, which the former ObjectDisposed/OperationCanceled-only filter misclassified.
⚠ 2026-11-16: the pending sample cannot prove causal ordering of completion vs disposal;
the TOCTOU interval is explicitly admitted and covered by a deterministic control.

⚠ 2026-11-16: an eager post-Process.Start argv check can observe a pre-exec process;
retain the proc identity immediately, but check nonce only at the decision boundaries.

## Acceptance criteria

2026-11-16 CPU evidence: c0 hosted CI and Release cohorts pass on Windows and Linux;
Linux native process controls at source `524df1fa` and frozen-test red/green at `e041385d`
retain their actual identities in owner-held `fix-evidence/root/owner-524-linux/` and
`fix-evidence/root/exec-e041-baseline/`. Final c0 readiness controls are recorded in
`fix-evidence/root/readiness-c0-linux/`. The individual criteria below cite their completed controls; no hardware result is
inferred from these CPU process checks.
- [x] 2026-11-16 — Real separate parent starts bootstrap; death before parent-open, after the opened
  marker and racing a private server connection cannot start target, on both platforms.
  (2026-11-16: `OwnerLifetimeBootstrapTests` on both OSes in Release run 43861338174; `root/parent-mutants-c0-linux/index.json` and `parent-mutants-c0-windows/`.)

- [x] 2026-11-16 — A real bootstrap exits after injected post-spawn setup failure and no target runs;
  original exception and raw bytes survive, no unverified identity is signalled. A
  compiling no-rendezvous mutant fails, and inability to rendezvous is reported.
  (2026-11-16: `OwnerIdentityTests.SetupFailureRendezvousStopsRealBootstrap` on both OSes; `owner-setup-rendezvous-windows/` retains the compiling no-rendezvous red/green.)

- [x] 2026-11-16 — Linux safe force-reap probes `pidfd_send_signal` on retained `/proc/self`
  before spawning and refuses explicitly when unavailable; a capability-unavailable
  per-call seam proves no bootstrap or target started. A real wrong-channel-nonce or
  birth/stat mismatch fails without signalling. A matching retained child identity
  receives the signal, and the same handle reports ESRCH after death; no raw PID or
  descendant enumeration is used. Windows directly terminates the retained process
  handle. Execute compiling negative controls and the real controls on both OSes;
  native primitive probes alone do not prove forced PID reuse.
  (2026-11-16: `OwnerIdentityTests`, `OwnerPreStartTests`, `root/linux_bootstrap_signal_primitive.json`, `root/owner-e10-linux-mutants/index.json` and both parent-mutant receipts.)

- [x] 2026-11-16 — On Windows and Linux, real controls prove wrong birth identity and a parent dead
  before opening fail closed before target launch; a validated parent dying after open
  ends a still-unconnected bootstrap. Exercise connection/death race, a valid START,
  and four concurrent launches without inherited or cross-retained handles. Actual
  compiling negative controls must turn red for missing liveness or accepted mismatch;
  the native primitive probes alone do not prove forced PID reuse. Record source-bound
  evidence on each OS and preserve the existing deadlines and exact output/token rules.
  (2026-11-16: `OwnerIdentityTests`, `OwnerLifetimeBootstrapTests` and `OwnedProcessGuardTests.FourConcurrentOwnersKeepStreamsAndChannelsSeparate`; both parent-mutant receipts and Release run 43861338174.)

- [x] 2026-11-16 — Real target/descendant controls on both operating systems prove exit-code and
  byte-exact output preservation, actual target PID, completion, timeout, cancellation, failure to join,
  cleanup after target exit even when a live descendant closed both streams, expected
  owner death after cleanup, a checked empty-job/no-live-group postcondition, and no
  accidental handle inheritance, including four simultaneous distinct channels. An
  owner crash after RELEASE but before group kill cannot report success. Compiling mutations
  reproduce the old leak and fail; host-specific evidence identifies source and OS.
  (2026-11-16: `OwnedProcessGuardTests`, `OwnerChannelTests`, `OwnerPreStartTests` and the cross-platform owner receipts in `fix-evidence/process-owned-worker-97a0/` and `root/owner-524-linux/index.json`.)

## Taboos

- No global mutable process state, unbounded waits, hidden retries, policy relaxation,
  or promise to terminate descendants outside the owned job/group.
