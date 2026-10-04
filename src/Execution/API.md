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
(its section 4, findings D1 and F1, fixed in `9036c6a`) found no consumer scenario for
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
    public string? LibNvvmPath { get; init; }          // explicit libnvvm path (nvvm64_40_0.dll on Windows, libnvvm.so on Linux), tried first; given together with LibDevicePath or not at all (2026-09-26)
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
    public AcceleratorUnavailableException();
    public AcceleratorUnavailableException(string message);
    public AcceleratorUnavailableException(string message, Exception innerException);
    public AcceleratorUnavailableException(string message, IReadOnlyList<string> pathsTried, Exception? inner = null);
    public IReadOnlyList<string> PathsTried { get; }   // the libnvvm and libdevice paths examined, in order
}

public static class AcceleratorProbe             // replaces Engine on the package surface (F1)
{
    public static AcceleratorInfo Describe(EngineOptions? options = null);   // binds as Engine.Create would (the probe kernel included, 2026-09-26), releases the device, returns its description
    public static bool CudaForbidden { get; }     // the environment variable is "1"
}
```

`AcceleratorProbe.Describe` throws `AcceleratorUnavailableException` exactly as the
internal `Engine.Create` does (below); creating a CUDA context takes time, so call it
once per accelerator kind, not per case.

The first three constructors of `AcceleratorUnavailableException` were added
2026-09-24 by the root's Diagnostics constraint (CA1032), for the .NET exception
conventions; the tree itself always throws through the fourth. Their data property
takes a neutral value: `PathsTried` empty.

## Engine (tree contract) ✅

```csharp
internal sealed class Engine : IDisposable
{
    public static Engine Create(EngineOptions? options = null);
    public static bool CudaForbidden { get; }          // the environment variable is "1"
    public AcceleratorInfo Accelerator { get; }
    internal LaunchBudget Budget { get; }               // Execution.Chunks; the session's time budget for a launch (2026-09-28, F2), None off a device with no run-time limit
    internal KernelCache Launchers { get; }             // the typed launchers this engine compiled (2026-09-30); exposed so the tests node can watch them die at Dispose
    public UploadedTables Upload(SpeciesTable species, TransportTable? transport = null);
    public EquilibriumBatchResult Run(UploadedTables tables, EquilibriumBatch batch);
    public RocketBatchResult Run(UploadedTables tables, RocketBatch batch);
    public TransportBatchResult Run(UploadedTables tables, TransportBatch batch);
    public SpeciesFunctionBatchResult Run(UploadedTables tables, SpeciesFunctionBatch batch);
    public double[] ProbeMath(double[] inputs);        // [input * MathProbe.FunctionCount + function]
    internal void RunBatchLoop(Chunks.ChunkPlan plan, Chunks.ChunkBuffers buffers, RunTimer timer, Action<int> launch);  // the chunk loop every Run above drives; exposed for the tests node's own chunk-plan facts
    internal void MarkLost(AcceleratorUnavailableException timeout);  // marks this engine's session lost by an injected timeout (2026-09-29, review); exposed so the tests node can drive ThrowIfLost's refusal without a driver-touching CudaException
    internal bool DropsAfterLoss(CudaError error);      // ILGPU.Runtime.Cuda; the bare-CudaError half of AcceleratorSession.DropsAfterLoss (2026-09-29, review); exposed for the same reason as MarkLost above
    internal void DisposeAfterLoss(IDisposable disposable);  // disposes an UploadedTables buffer, dropping this engine's session's own sticky CudaException (2026-09-29, the third audit pass's finding 2); exposed for the tests node's own lost-session facts
    public void Dispose();                             // empties Launchers before it disposes the session (2026-09-30), so a disposed engine that stays reachable holds no compiled kernel
}

internal sealed class KernelCache                     // typed launchers of the entry points, compiled (and on CUDA post-linked) on first use, one per entry-point name; the engine's only synchronised piece
{
    public KernelCache(AcceleratorSession session);
    public int Count { get; }                          // how many launchers it holds; zero after Clear (2026-09-30)
    public TDelegate Get<TDelegate>(string name, out TimeSpan warmUp) where TDelegate : Delegate;   // warmUp is the compile time, zero when cached
    public void Clear();                               // drops every launcher and the ILGPU context's IR caches (2026-09-30); Engine.Dispose calls it
    internal static Kernel Load(AcceleratorSession session, string name);   // compiles (and on CUDA post-links) and loads the named entry point: the one load path of this node, the bind-time probe included
}

internal sealed class UploadedTables : IDisposable        // device copies of the tables; reusable across batches of the engine that made them
{
    public SpeciesTable Species { get; }
    public TransportTable? Transport { get; }
    public void Dispose();
}

internal static class MathProbe
{
    public static readonly IReadOnlyList<string> Functions;   // Exp, Log, Log10, Pow(1.37), Pow(1.4), Pow(4.6), Sqrt, Floor, Ceiling, Abs, Min(v,1), Max(v,1), Min(1,v), Max(1,v)
    public static int FunctionCount { get; }
    public static readonly IReadOnlyList<double> PowExponents;   // 1.37, 1.4, 4.6 (2026-09-27, the guards audit's F11)
    public const double PowExponent1 = 1.37;
    public const double PowExponent2 = 1.4;
    public const double PowExponent3 = 4.6;
    public static int OutputLength(int inputCount);        // inputCount * FunctionCount; ArgumentException above int.MaxValue (2026-09-30), the bound ProbeMath calls
}
```

⚠ 2026-09-27 (the guards audit's F11): `MathProbe` had one `PowExponent = 1.37` and `Functions` listed a single `Pow` entry, so the probe never exercised any other exponent. `Pow` is now probed at three exponents (1.37, 1.4, 4.6), each its own libdevice call; `PowExponent` is replaced by `PowExponent1`/`PowExponent2`/`PowExponent3` (the kernel-compatible consts `Kernels.Probe` inlines) and the host-readable `PowExponents`, and `Functions` names each exponent. `MathProbe` stays internal; the change is a tree-contract one, not a package-surface one.

⚠ 2026-09-28 (the second hidden-defect audit, Execution finding F1): `Functions` listed one `Min`/`Max` entry each, both compiled from `KernelMath.Min(v, 1.0)`/`Max(v, 1.0)` — the variable-first order only. ILGPU 1.5.3 moves a constant left operand of a floating comparison to the right and inverts its NaN ordering while doing so (root `BOOT.md`, the third ILGPU defect), so the constant-first order compiles to different PTX and, for a NaN `v`, answered differently on CUDA (`KernelMath.Min(1.0, NaN)` was `1.0` on CUDA, `NaN` on the CPU accelerator, before the thermo node's `KernelMath` was made to test both operands for NaN first). `Functions` now probes both orders (`Min(v,1)`/`Max(v,1)` and `Min(1,v)`/`Max(1,v)`), and Floor and Ceiling moved next to the other libdevice-calling entries: they were documented as "the compiler emits directly", which held only for Abs (the post-link's own wrapper inventory names `__nv_floor` and `__nv_ceil` among the seven wrappers it completes for the probe). `StrideCount`/`FunctionCount` rise from 12 to 14.

`Create` with `Auto` binds CUDA when all of the following hold, and the CPU
accelerator otherwise:
- CUDA is not forbidden;
- libnvvm and libdevice are found;
- the device exists and its CUDA context can be created;
- the math probe kernel, which calls every libdevice wrapper of the math list,
  post-links and loads on that device (2026-09-26).

With `Cuda` every one of those failures is an `AcceleratorUnavailableException`. A CUDA
engine therefore never reaches its first run on a device where no kernel can load.

⚠ 2026-09-26: the probe kernel was not part of the binding. On every GPU older than
Blackwell the post-link threw at the first run of every program: `Auto` had already
bound CUDA and had no fallback left, and `Describe` reported the device as usable
(`BOOT.md`, the ⚠ of the invariant "Every CUDA kernel goes through the post-link"). The kernel of each program is compiled (and on
CUDA post-linked) on its first run per engine and cached; that time is the run's
`WarmUp`. Batches are processed in chunks of at most `ChunkSize` cases, and fewer
when a chunk's device bytes would exceed `ScratchBytes`; the results of a batch do not
depend on the chunking. An engine is used from one thread at a time; its kernel cache
is the only synchronised piece.

**Warm-up cost on CUDA** (2026-09-28, the second audit's observation 3): the rocket
kernel's own compile takes 13 to 22 s on the reference machine; the CUDA driver's own
JIT of the post-linked PTX runs on top of that, 55 to 59 s on a compute-cache miss and
well under a second on a hit. The driver's cache is keyed by the PTX text, and ILGPU
names its generated symbols from process-wide counters, so every CUDA engine after the
first *in the same process* gets different names and misses the cache regardless of an
earlier engine's run. Advice for a consumer: one `Solver` per process amortises the
compile once; a process that creates and disposes several engines in turn pays this
cost again for each one.

**A launch's time budget on a display GPU** (2026-09-28, the second audit's Execution
finding F2). A device that reports a kernel run-time limit (Windows WDDM, or under
WSL2) has its driver kill a launch that runs too long and reset the shared display
driver; the CPU accelerator and a device without the limit (TCC mode, headless) are
not affected. `Engine`/`AcceleratorProbe` measure this at bind time and size later
chunks from it (`Execution.Chunks`' `LaunchBudget`, its own `BOOT.md`), but a single
case cannot be split across launches: a rocket or equilibrium system of about 16 or
more elements risks exceeding the default 2 s limit as one case, before any chunking
choice can help. Such a system belongs on the CPU accelerator, or on a CUDA device
without the run-time limit.

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

internal static class BatchLength                          // the one bound the batch constructors call (2026-09-30): a flat per-case host array must fit a 32-bit length
{
    public static int Of(int count, long perCase);         // count * perCase; ArgumentOutOfRangeException above int.MaxValue, before any allocation
}

internal sealed class EquilibriumBatch                     // structure of arrays, one entry per case; element order of the table
{
    public EquilibriumBatch(int count, int elementCount);                       // a cold batch: every case from the solver's own estimate
    public EquilibriumBatch(int count, int elementCount, int seedSpeciesCount); // a seeded batch (2026-10-04): every case from its row of SeedMoles
    public int Count { get; }
    public int ElementCount { get; }
    public int SeedSpeciesCount { get; }                 // 0 for a cold batch
    public ProblemKind[] Kind { get; }
    public double[] Pressure { get; }                    // [case] Pa
    public double[] Temperature { get; }                 // [case] K for tp, the estimate for hp and sp (0 = default)
    public double[] Target { get; }                      // [case] h in J/kg or s in J/(kg·K)
    public double[] ElementMoles { get; }                // [case * ElementCount + element] kmol per kg
    public double[]? SeedMoles { get; }                  // [case * SeedSpeciesCount + species] kmol per kg, table order; null for a cold batch
    public bool IsSeeded { get; }                        // SeedMoles is not null
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
    public MixtureState[] State { get; }                 // [case], zero where the status is not Ok, except NoGasPhase: its Temperature and Pressure
    public double[] Moles { get; }                       // [case * SpeciesCount + species]; on a seeded batch an InvalidInput case holds its seed
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

⚠ 2026-10-04: the equilibrium batch result's `State` said "zero where the status is not Ok". Wrong
since 0.2.2: a `NoGasPhase` case (no gas phase, the condensed minimum) carries its Temperature and
Pressure, the rest zero, as the root's failures-are-values invariant says. Found on 2026-10-04 by the
orchestrator, from the comparison of the gasless families on CUDA (`GpuCpuComparison`), which reads them;
the batch result has no multipliers, so such a case is compared on its moles and its state's two fields.

**Seeded equilibrium batches** (2026-10-04, 0.2.2). A batch built with `seedSpeciesCount` runs every case as
`EquilibriumSolver.Solve` does with `useMolesAsEstimate` ([Equilibrium](../Equilibrium/API.md)), its row of
`SeedMoles` being the moles the solver starts from and `Temperature` the hp and sp estimate; a previous result's
`Moles` row, of the same table, is a valid seed as it is. A seed of zeros is not the cold start: a batch is seeded or
cold as a whole, and a caller with both builds two batches. `SeedMoles` is never written by a run: the pipeline copies
it into the result's moles array, which the device reads before the launch and writes after it
(`Chunks`' `InputOutput`), so a seeded batch costs no more device memory than a cold one and uploads `species × 8`
bytes per case, the size of the download it already pays. The result's `Moles` of a case that ended `InvalidInput`
hold its seed, where a cold batch holds zeros. Seeding is tree contract only: `Problems` builds cold batches and the
package surface offers no seed.

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
(section 3, fixed in `9036c6a`) ran the same failure alongside the grant
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

⚠ 2026-09-30 (the memory investigation of 2026-09-29): a disposed `Engine` kept every kernel
it had compiled for as long as the engine object stayed reachable, and the rocket kernel's
compiled program had grown to 3 GB of ILGPU IR (`BOOT.md`, the criterion of that date). `Dispose`
now empties the kernel cache and the ILGPU context's caches before it disposes the session (dropping the launchers alone frees none of the 143 MiB the rocket kernel's IR keeps, 3.9 GB before the inlining bound: the context's caches hold it); `KernelCache.Count`, `Clear` and
`Engine.Launchers` are new, all tree contract, and no package surface moves. `BatchLength` is not
new: it stood under the batch constructors since 2026-09-28 and is named here since a fact of the
tests node now calls it, in place of allocating a 16 GB array to read a length. `MathProbe.OutputLength`
is the same bound for `ProbeMath`, whose inline check a fact could reach only through a 1.2 GB input
array; `ProbeMath`'s `ArgumentException` for such a count names `inputCount` now, where it named `inputs`.

## Errors

| Situation | Behaviour |
|---|---|
| `AcceleratorKind.Cuda` requested and CUDA forbidden, no libnvvm or libdevice, no device at the index, or the context cannot be created | `AcceleratorUnavailableException` naming the missing piece and every path tried |
| `AcceleratorKind.Cuda` requested and the math probe kernel cannot be post-linked or loaded on the device (2026-09-26) | `AcceleratorUnavailableException` at `Engine.Create` or `AcceleratorProbe.Describe`, its inner exception the post-link's `InvalidOperationException` (the next rows); with `Auto`, the CPU accelerator, the post-link's message in `CudaSkippedBecause` |
| ILGPU version or reflected member mismatch | `InvalidOperationException` at `Engine.Create` (reached through `AcceleratorProbe.Describe` or `Problems`' `Solver.Create`), naming the ILGPU version |
| one of `LibNvvmPath` and `LibDevicePath` given without the other (2026-09-26) | `ArgumentException` at `Engine.Create` or `AcceleratorProbe.Describe`, naming the missing option |
| a batch of zero cases or zero elements or species | `ArgumentOutOfRangeException` at construction |
| a batch of another element or species count than the table, a seeded equilibrium batch whose seed stride is not the table's species count or whose seed holds a value that is not finite (2026-10-04, naming the case and the species), tables of another engine, a transport run over tables uploaded without a transport table, a transport table of another species table, a chunk size or a scratch bound of zero or less | `ArgumentException` before any kernel runs (a batch's arrays cannot be inconsistent: every one is sized by its constructor from one count) |
| a kernel's PTX calls a wrapper ILGPU has no fragment for, the post-link produced no definition, libnvvm or the driver refused the PTX, or any libnvvm or driver call of the post-link returned a result other than success (2026-09-26, `BOOT.md`, "No libnvvm or driver result is ignored") | `InvalidOperationException` naming the wrapper, or the call and its result code, and carrying the compiler's or the driver's log where one exists, on the first run of that program; for the probe kernel, at binding (the row above) |
| a launch exceeds a display GPU's kernel run-time limit (2026-09-28, the second audit's Execution finding F2) | `AcceleratorUnavailableException` naming the limit, the chunk's case count and the CPU accelerator as remedy, its inner exception the driver's `CudaException` (`CUDA_ERROR_LAUNCH_TIMEOUT`); the context is lost with it, so the engine and its tables must be recreated |
| `Upload`, a `Run` overload or `ProbeMath` is called after a launch of the same engine has already timed out (2026-09-29, the third audit pass, finding 2) | `AcceleratorUnavailableException` naming the earlier timeout, its inner exception that earlier `AcceleratorUnavailableException`; no CUDA call is made. Disposing a timed-out engine or its `UploadedTables` never throws: ILGPU's own cleanup of the lost context raises the same sticky `CudaException`, which is dropped rather than replacing whatever is already propagating |
| a download from the accelerator leaves a chunk's host slice unwritten (2026-10-01, `Chunks/BOOT.md`, "A download that wrote nothing is refused"; never seen since the transfers are pinned) | `InvalidOperationException` from the `Run` overload, naming the element type and the chunk; no result is returned, so no `Ok` case carries zero figures |
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
