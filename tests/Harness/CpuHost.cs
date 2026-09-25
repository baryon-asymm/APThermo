using APThermo.Data;
using APThermo.Fixtures;
using ILGPU;
using ILGPU.Runtime;
using ILGPU.Runtime.CPU;

namespace APThermo.Harness;

/// <summary>
/// One ILGPU context with the CPU accelerator, the committed database (with <c>trans.inp</c>) and the tolerance table,
/// loaded once. One per test assembly, held by the consumer's collection or class fixture (BOOT.md, "one host, CPU
/// only"): it never creates a CUDA accelerator, so a test node built on it stays testable without CUDA.
/// </summary>
public sealed class CpuHost : IDisposable
{
    /// <summary>Creates the host: one ILGPU context, one CPU accelerator, the committed database and the tolerance table.</summary>
    public CpuHost()
    {
        Context = Context.Create(builder => builder.CPU());
        Accelerator = Context.CreateCPUAccelerator(0);
        Database = SpeciesDatabase.Load(Path.Combine(RepositoryPaths.Data, "thermo.inp"), Path.Combine(RepositoryPaths.Data, "trans.inp"));
        Tolerances = ToleranceTable.Load();
    }

    /// <summary>The ILGPU context the host was created with.</summary>
    public Context Context { get; }

    /// <summary>The CPU accelerator of <see cref="Context"/>.</summary>
    public Accelerator Accelerator { get; }

    /// <summary>The committed NASA species database, loaded once.</summary>
    public SpeciesDatabase Database { get; }

    /// <summary>The single tolerance table of the tree, loaded once.</summary>
    public ToleranceTable Tolerances { get; }

    /// <summary>Releases the accelerator and the context.</summary>
    public void Dispose()
    {
        Accelerator.Dispose();
        Context.Dispose();
    }
}
