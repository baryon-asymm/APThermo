# API.md — Execution

Namespace `AerospacePropellantThermodynamics.Execution`. The node exposes an engine
bound to one accelerator, batch containers for the three programs, and the
description of the accelerator that ran a batch. Everything not listed here is
internal and may change.

## Engine ⏳

```csharp
namespace AerospacePropellantThermodynamics.Execution;

public enum AcceleratorKind { Auto, Cpu, Cuda }

public sealed record EngineOptions
{
    public AcceleratorKind Accelerator { get; init; } = AcceleratorKind.Auto;
    public int CudaDeviceIndex { get; init; } = 0;
    public string? LibNvvmPath { get; init; }          // explicit override of the discovery order
    public string? LibDevicePath { get; init; }
    public int ChunkSize { get; init; } = 16384;
}

public sealed record AcceleratorInfo(
    AcceleratorKind Kind, string DeviceName, string IlgpuVersion,
    string? LibNvvmPath, string? LibDevicePath, int ThreadsOrMultiprocessors);

public sealed class Engine : IDisposable
{
    public static Engine Create(EngineOptions options);
    public AcceleratorInfo Accelerator { get; }
    public UploadedTables Upload(SpeciesTable species, TransportTable? transport);
    public EquilibriumBatchResult Run(UploadedTables tables, EquilibriumBatch batch);
    public RocketBatchResult Run(UploadedTables tables, RocketBatch batch);
    public TransportBatchResult Run(UploadedTables tables, RocketBatchResult stations, TransportBatch batch);
}

public sealed class UploadedTables : IDisposable        // device copies of the tables; reusable across batches
{
    public SpeciesTable Species { get; }
    public TransportTable? Transport { get; }
}
```

## Batches ⏳

```csharp
public sealed class EquilibriumBatch                     // structure of arrays, one entry per case
{
    public ProblemKind[] Kind;
    public double[] Pressure;                            // Pa
    public double[] Temperature;                         // K or estimate
    public double[] Target;                              // h or s
    public double[] ElementMoles;                        // [case * elements + element]
    public int Count { get; }
}

public sealed class RocketBatch
{
    public double[] ChamberPressure;                     // Pa
    public double[] ReactantEnthalpy;                    // J/kg
    public FlowModel[] Flow;
    public double[] ElementMoles;                        // [case * elements + element]
    public double[] ExitValues;                          // [case * exits + exit]
    public ExitSpecification[] ExitKinds;                // [exits], same for every case
    public int Count { get; }
    public int Exits { get; }
}

public sealed class TransportBatch
{
    public bool[] Stations;                              // [2 + exits], which stations to evaluate
}

public sealed class EquilibriumBatchResult
{
    public MixtureState[] State;                         // [case]
    public double[] Moles;                               // [case * species + species]
    public CaseStatus[] Status;                          // [case]
    public int[] Iterations;
    public RunTimings Timings;
    public AcceleratorInfo Accelerator;
}

public sealed class RocketBatchResult
{
    public MixtureState[] Stations;                      // [case * (2 + exits) + station]
    public double[] Moles;                               // [case * (2 + exits) * species + …]
    public PerformanceFigures[] Figures;                 // [case * exits + exit]
    public CaseStatus[] StationStatus;                   // [case * (2 + exits) + station]
    public CaseStatus[] Status;                          // [case]
    public RunTimings Timings;
    public AcceleratorInfo Accelerator;
}

public sealed class TransportBatchResult
{
    public TransportFigures[] Figures;                   // [case * (2 + exits) + station]
    public CaseStatus[] Status;                          // [case * (2 + exits) + station]
    public RunTimings Timings;
}

public sealed record RunTimings(TimeSpan WarmUp, TimeSpan Upload, TimeSpan Kernel, TimeSpan Download);
```

Array layouts are documented per field; the element and species orders are those of
the uploaded `SpeciesTable`. A batch may hold any number of cases; the engine chunks it.

## Errors

| Situation | Behaviour |
|---|---|
| `AcceleratorKind.Cuda` requested and no device, no driver, or no libdevice | `AcceleratorUnavailableException` naming the missing piece and every path tried |
| ILGPU version or reflected member mismatch | `InvalidOperationException` at `Engine.Create`, naming the ILGPU version |
| batch arrays of inconsistent lengths | `ArgumentException` before any kernel runs |
| a kernel's PTX calls a wrapper the post-link did not provide | `InvalidOperationException` naming the wrapper |
| per-case numerical failure | `CaseStatus` in the result; no exception |

## Side effects

Creates an ILGPU context and accelerator; reads the environment variable
`APTHERMO_NO_CUDA` and `CUDA_PATH`; loads native libraries (the CUDA driver, libnvvm)
only when CUDA is chosen. No files are written.

## Out of scope

- Turning propellant definitions into element moles and enthalpies: `Problems`.
- Interpreting statuses for a user, building result records with species names: `Problems`.
