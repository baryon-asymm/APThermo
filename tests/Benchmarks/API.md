# API.md — Benchmarks

The node is run from the command line; no node of the tree uses it. Its public types
exist because BenchmarkDotNet requires public benchmark classes. They are listed here so
that no public type goes undocumented (root taboo).

## Entry point ⏳

```csharp
// dotnet run -c Release --project tests/Benchmarks -- [BenchmarkDotNet arguments]
// e.g. --list flat, --filter *UserStates*
public static class Program
{
    public static int Main(string[] args);   // BenchmarkSwitcher over the groups below
}
```

## Groups ⏳

One class per group of `BOOT.md`, Constraints. The names are the design's; the coder
replaces this sketch with the real classes and their `[Benchmark]` methods, and changes
the heading's mark once they exist.

```csharp
public class BatchThroughputBenchmarks { }   // LOX/LH2 rocket sweep family, 1 000 / 10 000 / 100 000 cases, CPU accelerator and CUDA
public class ProblemKindBenchmarks { }       // tp, hp, sp, rocket shifting and frozen, with and without transport, CPU accelerator
public class SingleCaseBenchmarks { }        // one case through Solver
public class OneTimeCostBenchmarks { }       // database load, chemical system, kernel compilation, table upload
public class UserStatesBenchmarks { }        // the 48 hp states of data/user-states.json, CPU accelerator and CUDA
```

## Results

`results/<yyyy-mm-dd>-<commit>/` (BenchmarkDotNet markdown and CSV, `run.md`) and
`results/comparison-<yyyy-mm-dd>.md`, as `BOOT.md` describes. Text files; nothing reads
them but a person.
