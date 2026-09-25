# BOOT.md — Benchmarks/Runner

## Purpose

The console entry point of the benchmarks node. It exists because the root's
Diagnostics constraint (CA1515) forbids public types in an executable and BenchmarkDotNet
requires public benchmark classes: the classes live in the parent node's library, and
this node is the executable that runs them. Decided with the owner on 2026-09-25.

## Invariants

- The node holds one type, `Program`, internal, with `Main` and nothing else.
- `Main` runs `BenchmarkSwitcher.FromAssembly` over the parent's assembly with the
  parent's `BenchmarkEnvironment.Config`, and returns 1 when any summary carries a
  critical validation error, 0 otherwise. This is the behaviour of the parent's former
  `Program`, unchanged.
- It measures nothing, configures nothing and selects nothing by itself: every choice of
  what to run comes from the command-line arguments, as before.

## Dependencies

[Benchmarks](../API.md)

Outside the tree: BenchmarkDotNet 0.15.8, pinned in `Directory.Packages.props`.

## Constraints

Inherited from the root and from the parent ([BOOT.md](../BOOT.md)). In addition:

- Project `APThermo.Benchmarks.Runner` (`tests/Benchmarks/Runner/APThermo.Benchmarks.Runner.csproj`),
  namespace `APThermo.Benchmarks.Runner`, an executable in the solution, x64 like its
  parent, no test SDK. It references the parent's project and BenchmarkDotNet only.
- Whatever the parent's benchmarks need at run time (the `data/user-states.json` input,
  ILGPU's native libraries) reaches this project's output through the project
  reference; the runner copies nothing of its own.

## Acceptance criteria

- [ ] The project builds at 0 warnings and 0 errors under the root's Diagnostics
      constraint, with no suppression.
- [ ] `dotnet run -c Release --project tests/Benchmarks/Runner -- --list flat` lists the
      same benchmarks as the parent's former entry point, and the dry run of the parent's
      criterion completes.

## Taboos

- No benchmark, configuration or input in this node: they belong to the parent.
- No public type.
