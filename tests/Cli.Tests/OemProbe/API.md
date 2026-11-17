# API.md — Cli.Tests.OemProbe

## Controlled entry point ✅

`dotnet APThermo.Cli.Tests.OemProbe.dll <cli-arguments...>` requires redirected stdout,
sets the process-local .NET output writer to code page 866, writes
`initialWriterCodePage=866` to standard error,
then invokes the real CLI `Program.Main` with the supplied argument vector and returns
its exit code. Standard output contains only the CLI's own bytes. The parent launches
this test-only executable through the owned process runner; no physical console is
created or configured. There are no public library types.
