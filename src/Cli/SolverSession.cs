using System.Diagnostics;
using APThermo.Execution;
using APThermo.Problems;

namespace APThermo.Cli;

/// <summary>
/// The database and the solver of one run, with their timings. The solve timer starts when the engine itself is
/// created (the original tool's own measurement boundary) and keeps running until <see cref="Stop"/>, so it also
/// covers building the mixtures and solving; <see cref="Stop"/> builds the run section.
/// </summary>
internal sealed class SolverSession : IDisposable
{
    private readonly Stopwatch _solveWatch;

    private readonly bool _owned;

    private SolverSession(Solver solver, DatabaseInfo info, double databaseSeconds, Stopwatch solveWatch, bool owned)
    {
        _owned = owned;
        Solver = solver;
        DatabaseInfo = info;
        DatabaseSeconds = databaseSeconds;
        _solveWatch = solveWatch;
    }

    public Solver Solver { get; }

    public DatabaseInfo DatabaseInfo { get; }

    public double DatabaseSeconds { get; }

    /// <summary>
    /// Opens the database and a solver for it: a fresh solver the session owns and disposes, or, given a
    /// <paramref name="cache"/>, the cache's solver, which stays alive after the session.
    /// </summary>
    public static SolverSession Open(string? databaseDirectory, AcceleratorKind accelerator, SolverCache? cache)
    {
        var (database, info, databaseSeconds) = DatabaseFiles.Load(databaseDirectory);
        var watch = Stopwatch.StartNew();
        var solver = cache is null ? Solver.Create(database, new EngineOptions { Accelerator = accelerator }) : cache.SolverFor(database, accelerator);
        return new SolverSession(solver, info, databaseSeconds, watch, cache is null);
    }

    public RunInfo Stop(string command, IReadOnlyList<string> inputs, RunLimits limits)
    {
        _solveWatch.Stop();
        return new RunInfo(command, inputs, DatabaseInfo, Solver.Accelerator, new Timings(DatabaseSeconds, _solveWatch.Elapsed.TotalSeconds), limits);
    }

    public void Dispose()
    {
        if (_owned)
        {
            Solver.Dispose();
        }
    }
}
