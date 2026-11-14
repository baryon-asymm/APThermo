using System.Reflection;
using APThermo.Execution.Calibration;
using APThermo.Execution.Chunks;
using ILGPU;

namespace APThermo.Execution.Tests;

/// <summary>Real CPU-owned tables plus an explicitly synthetic bounded host calibration, without CUDA claims.</summary>
internal sealed class CalibrationScenario : IDisposable
{
    internal CalibrationScenario()
    {
        Cpu = Engine.Create(new EngineOptions { Accelerator = AcceleratorKind.Cpu });
        Tables = Cpu.Upload(FixtureBatches.RocketFamilies(EngineFixture.SharedDatabase)[0].Table);
        Cache = new LaunchCalibrationCache(Clock);
    }

    internal Engine Cpu { get; }
    internal UploadedTables Tables { get; }
    internal CalibrationClock Clock { get; } = new();
    internal LaunchCalibrationCache Cache { get; }
    internal Action<int> Launcher { get; } = _ => { };
    internal CalibrationViews Views { get; } = new(0, 2, default);
    internal ChunkPlan Plan { get; } = MakePlan(31, 16);
    internal AcceleratorSession Session => (AcceleratorSession)typeof(Engine)
        .GetField("_session", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(Cpu)!;

    internal static ChunkPlan MakePlan(int count, int size, int milliseconds = 500) =>
        ChunkPlan.For(count, 8, 1, new EngineOptions { ChunkSize = size },
            LaunchBudget.FromRunTimeLimit(TimeSpan.FromMilliseconds(milliseconds * 4)));

    internal CalibrationRun Begin() => Cache.Begin(Launcher, Tables, Plan, "input-A", Views);

    internal void Train()
    {
        using var run = Begin();
        Assert.Equal(1, run.FirstChunkCases());
        foreach (var cases in new[] { 1, 2, 4, 8, 16 })
        {
            run.ObserveSuccessfulLaunch(cases, TimeSpan.FromMilliseconds(5));
        }

        run.Complete();
    }

    public void Dispose()
    {
        Tables.Dispose();
        Cpu.Dispose();
    }
}

/// <summary>A controlled monotonic clock, including deliberate backward movement for refusal controls.</summary>
internal sealed class CalibrationClock : TimeProvider
{
    internal long Timestamp { get; set; }
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;
    public override long GetTimestamp() => Timestamp;
    internal void Advance(TimeSpan elapsed) => Timestamp += elapsed.Ticks;
}

/// <summary>Both current production scalar names, with a view deliberately excluded from scalar identity.</summary>
internal readonly record struct CalibrationViews(int Seeded, int ExitCount, ArrayView<double> Values);