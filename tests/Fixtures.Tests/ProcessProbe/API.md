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
