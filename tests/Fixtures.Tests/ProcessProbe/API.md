# API.md — ProcessProbe

## CLI ✅

`dotnet APThermo.Fixtures.Tests.ProcessProbe.dll <mode> <stdout-file> <stderr-file>
<environment-value> <exit-code> <ready-file>`.

Echo: exact supplied output/error bytes, environment PROBE_VALUE equality, selected
exit. Block: same bytes, own PID in ready-file, bounded twenty-second delay.
Spawn: one bounded child inherits streams; child PID in ready-file, parent writes
ready-file.parent and exits after child readiness. Tree mode keeps the parent alive
for a bounded delay instead, proving descendant termination when the parent is live.
Each blocking delay is at most twenty seconds, readiness at most five. No public C# declarations.

## Identity and controlled release ✅

Before publishing each PID marker, write `<marker>.identity.json` with ProcessId,
ParentProcessId (from PROBE_PARENT_ID), StartTimeUtcTicks and LinuxStartTicks (null
outside Linux). The marker is published atomically after that sidecar. Spawn sets
PROBE_PARENT_ID to the owning parent probe's PID for its child.
Controlled mode writes the same bytes/readiness and waits for `<ready-file>.release`,
then returns the supplied exit code. Its finite self-stop stays twenty seconds;
readiness still stays within five. Existing modes and their byte/exit contracts remain.

⚠ 2026-11-15: explicit identity and release replace PID-only timing assumptions in
the owning test controls; they are not a production process-runner interface.

Controlled mode also accepts `--channel <nonce>` after its six normal arguments.
This controlled substitute carries the owning operation's exact nonce for Linux
identity validation; it keeps the same bytes, readiness and bounded release behavior.

## Bootstrap parent test mode ⏳

`owner-parent <owner-assembly> <channel> <opened-marker> <target-marker> <ready-marker>`
starts the actual owner as this process's own child with the optional opened marker,
its own PID and birth identity, and a target that creates target-marker. It publishes
ready-marker and its child PID atomically, then exits upon ready-marker.release or
after the existing twenty-second finite bound. On Windows it names the caller's job
with the channel. This mode never starts a numerical workload.
