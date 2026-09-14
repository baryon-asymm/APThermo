using AerospacePropellantThermodynamics.Data;
using AerospacePropellantThermodynamics.Fixtures;
using AerospacePropellantThermodynamics.Harness;
using ILGPU;
using ILGPU.Runtime;

namespace AerospacePropellantThermodynamics.Performance.Tests;

/// <summary>The harness's CPU host (context, accelerator, database, tolerance table), shared by the collection.</summary>
public sealed class CpuFixture : IDisposable
{
    private readonly CpuHost _host = new();

    public Context Context => _host.Context;

    public Accelerator Accelerator => _host.Accelerator;

    public SpeciesDatabase Database => _host.Database;

    public ToleranceTable Tolerances => _host.Tolerances;

    public void Dispose() => _host.Dispose();
}

[CollectionDefinition(Name)]
public sealed class CpuCollection : ICollectionFixture<CpuFixture>
{
    public const string Name = "cpu";
}
