using APThermo.Data;
using APThermo.Fixtures;
using APThermo.Harness;
using ILGPU;
using ILGPU.Runtime;

namespace APThermo.Performance.Tests;

/// <summary>
/// The harness's CPU host (context, accelerator, database, tolerance table), shared by every test class of this node.
/// Held as the single <see cref="Shared"/> instance rather than through <c>ICollectionFixture&lt;T&gt;</c>: xUnit
/// requires a class fixture's consuming constructor to be the class's single public constructor, which would force
/// this internal-only helper public for no reason a consumer outside this node has (CA1515). The CPU accelerator
/// holds no resource a process exit does not already reclaim, so nothing is lost by not calling <see cref="Dispose"/>
/// at the collection's end, as <c>ICollectionFixture&lt;T&gt;</c> would have.
/// </summary>
internal sealed class CpuFixture : IDisposable
{
    /// <summary>The one instance every test class of this node shares.</summary>
    public static readonly CpuFixture Shared = new();

    private readonly CpuHost _host = new();

    public Context Context => _host.Context;

    public Accelerator Accelerator => _host.Accelerator;

    public SpeciesDatabase Database => _host.Database;

    public ToleranceTable Tolerances => _host.Tolerances;

    public void Dispose() => _host.Dispose();
}
