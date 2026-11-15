using System.Diagnostics;
using APThermo.Execution.Calibration;

namespace APThermo.Execution.Tests;

/// <summary>
/// The long-running sweep: the same batch on the CPU accelerator and on CUDA, timed as the median of three
/// timed runs after a warm-up, so a single slow or fast sample does not move the throughput tripwire; the CUDA kernel time is the
/// median of the timed runs' kernel times, not the first run's.
/// </summary>
internal sealed record SweepRun(RocketBatch Batch, RocketBatchResult Cpu, RocketBatchResult? Cuda, RocketBatchResult? CudaAgain, TimeSpan CpuSeconds, CudaTiming CudaTime)
{
    /// <summary>The bounded R09 summaries of the timed CPU runs, in original order.</summary>
    public IReadOnlyList<RunTimings> CpuLaunchRuns { get; init; } = [];

    public const int LongRunningCases = 100_000;
    public const string FamilyName = "lox-lh2_of4_pc10MPa_frozenAtChamber";
    public const string From = "lox-lh2_of4_pc7MPa_shiftingEquilibrium";
    public const string To = "lox-lh2_of8_pc7MPa_shiftingEquilibrium";

    private const int TimedRunCount = 3;

    /// <summary>CUDA leaves its idle P-state over several kernel launches, not one; the CPU accelerator has no such state.</summary>
    private const int CudaWarmUpRunCount = 5;

    public static SweepRun Run(EngineFixture fixture, int count)
    {
        var family = FixtureBatches.Family(fixture.Database, FamilyName);
        var batch = FixtureBatches.Sweep(family, From, To, count, 5.0e6, 10.0e6);
        var warmUp = FixtureBatches.Sweep(family, From, To, 64, 5.0e6, 10.0e6);

        using var cpuTables = fixture.Cpu.Upload(family.Table);
        _ = fixture.Cpu.Run(cpuTables, warmUp);
        var (cpu, cpuSeconds, cpuLaunchRuns) = TimeMedian(TimedRunCount, () => fixture.Cpu.Run(cpuTables, batch));

        if (fixture.Cuda is not { } engine)
        {
            return new SweepRun(batch, cpu, null, null, cpuSeconds, CudaTiming.None) { CpuLaunchRuns = cpuLaunchRuns };
        }

        using var cudaTables = engine.Upload(family.Table);
        for (var i = 0; i < CudaWarmUpRunCount; i++)
        {
            _ = engine.Run(cudaTables, warmUp);
        }

        var (cuda, cudaTime, again) = TimeMedianKeepingTwo(engine, cudaTables, batch);
        return new SweepRun(batch, cpu, cuda, again, cpuSeconds, cudaTime) { CpuLaunchRuns = cpuLaunchRuns };
    }

    /// <summary>Runs <paramref name="run"/> <paramref name="count"/> times, timed; returns the first result, unchanged median elapsed time and bounded per-run R09 summaries.</summary>
    private static (RocketBatchResult Result, TimeSpan Median, IReadOnlyList<RunTimings> LaunchRuns) TimeMedian(int count, Func<RocketBatchResult> run)
    {
        var elapsed = new TimeSpan[count];
        var observations = new RunTimings[count];
        var watch = new Stopwatch();
        RocketBatchResult? first = null;
        for (var i = 0; i < count; i++)
        {
            watch.Restart();
            var result = run();
            elapsed[i] = watch.Elapsed;
            observations[i] = result.Timings;
            first ??= result;
        }

        return (first!, CudaTiming.Median(elapsed), observations);
    }

    /// <summary>
    /// Times CUDA the same way as <see cref="TimeMedian"/>, taking the median kernel time of the runs too, and keeps the first two
    /// runs' results, so the determinism check still has two independent CUDA results to compare bit for bit. Only the small R09 summaries of every run are retained additionally.
    /// </summary>
    private static (RocketBatchResult First, CudaTiming Time, RocketBatchResult Second) TimeMedianKeepingTwo(Engine engine, UploadedTables tables, RocketBatch batch)
    {
        var results = new RocketBatchResult[TimedRunCount];
        var observations = new CalibrationObservation[TimedRunCount];
        var elapsed = new TimeSpan[TimedRunCount];
        var watch = new Stopwatch();
        for (var i = 0; i < TimedRunCount; i++)
        {
            watch.Restart();
            results[i] = engine.Run(tables, batch);
            elapsed[i] = watch.Elapsed;
            observations[i] = engine.CalibrationObservation;
        }

        var kernels = results.Select(result => result.Timings.Kernel).ToArray();
        var timing = CudaTiming.From(elapsed, kernels) with
        {
            LaunchRuns = [.. results.Select(result => result.Timings)],
            CalibrationRuns = observations,
        };
        return (results[0], timing, results[1]);
    }
}
