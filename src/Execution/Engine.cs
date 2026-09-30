using APThermo.Execution.Chunks;
using APThermo.Thermo;
using APThermo.Transport;
using ILGPU;
using ILGPU.Runtime;
using ILGPU.Runtime.Cuda;

namespace APThermo.Execution;

/// <summary>Runs the numerical programs of the tree over batches on one accelerator.</summary>
internal sealed class Engine : IDisposable
{
    private readonly AcceleratorSession _session;
    private readonly EngineOptions _options;
    private bool _disposed;

    private Engine(AcceleratorSession session, EngineOptions options)
    {
        _session = session;
        Launchers = new KernelCache(session);
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

        if ((options.LibNvvmPath is null) != (options.LibDevicePath is null))
        {
            var missing = options.LibNvvmPath is null ? nameof(EngineOptions.LibNvvmPath) : nameof(EngineOptions.LibDevicePath);
            throw new ArgumentException($"an explicit libnvvm/libdevice path pair must be given together; {missing} is missing", nameof(options));
        }

        LibDevicePostLink.AssertIlgpu();
        return new Engine(AcceleratorChoice.Decide(options).Session, options);
    }

    /// <summary>Copies the tables to the accelerator; reusable across batches until disposed. When a transport table is given
    /// and its own upload then fails, the species buffers already uploaded are disposed rather than left live until the
    /// engine itself is (BOOT.md, the audit's observations).</summary>
    public UploadedTables Upload(SpeciesTable species, TransportTable? transport = null)
    {
        ArgumentNullException.ThrowIfNull(species);
        ThrowIfDisposed();
        _session.ThrowIfLost();
        if (transport is not null && !ReferenceEquals(transport.Species, species))
        {
            throw new ArgumentException("the transport table was built for another species table", nameof(transport));
        }

        var speciesBuffers = SpeciesTableBuffers.Upload(_session.Accelerator, species);
        try
        {
            var transportBuffers = transport is null ? null : TransportTableBuffers.Upload(_session.Accelerator, transport);
            return new UploadedTables(this, species, transport, speciesBuffers, transportBuffers);
        }
        catch
        {
            speciesBuffers.Dispose();
            throw;
        }
    }

    /// <summary>Solves every case of the batch.</summary>
    public EquilibriumBatchResult Run(UploadedTables tables, EquilibriumBatch batch)
    {
        Guard(tables, batch);
        return EquilibriumPipeline.Run(_session, Launchers, _options, tables, batch);
    }

    /// <summary>Solves every case of the batch: chamber, throat and the exits.</summary>
    public RocketBatchResult Run(UploadedTables tables, RocketBatch batch)
    {
        Guard(tables, batch);
        return RocketPipeline.Run(_session, Launchers, _options, tables, batch);
    }

    /// <summary>Evaluates the transport properties of every station of the batch.</summary>
    public TransportBatchResult Run(UploadedTables tables, TransportBatch batch)
    {
        Guard(tables, batch);
        return TransportPipeline.Run(_session, Launchers, _options, tables, batch);
    }

    /// <summary>Evaluates Cp/R, H/RT and S/R of table species at temperatures, one entry per thread.</summary>
    public SpeciesFunctionBatchResult Run(UploadedTables tables, SpeciesFunctionBatch batch)
    {
        Guard(tables, batch);
        return SpeciesFunctionPipeline.Run(_session, Launchers, _options, tables, batch);
    }

    /// <summary>Runs the probe of the root's math list: <c>[input * MathProbe.FunctionCount + function]</c>.</summary>
    public double[] ProbeMath(double[] inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        ThrowIfDisposed();
        _session.ThrowIfLost();
        if (inputs.Length == 0)
        {
            return [];
        }

        // Kernels.Probe strides its output by MathProbe.StrideCount with 32-bit Index1D arithmetic (BOOT.md, the second
        // audit's observation 7): MathProbe.OutputLength refuses a count whose offsets would overflow.
        var outputLength = MathProbe.OutputLength(inputs.Length);
        var launch = Launchers.Get<Action<AcceleratorStream, Index1D, ArrayView<double>, ArrayView<double>>>(nameof(Kernels.Probe), out _);
        using var inputBuffer = _session.Accelerator.Allocate1D(inputs);
        using var outputBuffer = _session.Accelerator.Allocate1D<double>(outputLength);
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
        Launchers.Clear();
        _session.Dispose();
    }

    internal Accelerator IlgpuAccelerator => _session.Accelerator;

    /// <summary>The typed launchers this engine compiled (2026-09-30, "Release at dispose"): visible to the tests node the way
    /// <see cref="IlgpuAccelerator"/> already is, so a fact can watch a launcher die at <see cref="Dispose"/>, not to any consumer.</summary>
    internal KernelCache Launchers { get; }

    /// <summary>The launch budget this engine's accelerator was bound with (2026-09-28, "A launch fits a time budget"):
    /// visible to the tests node the way <see cref="IlgpuAccelerator"/> already is, not to any consumer.</summary>
    internal LaunchBudget Budget => _session.Budget;

    /// <summary>Runs <see cref="BatchRun.Execute"/> directly over this engine's session (2026-09-28, the guards audit):
    /// lets the tests node inject a launch failure and prove the timeout translation without a real batch or kernel.</summary>
    internal void RunBatchLoop(ChunkPlan plan, Chunks.ChunkBuffers buffers, RunTimer timer, Action<int> launch) =>
        BatchRun.Execute(_session, plan, buffers, timer, launch);

    /// <summary>Marks this session lost by an injected timeout, the same decision a real translated launch timeout
    /// records through <see cref="RunBatchLoop"/> (2026-09-29, review): lets the tests node prove <c>ThrowIfLost</c>'s
    /// refusal directly, without constructing a driver-touching <see cref="CudaException"/> to get there.</summary>
    internal void MarkLost(AcceleratorUnavailableException timeout) => _session.MarkLost(timeout);

    /// <summary>The bare-<see cref="CudaError"/> half of <see cref="AcceleratorSession.DropsAfterLoss(CudaError)"/>,
    /// exposed so the tests node can prove the decision itself without constructing a <see cref="CudaException"/>.</summary>
    internal bool DropsAfterLoss(CudaError error) => _session.DropsAfterLoss(error);

    /// <summary>Disposes a table buffer <see cref="UploadedTables"/> owns, reading this engine's own session for the
    /// one decision behind every drop (<see cref="AcceleratorSession.DropsAfterLoss(CudaException)"/>; BOOT.md, the
    /// third audit pass's finding 2): a lost session's own sticky <see cref="CudaException"/> is dropped, any other
    /// exception propagates.</summary>
    internal void DisposeAfterLoss(IDisposable disposable)
    {
        try
        {
            disposable.Dispose();
        }
        catch (CudaException failure) when (_session.DropsAfterLoss(failure))
        {
        }
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    /// <summary>What every run checks before its pipeline starts: the arguments, the engine and the ownership of the tables.</summary>
    private void Guard(UploadedTables tables, object batch)
    {
        ArgumentNullException.ThrowIfNull(tables);
        ArgumentNullException.ThrowIfNull(batch);
        ThrowIfDisposed();
        _session.ThrowIfLost();
        tables.ThrowIfNotOwned(this);
    }
}

/// <summary>Device copies of the tables, owned by the engine that uploaded them.</summary>
internal sealed class UploadedTables : IDisposable
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
        if (TransportBuffers is { } transport)
        {
            _engine.DisposeAfterLoss(transport);
        }

        _engine.DisposeAfterLoss(SpeciesBuffers);
    }
}
