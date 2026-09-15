using APThermo.Equilibrium;
using APThermo.Performance;
using APThermo.Thermo;
using APThermo.Transport;
using ILGPU;
using ILGPU.Runtime;

namespace APThermo.Execution;

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
        Guard(tables, batch);
        return RocketPipeline.Run(_session, _kernels, _options, tables, batch);
    }

    /// <summary>Evaluates the transport properties of every station of the batch.</summary>
    public TransportBatchResult Run(UploadedTables tables, TransportBatch batch)
    {
        Guard(tables, batch);
        return TransportPipeline.Run(_session, _kernels, _options, tables, batch);
    }

    /// <summary>Evaluates Cp/R, H/RT and S/R of table species at temperatures, one entry per thread.</summary>
    public SpeciesFunctionBatchResult Run(UploadedTables tables, SpeciesFunctionBatch batch)
    {
        Guard(tables, batch);
        return SpeciesFunctionPipeline.Run(_session, _kernels, _options, tables, batch);
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

        var launch = _kernels.Get<Action<AcceleratorStream, Index1D, ArrayView<double>, ArrayView<double>>>(nameof(Kernels.Probe), out _);
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
