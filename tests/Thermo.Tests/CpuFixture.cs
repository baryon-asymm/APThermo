using AerospacePropellantThermodynamics.Data;
using AerospacePropellantThermodynamics.Fixtures;
using ILGPU;
using ILGPU.Runtime;
using ILGPU.Runtime.CPU;

namespace AerospacePropellantThermodynamics.Thermo.Tests;

/// <summary>One ILGPU context with the CPU accelerator, the loaded database and the tolerance table, shared by a test class.</summary>
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

    /// <summary>A table of the given species with exactly the elements their formulas use, uploaded to the CPU accelerator.</summary>
    public SpeciesTableBuffers Upload(params string[] species)
    {
        var elements = species.SelectMany(s => Database[s].Formula.Select(p => p.Symbol)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        return SpeciesTableBuffers.Upload(Accelerator, SpeciesTable.Build(Database, elements, species));
    }

    public void Dispose()
    {
        Accelerator.Dispose();
        Context.Dispose();
    }
}
