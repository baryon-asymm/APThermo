# Run 2 — before, 2026-09-15

## Machine

- BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9445/25H2/2025Update/HudsonValley2)
- AMD Ryzen 7 7800X3D 4.20GHz, 1 CPU, 16 logical and 8 physical cores
- .NET SDK 10.0.112, host .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v4
- GPU: NVIDIA GeForce RTX 5070 Ti, driver 616.92

## Commit

- `427fc0d` on `bench/before-clean-code` (from `7661ea9`, code = `main`'s `8e36a27`,
  before the clean-code pass)
- Build: Release

## Configuration and run order

- Toolchain=InProcessNoEmitToolchain, InvocationCount=1, IterationCount=20,
  UnrollFactor=1, WarmupCount=5, one job per group (`tests/Benchmarks/BOOT.md`,
  `## Constraints`).
- Position in the timed comparison sequence: 2nd of 3 (order after, before, after).
- Ended: 2026-09-15T12:17:22+03:00. BenchmarkDotNet total run time 7:52.

## Conditions

- Build servers shut down; `CUDA_CACHE_DISABLE=1`.
- The only other dotnet processes at the start of the sequence were the editor's
  C# Dev Kit and its test host (`SCRATCH/bench-runs/conditions.txt`).
- Sequence start: 2026-09-15T11:57:34+03:00.
