using APThermo.Data;
using APThermo.Execution;
using APThermo.Fixtures;

namespace APThermo.Problems.Tests;

/// <summary>The committed databases, the tolerance table and one solver on the CPU accelerator, shared by the collection.</summary>
public sealed class SolverFixture : IDisposable
{
    /// <summary>Loaded once for the theory data (member data is static) and for the fixture.</summary>
    public static SpeciesDatabase SharedDatabase { get; } =
        SpeciesDatabase.Load(Path.Combine(RepositoryPaths.Data, "thermo.inp"), Path.Combine(RepositoryPaths.Data, "trans.inp"));

    /// <summary>Builds the shared database, tolerance table, solver and engine once for the whole collection.</summary>
    public SolverFixture()
    {
        Database = SharedDatabase;
        Tolerances = ToleranceTable.Load();
        Solver = Solver.Create(Database, new EngineOptions { Accelerator = AcceleratorKind.Cpu });
        Engine = Engine.Create(new EngineOptions { Accelerator = AcceleratorKind.Cpu });
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

/// <summary>Declares the "solver" xUnit collection so every test class of this node shares one <see cref="SolverFixture"/>.</summary>
[CollectionDefinition(Name)]
public sealed class SolverCollectionDefinition : ICollectionFixture<SolverFixture>
{
    /// <summary>The collection name the node's test classes reference through <see cref="CollectionAttribute"/>.</summary>
    public const string Name = "solver";
}
