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
    public CpuHost()
    {
        Context = Context.Create(builder => builder.CPU());
        Accelerator = Context.CreateCPUAccelerator(0);
        Database = SpeciesDatabase.Load(Path.Combine(RepositoryPaths.Data, "thermo.inp"), Path.Combine(RepositoryPaths.Data, "trans.inp"));
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
