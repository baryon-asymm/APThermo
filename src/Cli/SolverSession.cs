using System.Diagnostics;
using AerospacePropellantThermodynamics.Execution;
using AerospacePropellantThermodynamics.Problems;

namespace AerospacePropellantThermodynamics.Cli;

/// <summary>
/// The database and the solver of one run, with their timings. The solve timer starts when the engine itself is
/// created (the original tool's own measurement boundary) and keeps running until <see cref="Stop"/>, so it also
/// covers building the mixtures and solving; <see cref="Stop"/> builds the run section.
/// </summary>
internal sealed class SolverSession : IDisposable
{
    private readonly Stopwatch _solveWatch;

    private SolverSession(Solver solver, DatabaseInfo info, double databaseSeconds, Stopwatch solveWatch)
    {
        Solver = solver;
        DatabaseInfo = info;
        DatabaseSeconds = databaseSeconds;
        _solveWatch = solveWatch;
    }

    public Solver Solver { get; }

    public DatabaseInfo DatabaseInfo { get; }

    public double DatabaseSeconds { get; }

    public static SolverSession Open(string? databaseDirectory, AcceleratorKind accelerator)
    {
        var (database, info, databaseSeconds) = DatabaseFiles.Load(databaseDirectory);
        var watch = Stopwatch.StartNew();
        var solver = Solver.Create(database, new EngineOptions { Accelerator = accelerator });
        return new SolverSession(solver, info, databaseSeconds, watch);
    }

    public RunInfo Stop(string command, IReadOnlyList<string> inputs, RunLimits limits)
    {
        _solveWatch.Stop();
        return new RunInfo(command, inputs, DatabaseInfo, Solver.Accelerator, new Timings(DatabaseSeconds, _solveWatch.Elapsed.TotalSeconds), limits);
    }

    public void Dispose() => Solver.Dispose();
}
