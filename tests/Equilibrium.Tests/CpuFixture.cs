using AerospacePropellantThermodynamics.Data;
using AerospacePropellantThermodynamics.Fixtures;
using ILGPU;
using ILGPU.Runtime;
using ILGPU.Runtime.CPU;

namespace AerospacePropellantThermodynamics.Equilibrium.Tests;

/// <summary>One ILGPU context with the CPU accelerator, the committed database and the tolerance table, shared by the collection.</summary>
public sealed class CpuFixture : IDisposable
{
    public CpuFixture()
    {
        Context = Context.Create(builder => builder.CPU());
        Accelerator = Context.CreateCPUAccelerator(0);
        Database = SpeciesDatabase.Load(Path.Combine(RepositoryPaths.Data, "thermo.inp"));
        Tolerances = ToleranceTable.Load();
    }

    public Context Context { get; }

    public Accelerator Accelerator { get; }

    public SpeciesDatabase Database { get; }

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
