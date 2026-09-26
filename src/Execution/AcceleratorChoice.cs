using ILGPU;
using ILGPU.Runtime.CPU;
using ILGPU.Runtime.Cuda;

namespace APThermo.Execution;

/// <summary>
/// Turns the options into the accelerator to run on, by the rules of BOOT.md: CUDA when it is not forbidden, libnvvm and libdevice
/// are found and the device exists; otherwise the CPU accelerator with all cores. An explicit CUDA request fails instead of falling
/// back; an <see cref="AcceleratorKind.Auto"/> fallback keeps the reason.
/// </summary>
internal static class AcceleratorChoice
{
    /// <summary>True when the environment forbids CUDA (<see cref="EngineOptions.NoCudaVariable"/> is 1).</summary>
    public static bool CudaForbidden => Environment.GetEnvironmentVariable(EngineOptions.NoCudaVariable)?.Trim() == "1";

    /// <summary>The session the options ask for, with the reason when CUDA was skipped. The caller owns the session.</summary>
    public static AcceleratorDecision Decide(EngineOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.Accelerator == AcceleratorKind.Cpu)
        {
            return Decided(Cpu(null), null, []);
        }

        try
        {
            return Decided(Cuda(options), null, []);
        }
        catch (Exception failure) when (options.Accelerator == AcceleratorKind.Auto && failure is not OutOfMemoryException)
        {
            var tried = failure is AcceleratorUnavailableException refused ? refused.PathsTried : [];
            return Decided(Cpu(failure.Message), failure.Message, tried);
        }
    }

    /// <summary>Wraps a created session into its decision; disposes the session if wrapping it fails, since a session that
    /// does not escape into the returned decision would otherwise leak (CA2000).</summary>
    private static AcceleratorDecision Decided(AcceleratorSession session, string? cudaSkippedBecause, IReadOnlyList<string> pathsTried)
    {
        try
        {
            var decision = new AcceleratorDecision(session, cudaSkippedBecause, pathsTried);
            session = null!;
            return decision;
        }
        finally
        {
            session?.Dispose();
        }
    }

    private static AcceleratorSession Cpu(string? cudaSkippedBecause) =>
        AcceleratorSession.Build(Context.Create(builder => builder.CPU(CpuDeviceFor(Environment.ProcessorCount))), session =>
        {
            var accelerator = session.Attach(session.Context.CreateCPUAccelerator(0));
            return new AcceleratorInfo(AcceleratorKind.Cpu, accelerator.Name, LibDevicePostLink.IlgpuVersion, null, null, accelerator.NumThreads)
            {
                CudaSkippedBecause = cudaSkippedBecause,
            };
        });

    /// <summary>
    /// A CPU device sized for <paramref name="processorCount"/> (BOOT.md, "All cores"; the audit's F3), not ILGPU's fixed
    /// 16-thread <see cref="CPUDevice.Default"/>.
    ///
    /// ILGPU 1.5.3's <see cref="CPUDevice"/> constructor was measured directly (a reflection probe against the three
    /// constructor arguments, warp size, warps per multiprocessor and multiprocessors, each varied alone): the warp size
    /// needs no upper bound and only refuses 1 (thread counts of 2 through at least 1000 all construct); the warps per
    /// multiprocessor must be a power of two, and every one of 3, 5, 6, 7, 9, 10, 12, 24 and 48 throws
    /// <see cref="ArgumentOutOfRangeException"/> (misnaming <c>numThreadsPerWarp</c> even though the warps argument is the
    /// one at fault); the multiprocessor count carries no constraint ILGPU checks at all — every value tried, from 2 to
    /// 1 000 000, constructs. Keeping the warp size fixed at today's 4 (so the layout at 16 processors stays exactly
    /// (4, 4, 1), the shape every bit and throughput record was measured against) therefore reaches every total that is a
    /// multiple of 4 exactly, by choosing the multiprocessor count instead of leaving it at 1: the number of whole groups of
    /// 4 threads the count allows, <c>fourThreadGroups = processorCount / 4</c>, splits into a power-of-two warp count (its
    /// lowest set bit, the largest power of two that divides it) and an unconstrained multiprocessor count (the remaining
    /// factor), whose product reconstructs <c>fourThreadGroups</c> exactly. A count not a multiple of 4 (and, since the warp
    /// size floor is 2, a count of 1) cannot be matched exactly this way; the layout then falls back to the nearest total not
    /// above the count, which this same construction already produces (a group total is always at or under the count).
    /// </summary>
    internal static CPUDevice CpuDeviceFor(int processorCount)
    {
        var count = Math.Max(processorCount, 1);
        if (count < 4)
        {
            // Below one full group of 4 the warp size itself carries the count; 2 and 3 match exactly, 1 does not (ILGPU
            // refuses a one-thread warp, so the total floor is 2).
            return new CPUDevice(Math.Max(2, count), numWarpsPerMultiprocessor: 1, numMultiprocessors: 1);
        }

        var fourThreadGroups = count / 4;
        var warpsPerMultiprocessor = fourThreadGroups & -fourThreadGroups;
        var multiprocessors = fourThreadGroups / warpsPerMultiprocessor;
        return new CPUDevice(4, warpsPerMultiprocessor, multiprocessors);
    }

    private static AcceleratorSession Cuda(EngineOptions options)
    {
        if (CudaForbidden)
        {
            throw new AcceleratorUnavailableException($"CUDA was requested, but {EngineOptions.NoCudaVariable}=1 forbids it.", []);
        }

        var (dll, bitcode, tried) = LibDeviceLocator.Locate(options);
        return dll is null || bitcode is null
            ? throw new AcceleratorUnavailableException($"libnvvm ({LibDeviceLocator.LibraryFileName}) and libdevice (libdevice.10.bc) were not found.", tried)
            : AcceleratorSession.Build(CudaContext(dll, bitcode), session =>
            {
                // The library before the device (BOOT.md, the audit's F2): libnvvm is loaded and asked its IR version, and
                // the bitcode is read, before any CUDA context exists. The session keeps this one binding; no second
                // NvvmAPI.Create follows once the accelerator is up.
                var nvvm = session.Attach(LoadNvvm(dll, bitcode));
                var devices = session.Context.GetCudaDevices();
                if (options.CudaDeviceIndex < 0 || options.CudaDeviceIndex >= devices.Count)
                {
                    throw new AcceleratorUnavailableException($"CUDA device {options.CudaDeviceIndex} was requested, but {devices.Count} device(s) exist.", [dll, bitcode]);
                }

                var accelerator = session.Attach(CreateAccelerator(session.Context, options.CudaDeviceIndex, dll, bitcode));
                ProbeBinding(session, dll, bitcode);
                return new AcceleratorInfo(AcceleratorKind.Cuda, accelerator.Name, LibDevicePostLink.IlgpuVersion, dll, bitcode, accelerator.NumMultiprocessors);
            });
    }

    /// <summary>
    /// Loads libnvvm, asks its IR version and reads the bitcode, before any CUDA context exists (BOOT.md, the audit's F2): a
    /// bad library or an unreadable bitcode then never reaches <see cref="CreateAccelerator"/>, so it never leaks the raw
    /// CUDA context ILGPU's own accelerator constructor would otherwise have created first and had no handle left to
    /// release. Wraps every failure as <see cref="AcceleratorUnavailableException"/> naming both paths, the way
    /// <see cref="CudaContext"/> already does for the context.
    /// </summary>
    private static NvvmAPI LoadNvvm(string dll, string bitcode)
    {
        NvvmAPI? nvvm = null;
        try
        {
            nvvm = NvvmAPI.Create(dll, bitcode);
            var result = nvvm.GetIRVersion(out _, out _, out _, out _);
            if (result != NvvmResult.NVVM_SUCCESS)
            {
                throw new InvalidOperationException($"libnvvm's GetIRVersion returned {result}.");
            }

            _ = nvvm.LibDeviceBytes.Length; // the bitcode itself: a failure to read it surfaces here, not at the first compile
            return nvvm;
        }
        catch (Exception failure) when (failure is not OutOfMemoryException)
        {
            nvvm?.Dispose();
            throw new AcceleratorUnavailableException($"libnvvm or libdevice could not be loaded: {failure.Message}", [dll, bitcode], failure);
        }
    }

    /// <summary>
    /// Wraps ILGPU's own accelerator constructor (BOOT.md, the audit's F2): its failures otherwise escape unwrapped, and,
    /// for a cause this check cannot foresee, still leak the CUDA context ILGPU had already created before the failure,
    /// since ILGPU gives no handle to release one. <see cref="LoadNvvm"/> above keeps the known cause, a bad library, from
    /// ever reaching this call.
    /// </summary>
    private static CudaAccelerator CreateAccelerator(Context context, int deviceIndex, string dll, string bitcode)
    {
        try
        {
            return context.CreateCudaAccelerator(deviceIndex);
        }
        catch (Exception failure) when (failure is not OutOfMemoryException)
        {
            throw new AcceleratorUnavailableException($"the CUDA accelerator could not be created: {failure.Message}", [dll, bitcode], failure);
        }
    }

    /// <summary>
    /// CUDA is bound only when a kernel runs on it (BOOT.md): the math probe kernel is compiled, post-linked and loaded through
    /// the same path <see cref="KernelCache"/> uses, and released at once. A failure here becomes the same
    /// <see cref="AcceleratorUnavailableException"/> shape as every other bind failure, its inner exception the post-link's own,
    /// so a device on which no kernel can load is never reported as bound.
    /// </summary>
    private static void ProbeBinding(AcceleratorSession session, string dll, string bitcode)
    {
        try
        {
            using var probe = KernelCache.Load(session, nameof(Kernels.Probe));
        }
        catch (Exception failure) when (failure is not OutOfMemoryException)
        {
            throw new AcceleratorUnavailableException($"the math probe kernel could not be loaded: {failure.Message}", [dll, bitcode], failure);
        }
    }

    private static Context CudaContext(string dll, string bitcode)
    {
        try
        {
            // LibDevice() makes ILGPU emit the intrinsic calls; the wrappers themselves come from this node's post-link.
            return Context.Create(builder => builder.Cuda().Math(MathMode.Default).LibDevice(dll, bitcode));
        }
        catch (Exception failure)
        {
            throw new AcceleratorUnavailableException("the CUDA context could not be created (driver or device problem): " + failure.Message, [dll, bitcode], failure);
        }
    }
}
