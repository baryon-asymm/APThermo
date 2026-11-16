# API.md — ProcessOwner

## Bootstrap contract ⏳

The standalone BCL bootstrap is started by the Harness owner with a structured argv:

```text
--channel <pipe-name> --parent-pid <positive decimal> --parent-birth <decimal> [--job-name <name>] -- <executable> <argument>...
```

The executable and every argument are passed without shell interpolation, including
empty arguments. The caller supplies the target working directory, environment and
stdin/stdout/stderr semantics. The parent creates one private full-duplex named pipe
with a short random unique name and `CurrentUserOnly`; its endpoints are non-inheritable.
The bootstrap connects as client. The control/status channel is never inherited by the
target or another concurrently started bootstrap. On Windows the parent creates a unique named job,
configures kill-on-last-handle-close and retains a non-inheritable handle; the bootstrap
opens it, assigns itself before starting the target, then closes its own job handle.
Job-creation collisions, configuration, opening or assignment failures fail closed.
On Linux the bootstrap creates its own session and process group before target start.

Before launching bootstrap, the parent captures its own OS birth identity. The
`--parent-pid` argument is its positive decimal PID; `--parent-birth` is the unsigned
decimal process creation FILETIME on Windows or decimal `/proc/self/stat` field 22
start ticks on Linux. The bootstrap opens a non-inheritable stable observation of that
specific PID, validates its birth value and that it is live, and refuses before START
if opening, validation, observation, parsing or identity matching fails. It never
signals the parent PID. On Windows it retains a query/synchronize process handle,
checks creation FILETIME and observes death with `WaitForSingleObject`. On Linux it
opens `/proc/<pid>` with `O_DIRECTORY|O_CLOEXEC`, then reads `stat` relative to that
retained directory handle via `openat`; ESRCH or terminal Z/X means exited. Once
validated it never reopens the numeric PID path, so PID reuse cannot turn a different
process into the observed parent. Other observation errors fail closed, never alive.

Before the channel connects, race cancellable asynchronous `ConnectAsync` against
parent-death observation. A short polling cadence such as 100 ms on Linux is only an
observation cadence, not a new timeout; no dedicated monitoring thread is required.
If parent death or observation failure wins, close the channel and exit nonzero without
starting the target. If connection wins, cancel and observe the monitoring task and
close its retained handle; connection alone never authorizes launch, which still needs
a valid START frame. Once connected, parent-channel EOF governs owner cleanup as below.
Early parse, open, identity, connection and cancellation failures also close handles
and cancel and observe any monitoring task. Do not introduce a shorter connection
deadline or change the caller deadline, five-second cleanup, output, token or handle
inheritance semantics.

Both directions share the duplex pipe. Each frame is `APTO` (four ASCII bytes), version `1` (one byte),
one message-type byte, a four-byte unsigned little-endian payload length, then exactly
that many payload bytes. Payload length is at most 4096. Unknown versions/types,
invalid lengths, incomplete frames and unexpected message order fail closed. Types:

| Direction | Type byte | Payload | Meaning |
|---|---:|---|---|
| owner → parent | 1 | empty | READY: ownership established, no target started |
| parent → owner | 2 | empty | START: launch the target |
| parent → owner | 3 | empty | ABORT: do not launch; clean up and exit |
| owner → parent | 4 | signed 32-bit little-endian | EXIT: target's logical exit code |
| parent → owner | 5 | empty | RELEASE: target exited and streams drained; terminate remaining scope |
| owner → parent | 6 | stage byte, signed 32-bit little-endian OS error | ERROR: setup or target launch failed |
| parent → owner | 7 | empty | TERMINATE: terminate the owned job/group |
| owner → parent | 8 | positive signed 32-bit little-endian | STARTED: target process ID for diagnostics |

Stages in ERROR are 1 ownership setup, 2 target launch and 3 control/protocol failure.
The server accepts one bootstrap connection under the original deadline; the bootstrap
sets up ownership and connects before READY. A timeout before READY must stop/reap the
bootstrap, which has not received START and cannot have launched the target. Before
any START write begins, the parent closes control and directly terminates and reaps
only its retained bootstrap identity, even if it never connected or entered the
job/session. Windows retains the actual non-inheritable process handle and terminates
through that handle, without enumerating descendants. Linux test infrastructure first
opens a retained `/proc/self` directory handle and probes `pidfd_send_signal` with
signal 0 before `Process.Start`; unsupported, unavailable or refused capability is an
explicit infrastructure failure before any bootstrap or target is spawned. After
launch the parent opens one `O_DIRECTORY|O_CLOEXEC` `/proc/<childPID>` directory handle
and, through that retained view, validates birth/stat and the exact per-operation
random channel nonce in the structured command line before signalling. A mismatch or
observation error fails closed without signalling. Once the child view is validated,
never reopen its numeric PID; use `pidfd_send_signal` on that retained handle alone,
with ESRCH indicating that the original identity ended. Do not fall back to raw PID or
descendant enumeration. The test surrogate must carry the exact nonce in its argv,
or the control must use the real bootstrap. This Linux x64 capability is a test-harness
requirement only, not a requirement of the product package. No Linux Arm64 adapter
is claimed. Parent-liveness monitoring
is the separate proc-directory/openat observation described above.
The parent sets START-may-have-been-sent before beginning the write; a possibly partial
write uses owned-scope termination and verified postcondition, not that direct path.
The parent
checks pre-cancellation before bootstrap launch and again before START; cancellation after
START may race target creation, so the already-owned scope is terminated and reaped.
After START and successful target creation the bootstrap sends STARTED exactly once, before
EXIT. Its payload is the target PID, never the bootstrap PID; it is diagnostic only
and is not an ownership or termination identity. On target exit the bootstrap sends
EXIT but remains alive as the Linux group anchor;
it closes only its own stdout/stderr handles after target creation, allowing stream EOF
when target descendants close theirs. After EXIT and both output EOFs, RELEASE must
terminate all remaining ordinary owned descendants, even if they closed their streams.
The parent returns only after the owned scope and bootstrap have ended, all within the
original deadline. Linux group SIGKILL also terminates the bootstrap: an abnormal owner
exit is expected only after the parent has issued RELEASE or TERMINATE; preserve and
return the previously received target exit code. An acknowledgement or owner exit alone
does not prove cleanup: before success verify the retained Windows job has no active
processes, or on Linux that the owned session/group has no live members (terminal Z/X
states count as ended under R14). An unknown postcondition, live member, or owner exit
before authorized cleanup is an infrastructure error, not a successful target exit.
Do not signal a potentially reused or foreign PID or process group to perform this check.

ERROR, status EOF before authorized cleanup, an incomplete or malformed frame, or an
invalid sequence is an error; EOF caused by authorized owner termination is expected. Control EOF before START cannot launch the
target; after START it triggers owned-scope termination. If cancellation races READY,
the parent sends ABORT or closes control before START when possible; once START may
have been sent it terminates the scope before returning. After a primary timeout or
cancellation, cleanup uses its own independent deadline of at most five seconds, not
the already-cancelled primary token, and retains the primary failure and partial output.

A successful Linux cleanup additionally requires that no live process remains in the
original session, including a member that changed process group. Observation treats
terminal Z/X states as exited and never signals a numeric PID or group obtained from
that observation. An escaped group member may therefore make cleanup fail rather than
produce a false success.

The guarantee covers ordinary descendants remaining in the job/group. It does not
claim to stop an intentionally escaped group member, a process created outside the
Windows job or descendants after external destruction of the owner. The native calling conventions, non-inheritance and lifecycle must be tested on each
OS. On .NET 10 Unix, CurrentUserOnly enforces the peer user but does not promise that
the backing socket file itself has mode 0600; do not claim the .NET 11 behavior.

## Parent (tree contract) ✅

The standalone owner assembly grants its owning Harness assembly access to the
internal framing and Windows-job types; it does not reference Harness. The shared
implementation is compiled once in the owner assembly.

```csharp
internal static class Wire
{
    internal const byte Ready = 1;
    internal const byte Start = 2;
    internal const byte Abort = 3;
    internal const byte Exit = 4;
    internal const byte Release = 5;
    internal const byte Error = 6;
    internal const byte Terminate = 7;
    internal const byte Started = 8;
    internal static Task SendAsync(Stream stream, byte kind, ReadOnlyMemory<byte> payload, CancellationToken token);
    internal static Task<(byte Kind, byte[] Payload)> ReadAsync(Stream stream, CancellationToken token);
    internal static byte[] Code(int value);
}

internal sealed class WindowsJob : IDisposable
{
    internal static WindowsJob Create(string name);
    internal static void Join(string name);
    internal static void StopNamed(string name);
    internal int ActiveProcesses();
    internal void Stop();
    public void Dispose();
}
```

## Internal identity test contract ✅

The fixtures node uses reflection for `APThermo.Harness.ProcessOwner.ParentLifetime`:
`SelfBirth()` returns `ulong`; `Open(int pid, ulong birth)` returns an `IDisposable`
retained observation; `IsLive()` returns `bool` and `WaitForExitAsync(CancellationToken)`
returns `Task`. `APThermo.Harness.ProcessOwner.Program.ConnectWhileParentLivesAsync`
accepts `(NamedPipeClientStream pipe, ParentLifetime lifetime)` and returns `Task`.
Linux-only `APThermo.Harness.ProcessOwner.LinuxProcessIdentity.Open(int pid)` returns
a retained `IDisposable` identity; `RequireChannel(string channel)` validates the
nonce and `Send(int signal)` returns whether the identity still exists. These internal
test contracts are not public package interfaces.
