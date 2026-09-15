# BOOT.md — Benchmarks

## Purpose

Measures how fast the library computes, so that a change of the code, the clean-code
pass of 2026-09-14/15 first, can be compared against the code before it. The
bit-for-bit guards prove that the numbers did not change; nothing proves the speed.
Extracting kernel stages can change inlining on the CPU accelerator and in the
NVVM-generated CUDA code. This node is a BenchmarkDotNet console project, run by
hand, never by `dotnet test`. It records figures and asserts none.

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

- **Project.** `AerospacePropellantThermodynamics.Benchmarks`, an executable in the
  solution. It builds with the solution, so the protocol checks cover it. It has no
  test SDK, so `dotnet test` runs nothing of it.
  - BenchmarkDotNet requires public benchmark classes; each is named in `API.md`.
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
- **Results.** Text, committed.
  - BenchmarkDotNet's GitHub markdown and CSV go under
    `results/<yyyy-mm-dd>-<commit>/`, together with a `run.md` that records the
    machine, the driver, the commit, the build and what else was running (nothing).
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
     used (`SCRATCH/benchmarks-coder-report.md` of that session), recreated as a
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
      (`dotnet build AerospacePropellantThermodynamics.sln -c Release`, 0 errors), and
      `dotnet run -c Release --project tests/Benchmarks -- --list flat` lists all nine
      `[Benchmark]` methods of the five classes (group 5 rides on the `MemoryDiagnoser`
      of groups 1 and 6, `API.md`'s note on `## Groups`). The protocol checks stay
      green: `protocol_lint` 0 errors/0 warnings, and `APTHERMO_NO_CUDA=1 dotnet test
      tests/Protocol.Tests -c Release` 19/19 (surface, coverage, declarations,
      dependencies, shape).
- [x] 2026-09-15 — Every solving benchmark records its statuses and results hash. One
      dry run of every group (`--job Dry`, this machine, CUDA available) logged every
      case `ok`: group 1, 6/6 configurations (1 000/10 000/100 000 cases × CPU/CUDA)
      100 % `ok`; group 2, 12/12 configurations (six kinds × transport) `status=Ok`;
      group 3, `status=Ok`; group 6, 10/10 configurations (five selections × CPU/CUDA)
      100 % `ok`. No case failed.
- [x] 2026-09-15 — The user's state runs read `data/user-states.json`. Its four records
      and the pressure rule are the ones given to this coding session, verbatim; its 48
      states (first + i × step by index, record order, pressure index ascending within
      a record) equal `SCRATCH/user-states-48.json` bit for bit, checked field by field
      against every double's IEEE-754 bits (the check script is not part of this
      commit — a one-time verification, not a fixture the node reads). Every one of
      the 48 states was accepted (`ok=48/48` and `ok=12/12` per record, the previous
      criterion's group 6 run).
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
- [ ] The comparison run: after, before, after on the reference machine with nothing
      else running.
      - The results and `run.md` of each run are committed.
      - The CPU-accelerator results hashes of before and after are equal, group by
        group; the CUDA-side comparison follows the procedure under `## Constraints`
        and its per-field figures are recorded beside the timings.
      - `results/comparison-<date>.md` marks every change beyond the confidence
        intervals, and each marked change is explained or handed to a design session.

## Taboos

- No timing assertion anywhere in the tree.
- No benchmark inside `dotnet test`.
- No state record, pressure or fixture value typed into code.
- No figure recorded from a Debug build, from a machine under load, or from a run with
  another agent building.
- No change to library code for a benchmark's sake: this node measures, it does not
  tune. A finding goes to a design session.
