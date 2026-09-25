# API.md — Benchmarks/Runner

The node is run from the command line; no node of the tree uses it, and it exposes no
type (its one type, `Program`, is internal).

## Invocation ⏳

⏳ until the split of 2026-09-25 is coded.

```console
dotnet run -c Release --project tests/Benchmarks/Runner -- [BenchmarkDotNet arguments]
# e.g. --list flat, --filter *UserStates*, --job Dry
```

The exit code is 1 when any summary carries a critical validation error, 0 otherwise.
