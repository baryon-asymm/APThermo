# BOOT.md — Execution.Tests

## Purpose

The definition of what "`Execution` is ready" means. This node owns the tolerance
table for CUDA against the CPU accelerator and the approved throughput figures.

| Level | What it checks | Against what (source of truth) | State |
|---|---|---|---|
| L0 | accelerator choice and the environment variable; libdevice discovery messages; ILGPU version and reflected members asserted; batch array validation | documented behaviour; mutation of the assertion | ⏳ |
| L1 | the probe kernel with every function of the root's math list loads through the post-link on CUDA and matches the CPU accelerator | the CPU accelerator, the GPU/CPU tolerance table | ⏳ |
| L2 | a rocket batch of 100 000 cases on CUDA equals the CPU accelerator; determinism of two runs; chunking gives the same result as one chunk | the CPU accelerator; reflection-enumerated fields | ⏳ |
| Benchmark | throughput of the 100 000-case batch on CUDA against the CPU accelerator with all cores | the approved figures file (`Throughput.approved.txt`), asymmetry: may improve, must not regress below the root's 5× | ⏳ |
| Protocol | the tree invariant, documents against code | `AGENTS.md`, the surface snapshot | ⏳ |

## Invariants

- **The GPU/CPU tolerance table lives here, in one file**: relative 1e-10 on
  temperature, relative 1e-10 on mole fractions not below 1e-8, relative 1e-9 on the
  performance figures, 4 ULP on the probe kernel's math functions (measured 3 on the
  reference machine). Every entry carries its derivation.
- **CUDA tests are marked** `Category=Cuda` and `Category=LongRunning` for the
  benchmark; on a machine without CUDA the CUDA tests fail with the accelerator
  message, they do not skip, unless `APTHERMO_NO_CUDA=1` is set, in which case the
  CUDA category is excluded by the test command and the CPU tests still run.
- **The approved throughput file is a tripwire**: a run writes `Throughput.actual.txt`
  next to it; the test fails when the ratio falls below the approved one by more than
  20 % or below 5×.

## Dependencies

- [Execution](../../src/Execution/API.md) — what is being checked.
- [Thermo](../../src/Thermo/API.md) — tables and `MixtureState`.
- [Performance](../../src/Performance/API.md) — `PerformanceFigures`, flow models.
- [Data](../../src/Data/API.md) — the database.
- [Fixtures](../Fixtures/API.md) — the reference propellant inputs used to build the batch.

Outside the tree: xunit; ILGPU 1.5.3; an NVIDIA GPU with driver, libnvvm and
libdevice for the CUDA category.

## Constraints

- The CPU-only part of the node runs in the default test command; the CUDA category
  runs in the full set on the reference machine.
- Paths from the repository root; the actual throughput file is the only write, next
  to the approved one, and it is git-ignored.

## Acceptance criteria

- [ ] L0 and L1 green (date, test names).
- [ ] L2 green: 100 000-case equality, determinism, chunk independence (date, test names).
- [ ] Benchmark approved file present with the measured figures and the date of the
      measurement on the reference machine.
- [ ] Every check proven non-degenerate once: a wrapper removed from the post-link, a
      tolerance tightened to zero, the ILGPU version assertion pointed at a wrong
      version, each seen red.

## Taboos

- Do not loosen the GPU/CPU tolerance for green: a divergence is a finding about
  math functions or code generation and gets a design session.
- Do not mark a CUDA test skipped on a machine without CUDA: absence is a failure
  unless CUDA is forbidden explicitly.
- Do not commit the actual throughput file.
