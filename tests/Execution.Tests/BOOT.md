# BOOT.md — Execution.Tests

## Purpose

The definition of what "`Execution` is ready" means. This node owns the tolerance
table for CUDA against the CPU accelerator and the approved throughput figures.

| Level | What it checks | Against what (source of truth) | State |
|---|---|---|---|
| L0 | accelerator choice and the environment variable; libdevice discovery messages; ILGPU version and reflected members asserted; batch validation; chunk bounds; result layouts | documented behaviour; mutation of the assertion (`AcceleratorChoiceTests`) | ✅ |
| L0 | the reason of an `Auto` fallback is on the accelerator description (`CudaSkippedBecause`), naming what was missing and the paths tried; a scratch bound of zero or less is refused at `Create`; the post-link's missing-definition guard names the wrapper whose definition is absent, driven without a GPU through a wrapper body with one definition removed (`PostLinkTests`) | the `API.md` of `Execution` (2026-09-14) | ⏳ |
| L1 | the probe kernel with every function of the root's math list loads through the post-link on CUDA and matches the CPU accelerator; the CPU accelerator reproduces `System.Math` bit for bit | the CPU accelerator and `System.Math`, the GPU/CPU tolerance table (`ProbeKernelTests`) | ✅ |
| L2 | every fixture family and a 100 000-case sweep on CUDA equal the CPU accelerator; the CPU accelerator equals the numerical nodes called case by case; determinism of two runs; chunking gives the same result as one chunk; the species-function batch against the host functions and across accelerators | the CPU accelerator and the host calls; reflection-enumerated fields (`BatchTests`, `CudaTests`, `SpeciesFunctionTests`) | ✅ |
| Benchmark | throughput of the 100 000-case batch on CUDA against the CPU accelerator with all cores | the approved figures file (`Throughput.approved.txt`), asymmetry: may improve, must not regress below 80 % of the approved ratio or below the root's 5× (`CudaTests.Throughput_is_recorded_and_not_below_the_approved_ratio`) | ✅ |
| Protocol | the tree invariant, documents against code | `AGENTS.md`, the surface snapshot | ✅ (2026-09-13, the Protocol.Tests node) |

## Invariants

- **The GPU/CPU tolerance table lives here, in one file** (`GpuCpuTolerances.cs`),
  and every entry carries its derivation: relative 1e-10 on temperature; relative
  1e-10 on mole fractions not below 1e-8 at stations where both accelerators stopped
  after the same number of Newton steps, and 1e-9 where they did not; relative 1e-9
  on the other state fields, the performance figures and the transport figures; 4 ULP
  on the probe kernel's math functions (3 measured on the reference machine); at most
  one station in a thousand may stop after different numbers of Newton steps; 1e-10
  on the species functions, taken on the larger of 1 and the value, because H°/RT of a
  reference element at 298.15 K cancels to zero by construction and the condensed fits
  cancel by up to five decades (1.7e-11 measured on liquid water's Cp/R; the entry was
  first written as 1e-12 relative and calibrated on that measurement the same day).

  ⚠ 2026-09-12: the sketch had one tier, 1e-10 on every mole fraction above the
  floor. The 100 000-case sweep showed 12 mole fractions at 2 of its 400 000
  stations beyond it, by up to 3.3e-10, both at stations where CUDA had taken 3
  Newton steps and the CPU accelerator 2 or the reverse. The equilibrium solver
  polishes until its corrections are below 1e-11, the rounding floor of its linear
  solves; a last-ULP difference between libdevice and .NET flips that threshold at
  91 stations of the sweep, and the accelerator that takes one polish step more
  moves by up to 1.4e-11 in temperature and, through `(H_j/RT) Δln T + Σ a_ij Δπ_i`,
  by a few 1e-10 in the mole fraction of a minor species. Where the step counts
  agree the worst deviations are 3.4e-13 on temperature, 9e-12 on a mole fraction
  and 2e-12 on any other field. The second tier is derived from the polish
  threshold, not from the measurement; the share of such stations is bounded so
  that a systematic divergence (a single-precision or CORDIC function would flip
  the count everywhere) cannot hide behind the second tier. The root's invariant
  carries the same note.

  ⚠ 2026-09-14: "in one file" was not true: the front door tests node copies the
  mole-fraction floor (1e-8) and the polish-threshold tier (1e-9) for its reordered
  union batches, and the protocol forbids it to read this node's code. Found by the
  clean-code review (F-TF-05). Decided at the root: the two entries move to the
  fixtures node's tolerance table, which both nodes already depend on, as
  `moleFractionFloor` and `polishThresholdRelative` with their derivations, and this
  node's table keeps the GPU-specific entries; until that task lands the copy stands
  as a declared duplication.
- **CUDA tests are marked** `Category=Cuda` and the sweep and the benchmark also
  `Category=LongRunning`; when `APTHERMO_NO_CUDA=1` is set a CUDA-category test
  verifies the refusal of an explicit CUDA request and returns, so the full suite
  passes in that process; on a machine without CUDA and without the variable the
  CUDA tests fail with the accelerator message, they do not skip.
- **The approved throughput file is a tripwire**: a run writes `Throughput.actual.txt`
  next to it; the test fails when the ratio falls below the approved one by more than
  20 % or below 5×.
- **No expected value is typed into a test**: the CPU accelerator is compared with
  the numerical nodes called directly over the same buffers, CUDA with the CPU
  accelerator, and the reference temperature of the equilibrium family comes from the
  fixture.

## Dependencies

- [Execution](../../src/Execution/API.md) — what is being checked.
- [Thermo](../../src/Thermo/API.md) — tables, `MixtureState`, `CaseStatus`.
- [Equilibrium](../../src/Equilibrium/API.md) — the solver and scratch layout called case by case on the host.
- [Performance](../../src/Performance/API.md) — `PerformanceFigures`, the rocket solver called on the host, flow models.
- [Transport](../../src/Transport/API.md) — the transport table and evaluation called on the host, `TransportFigures`.
- [Data](../../src/Data/API.md) — the database.
- [Fixtures](../Fixtures/API.md) — the reference propellant inputs used to build the batches.

Outside the tree: xunit; ILGPU 1.5.3; an NVIDIA GPU with driver, libnvvm and
libdevice for the CUDA category.

## Constraints

- The CPU-only part of the node runs in the default test command; the CUDA category
  runs in the full set on the reference machine; the long-running category (the
  sweep and the benchmark, about 20 s) is excluded from the fast set.
- Paths from the repository root; the actual throughput file is the only write, next
  to the approved one, and it is git-ignored.
- One engine per accelerator is shared by the collection; tests that need a fresh
  engine (warm-up timing, chunk size) create and dispose their own.

## Acceptance criteria

- [x] 2026-09-12 — L0 and L1 green: `AcceleratorChoiceTests` (ten facts: the CPU
      engine's description, paths nowhere, discovery paths, the variable forbids CUDA
      and `Auto` falls back, no driver loaded when forbidden, the ILGPU assertion,
      wrapper names, inconsistent batches refused, chunk bounds, result layouts),
      `ProbeKernelTests` (`The_cpu_accelerator_reproduces_dotnet_math_exactly`,
      `Cuda_matches_the_cpu_accelerator_within_the_ulp_bound_for_every_function`).
- [x] 2026-09-12 — L2 green: `BatchTests` (`A_rocket_family_equals_the_host_solver_bit_for_bit`
      over every family, `Chunking_and_repetition_do_not_change_a_bit`,
      `The_transport_pass_equals_the_host_evaluation_bit_for_bit`,
      `An_equilibrium_family_equals_the_host_solver_bit_for_bit`); `CudaTests`
      (`A_rocket_family_on_cuda_matches_the_cpu_accelerator` over every family with
      the transport pass, `An_equilibrium_family_on_cuda_matches_the_cpu_accelerator`,
      `The_sweep_of_100000_cases_on_cuda_matches_the_cpu_accelerator_and_is_deterministic`);
      `SpeciesFunctionTests` (`The_cpu_accelerator_equals_the_host_functions_bit_for_bit`,
      `Cuda_matches_the_cpu_accelerator_within_the_table`,
      `A_species_index_outside_the_table_is_refused_before_any_kernel_runs`).
- [x] 2026-09-12 — Benchmark approved file present with the measured figures and the
      date of the measurement on the reference machine (`Throughput.approved.txt`:
      RTX 5070 Ti, 100 000 cases, 4 stations, 11 species, CUDA 0.170 s, CPU
      accelerator 9.544 s with 16 threads, 56.28×).
- [x] 2026-09-12 — Every check proven non-degenerate once, each mutation applied
      alone and seen red: the first wrapper fragment dropped from the NVVM module
      (the probe test, with the wrapper's name in the error); `ExpectedIlgpuVersion`
      set to 1.5.2.0 (every test of the node); the temperature tolerance set to zero
      and the transport tolerance set to zero (the CUDA family test); the rocket
      kernel's chamber pressure perturbed by 1e-12 (the host bit-equality test); the
      chunk no longer bounded by the scratch memory (the chunk-bounds test); the share
      of different step counts set to zero, the second mole-fraction tier set back to
      1e-10, and the determinism check pointed at the CPU result (each: the sweep
      test).
- [ ] The three facts of 2026-09-14 (the level table's second L0 row): the `Auto`
      fallback with discovery off and the explicit paths nowhere yields the CPU engine
      and `CudaSkippedBecause` naming what was missing and the paths tried (the
      mirror of `An_explicit_cuda_request_with_paths_nowhere_names_every_path_tried`),
      seen red against `8e36a27`; `ScratchBytes` of zero or less refused at `Create`
      naming the option; the post-link's missing-definition check names the wrapper
      when handed a wrapper body with one definition removed (`PostLinkTests`, no
      GPU) — the first non-degeneracy proof of the guard behind the node's third
      invariant.
- [ ] The support code in shape (the test review's F-TF-06 and F-TF-13):
      `BatchBuilders` becomes `FixtureBatches` (fixtures to families and batches),
      `HostSolves` (one case through the numerical nodes over the accelerator's own
      buffers, returning named record structs instead of tuples) and `BitEquality`
      (`SameBits`, `BitDifferences<T>`); no method over 60 lines or nested deeper
      than 3; every L2 fact green bit for bit and the node's mutations above still
      red. The hand-typed fact counts leave the criteria above; the listed names are
      the list.

## Taboos

- Do not loosen the GPU/CPU tolerance for green: a divergence is a finding about
  math functions or code generation and gets a design session. The second tier of
  the mole-fraction entry is not such a loosening: it is derived from the solver's
  stopping rule and guarded by the bound on the share of stations it applies to.
- Do not mark a CUDA test skipped on a machine without CUDA: absence is a failure
  unless CUDA is forbidden explicitly.
- Do not commit the actual throughput file.
