using System.Diagnostics;
using AerospacePropellantThermodynamics.Performance;
using AerospacePropellantThermodynamics.Thermo;
using AerospacePropellantThermodynamics.Transport;

namespace AerospacePropellantThermodynamics.Execution.Tests;

/// <summary>L0: accelerator choice, the environment variable, libdevice discovery messages, the ILGPU assertion, batch validation.</summary>
[Collection(EngineCollection.Name)]
public sealed class AcceleratorChoiceTests(EngineFixture fixture)
{
    [Fact]
    public void The_cpu_engine_names_itself_and_the_ilgpu_version()
    {
        var info = fixture.Cpu.Accelerator;
        Assert.Equal(AcceleratorKind.Cpu, info.Kind);
        Assert.Equal(LibDevicePostLink.ExpectedIlgpuVersion, info.IlgpuVersion);
        Assert.Null(info.LibNvvmPath);
        Assert.Null(info.LibDevicePath);
        Assert.True(info.ThreadsOrMultiprocessors >= 1);
        Assert.False(string.IsNullOrWhiteSpace(info.DeviceName));
        Assert.Null(info.CudaSkippedBecause);   // an engine asked for the CPU never tried CUDA
    }

    [Fact]
    public void An_auto_fallback_says_why_cuda_was_skipped_and_which_paths_were_tried()
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

    [Fact]
    public void An_explicit_cuda_request_with_paths_nowhere_names_every_path_tried()
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

    [Fact]
    public void Discovery_reports_the_toolkit_paths_it_examined()
    {
        var (dll, bitcode, tried) = LibDeviceLocator.Locate(new EngineOptions());
        Assert.NotEmpty(tried);
        Assert.All(tried, path => Assert.True(path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".bc", StringComparison.OrdinalIgnoreCase), path));
        if (dll is not null)
        {
            Assert.True(File.Exists(dll), dll);
            Assert.True(File.Exists(bitcode!), bitcode);
            Assert.EndsWith("nvvm64_40_0.dll", dll, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void The_variable_forbids_cuda_and_auto_falls_back_to_the_cpu()
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

    [Fact]
    public void No_cuda_driver_is_loaded_in_a_process_that_forbids_cuda()
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

    [Fact]
    public void The_ilgpu_assertion_fails_loudly_for_another_version()
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

    [Fact]
    public void Wrapper_names_are_read_from_the_ptx_without_the_prefix()
    {
        const string ptx = "call.uni (r), __ilgpu__nv_exp, (a);\ncall.uni (r), __ilgpu__nv_log10, (b);\ncall.uni (r), __ilgpu__nv_exp, (c);";
        Assert.Equal(["__nv_exp", "__nv_log10"], LibDevicePostLink.WrappersCalled(ptx));
    }

    [Fact]
    public void Inconsistent_batches_are_refused_before_any_kernel_runs()
    {
        var family = BatchBuilders.RocketFamilies(fixture.Database)[0];
        using var tables = fixture.Cpu.Upload(family.Table, family.Transport);
        var wrongElements = new RocketBatch(2, family.Table.ElementCount + 1, family.Inputs[0].ExitKinds);
        Assert.Throws<ArgumentException>(() => fixture.Cpu.Run(tables, wrongElements));
        var wrongSpecies = new TransportBatch(2, family.Table.SpeciesCount + 1);
        Assert.Throws<ArgumentException>(() => fixture.Cpu.Run(tables, wrongSpecies));
        var wrongEquilibrium = new EquilibriumBatch(2, family.Table.ElementCount + 1);
        Assert.Throws<ArgumentException>(() => fixture.Cpu.Run(tables, wrongEquilibrium));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RocketBatch(0, 2, []));

        using var other = Engine.Create(new EngineOptions { Accelerator = AcceleratorKind.Cpu });
        Assert.Throws<ArgumentException>(() => other.Run(tables, family.Batch()));
        using var withoutTransport = fixture.Cpu.Upload(family.Table);
        Assert.Throws<ArgumentException>(() => fixture.Cpu.Run(withoutTransport, new TransportBatch(1, family.Table.SpeciesCount)));
        var otherTable = BatchBuilders.RocketFamilies(fixture.Database)[1].Table;
        Assert.Throws<ArgumentException>(() => fixture.Cpu.Upload(otherTable, family.Transport));
    }

    [Fact]
    public void Chunks_are_bounded_by_the_chunk_size_and_the_scratch_memory()
    {
        var small = new EngineOptions { Accelerator = AcceleratorKind.Cpu, ChunkSize = 10, ScratchBytes = 1000 };
        Assert.Equal(10, ChunkPlan.For(1000, 12, small).Size);            // by chunk size
        Assert.Equal(5, ChunkPlan.For(1000, 200, small).Size);            // by memory: 200 bytes per case against the 1000 allowed
        Assert.Equal(1, ChunkPlan.For(1000, 8000, small).Size);           // never below one case
        Assert.Equal(3, ChunkPlan.For(3, 12, small).Size);                // never above the count
        Assert.Equal([new Chunk(0, 10), new Chunk(10, 10), new Chunk(20, 5)], ChunkPlan.For(25, 12, small).Chunks());
        Assert.Throws<ArgumentException>(() => Engine.Create(new EngineOptions { Accelerator = AcceleratorKind.Cpu, ChunkSize = 0 }));
        var refused = Assert.Throws<ArgumentException>(() => Engine.Create(new EngineOptions { Accelerator = AcceleratorKind.Cpu, ScratchBytes = 0 }));
        Assert.Contains("scratch bound", refused.Message, StringComparison.Ordinal);
        Assert.Throws<ArgumentException>(() => Engine.Create(new EngineOptions { Accelerator = AcceleratorKind.Cpu, ScratchBytes = -1 }));
    }

    [Fact]
    public void Result_layouts_follow_the_station_and_species_counts()
    {
        var family = BatchBuilders.RocketFamilies(fixture.Database)[0];
        using var tables = fixture.Cpu.Upload(family.Table, family.Transport);
        var batch = family.Batch();
        var result = fixture.Cpu.Run(tables, batch);
        Assert.Equal(batch.Count, result.Count);
        Assert.Equal(RocketLayout.StationCount(batch.Exits), result.StationCount);
        Assert.Equal(batch.Count * result.StationCount, result.Stations.Length);
        Assert.Equal(batch.Count * result.StationCount, result.Figures.Length);
        Assert.Equal((long)batch.Count * result.StationCount * family.Table.SpeciesCount, result.Moles.LongLength);
        Assert.Equal(fixture.Cpu.Accelerator, result.Accelerator);
        var transport = TransportBatch.FromRocket(result);
        Assert.Equal(result.Stations.Length, transport.Count);
        Assert.Same(result.Moles, transport.Moles);
        Assert.Equal(result.Stations.Select(s => s.Temperature), transport.Temperature);
        Assert.IsType<TransportFigures[]>(fixture.Cpu.Run(tables, transport).Figures);
    }
}
