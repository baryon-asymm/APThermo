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

    public SolverFixture()
    {
        Database = SharedDatabase;
        Tolerances = ToleranceTable.Load();
        Solver = Solver.Create(Database, new EngineOptions { Accelerator = AcceleratorKind.Cpu });
        Engine = Engine.Create(new EngineOptions { Accelerator = AcceleratorKind.Cpu });
    }

    public SpeciesDatabase Database { get; }

    public ToleranceTable Tolerances { get; }

    public Solver Solver { get; }

    /// <summary>A CPU engine of the execution node, to evaluate the transport solver on the reference's own composition (the defect signature).</summary>
    internal Engine Engine { get; }

    public void Dispose()
    {
        Solver.Dispose();
        Engine.Dispose();
    }
}

[CollectionDefinition(Name)]
public sealed class SolverCollection : ICollectionFixture<SolverFixture>
{
    public const string Name = "solver";
}
