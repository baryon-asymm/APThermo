using AerospacePropellantThermodynamics.Equilibrium;
using AerospacePropellantThermodynamics.Performance;
using AerospacePropellantThermodynamics.Thermo;
using AerospacePropellantThermodynamics.Transport;
using ILGPU;
using ILGPU.Runtime;

namespace AerospacePropellantThermodynamics.Execution;

/// <summary>Runs the numerical programs of the tree over batches on one accelerator.</summary>
public sealed class Engine : IDisposable
{
    private readonly AcceleratorSession _session;
    private readonly KernelCache _kernels;
    private readonly EngineOptions _options;
    private bool _disposed;

    private Engine(AcceleratorSession session, EngineOptions options)
    {
        _session = session;
        _kernels = new KernelCache(session);
        _options = options;
    }

    /// <summary>The accelerator this engine is bound to, and why it is that one.</summary>
    public AcceleratorInfo Accelerator => _session.Info;

    /// <summary>True when the environment forbids CUDA (<see cref="EngineOptions.NoCudaVariable"/> is 1).</summary>
    public static bool CudaForbidden => AcceleratorChoice.CudaForbidden;

    /// <summary>Creates an engine bound to the accelerator the options select (BOOT.md, accelerator choice).</summary>
    public static Engine Create(EngineOptions? options = null)
    {
        options ??= new EngineOptions();
        if (options.ChunkSize <= 0)
        {
            throw new ArgumentException("the chunk size must be positive", nameof(options));
        }

        if (options.ScratchBytes <= 0)
        {
            throw new ArgumentException("the scratch bound must be positive", nameof(options));
        }

        LibDevicePostLink.AssertIlgpu();
        return new Engine(AcceleratorChoice.Decide(options).Session, options);
    }

    /// <summary>Copies the tables to the accelerator; reusable across batches until disposed.</summary>
    public UploadedTables Upload(SpeciesTable species, TransportTable? transport = null)
    {
        ArgumentNullException.ThrowIfNull(species);
        ThrowIfDisposed();
        if (transport is not null && !ReferenceEquals(transport.Species, species))
        {
            throw new ArgumentException("the transport table was built for another species table", nameof(transport));
        }

        return new UploadedTables(this, species, transport, SpeciesTableBuffers.Upload(_session.Accelerator, species),
                                  transport is null ? null : TransportTableBuffers.Upload(_session.Accelerator, transport));
    }

    /// <summary>Solves every case of the batch.</summary>
    public EquilibriumBatchResult Run(UploadedTables tables, EquilibriumBatch batch)
    {
        Guard(tables, batch);
        return EquilibriumPipeline.Run(_session, _kernels, _options, tables, batch);
    }

    /// <summary>Solves every case of the batch: chamber, throat and the exits.</summary>
    public RocketBatchResult Run(UploadedTables tables, RocketBatch batch)
    {
        ArgumentNullException.ThrowIfNull(tables);
        ArgumentNullException.ThrowIfNull(batch);
        ThrowIfDisposed();
        tables.ThrowIfNotOwned(this);
        var table = tables.Species;
        batch.Validate(table.ElementCount);
        var speciesCount = table.SpeciesCount;
        var elementCount = table.ElementCount;
        var count = batch.Count;
        var exits = batch.Exits;
        var stationCount = batch.StationCount;
        var timer = new RunTimer();
        var launch = _kernels.Get<Action<AcceleratorStream, Index1D, SpeciesTableView, RocketBatchViews>>(nameof(Kernels.Rocket), out var warmUp);
        timer.AddWarmUp(warmUp);
        var doublesPerCase = ScratchLayout.DoublesPerCase(speciesCount, elementCount);
        var intsPerCase = ScratchLayout.IntsPerCase(speciesCount, elementCount);
        var chunk = ChunkSize(count, doublesPerCase + (long)stationCount * speciesCount, intsPerCase);

        var stations = new MixtureState[(long)count * stationCount];
        var moles = new double[(long)count * stationCount * speciesCount];
        var figures = new PerformanceFigures[(long)count * stationCount];
        var stationStatus = new int[(long)count * stationCount];
        var iterations = new int[(long)count * stationCount];
        var status = new int[count];
        var flows = batch.Flow.Select(f => (int)f).ToArray();
        var exitKinds = batch.ExitKinds.Select(k => (int)k).ToArray();

        using var pressureBuffer = _session.Accelerator.Allocate1D<double>(chunk);
        using var enthalpyBuffer = _session.Accelerator.Allocate1D<double>(chunk);
        using var estimateBuffer = _session.Accelerator.Allocate1D<double>(chunk);
        using var flowBuffer = _session.Accelerator.Allocate1D<int>(chunk);
        using var elementBuffer = _session.Accelerator.Allocate1D<double>((long)chunk * elementCount);
        using var exitValueBuffer = _session.Accelerator.Allocate1D<double>(Math.Max(1, (long)chunk * exits));
        using var exitKindBuffer = _session.Accelerator.Allocate1D<int>(Math.Max(1, exits));
        using var scratchDoubles = _session.Accelerator.Allocate1D<double>((long)chunk * doublesPerCase);
        using var scratchInts = _session.Accelerator.Allocate1D<int>((long)chunk * intsPerCase);
        using var stationBuffer = _session.Accelerator.Allocate1D<MixtureState>((long)chunk * stationCount);
        using var molesBuffer = _session.Accelerator.Allocate1D<double>((long)chunk * stationCount * speciesCount);
        using var multiplierBuffer = _session.Accelerator.Allocate1D<double>((long)chunk * stationCount * elementCount);
        using var figureBuffer = _session.Accelerator.Allocate1D<PerformanceFigures>((long)chunk * stationCount);
        using var stationStatusBuffer = _session.Accelerator.Allocate1D<int>((long)chunk * stationCount);
        using var iterationBuffer = _session.Accelerator.Allocate1D<int>((long)chunk * stationCount);
        using var statusBuffer = _session.Accelerator.Allocate1D<int>(chunk);
        if (exits > 0)
        {
            exitKindBuffer.CopyFromCPU(exitKinds);
        }

        var views = new RocketBatchViews(exits, pressureBuffer.View, enthalpyBuffer.View, estimateBuffer.View, flowBuffer.View, elementBuffer.View,
                                         exitValueBuffer.View, exitKindBuffer.View, scratchDoubles.View, scratchInts.View, stationBuffer.View,
                                         molesBuffer.View, multiplierBuffer.View, figureBuffer.View, stationStatusBuffer.View, iterationBuffer.View,
                                         statusBuffer.View);

        for (var offset = 0; offset < count; offset += chunk)
        {
            var n = Math.Min(chunk, count - offset);
            using (timer.Uploading())
            {
                Upload(pressureBuffer, batch.ChamberPressure, offset, n);
                Upload(enthalpyBuffer, batch.ReactantEnthalpy, offset, n);
                Upload(estimateBuffer, batch.TemperatureEstimate, offset, n);
                Upload(flowBuffer, flows, offset, n);
                Upload(elementBuffer, batch.ElementMoles, (long)offset * elementCount, (long)n * elementCount);
                if (exits > 0)
                {
                    Upload(exitValueBuffer, batch.ExitValues, (long)offset * exits, (long)n * exits);
                }

                molesBuffer.MemSetToZero();
                stationBuffer.MemSetToZero();
                figureBuffer.MemSetToZero();
            }

            using (timer.Launching())
            {
                launch(_session.Accelerator.DefaultStream, n, tables.SpeciesBuffers.View, views);
                _session.Accelerator.Synchronize();
            }

            using (timer.Downloading())
            {
                Download(stationBuffer, stations, (long)offset * stationCount, (long)n * stationCount);
                Download(molesBuffer, moles, (long)offset * stationCount * speciesCount, (long)n * stationCount * speciesCount);
                Download(figureBuffer, figures, (long)offset * stationCount, (long)n * stationCount);
                Download(stationStatusBuffer, stationStatus, (long)offset * stationCount, (long)n * stationCount);
                Download(iterationBuffer, iterations, (long)offset * stationCount, (long)n * stationCount);
                Download(statusBuffer, status, offset, n);
            }
        }

        return new RocketBatchResult(speciesCount, stationCount, stations, moles, figures,
                                     stationStatus.Select(s => (CaseStatus)s).ToArray(), iterations, status.Select(s => (CaseStatus)s).ToArray(),
                                     timer.Timings(), Accelerator);
    }

    /// <summary>Evaluates the transport properties of every station of the batch.</summary>
    public TransportBatchResult Run(UploadedTables tables, TransportBatch batch)
    {
        ArgumentNullException.ThrowIfNull(tables);
        ArgumentNullException.ThrowIfNull(batch);
        ThrowIfDisposed();
        tables.ThrowIfNotOwned(this);
        var transportBuffers = tables.TransportBuffers ?? throw new ArgumentException("the tables were uploaded without a transport table", nameof(tables));
        var table = tables.Species;
        batch.Validate(table.SpeciesCount);
        var speciesCount = table.SpeciesCount;
        var elementCount = table.ElementCount;
        var count = batch.Count;
        var timer = new RunTimer();
        var launch = _kernels.Get<Action<AcceleratorStream, Index1D, SpeciesTableView, TransportTableView, TransportBatchViews>>(nameof(Kernels.Transport), out var warmUp);
        timer.AddWarmUp(warmUp);
        var doublesPerCase = TransportLayout.DoublesPerCase(speciesCount, elementCount);
        var intsPerCase = TransportLayout.IntsPerCase(speciesCount, elementCount);
        var chunk = ChunkSize(count, doublesPerCase + speciesCount, intsPerCase);

        var figures = new TransportFigures[count];
        var status = new int[count];
        using var temperatureBuffer = _session.Accelerator.Allocate1D<double>(chunk);
        using var molesBuffer = _session.Accelerator.Allocate1D<double>((long)chunk * speciesCount);
        using var scratchDoubles = _session.Accelerator.Allocate1D<double>((long)chunk * doublesPerCase);
        using var scratchInts = _session.Accelerator.Allocate1D<int>((long)chunk * intsPerCase);
        using var figureBuffer = _session.Accelerator.Allocate1D<TransportFigures>(chunk);
        using var statusBuffer = _session.Accelerator.Allocate1D<int>(chunk);
        var views = new TransportBatchViews(temperatureBuffer.View, molesBuffer.View, scratchDoubles.View, scratchInts.View, figureBuffer.View, statusBuffer.View);

        for (var offset = 0; offset < count; offset += chunk)
        {
            var n = Math.Min(chunk, count - offset);
            using (timer.Uploading())
            {
                Upload(temperatureBuffer, batch.Temperature, offset, n);
                Upload(molesBuffer, batch.Moles, (long)offset * speciesCount, (long)n * speciesCount);
            }

            using (timer.Launching())
            {
                launch(_session.Accelerator.DefaultStream, n, tables.SpeciesBuffers.View, transportBuffers.View, views);
                _session.Accelerator.Synchronize();
            }

            using (timer.Downloading())
            {
                Download(figureBuffer, figures, offset, n);
                Download(statusBuffer, status, offset, n);
            }
        }

        return new TransportBatchResult(figures, status.Select(s => (CaseStatus)s).ToArray(), timer.Timings(), Accelerator);
    }

    /// <summary>Evaluates Cp/R, H/RT and S/R of table species at temperatures, one entry per thread.</summary>
    public SpeciesFunctionBatchResult Run(UploadedTables tables, SpeciesFunctionBatch batch)
    {
        ArgumentNullException.ThrowIfNull(tables);
        ArgumentNullException.ThrowIfNull(batch);
        ThrowIfDisposed();
        tables.ThrowIfNotOwned(this);
        var table = tables.Species;
        batch.Validate(table.SpeciesCount);
        var count = batch.Count;
        var timer = new RunTimer();
        var launch = _kernels.Get<Action<AcceleratorStream, Index1D, SpeciesTableView, SpeciesFunctionBatchViews>>(nameof(Kernels.Functions), out var warmUp);
        timer.AddWarmUp(warmUp);
        var chunk = ChunkSize(count, 3, 2);

        var cpOverR = new double[count];
        var hOverRT = new double[count];
        var sOverR = new double[count];
        var inRange = new int[count];
        using var speciesBuffer = _session.Accelerator.Allocate1D<int>(chunk);
        using var temperatureBuffer = _session.Accelerator.Allocate1D<double>(chunk);
        using var cpBuffer = _session.Accelerator.Allocate1D<double>(chunk);
        using var hBuffer = _session.Accelerator.Allocate1D<double>(chunk);
        using var sBuffer = _session.Accelerator.Allocate1D<double>(chunk);
        using var rangeBuffer = _session.Accelerator.Allocate1D<int>(chunk);
        var views = new SpeciesFunctionBatchViews(speciesBuffer.View, temperatureBuffer.View, cpBuffer.View, hBuffer.View, sBuffer.View, rangeBuffer.View);

        for (var offset = 0; offset < count; offset += chunk)
        {
            var n = Math.Min(chunk, count - offset);
            using (timer.Uploading())
            {
                Upload(speciesBuffer, batch.Species, offset, n);
                Upload(temperatureBuffer, batch.Temperature, offset, n);
            }

            using (timer.Launching())
            {
                launch(_session.Accelerator.DefaultStream, n, tables.SpeciesBuffers.View, views);
                _session.Accelerator.Synchronize();
            }

            using (timer.Downloading())
            {
                Download(cpBuffer, cpOverR, offset, n);
                Download(hBuffer, hOverRT, offset, n);
                Download(sBuffer, sOverR, offset, n);
                Download(rangeBuffer, inRange, offset, n);
            }
        }

        return new SpeciesFunctionBatchResult(cpOverR, hOverRT, sOverR, inRange.Select(r => r != 0).ToArray(), timer.Timings(), Accelerator);
    }

    /// <summary>Runs the probe of the root's math list: <c>[input * MathProbe.FunctionCount + function]</c>.</summary>
    public double[] ProbeMath(double[] inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        ThrowIfDisposed();
        if (inputs.Length == 0)
        {
            return [];
        }

        var timer = new RunTimer();
        var launch = _kernels.Get<Action<AcceleratorStream, Index1D, ArrayView<double>, ArrayView<double>>>(nameof(Kernels.Probe), out var warmUp);
        timer.AddWarmUp(warmUp);
        using var inputBuffer = _session.Accelerator.Allocate1D(inputs);
        using var outputBuffer = _session.Accelerator.Allocate1D<double>((long)inputs.Length * MathProbe.FunctionCount);
        launch(_session.Accelerator.DefaultStream, inputs.Length, inputBuffer.View, outputBuffer.View);
        _session.Accelerator.Synchronize();
        return outputBuffer.GetAsArray1D();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _session.Dispose();
    }

    internal Accelerator IlgpuAccelerator => _session.Accelerator;

    /// <summary>The largest number of cases per launch, by the one rule of <see cref="ChunkPlan"/>.</summary>
    private int ChunkSize(int count, long doublesPerCase, long intsPerCase) =>
        ChunkPlan.For(count, doublesPerCase * sizeof(double) + intsPerCase * sizeof(int), _options).Size;

    private static void Upload<T>(MemoryBuffer1D<T, Stride1D.Dense> buffer, T[] source, long offset, long length) where T : unmanaged
    {
        if (length > 0)
        {
            buffer.View.SubView(0, length).CopyFromCPU(ref source[offset], length);
        }
    }

    private static void Download<T>(MemoryBuffer1D<T, Stride1D.Dense> buffer, T[] target, long offset, long length) where T : unmanaged
    {
        if (length > 0)
        {
            buffer.View.SubView(0, length).CopyToCPU(ref target[offset], length);
        }
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    /// <summary>What every run checks before its pipeline starts: the arguments, the engine and the ownership of the tables.</summary>
    private void Guard(UploadedTables tables, object batch)
    {
        ArgumentNullException.ThrowIfNull(tables);
        ArgumentNullException.ThrowIfNull(batch);
        ThrowIfDisposed();
        tables.ThrowIfNotOwned(this);
    }
}

/// <summary>Device copies of the tables, owned by the engine that uploaded them.</summary>
public sealed class UploadedTables : IDisposable
{
    private readonly Engine _engine;
    private bool _disposed;

    internal UploadedTables(Engine engine, SpeciesTable species, TransportTable? transport, SpeciesTableBuffers speciesBuffers, TransportTableBuffers? transportBuffers)
    {
        _engine = engine;
        Species = species;
        Transport = transport;
        SpeciesBuffers = speciesBuffers;
        TransportBuffers = transportBuffers;
    }

    public SpeciesTable Species { get; }

    public TransportTable? Transport { get; }

    internal SpeciesTableBuffers SpeciesBuffers { get; }

    internal TransportTableBuffers? TransportBuffers { get; }

    internal void ThrowIfNotOwned(Engine engine)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!ReferenceEquals(engine, _engine))
        {
            throw new ArgumentException("the tables were uploaded by another engine");
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        TransportBuffers?.Dispose();
        SpeciesBuffers.Dispose();
    }
}
