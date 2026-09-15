namespace APThermo.Execution;

/// <summary>Which accelerator an engine binds to.</summary>
public enum AcceleratorKind
{
    /// <summary>CUDA when a device, libnvvm and libdevice are found and CUDA is not forbidden; otherwise the CPU accelerator.</summary>
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

    public AcceleratorKind Accelerator { get; init; } = AcceleratorKind.Auto;

    public int CudaDeviceIndex { get; init; }

    /// <summary>Explicit path of <c>nvvm64_40_0.dll</c>; tried first.</summary>
    public string? LibNvvmPath { get; init; }

    /// <summary>Explicit path of <c>libdevice.10.bc</c>; tried first.</summary>
    public string? LibDevicePath { get; init; }

    /// <summary>Whether <c>CUDA_PATH</c> and the CUDA toolkit directories are searched after the explicit paths.</summary>
    public bool LibDeviceDiscovery { get; init; } = true;

    public int ChunkSize { get; init; } = DefaultChunkSize;

    public long ScratchBytes { get; init; } = DefaultScratchBytes;
}

/// <summary>The accelerator that produced a batch result.</summary>
public sealed record AcceleratorInfo(
    AcceleratorKind Kind, string DeviceName, string IlgpuVersion,
    string? LibNvvmPath, string? LibDevicePath, int ThreadsOrMultiprocessors)
{
    /// <summary>
    /// Why <see cref="AcceleratorKind.Auto"/> fell back to the CPU accelerator: the failure that turned the choice, the forbidding
    /// variable included, with the paths tried where they apply. Null when CUDA was bound or the options asked for the CPU.
    /// </summary>
    public string? CudaSkippedBecause { get; init; }
}

/// <summary>Where the time of a run went. Warm-up is the kernel compilation on first use and is zero afterwards.</summary>
public sealed record RunTimings(TimeSpan WarmUp, TimeSpan Upload, TimeSpan Kernel, TimeSpan Download);

/// <summary>A requested accelerator cannot be created; the message names the missing piece and every path that was tried.</summary>
public sealed class AcceleratorUnavailableException : Exception
{
    public AcceleratorUnavailableException(string message, IReadOnlyList<string> pathsTried, Exception? inner = null)
        : base(pathsTried.Count == 0 ? message : message + " Paths tried: " + string.Join("; ", pathsTried) + ".", inner)
    {
        PathsTried = pathsTried;
    }

    /// <summary>The libnvvm and libdevice paths that were examined, in order.</summary>
    public IReadOnlyList<string> PathsTried { get; }
}
