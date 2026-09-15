# Comparison, 2026-09-15

Timed comparison run of the reference machine (`tests/Benchmarks/BOOT.md`, `## Constraints`, `## Run conditions`), order after, before, after, one configuration (5 warmup / 20 measured iterations, in-process toolchain):

- after-1: `results/2026-09-15-8f99d11-run1/` -- commit `8f99d11`, `claude/benchmarks`.
- before-2: `results/2026-09-15-427fc0d-run2/` -- commit `427fc0d`, `bench/before-clean-code` (from `7661ea9`, code = `main`'s `8e36a27`).
- after-3: `results/2026-09-15-8f99d11-run3/` -- commit `8f99d11`, `claude/benchmarks`.

Build servers were shut down and `CUDA_CACHE_DISABLE=1` was set for the whole sequence; the only other dotnet processes were the editor's C# Dev Kit and its test host. Each run's `run.md` carries the full detail.

The 99 % confidence half-interval below is Student's t for n - 1 = 19 degrees of freedom (n = 20, `IterationCount` in every report CSV), t = 2.8609, applied as `t * StdDev / sqrt(n)` to each CSV's own `Mean` and `StdDev` columns -- no figure is typed in from elsewhere. `ratio` is each after run's mean divided by the before mean. `slower`/`faster` marks a row where the before interval overlaps neither after interval and both after means move the same way; `drift` marks a row where the two after intervals do not overlap each other.

## Group 1 -- batch throughput

| Benchmark | Before (427fc0d) | After-1 (8f99d11) | After-3 (8f99d11) | Ratio 1/before | Ratio 3/before | Mark |
|---|---|---|---|---|---|---|
| SolveBatch / 1000 / Cpu | 37.770 +/- 1.123 ms | 36.250 +/- 1.086 ms | 35.870 +/- 1.305 ms | 0.960 | 0.950 | - |
| SolveBatch / 1000 / Cuda | 16.160 +/- 0.457 ms | 16.070 +/- 0.375 ms | 15.600 +/- 0.164 ms | 0.994 | 0.965 | - |
| SolveBatch / 10000 / Cpu | 376.19 +/- 6.88 ms | 351.30 +/- 8.45 ms | 335.64 +/- 3.68 ms | 0.934 | 0.892 | faster, drift |
| SolveBatch / 10000 / Cuda | 18.560 +/- 0.397 ms | 18.600 +/- 0.376 ms | 17.770 +/- 0.170 ms | 1.002 | 0.957 | drift |
| SolveBatch / 100000 / Cpu | 3,609.45 +/- 26.62 ms | 3,470.44 +/- 21.22 ms | 3,362.50 +/- 11.46 ms | 0.961 | 0.932 | faster, drift |
| SolveBatch / 100000 / Cuda | 123.33 +/- 0.89 ms | 139.45 +/- 3.37 ms | 130.82 +/- 2.64 ms | 1.131 | 1.061 | slower, drift |

## Group 2 -- problem kinds, CPU accelerator

| Benchmark | Before (427fc0d) | After-1 (8f99d11) | After-3 (8f99d11) | Ratio 1/before | Ratio 3/before | Mark |
|---|---|---|---|---|---|---|
| Solve / False / Hp | 0.2793 +/- 0.0216 ms | 0.3022 +/- 0.0191 ms | 0.2813 +/- 0.0113 ms | 1.082 | 1.007 | - |
| Solve / False / RocketFrozenAtChamber | 0.4230 +/- 0.0220 ms | 0.4404 +/- 0.0193 ms | 0.4434 +/- 0.0349 ms | 1.041 | 1.048 | - |
| Solve / False / RocketShiftingEquilibrium | 0.4978 +/- 0.0465 ms | 0.5329 +/- 0.0388 ms | 0.5196 +/- 0.0347 ms | 1.071 | 1.044 | - |
| Solve / False / RocketFrozenAtThroat | 0.4438 +/- 0.0468 ms | 0.4465 +/- 0.0205 ms | 0.4548 +/- 0.0209 ms | 1.006 | 1.025 | - |
| Solve / False / Sp | 0.2694 +/- 0.0144 ms | 0.3089 +/- 0.0174 ms | 0.3000 +/- 0.0167 ms | 1.147 | 1.114 | - |
| Solve / False / Tp | 1.334 +/- 0.083 ms | 1.159 +/- 0.105 ms | 1.089 +/- 0.066 ms | 0.869 | 0.817 | - |
| Solve / True / Hp | 0.4649 +/- 0.0287 ms | 0.4885 +/- 0.0237 ms | 0.5148 +/- 0.0489 ms | 1.051 | 1.107 | - |
| Solve / True / RocketFrozenAtChamber | 0.6735 +/- 0.0488 ms | 0.6839 +/- 0.0412 ms | 0.6645 +/- 0.0297 ms | 1.015 | 0.987 | - |
| Solve / True / RocketShiftingEquilibrium | 0.6921 +/- 0.0367 ms | 0.7361 +/- 0.0385 ms | 0.7239 +/- 0.0345 ms | 1.064 | 1.046 | - |
| Solve / True / RocketFrozenAtThroat | 0.6914 +/- 0.0351 ms | 0.7499 +/- 0.0607 ms | 0.6807 +/- 0.0451 ms | 1.085 | 0.985 | - |
| Solve / True / Sp | 0.4671 +/- 0.0324 ms | 0.5087 +/- 0.0281 ms | 0.5202 +/- 0.0378 ms | 1.089 | 1.114 | - |
| Solve / True / Tp | 2.506 +/- 0.113 ms | 2.001 +/- 0.104 ms | 2.013 +/- 0.065 ms | 0.798 | 0.803 | faster |

## Group 3 -- one case through Solver

| Benchmark | Before (427fc0d) | After-1 (8f99d11) | After-3 (8f99d11) | Ratio 1/before | Ratio 3/before | Mark |
|---|---|---|---|---|---|---|
| Solve | 0.2564 +/- 0.0157 ms | 0.2767 +/- 0.0121 ms | 0.2747 +/- 0.0160 ms | 1.079 | 1.071 | - |

## Group 4 -- one-time costs

| Benchmark | Before (427fc0d) | After-1 (8f99d11) | After-3 (8f99d11) | Ratio 1/before | Ratio 3/before | Mark |
|---|---|---|---|---|---|---|
| AssembleChemicalSystem | 0.5366 +/- 0.0064 ms | 0.4736 +/- 0.0425 ms | 0.4560 +/- 0.0268 ms | 0.883 | 0.850 | faster |
| CompileCpuKernel | 170.33 +/- 7.59 ms | 284.28 +/- 4.94 ms | 282.33 +/- 7.82 ms | 1.669 | 1.658 | slower |
| CompileCudaKernel | 2,973.65 +/- 36.12 ms | 2,980.35 +/- 20.51 ms | 2,953.92 +/- 17.68 ms | 1.002 | 0.993 | - |
| LoadDatabase | 10.796 +/- 0.157 ms | 11.073 +/- 0.178 ms | 10.836 +/- 0.224 ms | 1.026 | 1.004 | - |
| UploadSpeciesTable | 0.0108 +/- 0.0009 ms | 0.0108 +/- 0.0012 ms | 0.0102 +/- 0.0008 ms | 0.995 | 0.941 | - |

## Group 6 -- user state runs

| Benchmark | Before (427fc0d) | After-1 (8f99d11) | After-3 (8f99d11) | Ratio 1/before | Ratio 3/before | Mark |
|---|---|---|---|---|---|---|
| SolveStates / Cpu / All | 23.583 +/- 0.377 ms | 23.130 +/- 0.402 ms | 22.214 +/- 0.352 ms | 0.981 | 0.942 | drift |
| SolveStates / Cpu / Record1 | 5.871 +/- 0.199 ms | 5.481 +/- 0.252 ms | 5.582 +/- 0.198 ms | 0.934 | 0.951 | - |
| SolveStates / Cpu / Record2 | 5.684 +/- 0.244 ms | 5.653 +/- 0.307 ms | 5.502 +/- 0.246 ms | 0.995 | 0.968 | - |
| SolveStates / Cpu / Record3 | 3.039 +/- 0.133 ms | 3.151 +/- 0.153 ms | 2.995 +/- 0.192 ms | 1.037 | 0.986 | - |
| SolveStates / Cpu / Record4 | 10.548 +/- 0.252 ms | 9.919 +/- 0.379 ms | 9.772 +/- 0.406 ms | 0.940 | 0.926 | - |
| SolveStates / Cuda / All | 200.79 +/- 1.51 ms | 186.70 +/- 4.09 ms | 176.33 +/- 1.15 ms | 0.930 | 0.878 | faster, drift |
| SolveStates / Cuda / Record1 | 84.058 +/- 0.683 ms | 83.103 +/- 1.937 ms | 78.692 +/- 0.539 ms | 0.989 | 0.936 | drift |
| SolveStates / Cuda / Record2 | 84.285 +/- 0.563 ms | 81.894 +/- 1.487 ms | 76.763 +/- 0.537 ms | 0.972 | 0.911 | faster, drift |
| SolveStates / Cuda / Record3 | 43.159 +/- 0.083 ms | 41.756 +/- 1.031 ms | 40.278 +/- 0.079 ms | 0.967 | 0.933 | faster, drift |
| SolveStates / Cuda / Record4 | 191.03 +/- 1.35 ms | 178.95 +/- 3.28 ms | 167.09 +/- 1.26 ms | 0.937 | 0.875 | faster, drift |

## Same-work verdict (step 2)

Every solving benchmark's per-configuration status line and results hash (`ResultHash`, the `Harness` bit hash) were read from the three run logs (`SCRATCH/bench-runs/after-1.log`, `before-2.log`, `after-3.log`).

- **CPU accelerator: bit-for-bit equal**, before and both after runs, for every configuration: group 1 (`BatchThroughput`, `Cpu`, 1000/10000/100000 cases), group 2 (`ProblemKind`, all six kinds, transport on and off), group 3 (`SingleCase`), group 6 (`UserStates`, `Cpu`, `All`/`Record1`-`Record4`). No CPU-accelerator inequality was found; every group stays a pass on this ground.
- **CUDA: statuses and iteration counts equal**, before and after, on every sampled station; **the results hashes differ** between before and after (the after-1 and after-3 hashes are identical to each other on every CUDA configuration, so the after build is itself deterministic). This is the last-ULP libnvvm/.NET effect the node's `BOOT.md` documents under `## Invariants`, not a same-work failure by itself -- it is judged by the CUDA comparison procedure below.

## The CUDA comparison procedure (step 3)

Groups 1 and 6 are the only CUDA configurations `BOOT.md`'s procedure covers. The library code between the committed before (`427fc0d`) and this after (`8f99d11`) is unchanged in `src/`: `git diff --stat f968c35 427fc0d -- src/` and `git diff --stat 96439f4 8f99d11 -- src/` are both empty, and the after-1 vs. after-3 CUDA hashes above confirm the accelerator reproduces the same build deterministically. The dump-and-compare step of the procedure was already executed once for this exact before/after code pair, at commits `f968c35`/`96439f4` (the `f968c35` commit message and `tests/Benchmarks/BOOT.md`, `## Invariants`, the 2026-09-15 warning paragraph); no library code moved since, so its figures are reproduced here rather than re-dumped, and no temporary dump code was written for this comparison run.

| Group | Configuration | Stations | Status | Iteration-count mismatches | Largest relative difference per field |
|---|---|---|---|---|---|
| 1 | BatchThroughput, CUDA, 1000 cases | 4,000 | equal | 0 / 4,000 | CvEquilibrium 4.863331e-16, CpEquilibrium 4.160798e-16, all other fields and mole fractions 0.0 |
| 1 | BatchThroughput, CUDA, 10000 cases | 40,000 | equal | 0 / 40,000 | CvEquilibrium 4.863331e-16, CpEquilibrium 4.160798e-16, all other fields and mole fractions 0.0 (same fixture case replicated) |
| 1 | BatchThroughput, CUDA, 100000 cases | -- | not dumped | -- | not dumped -- `BOOT.md`'s own procedure text: a conclusion already four orders of magnitude inside the tolerance at 1000/10000 cases would not change |
| 6 | UserStates, CUDA, all 48 states | 48 | equal | 0 / 48 | Entropy 2.184873e-16, all other fields and mole fractions 0.0 |

Every figure is at machine epsilon and inside the first tier of `Execution.Tests`' `GpuCpuTolerances.Entries` (relative 1e-10 on temperature, 1e-9 on other fields), far inside the loosest tier (1e-9 on mole fractions) the node's invariant asks of a station whose iteration count does not match -- and no station's iteration count differs here. No CUDA status mismatch, no differing iteration count and no field outside the table.

## Marked changes

- Group 1 -- batch throughput / SolveBatch / 10000 / Cpu: ratio 1 = 0.934, ratio 3 = 0.892 -- faster, drift
- Group 1 -- batch throughput / SolveBatch / 10000 / Cuda: ratio 1 = 1.002, ratio 3 = 0.957 -- drift
- Group 1 -- batch throughput / SolveBatch / 100000 / Cpu: ratio 1 = 0.961, ratio 3 = 0.932 -- faster, drift
- Group 1 -- batch throughput / SolveBatch / 100000 / Cuda: ratio 1 = 1.131, ratio 3 = 1.061 -- slower, drift
- Group 2 -- problem kinds, CPU accelerator / Solve / True / Tp: ratio 1 = 0.798, ratio 3 = 0.803 -- faster
- Group 4 -- one-time costs / AssembleChemicalSystem: ratio 1 = 0.883, ratio 3 = 0.850 -- faster
- Group 4 -- one-time costs / CompileCpuKernel: ratio 1 = 1.669, ratio 3 = 1.658 -- slower
- Group 6 -- user state runs / SolveStates / Cpu / All: ratio 1 = 0.981, ratio 3 = 0.942 -- drift
- Group 6 -- user state runs / SolveStates / Cuda / All: ratio 1 = 0.930, ratio 3 = 0.878 -- faster, drift
- Group 6 -- user state runs / SolveStates / Cuda / Record1: ratio 1 = 0.989, ratio 3 = 0.936 -- drift
- Group 6 -- user state runs / SolveStates / Cuda / Record2: ratio 1 = 0.972, ratio 3 = 0.911 -- faster, drift
- Group 6 -- user state runs / SolveStates / Cuda / Record3: ratio 1 = 0.967, ratio 3 = 0.933 -- faster, drift
- Group 6 -- user state runs / SolveStates / Cuda / Record4: ratio 1 = 0.937, ratio 3 = 0.875 -- faster, drift

