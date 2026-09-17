# BOOT.md — Execution.Tests

## Purpose

The definition of what "`Execution` is ready" means. This node owns the tolerance
table for CUDA against the CPU accelerator and the approved throughput figures.

| Level | What it checks | Against what (source of truth) | State |
|---|---|---|---|
| L0 | accelerator choice and the environment variable; libdevice discovery messages; ILGPU version and reflected members asserted; batch validation; chunk bounds; result layouts | documented behaviour; mutation of the assertion (`AcceleratorChoiceTests`) | ✅ |
| L0 | the reason of an `Auto` fallback is on the accelerator description (`CudaSkippedBecause`), naming what was missing and the paths tried; a scratch bound of zero or less is refused at `Create`; the post-link's missing-definition guard names the wrapper whose definition is absent, driven without a GPU through a wrapper body with one definition removed (`PostLinkTests`) | the `API.md` of `Execution` (2026-09-14) | ✅ (2026-09-14) |
| L1 | the probe kernel with every function of the root's math list loads through the post-link on CUDA and matches the CPU accelerator; the CPU accelerator reproduces `System.Math` bit for bit | the CPU accelerator and `System.Math`, the GPU/CPU tolerance table (`ProbeKernelTests`) | ✅ |
| L2 | every fixture family and a 100 000-case sweep on CUDA equal the CPU accelerator; the CPU accelerator equals the numerical nodes called case by case; determinism of two runs; chunking gives the same result as one chunk; the species-function batch against the host functions and across accelerators | the CPU accelerator and the host calls; reflection-enumerated fields (`BatchTests`, `CudaTests`, `SpeciesFunctionTests`) | ✅ |
| Benchmark | throughput of the 100 000-case batch on CUDA against the CPU accelerator with all cores | the approved figures file for the running platform (`Throughput.approved.txt`, `Throughput.linux.approved.txt` on Linux, 2026-09-17), asymmetry: may improve, must not regress below 80 % of the approved ratio or below the root's 5× (`CudaTests.Throughput_is_recorded_and_not_below_the_approved_ratio`) | ✅ |
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

  ⚠ 2026-09-14: "in one file" was not true: the front door tests node copied the
  mole-fraction floor (1e-8) and the polish-threshold tier (1e-9) for its reordered
  union batches, and the protocol forbids it to read this node's code. Found by the
  clean-code review (F-TF-05). Resolved the same day: the two entries moved to the
  fixtures node's tolerance table, which both nodes already depend on
  (`tests/Fixtures/tolerances.json`, `moleFractionFloor` and
  `polishThresholdRelative`, with their derivations); `GpuCpuTolerances.MoleFractionFloor`
  and `MoleFractionRelative` now take that table and read the two entries from it, and
  this node's own table keeps only the GPU-specific entries (temperature, moleFraction,
  state, figures, transport, functions) that have no place in a table of comparisons
  with the reference.
- **Bit comparison goes through the harness** (2026-09-14): `BitEquality.cs`'s
  `SameBits` and `BitDifferences<T>` were, field for field, the harness's `Bits.Same`
  and `Bits.Differences<T>`; the file is gone and every call site of this node reads
  `APThermo.Harness.Bits` instead, so the acceptance
  criterion below that names `BitEquality.cs` as one of the F-TF-06 split's four files
  now names a file this node no longer has.
- **CUDA tests are marked** `Category=Cuda` and the sweep and the benchmark also
  `Category=LongRunning`; when `APTHERMO_NO_CUDA=1` is set a CUDA-category test
  verifies the refusal of an explicit CUDA request and returns, so the full suite
  passes in that process; on a machine without CUDA and without the variable the
  CUDA tests fail with the accelerator message, they do not skip.
- **The approved throughput file is a tripwire**: a run writes `Throughput.actual.txt`
  next to it; the test fails when the ratio falls below the approved one by more than
  20 % or below 5×.

  ⚠ 2026-09-17: this bullet assumed one approved file. The root's platform constraint
  keeps a Windows and a Linux record for the bit snapshots (`Harness`'s `BOOT.md`), and
  the same reasoning applies here: a benchmark run under WSL2 times a CPU accelerator
  and a CUDA path that both include the hypervisor's virtualization overhead, so its
  ratio is not comparable to a native Windows run. `ApprovedPathFor` picks
  `Throughput.approved.txt` or `Throughput.linux.approved.txt` for the running
  platform, the same one place `Bits.approved.txt`'s per-node counterpart uses; the
  actual file is written beside whichever one is read, so `Throughput.linux.actual.txt`
  on Linux. The root's 5× floor is not a per-platform figure and applies to both.
- **No expected value is typed into a test**: the CPU accelerator is compared with
  the numerical nodes called directly over the same buffers, CUDA with the CPU
  accelerator, and the reference temperature of the equilibrium family comes from the
  fixture.

## Dependencies

- [Execution](../../src/Execution/API.md) — what is being checked.
- [Execution.Chunks](../../src/Execution/Chunks/API.md) — `Chunk` and `ChunkPlan`, in
  the chunk-plan unit facts (a child node of `Execution`, 2026-09-15).
- [Thermo](../../src/Thermo/API.md) — tables, `MixtureState`, `CaseStatus`.
- [Equilibrium](../../src/Equilibrium/API.md) — the solver and scratch layout called case by case on the host.
- [Performance](../../src/Performance/API.md) — `PerformanceFigures`, the rocket solver called on the host, flow models.
- [Transport](../../src/Transport/API.md) — the transport table and evaluation called on the host, `TransportFigures`.
- [Data](../../src/Data/API.md) — the database.
- [Fixtures](../Fixtures/API.md) — the reference propellant inputs used to build the
  batches, and the mole-fraction floor and polish-threshold tier of the tolerance table.
- [Harness](../Harness/API.md) — bit comparison (`Bits.Same`, `Bits.Differences`) and
  the per-platform approved path (`ApprovedSnapshot.ApprovedPathFor`, 2026-09-17).

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

- [x] 2026-09-12 — L0 and L1 green: `AcceleratorChoiceTests` (the CPU engine's
      description, paths nowhere, discovery paths, the variable forbids CUDA and
      `Auto` falls back, no driver loaded when forbidden, the ILGPU assertion,
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
- [x] 2026-09-14 — The three facts of 2026-09-14 (the level table's second L0 row):
      the `Auto` fallback with discovery off and the explicit paths nowhere yields
      the CPU engine and `CudaSkippedBecause` naming what was missing and the paths
      tried (`AcceleratorChoiceTests.An_auto_fallback_says_why_cuda_was_skipped_and_which_paths_were_tried`,
      the mirror of `An_explicit_cuda_request_with_paths_nowhere_names_every_path_tried`),
      red against the code of `8e36a27` (where `AcceleratorInfo` said nothing) before
      `c10ab0e`; `ScratchBytes` of zero or less refused at `Create` naming the option
      (`Chunks_are_bounded_by_the_chunk_size_and_the_scratch_memory`); the post-link's
      missing-definition check names the wrapper when handed a wrapper body with one
      definition removed, without a GPU (`PostLinkTests.A_wrapper_body_with_one_definition_removed_names_that_wrapper`
      and its two siblings) — the first non-degeneracy proof of the guard behind the
      node's third invariant.
- [x] 2026-09-14 — The support code in shape (the test review's F-TF-06 and F-TF-13):
      `BatchBuilders` is gone, replaced by `FixtureBatches.cs` (`RocketInputs`,
      `RocketFamily`, fixtures to families and batches), `HostSolves.cs` (one case
      through the numerical nodes over the accelerator's own buffers, returning the
      named record structs `HostRocketCase`, `HostEquilibriumCase`,
      `HostTransportStation` instead of tuples), `BitEquality.cs` (`SameBits`,
      `BitDifferences<T>`) and `SweepRun.cs` (the long-running sweep, not named by
      F-TF-06 but sharing none of the three axes above); no method over 60 lines or
      nested deeper than 3, covered by the protocol tests node's `ShapeTests`, all
      ten facts green at `62cd99e`; every L2 fact green bit for bit after the split
      (`APThermo.Execution.Tests.dll`: 41 passed) and the
      node's mutations re-run alone and seen red where the touched code moved: the
      rocket kernel's chamber pressure perturbed by a relative `1e-12`
      (`batch.ChamberPressures[index] * (1.0 + 1e-12)` in `Kernels.Rocket`) reddened
      `A_rocket_family_equals_the_host_solver_bit_for_bit` for every family, and the
      chunk bound with its memory clamp removed from `ChunkPlan.For` reddened
      `Chunks_are_bounded_by_the_chunk_size_and_the_scratch_memory`; both reverted
      and the suite green again before committing. The hand-typed fact counts left
      the criteria above; the listed names are the list.

      ⚠ 2026-09-15: "nested deeper than 3" was not measured: `inventory.py` counts
      lines only, and `BatchTests.A_rocket_family_equals_the_host_solver_bit_for_bit`
      and `SpeciesFunctionTests.Cuda_matches_the_cpu_accelerator_within_the_table`
      nested 4 deep at this tick's commit (`7a3dedb`). Found by the repair review
      (R-Execution.Tests-4); both were brought to 3 on 2026-09-15 (the criterion
      below), where this document's earlier silence on the point is corrected.
- [x] 2026-09-15 — Two more constructions restructured to the root's parameter limit:
      `RocketInputs` (`FixtureBatches.cs`) fell
      from 9 to 6 parameters, split along the domain axes of a rocket fixture's
      chemical system (`ChemicalSystem`: elements, element moles, products - 3
      parameters) and its exit layout (`ExitPlan`: values, kinds - 2 parameters), the
      combustion conditions and the transport flag kept flat; the old field names
      stay as forwarding properties, so every read call site is unchanged and only
      the one construction site, in `RocketInputs.Of`, changed. `CudaTests.CompareMoles`
      fell from 8 to 5 parameters: the two mole arrays under comparison, the index
      into them and the species table that reads them became `MoleSample` (4
      parameters), a type local to `CudaTests.cs`; its two call sites (one in
      `An_equilibrium_family_on_cuda_matches_the_cpu_accelerator`, the other in the
      private `CompareRocket`, itself called from the rocket-family and the sweep
      tests) construct it in place of the four separate parameters.

      ⚠ 2026-09-15: `MoleSample` is no longer local to `CudaTests.cs`, and
      `CompareRocket` is gone: both moved to `GpuCpuComparison.cs` the same day, the
      criterion below.

      Two nesting-depth-4 violations found by the same review were fixed alongside:
      `BatchTests.A_rocket_family_equals_the_host_solver_bit_for_bit`'s
      species-by-species mole loop, four levels deep inside the case loop, the
      station loop and its own species loop, moved to `StationMoleDifferences`
      (nesting 2 on its own); `SpeciesFunctionTests.Cuda_matches_the_cpu_accelerator_within_the_table`'s
      three-function comparison, four levels deep inside the family loop, the entry
      loop and its own function loop, moved to `CompareFunctions` (nesting 2 on its
      own). Neither method nests deeper than 3 now.

      Verified: build clean, 0 warnings; 41 of 41 fast tests green
      (`APThermo.Execution.Tests.dll`); `protocol_lint`
      0 errors, 0 warnings; `Protocol.Tests` 9 of 9 green. This node keeps no
      `Bits.approved.txt` of its own (its bit comparisons run the host call inside
      the same test, not against a recorded snapshot), so there is no hash to
      compare before and after.

      ⚠ 2026-09-15: "the old field names stay as forwarding properties, so every read
      call site is unchanged" did not age well, for the same reason the equivalent
      shortcut in the Performance.Tests node did not: `ChemicalSystem` mixed a
      family's shared axis with one fixture's own. `RocketFamilies` groups fixtures by
      `BatchKey`, and `BatchKey` never read `ElementMoles` — only `Elements`,
      `Products` and the exit kinds — confirming `ElementMoles` was never part of what
      a family shares; it is one fixture's own starting composition, exactly like
      `ReactantEnthalpy`, which already sat outside `ChemicalSystem`. Found by the
      repair review (R-Execution.Tests-1). `ChemicalSystem` narrowed to `Elements`,
      `Products` (2 parameters); a new `Mixture` record holds `ElementMoles` and
      `ReactantEnthalpy` (2 parameters); `RocketInputs` keeps `System`, `Mixture`,
      `ChamberPressure`, `Flow`, `Exits`, `Transport`, still 6 parameters. The
      forwarding properties (`Elements`, `ElementMoles`, `Products`, `ExitValues`,
      `ExitKinds`) are gone; the four read call sites this document said were
      unchanged (`RocketFamily.Batch`, `RocketFamilies`, `Sweep`, all in
      `FixtureBatches.cs`) and the fifth this document did not mention
      (`AcceleratorChoiceTests.Inconsistent_batches_are_refused_before_any_kernel_runs`)
      all name `.System.` or `.Mixture.` or `.Exits.` directly now. No behaviour
      changed: the same fields, on the same two records, under new names one level
      down.

      By the same `CouplingMeasures` run, `FixtureBatches` itself moved from Ce=14 to
      Ce=17 (`ChemicalSystem`, `Mixture` and `ExitPlan` newly named directly in
      `RocketFamilies` and `Sweep`, for the same reason as `RocketCase` in the
      Performance.Tests node); its test-fixture neighbours measure
      `AcceleratorChoiceTests` Ce=32, `BatchTests` Ce=31, `CudaTests` Ce=26,
      `HostSolves` Ce=28, `SpeciesFunctionTests` Ce=16, none touched by this cut. The
      root limits the efferent coupling of the `src` types only, so a test type's
      figure is recorded, not limited.

      Verified: build clean, 0 warnings; 41 of 41 fast tests green; `protocol_lint`
      0 errors, 0 warnings.

- [x] 2026-09-15 — The GPU/CPU comparison logic `CudaTests.cs` carried alongside its `[Fact]`/
      `[Theory]` methods — `MoleSample`, `CompareRocket`, `CompareMoles`, `Record`,
      `Worst` — moved to a new file, `GpuCpuComparison.cs`: one stateful type,
      `GpuCpuComparison`, built from the tolerance table once per test and holding the
      worst deviation per field and the different-step count as it accumulates them,
      with `Rocket(RocketBatchResult, RocketBatchResult, RocketFamily)` (was
      `CompareRocket`, 3 parameters), `Moles(double[], double[], long, SpeciesTable,
      bool, string)` (was `CompareMoles`, 6 parameters, its four `MoleSample` fields
      unpacked back to plain parameters — `MoleSample` had one caller-supplied field
      per call and was never kept, so it named no concept of its own), `Record` (the
      `GpuCpuTolerances.Compare` callback), a new `CountSteps(bool)` and `Worst()` as
      its methods. `MoleSample` is gone. The three CUDA test methods that owned a
      `worst` dictionary, and either a `differentSteps` local incremented inline
      (the equilibrium family test) or passed by `ref` into `CompareRocket` (the
      rocket family and sweep tests), now own one `GpuCpuComparison` instead, call
      `.CountSteps(sameSteps)` where they used to increment their own local, and read
      `.DifferentSteps` back: the equilibrium test's step count moved from a local
      variable to the same shared counter `Rocket` itself feeds, so all three tests
      now count steps the same way. No behaviour change: the same comparisons, the
      same tolerance calls, the same accumulation — `worst` shared across a test
      method's rocket-then-transport phases
      (`A_rocket_family_on_cuda_matches_the_cpu_accelerator`) is still one dictionary
      shared the same way, now the one instance's private field instead of a local
      passed to both phases. `ShapeTests.No_type_spans_more_than_400_lines` and
      `ShapeTests.No_method_spans_more_than_60_lines` both hold for `CudaTests` and
      `GpuCpuComparison`; `GpuCpuComparison`'s own Ce is 8, `CudaTests`' own Ce is 26,
      unchanged from before the cut, both recorded by `CouplingMeasures` and not
      limited, since the root's coupling rule holds for `src` types only.

      This is a mechanical port: `Rocket`/`Moles`/`CountSteps` cannot be exercised
      without a CUDA device, so the CPU-only fast suite (`APTHERMO_NO_CUDA=1`, which
      makes `RequireCuda()` return null and every CUDA-marked test return before
      reaching this code) proves only that it builds and that every other fact stays
      green; the actual arithmetic is unchanged from the moved code, read side by
      side at the move. Applies R-Execution.Tests-2 of the repair review.

      Verified on the reference machine, `APTHERMO_NO_CUDA` unset, one run at a
      time: `ProbeKernelTests.Cuda_matches_the_cpu_accelerator_within_the_ulp_bound_for_every_function`
      green (R-Execution-2's own guard, exercising the merged `CompileWrappers`);
      `CudaTests.A_rocket_family_on_cuda_matches_the_cpu_accelerator` and
      `An_equilibrium_family_on_cuda_matches_the_cpu_accelerator` together, 9 of 9
      green (every rocket family plus the equilibrium family, through `Rocket`,
      `Moles`, `CountSteps` and `Record` on real hardware); then
      `The_sweep_of_100000_cases_on_cuda_matches_the_cpu_accelerator_and_is_deterministic`
      alone, green (400 000 stations through `Rocket`, both accelerators agreeing
      within the tolerance table, CUDA deterministic across two runs).
      `Throughput_is_recorded_and_not_below_the_approved_ratio` was not run, as the
      decision records.

## Taboos

- Do not loosen the GPU/CPU tolerance for green: a divergence is a finding about
  math functions or code generation and gets a design session. The second tier of
  the mole-fraction entry is not such a loosening: it is derived from the solver's
  stopping rule and guarded by the bound on the share of stations it applies to.
- Do not mark a CUDA test skipped on a machine without CUDA: absence is a failure
  unless CUDA is forbidden explicitly.
- Do not commit the actual throughput file.
