using System.Diagnostics;
using System.Reflection;
using AerospacePropellantThermodynamics.Equilibrium;
using AerospacePropellantThermodynamics.Performance;
using AerospacePropellantThermodynamics.Thermo;
using AerospacePropellantThermodynamics.Transport;
using ILGPU;
using ILGPU.Backends.EntryPoints;
using ILGPU.Backends.PTX;
using ILGPU.Runtime;
using ILGPU.Runtime.CPU;
using ILGPU.Runtime.Cuda;

namespace AerospacePropellantThermodynamics.Execution;

/// <summary>Runs the numerical programs of the tree over batches on one accelerator.</summary>
public sealed class Engine : IDisposable
{
    private readonly Context _context;
    private readonly Accelerator _accelerator;
    private readonly NvvmAPI? _nvvm;
    private readonly EngineOptions _options;
    private readonly Dictionary<string, Delegate> _kernels = new(StringComparer.Ordinal);
    private readonly object _gate = new();
    private bool _disposed;

    private Engine(Context context, Accelerator accelerator, NvvmAPI? nvvm, EngineOptions options, AcceleratorInfo info)
    {
        _context = context;
        _accelerator = accelerator;
        _nvvm = nvvm;
        _options = options;
        Accelerator = info;
    }

    /// <summary>The accelerator this engine is bound to.</summary>
    public AcceleratorInfo Accelerator { get; }

    /// <summary>True when the environment forbids CUDA (<see cref="EngineOptions.NoCudaVariable"/> is 1).</summary>
    public static bool CudaForbidden => Environment.GetEnvironmentVariable(EngineOptions.NoCudaVariable)?.Trim() == "1";

    /// <summary>Creates an engine bound to the accelerator the options select (BOOT.md, accelerator choice).</summary>
    public static Engine Create(EngineOptions? options = null)
    {
        options ??= new EngineOptions();
        if (options.ChunkSize <= 0)
        {
            throw new ArgumentException("the chunk size must be positive", nameof(options));
        }

        LibDevicePostLink.AssertIlgpu();
        if (options.Accelerator == AcceleratorKind.Cpu)
        {
            return CreateCpu(options);
        }

        if (CudaForbidden)
        {
            if (options.Accelerator == AcceleratorKind.Cuda)
            {
                throw new AcceleratorUnavailableException($"CUDA was requested, but {EngineOptions.NoCudaVariable}=1 forbids it.", []);
            }

            return CreateCpu(options);
        }

        var (dll, bitcode, tried) = LibDeviceLocator.Locate(options);
        if (dll is null || bitcode is null)
        {
            if (options.Accelerator == AcceleratorKind.Cuda)
            {
                throw new AcceleratorUnavailableException("libnvvm (nvvm64_40_0.dll) and libdevice (libdevice.10.bc) were not found.", tried);
            }

            return CreateCpu(options);
        }

        try
        {
            return CreateCuda(options, dll, bitcode);
        }
        catch (Exception exception) when (options.Accelerator == AcceleratorKind.Auto && exception is not OutOfMemoryException)
        {
            return CreateCpu(options);
        }
    }

    private static Engine CreateCpu(EngineOptions options)
    {
        var context = Context.Create(builder => builder.CPU());
        var accelerator = context.CreateCPUAccelerator(0);
        var info = new AcceleratorInfo(AcceleratorKind.Cpu, accelerator.Name, LibDevicePostLink.IlgpuVersion, null, null, accelerator.NumThreads);
        return new Engine(context, accelerator, null, options, info);
    }

    private static Engine CreateCuda(EngineOptions options, string dll, string bitcode)
    {
        Context? context = null;
        Accelerator? accelerator = null;
        NvvmAPI? nvvm = null;
        try
        {
            try
            {
                context = Context.Create(builder => builder.Cuda().Math(MathMode.Default).LibDevice(dll, bitcode));
            }
            catch (Exception exception)
            {
                throw new AcceleratorUnavailableException("the CUDA context could not be created (driver or device problem): " + exception.Message, [dll, bitcode], exception);
            }

            var devices = context.GetCudaDevices();
            if (options.CudaDeviceIndex < 0 || options.CudaDeviceIndex >= devices.Count)
            {
                throw new AcceleratorUnavailableException($"CUDA device {options.CudaDeviceIndex} was requested, but {devices.Count} device(s) exist.", [dll, bitcode]);
            }

            accelerator = context.CreateCudaAccelerator(options.CudaDeviceIndex);
            nvvm = NvvmAPI.Create(dll, bitcode);
            var info = new AcceleratorInfo(AcceleratorKind.Cuda, accelerator.Name, LibDevicePostLink.IlgpuVersion, dll, bitcode, accelerator.NumMultiprocessors);
            var engine = new Engine(context, accelerator, nvvm, options, info);
            context = null;
            accelerator = null;
            nvvm = null;
            return engine;
        }
        finally
        {
            nvvm?.Dispose();
            accelerator?.Dispose();
            context?.Dispose();
        }
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

        return new UploadedTables(this, species, transport, SpeciesTableBuffers.Upload(_accelerator, species),
                                  transport is null ? null : TransportTableBuffers.Upload(_accelerator, transport));
    }

    /// <summary>Solves every case of the batch.</summary>
    public EquilibriumBatchResult Run(UploadedTables tables, EquilibriumBatch batch)
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
        var timer = new Timer();
        var launch = LoadKernel<Action<AcceleratorStream, Index1D, SpeciesTableView, EquilibriumBatchViews>>(nameof(Kernels.Equilibrium), timer);
        var doublesPerCase = ScratchLayout.DoublesPerCase(speciesCount, elementCount);
        var intsPerCase = ScratchLayout.IntsPerCase(speciesCount, elementCount);
        var chunk = ChunkSize(count, doublesPerCase, intsPerCase);

        var states = new MixtureState[count];
        var moles = new double[(long)count * speciesCount];
        var status = new int[count];
        var iterations = new int[count];
        var kinds = batch.Kind.Select(k => (int)k).ToArray();

        using var kindBuffer = _accelerator.Allocate1D<int>(chunk);
        using var pressureBuffer = _accelerator.Allocate1D<double>(chunk);
        using var temperatureBuffer = _accelerator.Allocate1D<double>(chunk);
        using var targetBuffer = _accelerator.Allocate1D<double>(chunk);
        using var elementBuffer = _accelerator.Allocate1D<double>((long)chunk * elementCount);
        using var scratchDoubles = _accelerator.Allocate1D<double>((long)chunk * doublesPerCase);
        using var scratchInts = _accelerator.Allocate1D<int>((long)chunk * intsPerCase);
        using var molesBuffer = _accelerator.Allocate1D<double>((long)chunk * speciesCount);
        using var multiplierBuffer = _accelerator.Allocate1D<double>((long)chunk * elementCount);
        using var stateBuffer = _accelerator.Allocate1D<MixtureState>(chunk);
        using var statusBuffer = _accelerator.Allocate1D<int>(chunk);
        using var iterationBuffer = _accelerator.Allocate1D<int>(chunk);
        var views = new EquilibriumBatchViews(kindBuffer.View, pressureBuffer.View, temperatureBuffer.View, targetBuffer.View, elementBuffer.View,
                                              scratchDoubles.View, scratchInts.View, molesBuffer.View, multiplierBuffer.View, stateBuffer.View,
                                              statusBuffer.View, iterationBuffer.View);

        for (var offset = 0; offset < count; offset += chunk)
        {
            var n = Math.Min(chunk, count - offset);
            timer.Start();
            Upload(kindBuffer, kinds, offset, n);
            Upload(pressureBuffer, batch.Pressure, offset, n);
            Upload(temperatureBuffer, batch.Temperature, offset, n);
            Upload(targetBuffer, batch.Target, offset, n);
            Upload(elementBuffer, batch.ElementMoles, (long)offset * elementCount, (long)n * elementCount);
            molesBuffer.MemSetToZero();
            stateBuffer.MemSetToZero();
            timer.Stop(ref timer.Upload);

            timer.Start();
            launch(_accelerator.DefaultStream, n, tables.SpeciesBuffers.View, views);
            _accelerator.Synchronize();
            timer.Stop(ref timer.Kernel);

            timer.Start();
            Download(stateBuffer, states, offset, n);
            Download(molesBuffer, moles, (long)offset * speciesCount, (long)n * speciesCount);
            Download(statusBuffer, status, offset, n);
            Download(iterationBuffer, iterations, offset, n);
            timer.Stop(ref timer.Download);
        }

        return new EquilibriumBatchResult(speciesCount, states, moles, status.Select(s => (CaseStatus)s).ToArray(), iterations, timer.Timings(), Accelerator);
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
        var timer = new Timer();
        var launch = LoadKernel<Action<AcceleratorStream, Index1D, SpeciesTableView, RocketBatchViews>>(nameof(Kernels.Rocket), timer);
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

        using var pressureBuffer = _accelerator.Allocate1D<double>(chunk);
        using var enthalpyBuffer = _accelerator.Allocate1D<double>(chunk);
        using var estimateBuffer = _accelerator.Allocate1D<double>(chunk);
        using var flowBuffer = _accelerator.Allocate1D<int>(chunk);
        using var elementBuffer = _accelerator.Allocate1D<double>((long)chunk * elementCount);
        using var exitValueBuffer = _accelerator.Allocate1D<double>(Math.Max(1, (long)chunk * exits));
        using var exitKindBuffer = _accelerator.Allocate1D<int>(Math.Max(1, exits));
        using var scratchDoubles = _accelerator.Allocate1D<double>((long)chunk * doublesPerCase);
        using var scratchInts = _accelerator.Allocate1D<int>((long)chunk * intsPerCase);
        using var stationBuffer = _accelerator.Allocate1D<MixtureState>((long)chunk * stationCount);
        using var molesBuffer = _accelerator.Allocate1D<double>((long)chunk * stationCount * speciesCount);
        using var multiplierBuffer = _accelerator.Allocate1D<double>((long)chunk * stationCount * elementCount);
        using var figureBuffer = _accelerator.Allocate1D<PerformanceFigures>((long)chunk * stationCount);
        using var stationStatusBuffer = _accelerator.Allocate1D<int>((long)chunk * stationCount);
        using var iterationBuffer = _accelerator.Allocate1D<int>((long)chunk * stationCount);
        using var statusBuffer = _accelerator.Allocate1D<int>(chunk);
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
            timer.Start();
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
            timer.Stop(ref timer.Upload);

            timer.Start();
            launch(_accelerator.DefaultStream, n, tables.SpeciesBuffers.View, views);
            _accelerator.Synchronize();
            timer.Stop(ref timer.Kernel);

            timer.Start();
            Download(stationBuffer, stations, (long)offset * stationCount, (long)n * stationCount);
            Download(molesBuffer, moles, (long)offset * stationCount * speciesCount, (long)n * stationCount * speciesCount);
            Download(figureBuffer, figures, (long)offset * stationCount, (long)n * stationCount);
            Download(stationStatusBuffer, stationStatus, (long)offset * stationCount, (long)n * stationCount);
            Download(iterationBuffer, iterations, (long)offset * stationCount, (long)n * stationCount);
            Download(statusBuffer, status, offset, n);
            timer.Stop(ref timer.Download);
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
        var timer = new Timer();
        var launch = LoadKernel<Action<AcceleratorStream, Index1D, SpeciesTableView, TransportTableView, TransportBatchViews>>(nameof(Kernels.Transport), timer);
        var doublesPerCase = TransportLayout.DoublesPerCase(speciesCount, elementCount);
        var intsPerCase = TransportLayout.IntsPerCase(speciesCount, elementCount);
        var chunk = ChunkSize(count, doublesPerCase + speciesCount, intsPerCase);

        var figures = new TransportFigures[count];
        var status = new int[count];
        using var temperatureBuffer = _accelerator.Allocate1D<double>(chunk);
        using var molesBuffer = _accelerator.Allocate1D<double>((long)chunk * speciesCount);
        using var scratchDoubles = _accelerator.Allocate1D<double>((long)chunk * doublesPerCase);
        using var scratchInts = _accelerator.Allocate1D<int>((long)chunk * intsPerCase);
        using var figureBuffer = _accelerator.Allocate1D<TransportFigures>(chunk);
        using var statusBuffer = _accelerator.Allocate1D<int>(chunk);
        var views = new TransportBatchViews(temperatureBuffer.View, molesBuffer.View, scratchDoubles.View, scratchInts.View, figureBuffer.View, statusBuffer.View);

        for (var offset = 0; offset < count; offset += chunk)
        {
            var n = Math.Min(chunk, count - offset);
            timer.Start();
            Upload(temperatureBuffer, batch.Temperature, offset, n);
            Upload(molesBuffer, batch.Moles, (long)offset * speciesCount, (long)n * speciesCount);
            timer.Stop(ref timer.Upload);

            timer.Start();
            launch(_accelerator.DefaultStream, n, tables.SpeciesBuffers.View, transportBuffers.View, views);
            _accelerator.Synchronize();
            timer.Stop(ref timer.Kernel);

            timer.Start();
            Download(figureBuffer, figures, offset, n);
            Download(statusBuffer, status, offset, n);
            timer.Stop(ref timer.Download);
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
        var timer = new Timer();
        var launch = LoadKernel<Action<AcceleratorStream, Index1D, SpeciesTableView, SpeciesFunctionBatchViews>>(nameof(Kernels.Functions), timer);
        var chunk = ChunkSize(count, 3, 2);

        var cpOverR = new double[count];
        var hOverRT = new double[count];
        var sOverR = new double[count];
        var inRange = new int[count];
        using var speciesBuffer = _accelerator.Allocate1D<int>(chunk);
        using var temperatureBuffer = _accelerator.Allocate1D<double>(chunk);
        using var cpBuffer = _accelerator.Allocate1D<double>(chunk);
        using var hBuffer = _accelerator.Allocate1D<double>(chunk);
        using var sBuffer = _accelerator.Allocate1D<double>(chunk);
        using var rangeBuffer = _accelerator.Allocate1D<int>(chunk);
        var views = new SpeciesFunctionBatchViews(speciesBuffer.View, temperatureBuffer.View, cpBuffer.View, hBuffer.View, sBuffer.View, rangeBuffer.View);

        for (var offset = 0; offset < count; offset += chunk)
        {
            var n = Math.Min(chunk, count - offset);
            timer.Start();
            Upload(speciesBuffer, batch.Species, offset, n);
            Upload(temperatureBuffer, batch.Temperature, offset, n);
            timer.Stop(ref timer.Upload);

            timer.Start();
            launch(_accelerator.DefaultStream, n, tables.SpeciesBuffers.View, views);
            _accelerator.Synchronize();
            timer.Stop(ref timer.Kernel);

            timer.Start();
            Download(cpBuffer, cpOverR, offset, n);
            Download(hBuffer, hOverRT, offset, n);
            Download(sBuffer, sOverR, offset, n);
            Download(rangeBuffer, inRange, offset, n);
            timer.Stop(ref timer.Download);
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

        var timer = new Timer();
        var launch = LoadKernel<Action<AcceleratorStream, Index1D, ArrayView<double>, ArrayView<double>>>(nameof(Kernels.Probe), timer);
        using var inputBuffer = _accelerator.Allocate1D(inputs);
        using var outputBuffer = _accelerator.Allocate1D<double>((long)inputs.Length * MathProbe.FunctionCount);
        launch(_accelerator.DefaultStream, inputs.Length, inputBuffer.View, outputBuffer.View);
        _accelerator.Synchronize();
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
        _nvvm?.Dispose();
        _accelerator.Dispose();
        _context.Dispose();
    }

    internal Accelerator IlgpuAccelerator => _accelerator;

    /// <summary>The largest number of cases per launch: the option's chunk size, bounded by the scratch memory the option allows.</summary>
    internal int ChunkSize(int count, long doublesPerCase, long intsPerCase)
    {
        var bytesPerCase = doublesPerCase * sizeof(double) + intsPerCase * sizeof(int);
        var byMemory = Math.Max(1L, _options.ScratchBytes / Math.Max(1L, bytesPerCase));
        return (int)Math.Min(count, Math.Min(_options.ChunkSize, byMemory));
    }

    private TDelegate LoadKernel<TDelegate>(string name, Timer timer) where TDelegate : Delegate
    {
        lock (_gate)
        {
            if (_kernels.TryGetValue(name, out var cached))
            {
                return (TDelegate)cached;
            }

            var watch = Stopwatch.StartNew();
            var method = typeof(Kernels).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)
                         ?? throw new InvalidOperationException($"no kernel named {name}");
            Kernel kernel;
            if (_accelerator is CudaAccelerator cuda)
            {
                // Every CUDA kernel goes through the post-link; the CPU accelerator loads the method as ILGPU does.
                var entry = EntryPointDescription.FromImplicitlyGroupedKernel(method);
                var compiled = (PTXCompiledKernel)cuda.Backend.Compile(entry, KernelSpecialization.Empty);
                kernel = _accelerator.LoadAutoGroupedKernel(LibDevicePostLink.Link(cuda, _nvvm!, compiled));
            }
            else
            {
                kernel = _accelerator.LoadAutoGroupedKernel(method);
            }

            var launcher = kernel.CreateLauncherDelegate<TDelegate>();
            _kernels[name] = launcher;
            timer.WarmUp += watch.Elapsed;
            return launcher;
        }
    }

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

    /// <summary>Accumulates the timings of one run.</summary>
    private sealed class Timer
    {
        public TimeSpan WarmUp;
        public TimeSpan Upload;
        public TimeSpan Kernel;
        public TimeSpan Download;
        private readonly Stopwatch _watch = new();

        public void Start() => _watch.Restart();

        public void Stop(ref TimeSpan total)
        {
            _watch.Stop();
            total += _watch.Elapsed;
        }

        public RunTimings Timings() => new(WarmUp, Upload, Kernel, Download);
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
