using ILGPU;
using ILGPU.Runtime;
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
            return new AcceleratorDecision(Cpu(null), null, []);
        }

        try
        {
            return new AcceleratorDecision(Cuda(options), null, []);
        }
        catch (Exception failure) when (options.Accelerator == AcceleratorKind.Auto && failure is not OutOfMemoryException)
        {
            var tried = failure is AcceleratorUnavailableException refused ? refused.PathsTried : [];
            return new AcceleratorDecision(Cpu(failure.Message), failure.Message, tried);
        }
    }

    private static AcceleratorSession Cpu(string? cudaSkippedBecause) =>
        AcceleratorSession.Build(Context.Create(builder => builder.CPU()), session =>
        {
            var accelerator = session.Attach(session.Context.CreateCPUAccelerator(0));
            return new AcceleratorInfo(AcceleratorKind.Cpu, accelerator.Name, LibDevicePostLink.IlgpuVersion, null, null, accelerator.NumThreads)
            {
                CudaSkippedBecause = cudaSkippedBecause,
            };
        });

    private static AcceleratorSession Cuda(EngineOptions options)
    {
        if (CudaForbidden)
        {
            throw new AcceleratorUnavailableException($"CUDA was requested, but {EngineOptions.NoCudaVariable}=1 forbids it.", []);
        }

        var (dll, bitcode, tried) = LibDeviceLocator.Locate(options);
        if (dll is null || bitcode is null)
        {
            throw new AcceleratorUnavailableException("libnvvm (nvvm64_40_0.dll) and libdevice (libdevice.10.bc) were not found.", tried);
        }

        return AcceleratorSession.Build(CudaContext(dll, bitcode), session =>
        {
            var devices = session.Context.GetCudaDevices();
            if (options.CudaDeviceIndex < 0 || options.CudaDeviceIndex >= devices.Count)
            {
                throw new AcceleratorUnavailableException($"CUDA device {options.CudaDeviceIndex} was requested, but {devices.Count} device(s) exist.", [dll, bitcode]);
            }

            var accelerator = session.Attach(session.Context.CreateCudaAccelerator(options.CudaDeviceIndex));
            session.Attach(NvvmAPI.Create(dll, bitcode));
            return new AcceleratorInfo(AcceleratorKind.Cuda, accelerator.Name, LibDevicePostLink.IlgpuVersion, dll, bitcode, accelerator.NumMultiprocessors);
        });
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
