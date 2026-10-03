using APThermo.Data;
using APThermo.Execution;
using APThermo.Problems;

namespace APThermo.Cli;

/// <summary>
/// Warm solvers kept across in-process invocations of <see cref="Program.RunCached"/>: one per database content (its
/// provenance hashes) and requested accelerator, so that the kernels a solver compiles on its first run are compiled
/// once per cache instead of once per invocation. The cache is an explicit parameter, never a static, so this node
/// holds no hidden state. An engine is used from one thread at a time (Execution API), so an invocation holds
/// <see cref="Gate"/> for its whole length.
/// </summary>
internal sealed class SolverCache : IDisposable
{
    private readonly Dictionary<(string Thermo, string? Trans, AcceleratorKind Accelerator), Solver> _solvers = [];

    /// <summary>Creates an empty cache; no solver exists until an invocation asks for one.</summary>
    public SolverCache()
    {
    }

    /// <summary>The number of solvers this cache has created: the non-degeneracy figure of the one-solver-per-key rule.</summary>
    public int Count => _solvers.Count;

    /// <summary>Held by <see cref="Program.RunCached"/> for the length of one invocation, which serializes the invocations.</summary>
    internal Lock Gate { get; } = new();

    /// <summary>The solver of this database content and accelerator, created on the first request and reused after.</summary>
    internal Solver SolverFor(SpeciesDatabase database, AcceleratorKind accelerator)
    {
        ArgumentNullException.ThrowIfNull(database);
        var key = (database.Provenance.ThermoSha256, database.Provenance.TransSha256, accelerator);
        if (!_solvers.TryGetValue(key, out var solver))
        {
            solver = Solver.Create(database, new EngineOptions { Accelerator = accelerator });
            _solvers[key] = solver;
        }

        return solver;
    }

    /// <summary>Disposes every solver this cache created.</summary>
    public void Dispose()
    {
        foreach (var solver in _solvers.Values)
        {
            solver.Dispose();
        }

        _solvers.Clear();
    }
}
