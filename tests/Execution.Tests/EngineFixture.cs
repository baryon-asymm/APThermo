using APThermo.Data;
using APThermo.Fixtures;

namespace APThermo.Execution.Tests;

/// <summary>The committed databases, a CPU engine, and the CUDA engine of the reference machine (null when CUDA is forbidden), shared by the collection.</summary>
public sealed class EngineFixture : IDisposable
{
    private readonly Lazy<Engine?> _cuda;
    private readonly Lazy<SweepRun> _sweep;

    /// <summary>The committed databases, loaded once for the theory data (member data is static) and for the fixture.</summary>
    public static SpeciesDatabase SharedDatabase { get; } =
        SpeciesDatabase.Load(Path.Combine(RepositoryPaths.Data, "thermo.inp"), Path.Combine(RepositoryPaths.Data, "trans.inp"));

    public EngineFixture()
    {
        Database = SharedDatabase;
        Tolerances = ToleranceTable.Load();
        Cpu = Engine.Create(new EngineOptions { Accelerator = AcceleratorKind.Cpu });
        _cuda = new Lazy<Engine?>(() => Engine.CudaForbidden ? null : Engine.Create(new EngineOptions { Accelerator = AcceleratorKind.Cuda }));
        _sweep = new Lazy<SweepRun>(() => SweepRun.Run(this, SweepRun.LongRunningCases));
    }

    public SpeciesDatabase Database { get; }

    public ToleranceTable Tolerances { get; }

    /// <summary>The CPU accelerator engine.</summary>
    internal Engine Cpu { get; }

    /// <summary>The CUDA engine, created on first use; null when the environment forbids CUDA.</summary>
    internal Engine? Cuda => _cuda.Value;

    /// <summary>The 100 000-case sweep on both accelerators, run once for the long-running tests.</summary>
    internal SweepRun Sweep => _sweep.Value;

    /// <summary>
    /// The CUDA engine for a CUDA-category test. When CUDA is forbidden by the environment the test verifies the refusal instead and
    /// returns null, so that the full suite passes under APTHERMO_NO_CUDA=1; on a machine without CUDA the creation fails loudly.
    /// </summary>
    internal Engine? RequireCuda()
    {
        if (!Engine.CudaForbidden)
        {
            return Cuda!;
        }

        var refused = Assert.Throws<AcceleratorUnavailableException>(() => Engine.Create(new EngineOptions { Accelerator = AcceleratorKind.Cuda }));
        Assert.Contains(EngineOptions.NoCudaVariable, refused.Message, StringComparison.Ordinal);
        return null;
    }

    public void Dispose()
    {
        if (_cuda.IsValueCreated)
        {
            _cuda.Value?.Dispose();
        }

        Cpu.Dispose();
    }
}

[CollectionDefinition(Name)]
public sealed class EngineCollection : ICollectionFixture<EngineFixture>
{
    public const string Name = "engine";
}
