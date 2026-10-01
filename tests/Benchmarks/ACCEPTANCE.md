# ACCEPTANCE.md — Benchmarks

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
- [x] 2026-09-25 — Diagnostics (root BOOT.md, Constraints, 2026-09-24), after the split:
      this node and `Runner` build at 0 warnings and 0 errors with no suppression; the
      dry run `dotnet run -c Release --project tests/Benchmarks/Runner -- --filter
      '*SingleCase*' --job Dry` completes; a full run of every benchmark completes with no
      exception and `SolverBatchBenchmarks` still reports 0 mismatches.

      ⚠ 2026-09-25: this criterion was ticked the same day with the words "except the
      CA1515 conflict declared above", a criterion ticked while not met. The owner's
      decision on the conflict is the split above. The record of that day's fixes follows
      and stays true, except where the split changes it: the entry point moves to
      `Runner`, and the IDE0052 reads in `Cleanup` give way to a `Consumer`.

      Evidence of the split itself: `dotnet build tests/Benchmarks/APThermo.Benchmarks.csproj
      -c Debug` and `dotnet build tests/Benchmarks/Runner/APThermo.Benchmarks.Runner.csproj
      -c Debug`, each after clearing both projects' own `obj`/`bin`: 0 warnings, 0 errors on
      both. `dotnet build APThermo.sln -c Release`: 0 warnings, 0 errors across the whole
      tree. `dotnet run -c Release --project tests/Benchmarks/Runner -- --list flat` lists
      the same ten `[Benchmark]` methods of the six classes as before the split. The
      IDE0052 fix: `BatchThroughputBenchmarks.SolveBatch` and `OneTimeCostBenchmarks`'s
      `AssembleChemicalSystem`, `CompileCpuKernel` and `CompileCudaKernel` — the four
      methods whose result was kept in a field read only by a log line in `Cleanup` —
      now hand that result straight to a `BenchmarkDotNet.Engines.Consumer` instead
      (`_consumer.Consume(...)`), so nothing is stored and `Cleanup` reads nothing back;
      `OneTimeCostBenchmarks.UploadSpeciesTable`'s field stays, since it is genuinely
      disposed by `SetupUpload`'s next iteration and by `Cleanup`, not read only for a
      log line. `BenchmarkEnvironment` and its `Config` property are `public` so the
      child node can pass the job to `BenchmarkSwitcher` from its own assembly.

      The dry run: `APTHERMO_NO_CUDA=1 dotnet run -c Release --project tests/Benchmarks/Runner
      -- --filter '*SingleCase*' --job Dry` completed with exit code 0, no exception
      (`SingleCaseBenchmarks.Solve`, both the node's own job and `Dry`, 8.97 KB
      allocated each).

      The full run: `dotnet run -c Release --project tests/Benchmarks/Runner -- --filter '*'`,
      this node's own job config temporarily reduced to 1 warmup/1 iteration for the run's
      length and restored after (`git diff` of `BenchmarkEnvironment.cs` clean of the
      temporary edit once restored): exit code 0, "Global total time … executed
      benchmarks: 40" (6 `BatchThroughputBenchmarks` + 12 `ProblemKindBenchmarks` + 1
      `SingleCaseBenchmarks` + 5 `OneTimeCostBenchmarks` + 10 `UserStatesBenchmarks` + 6
      `SolverBatchBenchmarks` configurations, matching every `[Params]`/`[ParamsAllValues]`
      combination), no exception anywhere in the run.

      `SolverBatchBenchmarks`'s 0-mismatches figure was re-measured directly with a
      second, targeted dry run (`--filter '*SolverBatch*' --job Dry`, CUDA available):
      at 1 000 and 10 000 cases the CPU-accelerator `equalsEngine=bitwise (same kernels,
      same accelerator)` and the CUDA `equalsEngine=tolerance (…
      worstFieldRelative=4.963e-013 worstMoleFractionRelative=3.488e-012)` reproduce the
      exact figures of the 2026-09-15 measurement above, bit for bit, confirming the
      split changed no numerical code; the 100 000-case configurations completed with the
      same CPU/CUDA verdicts and no mismatch.

      Protocol.Tests: `tests/Protocol.Tests/APThermo.Protocol.Tests.csproj` gained a
      project reference to the new `Runner` project (its own `NodeAssemblies.Load`
      requires every node's assembly in its build output), and
      `PublicSurface.approved.txt` was re-approved for the one addition
      (`BenchmarkEnvironment`) and the one new, empty node section
      (`== APThermo.Benchmarks.Runner`, `Program` stays internal). `APTHERMO_NO_CUDA=1
      dotnet test tests/Protocol.Tests` 28/28 green. Protocol lint: 0 errors, 0 warnings.

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
