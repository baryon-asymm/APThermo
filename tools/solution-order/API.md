# API.md — solution test ordering

## Self-test ✅

`python -X utf8 tools/solution-order/test_solution_order.py` runs bounded synthetic
solution controls against the actual root `Directory.Solution.targets`. It reports
normal test success or nonzero failure; it does not execute product tests or CUDA.

## Build barrier ✅

The root solution-level target runs solution `Build` before `VSTest` unless the caller
sets `VSTestNoBuild=true`, as `dotnet test --no-build` does. Project-only commands and
the caller's test filter are not changed. See [BOOT.md](BOOT.md) for proof scope.
