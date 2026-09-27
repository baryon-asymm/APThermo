using ILGPU.Runtime.Cuda;
using Xunit.Abstractions;

namespace APThermo.Execution.Tests;

/// <summary>
/// The bad library (BOOT.md, "The library before the device"; the audit's F2): a file named as the platform's libnvvm that is
/// not a library, paired with the real bitcode this machine's discovery finds, discovery off. Before the fix an explicit
/// <see cref="AcceleratorKind.Cuda"/> request threw the runtime's own unwrapped exception and an <see cref="AcceleratorKind.Auto"/>
/// fallback recorded a reason with no path, and either one first let ILGPU's accelerator constructor create a CUDA context it
/// then had no handle left to release: about 190 MiB of device memory per attempt (the audit's own measurement, 20 attempts
/// losing 3 800 MiB). Checking libnvvm and the bitcode before any CUDA context exists keeps the bad library from ever
/// reaching the device.
/// </summary>
[Collection(EngineFixture.CollectionName)]
public sealed class BadLibraryTests(ITestOutputHelper output)
{
    /// <summary>An explicit request names both paths; an <c>Auto</c> fallback names the library path as the reason; and the
    /// library never reaches the device, so 20 such <c>Auto</c> creations leave free device memory within 64 MiB of where it
    /// started.</summary>
    [Fact]
    [Trait("Category", "Cuda")]
    public void ABadLibraryNamesBothPathsAndNeverReachesTheDevice()
    {
        var cuda = EngineFixture.Shared.RequireCuda();
        if (cuda is null)
        {
            return;
        }

        var directory = Directory.CreateTempSubdirectory("apthermo-bad-library-").FullName;
        try
        {
            var (bogus, bitcode) = BogusLibrary(directory);
            var explicitRequest = new EngineOptions { Accelerator = AcceleratorKind.Cuda, LibNvvmPath = bogus, LibDevicePath = bitcode, LibDeviceDiscovery = false };
            var refused = Assert.Throws<AcceleratorUnavailableException>(() => Engine.Create(explicitRequest));
            Assert.Contains(bogus, refused.Message, StringComparison.Ordinal);
            Assert.Contains(bitcode, refused.Message, StringComparison.Ordinal);
            Assert.Equal([bogus, bitcode], refused.PathsTried);

            var autoOptions = new EngineOptions { Accelerator = AcceleratorKind.Auto, LibNvvmPath = bogus, LibDevicePath = bitcode, LibDeviceDiscovery = false };
            AssertFallsBackNamingTheLibrary(autoOptions, bogus);
            AssertNoMemoryLeak(cuda, autoOptions);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    /// <summary>One <c>Auto</c> creation with the bad library binds the CPU accelerator and names the library path as the reason.</summary>
    private static void AssertFallsBackNamingTheLibrary(EngineOptions autoOptions, string bogus)
    {
        using var auto = Engine.Create(autoOptions);
        Assert.Equal(AcceleratorKind.Cpu, auto.Accelerator.Kind);
        var reason = auto.Accelerator.CudaSkippedBecause;
        Assert.NotNull(reason);
        Assert.Contains(bogus, reason, StringComparison.Ordinal);
    }

    /// <summary>20 failed <c>Auto</c> creations with the bad library cost at most 64 MiB of free device memory, read from the
    /// shared CUDA engine's own accelerator (bound to the calling thread first, as the driver requires).</summary>
    private void AssertNoMemoryLeak(Engine cuda, EngineOptions autoOptions)
    {
        var before = FreeDeviceMemory(cuda);
        for (var i = 0; i < 20; i++)
        {
            using var attempt = Engine.Create(autoOptions);
            Assert.Equal(AcceleratorKind.Cpu, attempt.Accelerator.Kind);
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var after = FreeDeviceMemory(cuda);
        output.WriteLine($"free device memory before {before / (1 << 20)} MiB, after 20 failed CUDA binds {after / (1 << 20)} MiB; lost {(before - after) / (1 << 20)} MiB");
        Assert.True(before - after <= 64L << 20, $"20 failed CUDA binds cost {(before - after) / (1 << 20)} MiB of device memory");
    }

    private static long FreeDeviceMemory(Engine cuda)
    {
        var accelerator = (CudaAccelerator)cuda.IlgpuAccelerator;
        accelerator.Bind();
        var result = GetMemoryInfo(out var free, out _);
        Assert.Equal(CudaError.CUDA_SUCCESS, result);
        return free;
    }

    /// <summary>Pins the <c>long</c> overload of <c>CudaAPI.GetMemoryInfo</c>, which is otherwise ambiguous with its
    /// <c>nint</c> sibling when the caller writes <c>out var</c>.</summary>
    private static CudaError GetMemoryInfo(out long free, out long total) => CudaAPI.CurrentAPI.GetMemoryInfo(out free, out total);

    /// <summary>A file named as the platform's libnvvm that is not a library, under <paramref name="directory"/>, paired with
    /// the real bitcode this machine's discovery finds.</summary>
    private static (string Bogus, string Bitcode) BogusLibrary(string directory)
    {
        var (_, bitcode, _) = LibDeviceLocator.Locate(new EngineOptions());
        Assert.NotNull(bitcode);
        var bogus = Path.Combine(directory, LibDeviceLocator.LibraryFileName);
        File.WriteAllText(bogus, "not a library");
        return (bogus, bitcode!);
    }
}
