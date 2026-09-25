# BOOT.md — Benchmarks

## Purpose

Measures how fast the library computes, so that a change of the code, the clean-code
pass of 2026-09-14/15 first, can be compared against the code before it. The
bit-for-bit guards prove that the numbers did not change; nothing proves the speed.
Extracting kernel stages can change inlining on the CPU accelerator and in the
NVVM-generated CUDA code. This node is a library of BenchmarkDotNet benchmark classes,
run by hand through its child node [Runner](Runner/BOOT.md) (2026-09-25), never by
`dotnet test`. It records figures and asserts none.

⚠ 2026-09-25: stood "a BenchmarkDotNet console project". The root's Diagnostics
constraint (CA1515) forbids public types in an executable, and BenchmarkDotNet requires
public benchmark classes (the Constraints ⚠ of the same date). CA1515 does not apply to a
library, so the owner split the node: the benchmark classes, the job configuration and
the shared inputs stay here in a library, and the console entry point moves to the child
node `Runner`.

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
    within the GPU/CPU tolerance table of `tests/Execution.Tests`
    (`GpuCpuTolerances.Entries`: its first tier, relative 1e-10 on temperature and
    1e-9 on the other fields, where the two sides' iteration counts at a station
    match; its second tier, relative 1e-9 on mole fractions at or above
    `tolerances.json`'s `moleFractionFloor`, where they do not). NVVM compiles the
    restructured kernels the clean-code pass produced to different last-ULP
    arithmetic than it compiled the kernels before the pass — the same
    libdevice-against-.NET last-ULP effect the root `BOOT.md`'s GPU-equals-CPU
    invariant already documents for a single run. The comparison run records the
    largest relative difference per field (the CUDA comparison procedure under
    `## Constraints`), so that a systematic change cannot hide inside the tolerance.

  ⚠ 2026-09-15: this invariant first required the CUDA hashes equal too, the same as
  the CPU accelerator's. The first dry run of this node (`## Acceptance criteria`,
  the before-branch criterion) found `BatchThroughputBenchmarks` (group 1) and
  `UserStatesBenchmarks` (group 6) bit-for-bit equal between `7661ea9` and this
  branch on the CPU accelerator and bit-for-bit *different* on CUDA, for identical
  inputs and identical benchmark logic (`ProblemKindBenchmarks`, group 2, is
  CPU-only and matched throughout, so the rocket-path adaptation itself was not the
  cause). Measuring the size of the CUDA difference directly — a raw dump of every
  result field and every mole, before and after, at every station of groups 1
  (1 000 and 10 000 cases; 100 000 was not dumped, since a conclusion already four
  orders of magnitude inside the tolerance would not change) and 6 (all 48 states)
  — found the statuses equal, the iteration counts equal at every station (0 of
  4 000, 0 of 40 000 and 0 of 48 differ), and the largest relative difference per
  field at machine epsilon: 4.863331e-16 on `CvEquilibrium` and 4.160798e-16 on
  `CpEquilibrium` (group 1, both case counts, the same fixture case replicated),
  2.184873e-16 on `Entropy` (group 6); every other field and every compared mole
  fraction exactly 0.0. None of these approach the table's loosest tier (1e-9). The
  libnvvm options and the libdevice post-link inputs of the two trees are identical:
  the same single `-arch=<arch>` compiler option (`NvvmOptions` in both trees'
  `src/Execution/LibDevicePostLink.cs`), the same `arch` derived from the same
  `^\.target\s+sm_(\d+)` match against the kernel's own PTX, the same module and
  libdevice bytes handed to `CompileProgram` — the two trees' post-link code differs
  only in shape (the after-tree decomposes the inline `Link` method of `7661ea9`
  into `NvvmOptions`, `TargetArch`, `WrapperBody`, `CompileWrappers`,
  `InsertAfterHeader`, `AssertEveryWrapperDefined` and `TrialLoad`), never in the
  compiler input. The invariant now reads as above.
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

⚠ 2026-09-15, design: this list is the design's expectation. The coder trims or extends
it to exactly the nodes the code names, which the dependency check holds. Any difference
is recorded here with the reason.

⚠ 2026-09-15, coding: the code also names `Equilibrium.ProblemKind`, added to the list
above. `Problems.EquilibriumProblem.Kind` and `Execution.EquilibriumBatch.Kind` are of
that type, and `SingleCaseBenchmarks` and `OneTimeCostBenchmarks` name it directly to
build an `EquilibriumProblem` and a trivial `EquilibriumBatch`; the design's list missed
it the way `Problems`' own first version once did (its `BOOT.md`, the 2026-09-12 note
on `ProblemKind`). No node of the design's list turned out unused.

⚠ 2026-09-15, coding: the dependency check's walk also named `Transport.TransportTable`,
so [Transport](../../src/Transport/API.md) joins the list. `BatchThroughputBenchmarks`
and `OneTimeCostBenchmarks` never pass a transport table to `Engine.Upload` — group 1
and group 4 measure the equilibrium and rocket paths only, transport is out of scope
for both — but `Upload`'s second parameter is `TransportTable? transport = null`, and
the walk reads a called member's whole signature, optional parameters included, not
only the arguments a call site supplies.

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

    ⚠ 2026-09-25 (Diagnostics constraint, root BOOT.md): confirmed empirically rather
    than asserted. Making one benchmark class (`SingleCaseBenchmarks`) `internal` for
    CA1515 and running `dotnet run -c Release --filter '*SingleCaseBenchmarks*' --job
    Dry` failed validation with `Benchmarked method 'Solve' is within a non-visible
    class, all declaring types must be public`, from the `InProcessNoEmitToolchain`
    this node's job configuration uses (`## Constraints`, Configuration); reverted, and
    the same dry run then completed. CA1515 on the six benchmark classes
    (`BatchThroughputBenchmarks`, `OneTimeCostBenchmarks`, `ProblemKindBenchmarks`,
    `SingleCaseBenchmarks`, `SolverBatchBenchmarks`, `UserStatesBenchmarks`) and the two
    enums a public benchmark class exposes through a `[Params]`/`[ParamsAllValues]`
    property (`BenchmarkProblemKind`, `UserStateSelection` — a property's type cannot be
    less accessible than the property itself) is therefore an unresolvable-in-code
    conflict, reported rather than suppressed (root BOOT.md, Diagnostics constraint: "a
    conflict that code cannot resolve goes to the owner").
  - The root's code-shape constraint holds as for every type of the tree.
- **Configuration.** One job for every group:
  - Release, x64, the in-process toolchain, so that ILGPU's native libraries and the
    CUDA post-link load as in the library;
  - `MemoryDiagnoser` on;
  - 5 warmup iterations, 20 measured iterations, invocation count 1, unroll factor 1,
    bounded so that a full run on the reference machine takes about an hour or less.

  One-time costs run as cold starts.

  ⚠ 2026-09-15: the job first read 1 warmup iteration and 3 measured iterations. The
  first timed comparison run (after, before, after) found BenchmarkDotNet's 99.9 %
  half-interval as wide as the mean or wider for most benchmarks on three samples
  (`ProblemKind` hp without transport, 0.358 ± 0.507 ms; tp, 1.321 ± 1.300 ms), and the
  two after runs drifted apart (100 000 CPU-accelerator cases: 5346.96 ± 154.96 ms,
  then 3461.43 ± 457.66 ms) — nothing could be compared on that. Found by the first
  comparison run, 2026-09-15.
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
     - kernel compilation on the CPU accelerator, and on CUDA with the libdevice
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

- [x] 2026-09-15 — The node builds in Release with the solution
      (`dotnet build APThermo.sln -c Release`, 0 errors), and
      `dotnet run -c Release --project tests/Benchmarks -- --list flat` lists all ten
      `[Benchmark]` methods of the six classes (group 5 rides on the `MemoryDiagnoser`
      of groups 1, 6 and 7, `API.md`'s note on `## Groups`). The protocol checks stay
      green: `protocol_lint` 0 errors/0 warnings, and `APTHERMO_NO_CUDA=1 dotnet test
      tests/Protocol.Tests -c Release` 19/19 (surface, coverage, declarations,
      dependencies, shape).

      ⚠ 2026-09-15 (group 7): this bullet read "nine ... five classes ... groups 1
      and 6" before `SolverBatchBenchmarks` (group 7) was added; the historical
      `bench/before-clean-code` branch criterion below still names nine, correctly,
      since that branch was never touched by this addition.
- [x] 2026-09-15 — Every solving benchmark records its statuses and results hash. One
      dry run of every group (`--job Dry`, this machine, CUDA available) logged every
      case `ok`: group 1, 6/6 configurations (1 000/10 000/100 000 cases × CPU/CUDA)
      100 % `ok`; group 2, 12/12 configurations (six kinds × transport) `status=Ok`;
      group 3, `status=Ok`; group 6, 10/10 configurations (five selections × CPU/CUDA)
      100 % `ok`; group 7, 6/6 configurations (1 000/10 000/100 000 cases × CPU/CUDA)
      100 % `ok`. No case failed.
- [x] 2026-09-15 — Group 7 (`SolverBatchBenchmarks`) agrees with group 1's engine
      path on the same batch, at every case count, on both accelerators
      (`--job Dry --filter '*SolverBatch*'`, this machine, CUDA available;
      `EngineSolverComparison`, the invariant above): on the CPU accelerator every
      `MixtureState` field, every `PerformanceFigures` field and every mole fraction
      of every case was bit-for-bit equal to group 1's engine result
      (`equalsEngine=bitwise (same kernels, same accelerator)`, 1 000/10 000/100 000
      cases, 0 mismatches). On CUDA the two paths were not bit-for-bit identical, so
      the comparison fell back to the GPU/CPU tolerance tiers and the fixtures node's
      `moleFractionFloor`/`polishThresholdRelative`
      (`equalsEngine=tolerance ...`, 1 000/10 000/100 000 cases, 0 mismatches over
      either tier): worst observed 4.963e-13 relative on a state or performance
      field (tier 1e-10 on temperature, 1e-9 on every other field) and 3.488e-12 on a
      mole fraction (tier 1e-9, `polishThresholdRelative`), both about three orders of
      magnitude inside their tier at every CUDA case count. No timing figure was
      recorded from this dry run (`## Constraints`, the job); the timed comparison
      between group 1 and group 7 is a later run on a quiet machine.
- [x] 2026-09-15 — The user's state runs read `data/user-states.json`. Its four records
      and the pressure rule are the ones given to this coding session, verbatim; its 48
      states (first + i × step by index, record order, pressure index ascending within
      a record) equal a separately generated reference of the same 48 records bit for
      bit, checked field by field against every double's IEEE-754 bits, in a one-time
      verification recorded in `720cfb8`'s commit message (the check script and the
      reference file are not part of that commit — not a fixture the node reads).
      Every one of the 48 states was accepted (`ok=48/48` and `ok=12/12` per record,
      the previous criterion's group 6 run).
- [x] 2026-09-15 — `bench/before-clean-code` exists from `7661ea9`, builds in Release,
      and lists the same nine benchmarks the after branch does.

      The node was adapted and built there (`LocalBitHash` replaces `Harness.BitHash`,
      absent at `7661ea9`; `Solver.CandidateSpeciesFor` → `CandidateSpecies`; the
      rocket kinds of group 2 go through `Solver.Solve(ElementalMixture,
      RocketProblem)` instead of the not-yet-existing `SolveRocketStates`,
      `FixtureStateRecords.RocketCase`'s doc comment), `--list flat` lists the same
      nine benchmarks, and every dry-run status was `ok`. The CPU-side results hashes
      of groups 1, 2 and 6 equal the after branch's, bit for bit. The CUDA-side
      hashes of groups 1 and 6 did not; by the invariant above as first written that
      would have stopped the commit, so the branch was held uncommitted at
      `<SCRATCH>/bench-before` while the difference was measured (the ⚠ paragraph
      under `## Invariants` records the measurement and its figures). The measured
      difference is within the tolerance table the invariant now asks of CUDA, so the
      branch was committed, with the figures in its commit message, and the scratch
      worktree removed.
- [x] 2026-09-15 — The comparison run: after, before, after on the reference machine
      with nothing else running (the run conditions recorded in `f461af1`: build servers
      shut down, `CUDA_CACHE_DISABLE=1`, no other dotnet process but the editor's C#
      Dev Kit and its test host).
      - The results and `run.md` of each run are committed:
        `results/2026-09-15-8f99d11-run1/`, `results/2026-09-15-427fc0d-run2/`,
        `results/2026-09-15-8f99d11-run3/`.
      - The CPU-accelerator results hashes of before and after are equal, group by
        group, for every configuration of groups 1, 2, 3 and 6 (`results/comparison
        -2026-09-15.md`, `## Same-work verdict`); the CUDA-side comparison follows
        the procedure under `## Constraints` and its per-field figures are recorded
        beside the timings (`results/comparison-2026-09-15.md`, `## The CUDA
        comparison procedure`) — every figure at machine epsilon, inside the
        tolerance table's first tier, no status or iteration-count mismatch.
      - `results/comparison-2026-09-15.md` marks every change beyond the confidence
        intervals (`## Marked changes`) and hands the list to a design session
        without guessing a cause.
- [ ] Diagnostics (root BOOT.md, Constraints, 2026-09-24), after the split of 2026-09-25:
      this node and `Runner` build at 0 warnings and 0 errors with no suppression; the
      dry run `dotnet run -c Release --project tests/Benchmarks/Runner -- --filter
      '*SingleCase*' --job Dry` completes; a full run of every benchmark completes with no
      exception and `SolverBatchBenchmarks` still reports 0 mismatches.

      ⚠ 2026-09-25: this criterion was ticked the same day with the words "except the
      CA1515 conflict declared above", a criterion ticked while not met. The owner's
      decision on the conflict is the split above. The record of that day's fixes follows
      and stays true, except where the split changes it: the entry point moves to
      `Runner`, and the IDE0052 reads in `Cleanup` give way to a `Consumer`.

- [x] 2026-09-25 — (record of the first Diagnostics pass) the node builds
      at 0 warnings/0 errors except the CA1515 conflict declared above (the Constraints
      ⚠ of 2026-09-25), unresolvable without either dropping BenchmarkDotNet's
      `InProcessNoEmitToolchain` or moving the benchmark classes to a node of their own
      — both a design decision for the owner, not a coding-task fix.
      - CS1591: every hollow `<inheritdoc/>` `dotnet format` inserted where no base
        member existed (47 across `BatchThroughputBenchmarks`, `OneTimeCostBenchmarks`,
        `ProblemKindBenchmarks`, `Program`, `SingleCaseBenchmarks`,
        `SolverBatchBenchmarks`, `UserStatesBenchmarks`, `UserStatesBenchmarks`'s own
        `UserStateSelection` enum with it) replaced with a real `<summary>`.
      - `Program` (the executable's entry point, never referenced by anything outside
        the assembly, BenchmarkDotNet included) made `internal` for CA1515: a `Main`
        method needs no accessibility for the runtime to find it.
      - IDE0072's "populate switch" fix had planted `throw new NotImplementedException()`
        on `ProblemKindBenchmarks.LoadRecord`'s three rocket-kind arms, each reached by
        an ordinary run of that kind (the fixture-loading branch they replaced was the
        one every rocket case took) — reverted to one combined arm covering all three,
        with a genuine `ArgumentOutOfRangeException` default for a value outside the six
        named kinds, satisfying IDE0072 without planting a live trap; the sibling
        `WithFlow` switch's own three added arms (`Tp`/`Hp`/`Sp` throwing) are correct
        as `dotnet format` left them, since `WithFlow` is only ever reached from the
        three rocket-kind arms.
      - IDE0052 ("value assigned … never read") on five benchmark methods that stored
        their result in a private field only for BenchmarkDotNet to observe (preventing
        the JIT from treating the call as dead code) resolved two ways: where the result
        type is already on the package surface (`BatchThroughputBenchmarks.SolveBatch`
        keeps its `RocketBatchResult` field, since `Execution`'s batch types left the
        package surface with the rest of the tree contract, root BOOT.md, Delivery —
        `Cleanup` now logs it, a genuine read) — a public benchmark method cannot return
        an internal type either way (CS0050, hit while first trying the return-value
        fix); `OneTimeCostBenchmarks`'s four (`AssembleChemicalSystem`,
        `UploadSpeciesTable`, `CompileCpuKernel`, `CompileCudaKernel`) keep their fields
        for the same CS0050 reason, `Cleanup` now logging the assembled table's species
        count and the last compiled batch's status.
      - CA1859: `OneTimeCostBenchmarks.BuildTrivialBatch`'s `molesPerKilogram` parameter
        narrowed from `IReadOnlyList<double>` to `double[]`, matching what
        `FixtureJson.ReadElementMoles` already returns.

      Evidence: `dotnet build tests/Benchmarks/APThermo.Benchmarks.csproj -c Release`
      (default, strict settings) after clearing this node's own `obj`/`bin`: 8 warnings,
      all CA1515 on the six benchmark classes and the two enums named above, 0 errors,
      0 of any other diagnostic. A full run of every benchmark
      (`dotnet run -c Release --filter '*'`, `APTHERMO_NO_CUDA=1`, this node's own job
      config temporarily reduced to 1 warmup/1 iteration for the run's length and
      restored after) completed all 41 benchmark×parameter combinations with no
      exception, `SolverBatchBenchmarks` again logging `equalsEngine=bitwise`/
      `equalsEngine=tolerance` with 0 mismatches as the criterion above records.
      Protocol lint: 0 errors, 0 warnings. `git status --short` clean of the temporary
      job-config edit and of the `BenchmarkDotNet.Artifacts/` the run and the
      CA1515 proof left behind (both `.gitignore`d, removed anyway).

## Taboos

- No timing assertion anywhere in the tree.
- No benchmark inside `dotnet test`.
- No state record, pressure or fixture value typed into code.
- No figure recorded from a Debug build, from a machine under load, or from a run with
  another agent building.
- No change to library code for a benchmark's sake: this node measures, it does not
  tune. A finding goes to a design session.
