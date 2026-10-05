namespace APThermo.Execution;

/// <summary>Which accelerator an engine binds to.</summary>
public enum AcceleratorKind
{
    /// <summary>CUDA when a device exists and CUDA is not forbidden; otherwise the CPU accelerator.</summary>
    Auto,

    /// <summary>The CPU accelerator of ILGPU with all cores; no CUDA API is touched.</summary>
    Cpu,

    /// <summary>A CUDA device; fails instead of falling back and names what was missing.</summary>
    Cuda,
}

/// <summary>How an engine is created.</summary>
public sealed record EngineOptions
{
    /// <summary>The name of the environment variable that forbids CUDA when set to <c>1</c>.</summary>
    public const string NoCudaVariable = "APTHERMO_NO_CUDA";

    /// <summary>Default largest number of cases (or stations) one kernel launch processes.</summary>
    public const int DefaultChunkSize = 16384;

    /// <summary>Default bound on the per-launch scratch memory, bytes; a chunk shrinks below <see cref="ChunkSize"/> to respect it.</summary>
    public const long DefaultScratchBytes = 256L << 20;

    /// <value>Which accelerator to bind to. Defaults to <see cref="AcceleratorKind.Auto"/>.</value>
    public AcceleratorKind Accelerator { get; init; } = AcceleratorKind.Auto;

    /// <value>The index of the CUDA device to bind to, when more than one is present. Defaults to 0.</value>
    public int CudaDeviceIndex { get; init; }

    /// <value>The largest number of cases (or stations) one kernel launch processes. Defaults to
    /// <see cref="DefaultChunkSize"/>.</value>
    public int ChunkSize { get; init; } = DefaultChunkSize;

    /// <value>The bound, in bytes, on the per-launch scratch memory; a chunk shrinks below
    /// <see cref="ChunkSize"/> to respect it. Defaults to <see cref="DefaultScratchBytes"/>.</value>
    public long ScratchBytes { get; init; } = DefaultScratchBytes;
}

/// <summary>
/// The accelerator that produced a batch result. Nominal, with an internal constructor (root <c>BOOT.md</c>,
/// Delivery: Tree contracts, "records the library creates for consumers ... have internal constructors"): no
/// consumer builds one, only reads it from <c>Solver.Accelerator</c> or a batch result's <c>Accelerator</c>, so
/// a field added in 0.x breaks nobody.
/// </summary>
public sealed record AcceleratorInfo
{
    internal AcceleratorInfo(AcceleratorKind kind, string deviceName, string ilgpuVersion, int threadsOrMultiprocessors)
    {
        Kind = kind;
        DeviceName = deviceName;
        IlgpuVersion = ilgpuVersion;
        ThreadsOrMultiprocessors = threadsOrMultiprocessors;
    }

    /// <value>Which accelerator was bound.</value>
    public AcceleratorKind Kind { get; }

    /// <value>The name of the bound device, as ILGPU reports it.</value>
    public string DeviceName { get; }

    /// <value>The version of the ILGPU package in use.</value>
    public string IlgpuVersion { get; }

    /// <value>The number of hardware threads (CPU accelerator) or multiprocessors (CUDA) of the bound device.</value>
    public int ThreadsOrMultiprocessors { get; }

    /// <summary>
    /// Why <see cref="AcceleratorKind.Auto"/> fell back to the CPU accelerator: the failure that turned the choice, the forbidding
    /// variable included. Null when CUDA was bound or the options asked for the CPU.
    /// </summary>
    public string? CudaSkippedBecause { get; init; }
}

/// <summary>Where the time of a run went. Warm-up is the kernel compilation on first use and is zero afterwards.</summary>
internal sealed record RunTimings(TimeSpan WarmUp, TimeSpan Upload, TimeSpan Kernel, TimeSpan Download);

/// <summary>A requested accelerator cannot be created; the message names the missing piece.</summary>
public sealed class AcceleratorUnavailableException : Exception
{
    /// <summary>Initializes a new instance with no message (the .NET exception conventions, CA1032).</summary>
    public AcceleratorUnavailableException() : base()
    {
    }

    /// <summary>Initializes a new instance with a description of what is missing.</summary>
    /// <param name="message">A description of what is missing.</param>
    public AcceleratorUnavailableException(string message) : base(message)
    {
    }

    /// <summary>Initializes a new instance with a description of what is missing and the exception that caused it.</summary>
    /// <param name="message">A description of what is missing.</param>
    /// <param name="innerException">The exception that caused this one.</param>
    public AcceleratorUnavailableException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
