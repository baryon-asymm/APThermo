# API.md — Execution

Namespace `APThermo.Execution`. The node exposes the accelerator options and
description, the CUDA-forbidden probe, and the exception a failed bind raises.
Everything not listed here, in a package-surface section (one whose heading carries
no `(tree contract)` mark), is internal and may change without notice (root
`BOOT.md`, Delivery: Public surface). The tree-contract sections below list the
internal types `Problems` uses to run batches (root `BOOT.md`, Delivery: Tree
contracts); the assembly grants `InternalsVisibleTo` to `Problems`, its mirroring
test node, `Benchmarks`, and `ILGPURuntime` for the kernel-parameter views structs
(`APThermo.Execution.csproj`).

⚠ 2026-09-15 (distribution phase): the review of that day
(`SCRATCH/api-review-report.md`, section 4, D1 and F1) found no consumer scenario for
`Engine`, the eight batch and batch-result types, `UploadedTables`, `RunTimings` or
`MathProbe`: every use is `Problems` composing a batch run, or this node's own tests.
They moved from the package surface into the tree contract below. `Engine` itself was
a FACADE verdict, not a straight demotion: consumers used it for exactly two
questions ("what do these options bind to", "is CUDA forbidden"), both now answered by
the new `AcceleratorProbe` below, so `Engine` is internal and `AcceleratorProbe`
replaces it on the package surface. `AcceleratorInfo`'s constructor is internal (M1):
no consumer builds one, only reads it.

## Accelerator ✅

```csharp
namespace APThermo.Execution;

public enum AcceleratorKind { Auto, Cpu, Cuda }

public sealed record EngineOptions
{
    public const string NoCudaVariable = "APTHERMO_NO_CUDA";   // "1" forbids CUDA
    public const int DefaultChunkSize = 16384;
    public const long DefaultScratchBytes = 256L << 20;
    public AcceleratorKind Accelerator { get; init; } = AcceleratorKind.Auto;
    public int CudaDeviceIndex { get; init; } = 0;
    public string? LibNvvmPath { get; init; }          // explicit libnvvm path (nvvm64_40_0.dll on Windows, libnvvm.so on Linux), tried first
    public string? LibDevicePath { get; init; }        // explicit libdevice.10.bc, tried first
    public bool LibDeviceDiscovery { get; init; } = true;   // CUDA_PATH and the toolkit directories after the explicit pair
    public int ChunkSize { get; init; } = DefaultChunkSize;         // cases (or stations) per launch
    public long ScratchBytes { get; init; } = DefaultScratchBytes;  // a chunk shrinks so that its device bytes (every buffer of the chunk) stay within this; positive
}

public sealed record AcceleratorInfo             // the constructor is internal (M1): a consumer only reads one
{
    public AcceleratorKind Kind { get; }
    public string DeviceName { get; }
    public string IlgpuVersion { get; }
    public string? LibNvvmPath { get; }
    public string? LibDevicePath { get; }
    public int ThreadsOrMultiprocessors { get; }
    public string? CudaSkippedBecause { get; init; }   // Auto fell back to the CPU accelerator: the failure that turned the choice, the forbidding variable included, with the paths tried where they apply; null when CUDA was bound or the options asked for the CPU
}

public sealed class AcceleratorUnavailableException : Exception
{
    public AcceleratorUnavailableException(string message, IReadOnlyList<string> pathsTried, Exception? inner = null);
    public IReadOnlyList<string> PathsTried { get; }   // the libnvvm and libdevice paths examined, in order
}

public static class AcceleratorProbe             // replaces Engine on the package surface (F1)
{
    public static AcceleratorInfo Describe(EngineOptions? options = null);   // binds as Engine.Create would, releases the device, returns its description
    public static bool CudaForbidden { get; }     // the environment variable is "1"
}
```

`AcceleratorProbe.Describe` throws `AcceleratorUnavailableException` exactly as the
internal `Engine.Create` does (below); creating a CUDA context takes time, so call it
once per accelerator kind, not per case.

## Engine (tree contract) ✅

```csharp
internal sealed class Engine : IDisposable
{
    public static Engine Create(EngineOptions? options = null);
    public static bool CudaForbidden { get; }          // the environment variable is "1"
    public AcceleratorInfo Accelerator { get; }
    public UploadedTables Upload(SpeciesTable species, TransportTable? transport = null);
    public EquilibriumBatchResult Run(UploadedTables tables, EquilibriumBatch batch);
    public RocketBatchResult Run(UploadedTables tables, RocketBatch batch);
    public TransportBatchResult Run(UploadedTables tables, TransportBatch batch);
    public SpeciesFunctionBatchResult Run(UploadedTables tables, SpeciesFunctionBatch batch);
    public double[] ProbeMath(double[] inputs);        // [input * MathProbe.FunctionCount + function]
    public void Dispose();
}

internal sealed class UploadedTables : IDisposable        // device copies of the tables; reusable across batches of the engine that made them
{
    public SpeciesTable Species { get; }
    public TransportTable? Transport { get; }
    public void Dispose();
}

internal static class MathProbe
{
    public static readonly IReadOnlyList<string> Functions;   // Exp, Log, Log10, Pow, Sqrt, Abs, Min, Max, Floor, Ceiling
    public static int FunctionCount { get; }
    public const double PowExponent = 1.37;
}
```

`Create` with `Auto` binds CUDA when CUDA is not forbidden, libnvvm and libdevice are
found and the device exists, and the CPU accelerator otherwise, including when the
CUDA context cannot be created. With `Cuda` every one of those failures is an
`AcceleratorUnavailableException`. The kernel of each program is compiled (and on
CUDA post-linked) on its first run per engine and cached; that time is the run's
`WarmUp`. Batches are processed in chunks of at most `ChunkSize` cases, and fewer
when a chunk's device bytes would exceed `ScratchBytes`; the results of a batch do not
depend on the chunking. An engine is used from one thread at a time; its kernel cache
is the only synchronised piece.

⚠ 2026-09-14 (the clean-code review): `Create` with `Auto` swallowed every CUDA
failure into a discarded exception and returned a CPU engine whose description said
nothing, so a machine with a broken CUDA installation ran the 56× slower path without
a word. The fallback stays; `AcceleratorInfo.CudaSkippedBecause` now carries the
reason, and the snapshot moved with it. The chunk bound counted only the scratch and
the moles; it now counts every buffer of the chunk, and a `ScratchBytes` of zero or
less is refused like a `ChunkSize` of zero (it used to shrink every launch to one case
silently). The error table's "batch arrays of inconsistent lengths" described a state
the batch constructors make impossible and is gone with the branches that could not
fire.

## Batches (tree contract) ✅

```csharp
internal sealed record RunTimings(TimeSpan WarmUp, TimeSpan Upload, TimeSpan Kernel, TimeSpan Download);

internal sealed class EquilibriumBatch                     // structure of arrays, one entry per case; element order of the table
{
    public EquilibriumBatch(int count, int elementCount);
    public int Count { get; }
    public int ElementCount { get; }
    public ProblemKind[] Kind { get; }
    public double[] Pressure { get; }                    // [case] Pa
    public double[] Temperature { get; }                 // [case] K for tp, the estimate for hp and sp (0 = default)
    public double[] Target { get; }                      // [case] h in J/kg or s in J/(kg·K)
    public double[] ElementMoles { get; }                // [case * ElementCount + element] kmol per kg
}

internal sealed class RocketBatch
{
    public RocketBatch(int count, int elementCount, ExitSpecification[] exitKinds);
    public int Count { get; }
    public int ElementCount { get; }
    public int Exits { get; }
    public int StationCount { get; }                     // 2 + Exits
    public double[] ChamberPressure { get; }             // [case] Pa
    public double[] ReactantEnthalpy { get; }            // [case] J/kg
    public double[] TemperatureEstimate { get; }         // [case] K, 0 = default
    public FlowModel[] Flow { get; }
    public double[] ElementMoles { get; }                // [case * ElementCount + element]
    public double[] ExitValues { get; }                  // [case * Exits + exit]
    public ExitSpecification[] ExitKinds { get; }        // [exit], the same for every case
}

internal sealed class TransportBatch                       // stations to evaluate: a temperature and a composition each
{
    public TransportBatch(int stationCount, int speciesCount);
    public static TransportBatch FromRocket(RocketBatchResult result);         // every station, the moles array shared
    public static TransportBatch FromEquilibrium(EquilibriumBatchResult result);
    public int Count { get; }
    public int SpeciesCount { get; }
    public double[] Temperature { get; }                 // [station] K
    public double[] Moles { get; }                       // [station * SpeciesCount + species] kmol per kg
}

internal sealed class EquilibriumBatchResult
{
    public int Count { get; }
    public int SpeciesCount { get; }
    public MixtureState[] State { get; }                 // [case], zero where the status is not Ok
    public double[] Moles { get; }                       // [case * SpeciesCount + species]
    public CaseStatus[] Status { get; }
    public int[] Iterations { get; }
    public RunTimings Timings { get; }
    public AcceleratorInfo Accelerator { get; }
}

internal sealed class RocketBatchResult
{
    public int Count { get; }
    public int SpeciesCount { get; }
    public int StationCount { get; }                     // 2 + exits
    public MixtureState[] Stations { get; }              // [case * StationCount + station]
    public double[] Moles { get; }                       // [(case * StationCount + station) * SpeciesCount + species]
    public PerformanceFigures[] Figures { get; }         // [case * StationCount + station]
    public CaseStatus[] StationStatus { get; }           // [case * StationCount + station]
    public int[] Iterations { get; }                     // [case * StationCount + station]
    public CaseStatus[] Status { get; }                  // [case]
    public RunTimings Timings { get; }
    public AcceleratorInfo Accelerator { get; }
}

internal sealed class TransportBatchResult
{
    public int Count { get; }
    public TransportFigures[] Figures { get; }           // [station]
    public CaseStatus[] Status { get; }                  // [station]
    public RunTimings Timings { get; }
    public AcceleratorInfo Accelerator { get; }
}

internal sealed class SpeciesFunctionBatch                 // evaluations of the species functions: a table species and a temperature per entry
{
    public SpeciesFunctionBatch(int count);
    public int Count { get; }
    public int[] Species { get; }                        // [entry] table index
    public double[] Temperature { get; }                 // [entry] K
}

internal sealed class SpeciesFunctionBatchResult          // dimensionless, as Thermo's SpeciesFunctions return them
{
    public int Count { get; }
    public double[] CpOverR { get; }                     // [entry]
    public double[] HOverRT { get; }                     // [entry]
    public double[] SOverR { get; }                      // [entry]
    public bool[] InRange { get; }                       // [entry] first lower bound ≤ T ≤ last upper bound; outside, the nearest interval was evaluated
    public RunTimings Timings { get; }
    public AcceleratorInfo Accelerator { get; }
}
```

⚠ 2026-09-12: the species-function batch was added for the front door, which needs
H°(T) of reactant records at their temperatures. The tree's only species functions are
the kernel-compatible ones of `Thermo`, and running them needs a view over accelerator
memory, which this node owns; an evaluation on the host would be a second
implementation of the polynomial.

The kernel parameter structs `EquilibriumBatchViews`, `RocketBatchViews`,
`TransportBatchViews` and `SpeciesFunctionBatchViews` (declared in `Kernels.cs`) are
`internal`; they carry the device views of one chunk, are named by no other assembly
(the assembly's own `ILGPURuntime` grant is what ILGPU needs, below), and are not
meant to be used from outside this node at all.

⚠ 2026-09-15 (distribution phase): this paragraph said the four structs "are public
only because ILGPU requires kernel parameter types to be". Wrong: ILGPU 1.5.3 needs
only `[assembly: InternalsVisibleTo("ILGPURuntime")]` on the declaring assembly (its
own dynamic runtime assembly's name, found by reflecting on ILGPU.dll itself) to load
an `internal` kernel parameter type, method or view element — public is only one way
to satisfy it, not a requirement. The claim entered with `f2e5de7` and `53ec9fb`
(2026-09-12), recording that the CPU accelerator failed to load a struct made internal
without ever trying the grant; the API review of 2026-09-15
(`SCRATCH/api-review-report.md`, section 3) ran the same failure alongside the grant
and confirmed it fixes it, on the CPU accelerator and on CUDA. The four structs are
internal now, with `[assembly: InternalsVisibleTo("ILGPURuntime")]` on this assembly
(`APThermo.Execution.csproj`); the CPU-accelerator tests were shown to fail without
that line before it was added (AGENTS.md §13).

⚠ 2026-09-13: the list named three structs after the fourth had been added with the
species-function batch; found by the coverage check of the protocol tests node, the
first time it ran.

⚠ 2026-09-12: the sketch had the rocket figures per exit (`[case * exits + exit]`),
no `Iterations`, a transport run taking a `RocketBatchResult` and a station mask
(`TransportBatch { bool[] Stations }`), and results without `SpeciesCount` and
`StationCount`. The figures are per station as `Performance` reports them; the
transport pass takes a plain batch of stations, built from either result by the two
factories, so that the engine does not know where a composition came from; the counts
make the flat layouts self-describing. `LibDeviceDiscovery` and `ScratchBytes` were
added to the options: the first so that a test can prove the "paths tried" message
on a machine with a toolkit, the second because the transport scratch is 40 KB per
station and a fixed chunk of 16 384 would take 700 MB.

## Errors

| Situation | Behaviour |
|---|---|
| `AcceleratorKind.Cuda` requested and CUDA forbidden, no libnvvm or libdevice, no device at the index, or the context cannot be created | `AcceleratorUnavailableException` naming the missing piece and every path tried |
| ILGPU version or reflected member mismatch | `InvalidOperationException` at `Engine.Create` (reached through `AcceleratorProbe.Describe` or `Problems`' `Solver.Create`), naming the ILGPU version |
| a batch of zero cases or zero elements or species | `ArgumentOutOfRangeException` at construction |
| a batch of another element or species count than the table, tables of another engine, a transport run over tables uploaded without a transport table, a transport table of another species table, a chunk size or a scratch bound of zero or less | `ArgumentException` before any kernel runs (a batch's arrays cannot be inconsistent: every one is sized by its constructor from one count) |
| a kernel's PTX calls a wrapper ILGPU has no fragment for, the post-link produced no definition, libnvvm or the driver refused the PTX | `InvalidOperationException` naming the wrapper or carrying the compiler's log, on the first run of that program |
| per-case numerical failure | `CaseStatus` in the result; no exception |
| a disposed engine or tables | `ObjectDisposedException` |

## Side effects

Creates an ILGPU context and accelerator; reads the environment variables
`APTHERMO_NO_CUDA`, `CUDA_PATH`, `ProgramFiles` (Windows discovery) and `CUDA_HOME`
(Linux discovery); loads native libraries (the CUDA driver, libnvvm) only when CUDA is
chosen. No files are written.

⚠ 2026-09-15 (distribution phase): this row named `CUDA_PATH` and `ProgramFiles` only,
before Linux discovery (`CUDA_HOME`, root BOOT.md's Platform constraint) was added.

## Out of scope

- Turning propellant definitions into element moles and enthalpies: `Problems`.
- Interpreting statuses for a user, building result records with species names: `Problems`.
