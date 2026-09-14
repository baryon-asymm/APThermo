using AerospacePropellantThermodynamics.Data;
using AerospacePropellantThermodynamics.Fixtures;
using AerospacePropellantThermodynamics.Harness;
using ILGPU;
using ILGPU.Runtime;

namespace AerospacePropellantThermodynamics.Transport.Tests;

/// <summary>The harness's CPU host (context, accelerator, database, tolerance table) plus the transport database, shared by the collection.</summary>
public sealed class CpuFixture : IDisposable
{
    private readonly CpuHost _host = new();

    public CpuFixture() => Transport = _host.Database.Transport ?? throw new InvalidOperationException("the transport database was not loaded");

    public Context Context => _host.Context;

    public Accelerator Accelerator => _host.Accelerator;

    public SpeciesDatabase Database => _host.Database;

    public TransportDatabase Transport { get; }

    public ToleranceTable Tolerances => _host.Tolerances;

    public void Dispose() => _host.Dispose();
}

[CollectionDefinition(Name)]
public sealed class CpuCollection : ICollectionFixture<CpuFixture>
{
    public const string Name = "cpu";
}
