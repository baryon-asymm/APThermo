using APThermo.Data;
using APThermo.Execution;
using APThermo.Fixtures;

namespace APThermo.Problems.Tests;

/// <summary>
/// The committed databases, the tolerance table, one solver and one engine on the CPU accelerator, shared by every
/// test class of this node. Held as the single <see cref="Shared"/> instance rather than through
/// <c>ICollectionFixture&lt;T&gt;</c>: xUnit requires a class fixture's consuming constructor to be the class's
/// single public constructor, which would force this internal-only helper public for no reason a consumer outside
/// this node has (CA1515). The classes that shared one <c>SolverFixture</c> through the "solver"
/// <c>ICollectionFixture&lt;T&gt;</c> collection at 8375261 still run sequentially relative to each other, tagged
/// <c>[Collection(CollectionName)]</c>, a string-named xUnit collection that needs no public
/// <c>[CollectionDefinition]</c> class to exist.
/// </summary>
internal sealed class SolverFixture : IDisposable
{
    /// <summary>The xUnit collection name every consuming class of this node is tagged with.</summary>
    public const string CollectionName = "solver";

    /// <summary>
    /// Loaded once for the theory data (member data is static) and for the fixture. Declared before <see cref="Shared"/>:
    /// static field and property initializers run in declaration order, and the constructor <see cref="Shared"/> triggers
    /// reads this property.
    /// </summary>
    public static SpeciesDatabase SharedDatabase { get; } =
        SpeciesDatabase.Load(Path.Combine(RepositoryPaths.Data, "thermo.inp"), Path.Combine(RepositoryPaths.Data, "trans.inp"));

    /// <summary>The one instance every test class of this node shares.</summary>
    public static readonly SolverFixture Shared = new();

    /// <summary>Builds the shared database, tolerance table, solver and engine once for the whole collection.</summary>
    public SolverFixture()
    {
        Database = SharedDatabase;
        Tolerances = ToleranceTable.Load();
        Solver = Solver.Create(Database, new EngineOptions { Accelerator = AcceleratorKind.Cpu });
        Engine = Engine.Create(new EngineOptions { Accelerator = AcceleratorKind.Cpu });
        AppDomain.CurrentDomain.ProcessExit += (_, _) => Dispose();
    }

    /// <summary>The committed NASA database, loaded once and shared by every case.</summary>
    public SpeciesDatabase Database { get; }

    /// <summary>The tolerance table the reference comparison reads (Fixtures node).</summary>
    public ToleranceTable Tolerances { get; }

    /// <summary>The one solver on the CPU accelerator the tests of this collection share.</summary>
    public Solver Solver { get; }

    /// <summary>A CPU engine of the execution node, to evaluate the transport solver on the reference's own composition (the defect signature).</summary>
    internal Engine Engine { get; }

    /// <summary>Disposes the shared solver and engine.</summary>
    public void Dispose()
    {
        Solver.Dispose();
        Engine.Dispose();
    }
}
