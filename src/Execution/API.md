# API.md — Execution

Namespace `AerospacePropellantThermodynamics.Execution`. The node exposes an engine
bound to one accelerator, batch containers and results for the three programs, the
description of the accelerator that ran a batch, and the probe of the root's math
list. Everything not listed here is internal and may change.

## Engine ✅

```csharp
namespace AerospacePropellantThermodynamics.Execution;

public enum AcceleratorKind { Auto, Cpu, Cuda }

public sealed record EngineOptions
{
    public const string NoCudaVariable = "APTHERMO_NO_CUDA";   // "1" forbids CUDA
    public const int DefaultChunkSize = 16384;
    public const long DefaultScratchBytes = 256L << 20;
    public AcceleratorKind Accelerator { get; init; } = AcceleratorKind.Auto;
    public int CudaDeviceIndex { get; init; } = 0;
    public string? LibNvvmPath { get; init; }          // explicit nvvm64_40_0.dll, tried first
    public string? LibDevicePath { get; init; }        // explicit libdevice.10.bc, tried first
    public bool LibDeviceDiscovery { get; init; } = true;   // CUDA_PATH and the toolkit directories after the explicit pair
    public int ChunkSize { get; init; } = DefaultChunkSize;         // cases (or stations) per launch
    public long ScratchBytes { get; init; } = DefaultScratchBytes;  // a chunk shrinks so that its scratch stays within this
}

public sealed record AcceleratorInfo(
    AcceleratorKind Kind, string DeviceName, string IlgpuVersion,
    string? LibNvvmPath, string? LibDevicePath, int ThreadsOrMultiprocessors);

public sealed record RunTimings(TimeSpan WarmUp, TimeSpan Upload, TimeSpan Kernel, TimeSpan Download);

public sealed class AcceleratorUnavailableException : Exception
{
    public AcceleratorUnavailableException(string message, IReadOnlyList<string> pathsTried, Exception? inner = null);
    public IReadOnlyList<string> PathsTried { get; }   // the libnvvm and libdevice paths examined, in order
}

public sealed class Engine : IDisposable
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

public sealed class UploadedTables : IDisposable        // device copies of the tables; reusable across batches of the engine that made them
{
    public SpeciesTable Species { get; }
    public TransportTable? Transport { get; }
    public void Dispose();
}

public static class MathProbe
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
when a chunk's scratch would exceed `ScratchBytes`; the results of a batch do not
depend on the chunking.

## Batches ✅

```csharp
public sealed class EquilibriumBatch                     // structure of arrays, one entry per case; element order of the table
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

public sealed class RocketBatch
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

public sealed class TransportBatch                       // stations to evaluate: a temperature and a composition each
{
    public TransportBatch(int stationCount, int speciesCount);
    public static TransportBatch FromRocket(RocketBatchResult result);         // every station, the moles array shared
    public static TransportBatch FromEquilibrium(EquilibriumBatchResult result);
    public int Count { get; }
    public int SpeciesCount { get; }
    public double[] Temperature { get; }                 // [station] K
    public double[] Moles { get; }                       // [station * SpeciesCount + species] kmol per kg
}

public sealed class EquilibriumBatchResult
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

public sealed class RocketBatchResult
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

public sealed class TransportBatchResult
{
    public int Count { get; }
    public TransportFigures[] Figures { get; }           // [station]
    public CaseStatus[] Status { get; }                  // [station]
    public RunTimings Timings { get; }
    public AcceleratorInfo Accelerator { get; }
}

public sealed class SpeciesFunctionBatch                 // evaluations of the species functions: a table species and a temperature per entry
{
    public SpeciesFunctionBatch(int count);
    public int Count { get; }
    public int[] Species { get; }                        // [entry] table index
    public double[] Temperature { get; }                 // [entry] K
}

public sealed class SpeciesFunctionBatchResult          // dimensionless, as Thermo's SpeciesFunctions return them
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

The kernel parameter structs `EquilibriumBatchViews`, `RocketBatchViews` and
`TransportBatchViews` are public only because ILGPU requires kernel parameter types to
be; they carry the device views of one chunk and are not meant to be used from outside.

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
| ILGPU version or reflected member mismatch | `InvalidOperationException` at `Engine.Create`, naming the ILGPU version |
| a batch of zero cases or zero elements or species | `ArgumentOutOfRangeException` at construction |
| batch arrays of inconsistent lengths, a batch of another element or species count than the table, tables of another engine, a transport run over tables uploaded without a transport table, a transport table of another species table, a chunk size of zero | `ArgumentException` before any kernel runs |
| a kernel's PTX calls a wrapper ILGPU has no fragment for, the post-link produced no definition, libnvvm or the driver refused the PTX | `InvalidOperationException` naming the wrapper or carrying the compiler's log, on the first run of that program |
| per-case numerical failure | `CaseStatus` in the result; no exception |
| a disposed engine or tables | `ObjectDisposedException` |

## Side effects

Creates an ILGPU context and accelerator; reads the environment variables
`APTHERMO_NO_CUDA`, `CUDA_PATH` and `ProgramFiles`; loads native libraries (the CUDA
driver, libnvvm) only when CUDA is chosen. No files are written.

## Out of scope

- Turning propellant definitions into element moles and enthalpies: `Problems`.
- Interpreting statuses for a user, building result records with species names: `Problems`.
