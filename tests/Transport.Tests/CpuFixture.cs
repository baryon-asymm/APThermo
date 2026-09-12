using AerospacePropellantThermodynamics.Data;
using AerospacePropellantThermodynamics.Fixtures;
using ILGPU;
using ILGPU.Runtime;
using ILGPU.Runtime.CPU;

namespace AerospacePropellantThermodynamics.Transport.Tests;

/// <summary>One ILGPU context with the CPU accelerator, the committed databases (thermo and transport) and the tolerance table, shared by the collection.</summary>
public sealed class CpuFixture : IDisposable
{
    public CpuFixture()
    {
        Context = Context.Create(builder => builder.CPU());
        Accelerator = Context.CreateCPUAccelerator(0);
        Database = SpeciesDatabase.Load(Path.Combine(RepositoryPaths.Data, "thermo.inp"), Path.Combine(RepositoryPaths.Data, "trans.inp"));
        Transport = Database.Transport ?? throw new InvalidOperationException("the transport database was not loaded");
        Tolerances = ToleranceTable.Load();
    }

    public Context Context { get; }

    public Accelerator Accelerator { get; }

    public SpeciesDatabase Database { get; }

    public TransportDatabase Transport { get; }

    public ToleranceTable Tolerances { get; }

    public void Dispose()
    {
        Accelerator.Dispose();
        Context.Dispose();
    }
}

[CollectionDefinition(Name)]
public sealed class CpuCollection : ICollectionFixture<CpuFixture>
{
    public const string Name = "cpu";
}
