# BOOT.md — .github/diagnostics/IsaProbe

## Purpose

A console program that prints the instruction-set facts .NET itself sees on the machine it
runs on: the process architecture, the operating system, whether `Avx2`, `Fma` and
`Avx512F` are supported, and the logical core count. The runner-diagnostics action
(`../../actions/runner-diagnostics`) runs it in every job of both workflows once the .NET
SDK is set up, so that a bit-snapshot difference on a hosted runner can be read back
against the hardware and the runtime that produced it (the hosted-runner bit investigation
of 2026-09-18, root `BOOT.md`, platform constraint). It is configuration's tool, not the
product's.

## Invariants

- **It only reads and prints.** The program reads `System.Runtime.InteropServices` and
  `System.Runtime.Intrinsics.X86` facts and writes six lines to the standard output; it
  touches no file and no network, and returns no exit code of its own other than 0.
- **It is not part of the product.** It never enters `APThermo.sln`, no node depends on it
  and no package carries it; it is run directly with `dotnet run --project`.

## Dependencies

None.

Outside the tree: the .NET SDK of `global.json` (`System.Runtime.InteropServices`,
`System.Runtime.Intrinsics.X86`).

## Constraints

- The root's Diagnostics constraint binds it like every project: `Directory.Build.props`
  applies, the maximum analyzers run, nothing is suppressed, and `DiagnosticsTests` reads
  its files.
- Its output is the contract the runner-diagnostics action prints into a job's log: one
  `Name: value` line per fact, in the order of [API.md](API.md). A fact is added by
  appending a line, never by reformatting the existing ones.
- The probe holds no type of the tree and no named type of its own beyond the top-level
  program; there is nothing for the reflection checks of `AGENTS.md` §13 to measure.

## Acceptance criteria

- [x] 2026-10-01 — The program builds under the root's settings with 0 warnings and prints
      the six lines of [API.md](API.md) on the reference machine (Windows 11 x64):
      `dotnet run --project .github/diagnostics/IsaProbe --configuration Release`.
- [ ] A hosted runner's log carries the six lines in a job of each workflow: the runner
      diagnostics step of a CI run and of a dispatch run of the release workflow, after the
      comment-only edits of 2026-10-01 (`../BOOT.md`, `## Acceptance criteria`).

## Taboos

- No assertion and no exit code other than 0: a diagnostic that fails a job is a check, and
  a check belongs in a test node.
- No reference to a node of the tree: the probe must run on a machine that has built
  nothing else.
