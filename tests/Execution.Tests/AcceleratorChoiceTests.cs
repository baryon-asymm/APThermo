using System.Diagnostics;
using APThermo.Execution.Chunks;
using APThermo.Performance;
using APThermo.Transport;

namespace APThermo.Execution.Tests;

/// <summary>L0: accelerator choice, the environment variable, libdevice discovery messages, the ILGPU assertion, batch validation.</summary>
[Collection(EngineFixture.CollectionName)]
public sealed class AcceleratorChoiceTests
{
    /// <summary>The cpu engine names itself and the ilgpu version.</summary>
    [Fact]
    public void TheCpuEngineNamesItselfAndTheIlgpuVersion()
    {
        var info = EngineFixture.Shared.Cpu.Accelerator;
        Assert.Equal(AcceleratorKind.Cpu, info.Kind);
        Assert.Equal(LibDevicePostLink.ExpectedIlgpuVersion, info.IlgpuVersion);
        Assert.Null(info.LibNvvmPath);
        Assert.Null(info.LibDevicePath);
        Assert.True(info.ThreadsOrMultiprocessors >= 1);
        Assert.False(string.IsNullOrWhiteSpace(info.DeviceName));
        Assert.Null(info.CudaSkippedBecause);   // an engine asked for the CPU never tried CUDA
    }

    /// <summary>An auto fallback says why cuda was skipped and which paths were tried.</summary>
    [Fact]
    public void AnAutoFallbackSaysWhyCudaWasSkippedAndWhichPathsWereTried()
    {
        const string dll = @"X:\nowhere\nvvm64_40_0.dll";
        const string bitcode = @"X:\nowhere\libdevice.10.bc";
        var options = new EngineOptions { Accelerator = AcceleratorKind.Auto, LibNvvmPath = dll, LibDevicePath = bitcode, LibDeviceDiscovery = false };
        using var engine = Engine.Create(options);
        Assert.Equal(AcceleratorKind.Cpu, engine.Accelerator.Kind);
        var reason = engine.Accelerator.CudaSkippedBecause;
        Assert.NotNull(reason);
        if (Engine.CudaForbidden)
        {
            Assert.Contains(EngineOptions.NoCudaVariable, reason, StringComparison.Ordinal);
            return;
        }

        Assert.Contains("libdevice", reason, StringComparison.Ordinal);
        Assert.Contains(dll, reason, StringComparison.Ordinal);
        Assert.Contains(bitcode, reason, StringComparison.Ordinal);

        // The reason survives the fallback as a value, not only as a sentence: the decision carries the paths it examined.
        var decision = AcceleratorChoice.Decide(options);
        using (decision.Session)
        {
            Assert.Equal(reason, decision.CudaSkippedBecause);
            Assert.Equal([dll, bitcode], decision.PathsTried);
        }
    }

    /// <summary>An explicit cuda request with paths nowhere names every path tried.</summary>
    [Fact]
    public void AnExplicitCudaRequestWithPathsNowhereNamesEveryPathTried()
    {
        const string dll = @"X:\nowhere\nvvm64_40_0.dll";
        const string bitcode = @"X:\nowhere\libdevice.10.bc";
        var options = new EngineOptions { Accelerator = AcceleratorKind.Cuda, LibNvvmPath = dll, LibDevicePath = bitcode, LibDeviceDiscovery = false };
        var refused = Assert.Throws<AcceleratorUnavailableException>(() => Engine.Create(options));
        if (Engine.CudaForbidden)
        {
            Assert.Contains(EngineOptions.NoCudaVariable, refused.Message, StringComparison.Ordinal);
            return;
        }

        Assert.Contains(dll, refused.Message, StringComparison.Ordinal);
        Assert.Contains(bitcode, refused.Message, StringComparison.Ordinal);
        Assert.Equal([dll, bitcode], refused.PathsTried);
    }

    /// <summary>Discovery reports the toolkit paths it examined.</summary>
    [Fact]
    public void DiscoveryReportsTheToolkitPathsItExamined()
    {
        var (dll, bitcode, tried) = LibDeviceLocator.Locate(new EngineOptions());

        // The shape holds on every machine, toolkit or not: any path this locator ever tried is either the
        // platform's own libnvvm file name or a bitcode file, never anything else.
        Assert.All(tried, path => Assert.True(
            path.EndsWith(LibDeviceLocator.LibraryFileName, StringComparison.OrdinalIgnoreCase) || path.EndsWith(".bc", StringComparison.OrdinalIgnoreCase),
            path));

        // A non-empty list is only guaranteed where the locator has a candidate root to look under: an
        // environment variable it reads, or a directory it looks in. A bare runner with neither honestly
        // tries nothing, and that is not a defect of discovery.
        if (ACandidateToolkitRootExists())
        {
            Assert.NotEmpty(tried);
        }

        if (dll is not null)
        {
            Assert.True(File.Exists(dll), dll);
            Assert.True(File.Exists(bitcode!), bitcode);
            Assert.EndsWith(LibDeviceLocator.LibraryFileName, dll, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Whether this machine offers <see cref="LibDeviceLocator"/> at least one root to look under. On Linux
    /// its <c>ToolkitRoots</c> always yields the fixed root under <c>/usr/local</c>, whether or not that
    /// directory exists, so discovery there never examines nothing. On Windows it needs <c>CUDA_PATH</c> or a
    /// versioned directory under the default toolkit base to have anything to try.
    /// </summary>
    private static bool ACandidateToolkitRootExists()
    {
        if (OperatingSystem.IsLinux())
        {
            return true;
        }

        if (!OperatingSystem.IsWindows())
        {
            return false;   // an unsupported platform does no discovery at all
        }

        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("CUDA_PATH")))
        {
            return true;
        }

        var toolkitBase = Path.Combine(
            Environment.GetEnvironmentVariable("ProgramFiles") ?? @"C:\Program Files", "NVIDIA GPU Computing Toolkit", "CUDA");
        return Directory.Exists(toolkitBase) && Directory.GetDirectories(toolkitBase, "v*").Length > 0;
    }

    /// <summary>The variable forbids cuda and auto falls back to the cpu.</summary>
    [Fact]
    public void TheVariableForbidsCudaAndAutoFallsBackToTheCpu()
    {
        var previous = Environment.GetEnvironmentVariable(EngineOptions.NoCudaVariable);
        try
        {
            Environment.SetEnvironmentVariable(EngineOptions.NoCudaVariable, "1");
            Assert.True(Engine.CudaForbidden);
            using var auto = Engine.Create(new EngineOptions { Accelerator = AcceleratorKind.Auto });
            Assert.Equal(AcceleratorKind.Cpu, auto.Accelerator.Kind);
            var refused = Assert.Throws<AcceleratorUnavailableException>(() => Engine.Create(new EngineOptions { Accelerator = AcceleratorKind.Cuda }));
            Assert.Contains(EngineOptions.NoCudaVariable, refused.Message, StringComparison.Ordinal);
            Environment.SetEnvironmentVariable(EngineOptions.NoCudaVariable, "0");
            Assert.False(Engine.CudaForbidden);
        }
        finally
        {
            Environment.SetEnvironmentVariable(EngineOptions.NoCudaVariable, previous);
        }
    }

    /// <summary>No cuda driver is loaded in a process that forbids cuda.</summary>
    [Fact]
    public void NoCudaDriverIsLoadedInAProcessThatForbidsCuda()
    {
        if (!Engine.CudaForbidden)
        {
            return;   // the CUDA engine of this process may legitimately have loaded the driver
        }

        using var auto = Engine.Create();
        Assert.Equal(AcceleratorKind.Cpu, auto.Accelerator.Kind);
        var modules = Process.GetCurrentProcess().Modules.Cast<ProcessModule>().Select(m => m.ModuleName).ToList();
        Assert.DoesNotContain(modules, name => name.Contains("nvcuda", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(modules, name => name.Contains("nvvm", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>The ilgpu assertion fails loudly for another version.</summary>
    [Fact]
    public void TheIlgpuAssertionFailsLoudlyForAnotherVersion()
    {
        var wrong = Assert.Throws<InvalidOperationException>(() => LibDevicePostLink.AssertIlgpu("9.9.9.0"));
        Assert.Contains("9.9.9.0", wrong.Message, StringComparison.Ordinal);
        Assert.Contains(LibDevicePostLink.ExpectedIlgpuVersion, wrong.Message, StringComparison.Ordinal);
        var (fragments, assembly) = LibDevicePostLink.AssertIlgpu(LibDevicePostLink.ExpectedIlgpuVersion);
        Assert.Equal("fragments", fragments.Name);
        Assert.Equal("<PTXAssembly>k__BackingField", assembly.Name);
        var keys = ((Dictionary<string, string>)fragments.GetValue(null)!).Keys;
        Assert.Contains("__nv_exp", keys);
        Assert.Contains("__nv_log", keys);
        Assert.Contains("__nv_pow", keys);
    }

    /// <summary>Wrapper names are read from the ptx without the prefix.</summary>
    [Fact]
    public void WrapperNamesAreReadFromThePtxWithoutThePrefix()
    {
        const string ptx = "call.uni (r), __ilgpu__nv_exp, (a);\ncall.uni (r), __ilgpu__nv_log10, (b);\ncall.uni (r), __ilgpu__nv_exp, (c);";
        Assert.Equal(["__nv_exp", "__nv_log10"], LibDevicePostLink.WrappersCalled(ptx));
    }

    /// <summary>Inconsistent batches are refused before any kernel runs.</summary>
    [Fact]
    public void InconsistentBatchesAreRefusedBeforeAnyKernelRuns()
    {
        var family = FixtureBatches.RocketFamilies(EngineFixture.Shared.Database)[0];
        using var tables = EngineFixture.Shared.Cpu.Upload(family.Table, family.Transport);
        var wrongElements = new RocketBatch(2, family.Table.ElementCount + 1, family.Inputs[0].Exits.Kinds);
        _ = Assert.Throws<ArgumentException>(() => EngineFixture.Shared.Cpu.Run(tables, wrongElements));
        var wrongSpecies = new TransportBatch(2, family.Table.SpeciesCount + 1);
        _ = Assert.Throws<ArgumentException>(() => EngineFixture.Shared.Cpu.Run(tables, wrongSpecies));
        var wrongEquilibrium = new EquilibriumBatch(2, family.Table.ElementCount + 1);
        _ = Assert.Throws<ArgumentException>(() => EngineFixture.Shared.Cpu.Run(tables, wrongEquilibrium));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new RocketBatch(0, 2, []));

        using var other = Engine.Create(new EngineOptions { Accelerator = AcceleratorKind.Cpu });
        _ = Assert.Throws<ArgumentException>(() => other.Run(tables, family.Batch()));
        using var withoutTransport = EngineFixture.Shared.Cpu.Upload(family.Table);
        _ = Assert.Throws<ArgumentException>(() => EngineFixture.Shared.Cpu.Run(withoutTransport, new TransportBatch(1, family.Table.SpeciesCount)));
        var otherTable = FixtureBatches.RocketFamilies(EngineFixture.Shared.Database)[1].Table;
        _ = Assert.Throws<ArgumentException>(() => EngineFixture.Shared.Cpu.Upload(otherTable, family.Transport));
    }

    /// <summary>Chunks are bounded by the chunk size and the scratch memory.</summary>
    [Fact]
    public void ChunksAreBoundedByTheChunkSizeAndTheScratchMemory()
    {
        var small = new EngineOptions { Accelerator = AcceleratorKind.Cpu, ChunkSize = 10, ScratchBytes = 1000 };
        Assert.Equal(10, ChunkPlan.For(1000, 12, small).Size);            // by chunk size
        Assert.Equal(5, ChunkPlan.For(1000, 200, small).Size);            // by memory: 200 bytes per case against the 1000 allowed
        Assert.Equal(1, ChunkPlan.For(1000, 8000, small).Size);           // never below one case
        Assert.Equal(3, ChunkPlan.For(3, 12, small).Size);                // never above the count
        Assert.Equal([new Chunk(0, 10), new Chunk(10, 10), new Chunk(20, 5)], ChunkPlan.For(25, 12, small).Chunks());
        _ = Assert.Throws<ArgumentException>(() => Engine.Create(new EngineOptions { Accelerator = AcceleratorKind.Cpu, ChunkSize = 0 }));
        var refused = Assert.Throws<ArgumentException>(() => Engine.Create(new EngineOptions { Accelerator = AcceleratorKind.Cpu, ScratchBytes = 0 }));
        Assert.Contains("scratch bound", refused.Message, StringComparison.Ordinal);
        _ = Assert.Throws<ArgumentException>(() => Engine.Create(new EngineOptions { Accelerator = AcceleratorKind.Cpu, ScratchBytes = -1 }));
    }

    /// <summary>Result layouts follow the station and species counts.</summary>
    [Fact]
    public void ResultLayoutsFollowTheStationAndSpeciesCounts()
    {
        var family = FixtureBatches.RocketFamilies(EngineFixture.Shared.Database)[0];
        using var tables = EngineFixture.Shared.Cpu.Upload(family.Table, family.Transport);
        var batch = family.Batch();
        var result = EngineFixture.Shared.Cpu.Run(tables, batch);
        Assert.Equal(batch.Count, result.Count);
        Assert.Equal(RocketLayout.StationCount(batch.Exits), result.StationCount);
        Assert.Equal(batch.Count * result.StationCount, result.Stations.Length);
        Assert.Equal(batch.Count * result.StationCount, result.Figures.Length);
        Assert.Equal((long)batch.Count * result.StationCount * family.Table.SpeciesCount, result.Moles.LongLength);
        Assert.Equal(EngineFixture.Shared.Cpu.Accelerator, result.Accelerator);
        var transport = TransportBatch.FromRocket(result);
        Assert.Equal(result.Stations.Length, transport.Count);
        Assert.Same(result.Moles, transport.Moles);
        Assert.Equal(result.Stations.Select(s => s.Temperature), transport.Temperature);
        _ = Assert.IsType<TransportFigures[]>(EngineFixture.Shared.Cpu.Run(tables, transport).Figures);
    }
}
