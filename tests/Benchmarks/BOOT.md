# BOOT.md — Benchmarks

## Purpose

Measures how fast the library computes, so that a change of the code, the clean-code
pass of 2026-09-14/15 first, can be compared against the code before it. The
bit-for-bit guards prove that the numbers did not change; nothing proves the speed.
Extracting kernel stages can change inlining on the CPU accelerator and in the
the CUDA code. This node is a library of BenchmarkDotNet benchmark classes,
run by hand through its child node [Runner](Runner/BOOT.md) (2026-09-25), never by
`dotnet test`. It records figures and asserts none.

⚠ 2026-09-25: was a BenchmarkDotNet console project, now a library and the child
node `Runner` (CA1515) → HISTORY.md#console-to-library

## Invariants

- **Figures are recorded, never asserted.** No test of the tree compares a timing with
  a bound: a wall-clock assertion reddens on a busy machine for no defect (AGENTS.md
  §13, a perpetually red check). A regression is found by reading the comparison, and
  it goes to a design session.
- **The same work on both sides of a comparison.** Every benchmark that solves also
  records, once per configuration, the statuses of its cases and a hash of its results
  (the bits of the result structs and moles, the `Harness` bit hash).
  - On the CPU accelerator the before and after hashes must be equal: the pass was
    bit-for-bit there, so a difference on the CPU accelerator is a finding about the
    code, never noise of the measurement.
  - On CUDA the before and after statuses must be equal, and the results must agree
    within the relative tolerances of the comparison class (1e-10 on temperature, 1e-9 on
    the other fields, and `tolerances.json`'s `moleFractionFloor` and
    `polishThresholdRelative` for a mole fraction): they are the figures the
    execution tests node's GPU/CPU table held until 2026-10-05, kept here for the
    consumer path (`Solver` against the engine). Since that date CUDA equals the CPU
    accelerator bit for bit, so the comparison of a later change is expected bit-equal
    on both. The comparison run records the
    largest relative difference per field (the CUDA comparison procedure under
    `## Constraints`), so that a systematic change cannot hide inside the tolerance.
    ⚠ 2026-10-05: was the last-ULP difference of NVVM's libdevice arithmetic against
    .NET's named as the reason for the CUDA tolerance, now the tree's own math removed it;
    the tolerances stay for the consumer path → HISTORY.md#cuda-tolerance-own-math-2026-10-05

  ⚠ 2026-09-15: was CUDA hashes required equal to the before's, now the GPU/CPU
  tolerance tiers, largest difference 4.9e-16 → HISTORY.md#cuda-hash-tier
- **Group 7 compares `Solver` against the engine on a proven baseline, not an
  assumption.** `SolverBatchBenchmarks`'s `[GlobalSetup]` also runs group 1's engine
  path over the identical batch once and compares the two results field by field
  (`EngineSolverComparison`), then logs which relation holds. On the CPU accelerator
  a dry run found `Solver` and the engine bit-for-bit equal — every `MixtureState`
  and `PerformanceFigures` field and every mole fraction, at every case of every case
  count (2026-09-15, `## Acceptance criteria`). On CUDA the same dry run found the two
  paths *not* bit-for-bit identical, even though both call the identical kernel on the
  identical batch: the same last-ULP pattern the root `BOOT.md`'s GPU-equals-CPU
  invariant documents for CUDA against the CPU accelerator. A CUDA configuration is
  therefore held to the GPU/CPU tolerance tiers this node's own invariant above
  already quotes from the execution tests node (relative 1e-10 on temperature, 1e-9 on
  every other `MixtureState`/`PerformanceFigures` field — repeated as constants in
  `EngineSolverComparison`, since `Execution.Tests`' `GpuCpuTolerances` exposes
  nothing outward for Benchmarks to call) and, for the one field `Solver` derives by a
  division a summation order can round differently (a mole fraction, `n_j` over the
  moles of all species, `Problems/API.md`), to the fixtures node's own tolerance table
  for two paths of the tree's own code reaching the same mole fraction
  (`moleFractionFloor`, `polishThresholdRelative`, loaded from `Fixtures.ToleranceTable`,
  a declared dependency already). Measured 2026-09-15: worst observed 4.963e-13
  relative on a state or performance field, 3.488e-12 on a mole fraction, both three
  orders of magnitude inside their tier, at every CUDA case count.
- **Data come from files.** The user's state records are a data file of this node
  (`data/user-states.json`), with the pressures given as a rule: first, step, count.
  The code computes the pressure `first + i × step` by index, never by accumulation.
  Fixture-based benchmarks read `tests/Fixtures` files through `RepositoryPaths`.

  `data/user-states.json`'s format (2026-09-15, coding — the design left it to the
  coder): one object with `pressureRule` (`firstPascal`, `stepPascal`, `count`) and
  `records`, a list of `{ enthalpyPerKilogram, elementMolesPerKilogram }`, the element
  symbols and values verbatim as the user gave them and in mol per kilogram, the unit
  `Problems.StateRecord.Composition` and `Problems.ElementalMixture.ElementMoles` take
  (`Problems/API.md`) — unlike a fixture's own `case.inputs.elementMoles`, which is
  kmol per kilogram, the numerical nodes' unit (`FixtureJson`'s doc comments record the
  factor of a thousand between the two and where each is read). `UserStates.ReadByRecord`
  reads the file and generates a `StateRecord` per pressure of the rule for every
  record, in file order, pressure index ascending within a record: the same order the
  48-state verification of `## Acceptance criteria` checks.
- **Every group is listed by the machine.** `dotnet run -c Release --project
  tests/Benchmarks -- --list flat` prints every benchmark. The criteria below check
  against that list, not against a typed one.
- **CUDA is optional.** A CUDA benchmark on a machine without CUDA, or with
  `APTHERMO_NO_CUDA=1`, is skipped. The skip is recorded with the engine's
  `CudaSkippedBecause`; it never fails.

## Dependencies

[Problems](../../src/Problems/API.md)
[Execution](../../src/Execution/API.md)
[Thermo](../../src/Thermo/API.md)
[Equilibrium](../../src/Equilibrium/API.md)
[Performance](../../src/Performance/API.md)
[Data](../../src/Data/API.md)
[Transport](../../src/Transport/API.md)
[Fixtures](../Fixtures/API.md)
[Harness](../Harness/API.md)

Outside the tree: BenchmarkDotNet 0.15.8, pinned in `Directory.Packages.props`.

⚠ 2026-09-15, design: was this list the design's expectation, now exactly the nodes
the code names → HISTORY.md#deps-expectation

⚠ 2026-09-15, coding: was `Equilibrium` unlisted, now linked (its `ProblemKind` is
named directly) → HISTORY.md#deps-equilibrium

⚠ 2026-09-15, coding: was `Transport` unlisted, now linked (`Upload`'s signature
names `TransportTable`) → HISTORY.md#deps-transport

## Constraints

Inherited from the root ([BOOT.md](../../BOOT.md)). In addition:

- **Project.** `APThermo.Benchmarks`, a class library in the solution (2026-09-25; an
  executable before). It builds with the solution, so the protocol checks cover it. It has
  no test SDK, so `dotnet test` runs nothing of it. The executable is the child node
  [Runner](Runner/BOOT.md), which holds only the entry point.
  - BenchmarkDotNet requires public benchmark classes; each is named in `API.md`, and each
    public member carries XML documentation (CS1591).
  - The job configuration (`BenchmarkEnvironment.Config`) is public, so that the runner
    passes it to `BenchmarkSwitcher`; the classes stay discoverable through this assembly.
  - A benchmark whose result is a tree-contract type, which a public method cannot return
    (CS0050), hands it to a BenchmarkDotNet `Consumer` (`BenchmarkDotNet.Engines`) so that
    the call is not dead code. A field written only to defeat the JIT, and a read
    contrived only to satisfy IDE0052 (a log line in `Cleanup`), are not allowed.

    ⚠ 2026-09-25: was the need of public classes asserted, now confirmed by a dry run
    (an `internal` class fails validation) → HISTORY.md#ca1515-proof
  - The root's code-shape constraint holds as for every type of the tree.
- **Configuration.** One job for every group:
  - Release, x64, the in-process toolchain, so that ILGPU's native libraries and the
    CUDA post-link load as in the library;
  - `MemoryDiagnoser` on;
  - 5 warmup iterations, 20 measured iterations, invocation count 1, unroll factor 1,
    bounded so that a full run on the reference machine takes about an hour or less.

  One-time costs run as cold starts.

  ⚠ 2026-09-15: was 1 warmup and 3 measured iterations, now 5 and 20
  → HISTORY.md#job-iterations
- **Groups.** Every group gets its own class.
  1. **Batch throughput.** The LOX/LH2 rocket sweep family at 1 000, 10 000 and 100 000
     cases, on the CPU accelerator with all cores and on CUDA, through the `Engine`
     batch API. The engine's run timings are recorded beside the means: kernel against
     upload and download.
  2. **Problem kinds on the CPU accelerator.** tp, hp, sp, and rocket in shifting and in
     frozen flow, each with and without transport, so that a regression points at a
     node.
  3. **One case through `Solver`.** The latency a .NET caller sees.
  4. **One-time costs.**
     - database load;
     - chemical-system assembly;
     - kernel compilation on the CPU accelerator, and on CUDA with the PTX
       post-link;
     - species-table upload.
  5. **Allocations on the batch path.** The root's no-allocation-during-a-solve
     invariant gets a figure, from `MemoryDiagnoser`.
  6. **The user's state runs.** Four records of another simulation: element moles per
     kilogram and an enthalpy, AP/HTPB/Al compositions with 17.5, 20.7, 0 and 39.7 %
     Al by mass.
     - The pressures run from 1.0e6 to 6.5e6 Pa in steps of 5.0e5: 12 pressures, 48 hp
       states.
     - They go through `Solver.SolveStates` on the CPU accelerator and on CUDA: all 48
       together, and each record's 12 alone.
     - Record 4 reaches the 2700 K region of the `ALN(L)` enthalpy gap at 6.5 MPa, so
       its per-record figure is watched.
  7. **Solver against the engine** (2026-09-15). The same LOX/LH2 rocket sweep family
     as group 1, at the same case counts (1 000, 10 000, 100 000) and on the same
     accelerators, through the consumer path `Problems.Solver`'s
     `Solve(ElementalMixture, IReadOnlyList<RocketProblem>)` — the one mixture of
     group 1's fixture case with `CaseCount` identical `RocketProblem`s, `Only` set to
     the fixture's own product list so the two groups' species tables match — instead
     of the raw `Execution.Engine` batch API. What group 1 measures is the engine
     alone; this group measures what a .NET caller pays end to end: building the
     inputs (`ElementalMixture.Create`, the `RocketProblem` list), the solve, and
     materialising the `RocketResult`/`Station` records. The comparison between the
     two groups is read at equal case counts; group 1 is unchanged. `[GlobalSetup]`
     also runs group 1's engine path over the identical batch once (`RocketBatches`,
     shared with `BatchThroughputBenchmarks` so "the same cases" are built by one
     piece of code) and compares field by field against it (`EngineSolverComparison`,
     the invariant above): temperature, pressure, the mole fractions and the
     performance figures, logged as `equalsEngine=bitwise` or `equalsEngine=tolerance`
     beside the statuses and the results hash.
- **Results.** Text, committed.
  - BenchmarkDotNet's GitHub markdown and CSV go under
    `results/<yyyy-mm-dd>-<commit>-run<n>/`, together with a `run.md` that records the
    machine, the driver, the commit, the build and what else was running (nothing).
    The run number distinguishes same-commit runs of one timed comparison sequence
    (`## Run conditions`: after, before, after), since the date and commit alone
    repeat within it.
  - A comparison goes in `results/comparison-<yyyy-mm-dd>.md`: the mean and the 99 %
    confidence interval of each benchmark on each side, and the ratio. A change whose
    intervals do not overlap is marked.
- **The before point.** The branch `bench/before-clean-code`, from `7661ea9`.
  - `7661ea9` is the first commit of the clean-code pass and changes documents only, so
    its code is `main`'s `8e36a27`.
  - The branch carries a copy of this node, written against the public subset both
    versions share. The `Engine` batch API did not change, and `Solver.Solve` and
    `Solver.SolveStates` exist in both.
  - Every adaptation is listed in that branch's commit message. The known ones are
    `Solver.Mixture` → `MixtureOf`, `CandidateSpecies` → `CandidateSpeciesFor`, and
    `Reactant.Custom` with the formula inline.
  - The branch is never merged, and kept for reproducibility.
- **Run conditions.**
  - The reference machine, a Release build, and no agent or build running in parallel.
  - After and before run back to back in one sitting with one configuration, in the
    order after, before, after, so that drift shows.
- **The CUDA comparison procedure.** A bit hash cannot tell a last-ULP difference from
  a real one (the invariant above), so the comparison run adds this step for every
  CUDA configuration of groups 1 and 6, on both the before and the after build:
  1. Dump every result field and every mole of the run as raw IEEE-754 bits: unit
     count, stations per unit, species count, then per station the status, the
     iteration count, `MixtureState`'s and `PerformanceFigures`' fields in
     declaration order, then the mole array — the layout the 2026-09-15 measurement
     used (recorded in `96439f4`), recreated as a
     small temporary program or test and deleted afterwards, never committed: the
     dump touches accelerator internals no other benchmark needs and would only go
     stale between runs if it were kept.
  2. Compare the before and after dumps: statuses equal, the count of stations whose
     iteration count differs, the largest relative difference per field
     (`|a-b|/max(|a|,|b|)`), and for mole fractions the same restricted to species
     at or above `tolerances.json`'s `moleFractionFloor` (1e-8), split by whether the
     station's iteration count matched (the table's first tier) or not (its second).
  3. Record the largest relative difference per field and per configuration in
     `results/comparison-<date>.md` beside the timing comparison. A CUDA status
     mismatch, an iteration-count share large enough to hide a systematic change, or
     a difference outside the table is not a timing regression: it is a finding for
     a design session, and the comparison run stops short of marking that group a
     pass.

## Acceptance criteria

→ [ACCEPTANCE.md](ACCEPTANCE.md)

## Taboos

- No timing assertion anywhere in the tree.
- No benchmark inside `dotnet test`.
- No state record, pressure or fixture value typed into code.
- No figure recorded from a Debug build, from a machine under load, or from a run with
  another agent building.
- No change to library code for a benchmark's sake: this node measures, it does not
  tune. A finding goes to a design session.
