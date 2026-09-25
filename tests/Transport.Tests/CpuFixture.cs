using APThermo.Data;
using APThermo.Fixtures;
using APThermo.Harness;
using ILGPU;
using ILGPU.Runtime;

namespace APThermo.Transport.Tests;

/// <summary>
/// The harness's CPU host (context, accelerator, database, tolerance table) plus the transport database, shared by
/// every test class of this node. Held as the single <see cref="Shared"/> instance rather than through
/// <c>ICollectionFixture&lt;T&gt;</c>: xUnit requires a class fixture's consuming constructor to be the class's
/// single public constructor, which would force this internal-only helper public for no reason a consumer outside
/// this node has (CA1515, confirmed empirically: a public <c>CpuFixture</c> with a <c>[CollectionDefinition]</c>
/// wrapper fails both CA1515 and CA1711 on a clean build of this project, regardless of whether a consuming class
/// injects it). The classes that shared one <c>CpuFixture</c> through the "cpu" <c>ICollectionFixture&lt;T&gt;</c>
/// collection at 8375261 still run sequentially relative to each other, tagged
/// <c>[Collection(CollectionName)]</c>, a string-named xUnit collection that needs no public
/// <c>[CollectionDefinition]</c> class to exist.
/// </summary>
internal sealed class CpuFixture : IDisposable
{
    /// <summary>The xUnit collection name every consuming class of this node is tagged with.</summary>
    public const string CollectionName = "cpu";

    /// <summary>The one instance every tagged test class of this node shares.</summary>
    public static readonly CpuFixture Shared = new();

    private readonly CpuHost _host = new();

    public CpuFixture()
    {
        Transport = _host.Database.Transport ?? throw new InvalidOperationException("the transport database was not loaded");
        AppDomain.CurrentDomain.ProcessExit += (_, _) => Dispose();
    }

    public Context Context => _host.Context;

    public Accelerator Accelerator => _host.Accelerator;

    public SpeciesDatabase Database => _host.Database;

    public TransportDatabase Transport { get; }

    public ToleranceTable Tolerances => _host.Tolerances;

    public void Dispose() => _host.Dispose();
}
