# API.md — Benchmarks

The node is run from the command line; no node of the tree uses it. Its public types
exist because BenchmarkDotNet requires public benchmark classes, public `[Benchmark]`,
`[GlobalSetup]`, `[GlobalCleanup]`, `[IterationSetup]` and `[IterationCleanup]` methods,
and public `[Params]` properties. They are listed here so that no public type goes
undocumented (root taboo).

## Entry point ✅

```csharp
namespace AerospacePropellantThermodynamics.Benchmarks;

// dotnet run -c Release --project tests/Benchmarks -- [BenchmarkDotNet arguments]
// e.g. --list flat, --filter *UserStates*, --job Dry
public static class Program
{
    public static int Main(string[] args);   // BenchmarkSwitcher over the groups below, BenchmarkEnvironment.Config
}
```

## Groups ✅

One class per group of `BOOT.md`, Constraints, group 5 (allocations on the batch path)
excepted: it has no benchmark of its own and rides on the `MemoryDiagnoser` of
`BatchThroughputBenchmarks` and `UserStatesBenchmarks`, the two groups on the batch
path.

```csharp
namespace AerospacePropellantThermodynamics.Benchmarks;

// Group 1: the LOX/LH2 rocket sweep family at 1 000, 10 000 and 100 000 cases, on the
// CPU accelerator and on CUDA, through the raw Execution.Engine batch API.
public class BatchThroughputBenchmarks
{
    public int CaseCount { get; set; }                       // [Params] 1000, 10000, 100000
    public AcceleratorKind Accelerator { get; set; }          // [Params] Cpu, Cuda
    public void Setup();                                      // [GlobalSetup]
    public RocketBatchResult SolveBatch();                    // [Benchmark]
    public void Cleanup();                                    // [GlobalCleanup]
}

// The six problem kinds group 2 sweeps: tp, hp, sp, and rocket in each of its three
// flow models.
public enum BenchmarkProblemKind
{
    Tp, Hp, Sp, RocketShiftingEquilibrium, RocketFrozenAtChamber, RocketFrozenAtThroat,
}

// Group 2: one case of every problem kind, through Problems.Solver, on the CPU
// accelerator, each with and without transport.
public class ProblemKindBenchmarks
{
    public BenchmarkProblemKind Kind { get; set; }            // [ParamsAllValues]
    public bool Transport { get; set; }                       // [Params] false, true
    public void Setup();                                       // [GlobalSetup]
    public object Solve();                                     // [Benchmark]
    public void Cleanup();                                     // [GlobalCleanup]
}

// Group 3: one case through Problems.Solver's single-case overload, the latency a
// .NET caller sees, on the CPU accelerator.
public class SingleCaseBenchmarks
{
    public void Setup();                                       // [GlobalSetup]
    public EquilibriumResult Solve();                           // [Benchmark]
    public void Cleanup();                                      // [GlobalCleanup]
}

// Group 4: the one-time costs of database load, chemical-system assembly, kernel
// compilation on the CPU accelerator and on CUDA with the libdevice post-link, and
// species-table upload.
public class OneTimeCostBenchmarks
{
    public void Setup();                                       // [GlobalSetup]
    public SpeciesDatabase LoadDatabase();                      // [Benchmark]
    public SpeciesTable AssembleChemicalSystem();                // [Benchmark]
    public void SetupUpload();                                  // [IterationSetup(Target = nameof(UploadSpeciesTable))]
    public UploadedTables UploadSpeciesTable();                  // [Benchmark]
    public void SetupCpuCompile();                              // [IterationSetup(Target = nameof(CompileCpuKernel))]
    public EquilibriumBatchResult CompileCpuKernel();            // [Benchmark]
    public void CleanupCpuCompile();                            // [IterationCleanup(Target = nameof(CompileCpuKernel))]
    public void SetupCudaCompile();                             // [IterationSetup(Target = nameof(CompileCudaKernel))]
    public EquilibriumBatchResult CompileCudaKernel();           // [Benchmark]
    public void CleanupCudaCompile();                           // [IterationCleanup(Target = nameof(CompileCudaKernel))]
    public void Cleanup();                                      // [GlobalCleanup]
}

// Which states of data/user-states.json a run of UserStatesBenchmarks solves.
public enum UserStateSelection { All, Record1, Record2, Record3, Record4 }

// Group 6: the user's four AP/HTPB/Al records of data/user-states.json, through
// Solver.SolveStates, on the CPU accelerator and on CUDA — all 48 states together,
// and each record's 12 alone.
public class UserStatesBenchmarks
{
    public UserStateSelection Selection { get; set; }          // [ParamsAllValues]
    public AcceleratorKind Accelerator { get; set; }            // [Params] Cpu, Cuda
    public void Setup();                                        // [GlobalSetup]
    public IReadOnlyList<EquilibriumResult> SolveStates();      // [Benchmark]
    public void Cleanup();                                      // [GlobalCleanup]
}
```

`AcceleratorKind` is `Execution`'s; `EquilibriumResult` is `Problems`'s.
`BatchThroughputBenchmarks` and `OneTimeCostBenchmarks` bypass `Problems.Solver`
entirely for the raw `Execution.Engine` batch API, so their `Accelerator` axis maps
`Cuda` to `EngineOptions { Accelerator = AcceleratorKind.Auto }` (`AcceleratorSelection`,
internal): the one `AcceleratorKind` that falls back to the CPU accelerator instead of
throwing when CUDA is unavailable, recording why through `AcceleratorInfo.CudaSkippedBecause`
(BOOT.md, Invariants — CUDA is optional, a benchmark asking for it never fails).

Every group that solves logs one diagnostic line per configuration to the console
during `[GlobalSetup]` (BOOT.md, Invariants): the case count or kind, the accelerator,
the statuses (as an ok-count, or the flow/kind and status directly), and a
`Harness.BitHash` hex digest of the result's `MixtureState` fields, mole fractions and
status. This is the "hash of its results" the acceptance criteria read from a run's
console output.

⚠ 2026-09-15, coding: the design's sketch left `## Groups` under ⏳ with a class per
name and no members; this replaces it with the real classes BenchmarkDotNet requires
(every `[Benchmark]`, `[GlobalSetup]`, `[GlobalCleanup]`, `[IterationSetup]`,
`[IterationCleanup]` method and `[Params]`/`[ParamsAllValues]` property must be public
for the library to see it) and marks the section ✅.

## Results

`results/<yyyy-mm-dd>-<commit>/` (BenchmarkDotNet markdown and CSV, `run.md`) and
`results/comparison-<yyyy-mm-dd>.md`, as `BOOT.md` describes. Text files; nothing reads
them but a person. Not yet written: the comparison run is the coordinator's, not this
commit's (BOOT.md, Acceptance criteria).
