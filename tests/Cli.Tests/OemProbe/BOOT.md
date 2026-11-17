# BOOT.md — Cli.Tests.OemProbe

## Purpose

A test-only process that invokes the actual CLI entry point with a controlled initial
output encoding. It is owned and stopped by the shared process runner.

## Invariants

Require redirected stdout, register the BCL code-page provider, install a process-local
`Console.Out` writer using code page 866 without changing the console's physical code
page, report that writer's encoding to standard error, and pass all arguments unchanged
to `APThermo.Cli.Program.Main`. Return that entry point's exit code. No test environment
variable or extra product option affects the CLI.

## Dependencies

[Cli](../../../src/Cli/API.md)

Outside the tree: the .NET runtime and its code-page encoding provider.

## Constraints

The parent runs this executable through `Harness.DotnetProcess`, whose deadline and
owned-scope cleanup are unchanged. This controls a .NET writer; it does not claim to
create or configure a physical Windows console. Standalone real-console evidence is
recorded separately. The executable is test-only and is not packed.

## Acceptance criteria

- [ ] The controlled entry-point process reports a code page 866 writer before invocation;
  a Unicode database path survives strict UTF-8 decoding without a BOM after `Main`.
  Restoring the old `Main` path through `Console.Out` makes the same frozen process
  test fail.

## Taboos

Do not start external processes, perform numerical work of its own, or emit a document
before the real CLI entry point. Do not change the caller's shared console.
