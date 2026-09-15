using System.Diagnostics;

namespace APThermo.Execution.Tests;

/// <summary>The long-running sweep: the same batch on the CPU accelerator and on CUDA (twice), with the times.</summary>
internal sealed record SweepRun(RocketBatch Batch, RocketBatchResult Cpu, RocketBatchResult? Cuda, RocketBatchResult? CudaAgain, TimeSpan CpuSeconds, TimeSpan CudaSeconds)
{
    public const int LongRunningCases = 100_000;
    public const string FamilyName = "lox-lh2_of4_pc10MPa_frozenAtChamber";
    public const string From = "lox-lh2_of4_pc7MPa_shiftingEquilibrium";
    public const string To = "lox-lh2_of8_pc7MPa_shiftingEquilibrium";

    public static SweepRun Run(EngineFixture fixture, int count)
    {
        var family = FixtureBatches.Family(fixture.Database, FamilyName);
        var batch = FixtureBatches.Sweep(family, From, To, count, 5.0e6, 10.0e6);
        using var cpuTables = fixture.Cpu.Upload(family.Table);
        fixture.Cpu.Run(cpuTables, FixtureBatches.Sweep(family, From, To, 64, 5.0e6, 10.0e6));   // warm-up
        var watch = Stopwatch.StartNew();
        var cpu = fixture.Cpu.Run(cpuTables, batch);
        var cpuSeconds = watch.Elapsed;
        RocketBatchResult? cuda = null;
        RocketBatchResult? again = null;
        var cudaSeconds = TimeSpan.Zero;
        if (fixture.Cuda is { } engine)
        {
            using var cudaTables = engine.Upload(family.Table);
            engine.Run(cudaTables, FixtureBatches.Sweep(family, From, To, 64, 5.0e6, 10.0e6));   // warm-up
            watch.Restart();
            cuda = engine.Run(cudaTables, batch);
            cudaSeconds = watch.Elapsed;
            again = engine.Run(cudaTables, batch);
        }

        return new SweepRun(batch, cpu, cuda, again, cpuSeconds, cudaSeconds);
    }
}
