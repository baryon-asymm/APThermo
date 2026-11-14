using APThermo.Execution.Chunks;
using APThermo.Execution.Ptx;
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
    private readonly ExecutionResourceOwner _ownership;
    private bool _disposed;

    private Engine(AcceleratorSession session, EngineOptions options)
    {
        _session = session;
        Launchers = new KernelCache(session);
        _options = options;
        _ownership = new ExecutionResourceOwner();
        _ownership.Own(session);
        _ownership.Own(Launchers.Clear, prepend: true);
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

        PtxPostLink.AssertIlgpu();
        var session = AcceleratorChoice.Decide(options).Session;
        try
        {
            return new Engine(session, options);
        }
        catch (Exception primary)
        {
            session.DisposePreserving(primary);
            throw;
        }
    }

    /// <summary>Copies the tables to the accelerator; reusable across batches until disposed. When a transport table is given
    /// and its own upload then fails, the species buffers already uploaded are disposed rather than left live until the
    /// engine itself is (BOOT.md, the audit's observations).</summary>
    public UploadedTables Upload(SpeciesTable species, TransportTable? transport = null)
    {
        _session.Calibration.Clear();
        ArgumentNullException.ThrowIfNull(species);
        ThrowIfDisposed();
        _session.ThrowIfLost();
        ValidateTransport(species, transport);

        return TableUpload.Create(
            () => SpeciesTableBuffers.Upload(_session.Accelerator, species),
            () => transport is null ? null : TransportTableBuffers.Upload(_session.Accelerator, transport),
            (speciesBuffers, transportBuffers) => new UploadedTables(this, species, transport, speciesBuffers, transportBuffers),
            _session.CleanupFilter);
    }

    /// <summary>Solves every case of the batch.</summary>
    public EquilibriumBatchResult Run(UploadedTables tables, EquilibriumBatch batch)
    {
        try
        {
            Guard(tables, batch);
            return EquilibriumPipeline.Run(_session, Launchers, _options, tables, batch);
        }
        catch
        {
            _session.Calibration.Clear();
            throw;
        }
    }

    /// <summary>Solves every case of the batch: chamber, throat and the exits.</summary>
    public RocketBatchResult Run(UploadedTables tables, RocketBatch batch)
    {
        try
        {
            Guard(tables, batch);
            return RocketPipeline.Run(_session, Launchers, _options, tables, batch);
        }
        catch
        {
            _session.Calibration.Clear();
            throw;
        }
    }

    /// <summary>Evaluates the transport properties of every station of the batch.</summary>
    public TransportBatchResult Run(UploadedTables tables, TransportBatch batch)
    {
        try
        {
            Guard(tables, batch);
            return TransportPipeline.Run(_session, Launchers, _options, tables, batch);
        }
        catch
        {
            _session.Calibration.Clear();
            throw;
        }
    }

    /// <summary>Evaluates Cp/R, H/RT and S/R of table species at temperatures, one entry per thread.</summary>
    public SpeciesFunctionBatchResult Run(UploadedTables tables, SpeciesFunctionBatch batch)
    {
        try
        {
            Guard(tables, batch);
            return SpeciesFunctionPipeline.Run(_session, Launchers, _options, tables, batch);
        }
        catch
        {
            _session.Calibration.Clear();
            throw;
        }
    }

    /// <summary>Runs the probe of the root's math list: <c>[input * MathProbe.FunctionCount + function]</c>.</summary>
    public double[] ProbeMath(double[] inputs)
    {
        _session.Calibration.Clear();
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
        using var resources = new ExecutionResourceOwner();
        try
        {
            var inputBuffer = UploadProbeInput(resources, _session.Accelerator, _session.Accelerator.DefaultStream, inputs);
            var outputBuffer = resources.Acquire(() => _session.Accelerator.Allocate1D<double>(outputLength));
            launch(_session.Accelerator.DefaultStream, inputs.Length, inputBuffer.View, outputBuffer.View);
            _session.Accelerator.Synchronize();
            return outputBuffer.GetAsArray1D();
        }
        catch (Exception primary)
        {
            resources.PrepareCleanup(primary, _session.CleanupFilter);
            throw;
        }
    }

    /// <summary>Registers the input allocation before the initial host copy, so rollback covers copy failures.</summary>
    private static MemoryBuffer1D<double, Stride1D.Dense> UploadProbeInput(
        ExecutionResourceOwner resources, Accelerator accelerator, AcceleratorStream stream, double[] inputs)
    {
        var buffer = resources.Acquire(() => accelerator.Allocate1D<double>(inputs.Length));
        if (inputs.Length > 0)
        {
            buffer.View.CopyFromCPU(stream, inputs);
        }

        return buffer;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _ownership.PrepareCleanup(null, _session.CleanupFilter);
        _ownership.Dispose();
    }

    internal Accelerator IlgpuAccelerator => _session.Accelerator;

    internal void ClearCalibration() => _session.Calibration.Clear();

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
    internal void DisposeAfterLoss(IDisposable disposable) =>
        ExecutionResourceOwner.ReleaseOne(disposable, null, _session.CleanupFilter);

    private static void ValidateTransport(SpeciesTable species, TransportTable? transport)
    {
        if (transport is not null && !ReferenceEquals(transport.Species, species))
        {
            throw new ArgumentException("the transport table was built for another species table", nameof(transport));
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
    private readonly ExecutionResourceOwner _ownership = new();
    private bool _disposed;

    internal UploadedTables(Engine engine, SpeciesTable species, TransportTable? transport, SpeciesTableBuffers speciesBuffers, TransportTableBuffers? transportBuffers)
    {
        _engine = engine;
        Species = species;
        Transport = transport;
        SpeciesBuffers = speciesBuffers;
        TransportBuffers = transportBuffers;
        _ownership.Own(ReleaseSpecies);
        if (transportBuffers is not null)
        {
            _ownership.Own(ReleaseTransport, prepend: true);
        }
    }

    private void ReleaseSpecies() => _engine.DisposeAfterLoss(SpeciesBuffers);

    private void ReleaseTransport() => _engine.DisposeAfterLoss(TransportBuffers!);

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
        _engine.ClearCalibration();
        _ownership.Dispose();
    }
}
