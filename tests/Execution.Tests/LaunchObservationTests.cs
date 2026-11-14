using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using APThermo.Execution.Chunks;

namespace APThermo.Execution.Tests;

/// <summary>Deterministic per-launch bounds, plus the actual synchronized production recording boundary.</summary>
[Collection(EngineFixture.CollectionName)]
public sealed class LaunchObservationTests
{
    private static readonly TimeSpan Limit = LaunchBudget.DefaultRunTimeLimit / 4;

    /// <summary>The maximum is neither the sum nor the last sample; an equal maximum keeps its first identity.</summary>
    [Fact]
    public void MultipleSafeLaunchesCanHaveAnOverBudgetSum()
    {
        var timer = new RunTimer();
        timer.RecordSuccessfulLaunch(2, TimeSpan.FromMilliseconds(200));
        timer.RecordSuccessfulLaunch(1, TimeSpan.FromMilliseconds(300));
        timer.RecordSuccessfulLaunch(4, TimeSpan.FromMilliseconds(300));
        timer.RecordSuccessfulLaunch(1, TimeSpan.FromMilliseconds(100));
        var timings = timer.Timings();
        Assert.Equal(4, timings.LaunchCount);
        Assert.Equal(8, timings.CoveredCases);
        Assert.Equal(TimeSpan.FromMilliseconds(900), timings.Kernel);
        Assert.Equal(TimeSpan.FromMilliseconds(300), timings.MaxLaunchDuration);
        Assert.Equal(1, timings.MaxLaunchCases);
        Assert.Equal(2, timings.MaxLaunchOrdinal);
        Assert.True(timings.Kernel > Limit);
        Assert.Null(LaunchBudgetObservation.Violation(8, timings, Limit));
    }

    /// <summary>An over-budget launch cannot be hidden by a safe final sample or an acceptable average.</summary>
    [Fact]
    public void OneOverBudgetLaunchFailsEvenWhenTheAverageIsSafe()
    {
        var timer = new RunTimer();
        timer.RecordSuccessfulLaunch(1, TimeSpan.FromMilliseconds(1));
        timer.RecordSuccessfulLaunch(2, Limit + TimeSpan.FromTicks(1));
        timer.RecordSuccessfulLaunch(4, TimeSpan.FromMilliseconds(1));
        var timings = timer.Timings();
        Assert.True(timings.Kernel / timings.LaunchCount < Limit);
        Assert.Contains("above the budget", LaunchBudgetObservation.Violation(7, timings, Limit), StringComparison.Ordinal);
        Assert.Equal(2, timings.MaxLaunchOrdinal);
        Assert.Equal(2, timings.MaxLaunchCases);
    }

    /// <summary>The limit is inclusive; zero elapsed time still identifies a real first successful launch.</summary>
    [Fact]
    public void BoundaryAndZeroDurationObservationsRetainTheirIdentity()
    {
        var timer = new RunTimer();
        timer.RecordSuccessfulLaunch(1, TimeSpan.Zero);
        Assert.Equal(1, timer.Timings().MaxLaunchOrdinal);
        Assert.Equal(1, timer.Timings().MaxLaunchCases);
        Assert.Null(LaunchBudgetObservation.Violation(1, timer.Timings(), Limit));
        timer.RecordSuccessfulLaunch(2, Limit);
        Assert.Null(LaunchBudgetObservation.Violation(3, timer.Timings(), Limit));
        Assert.Equal(2, timer.Timings().MaxLaunchOrdinal);
    }

    /// <summary>Empty, omitted or inconsistent observations cannot certify every launch of a nonempty workload.</summary>
    [Fact]
    public void MissingCoverageAndMaximumIdentityAreRefused()
    {
        var timer = new RunTimer();
        Assert.Contains("no successful", LaunchBudgetObservation.Violation(1, timer.Timings(), Limit), StringComparison.Ordinal);
        timer.Add(RunPhase.Kernel, TimeSpan.FromMilliseconds(10));
        Assert.Contains("no successful", LaunchBudgetObservation.Violation(1, timer.Timings(), Limit), StringComparison.Ordinal);
        timer.RecordSuccessfulLaunch(2, TimeSpan.FromMilliseconds(10));
        var timings = timer.Timings();
        Assert.Contains("cover", LaunchBudgetObservation.Violation(3, timings, Limit), StringComparison.Ordinal);
        Assert.Contains("identity", LaunchBudgetObservation.Violation(2, timings with { MaxLaunchOrdinal = 0 }, Limit), StringComparison.Ordinal);
        Assert.Contains("identity", LaunchBudgetObservation.Violation(2, timings with { MaxLaunchCases = 3 }, Limit), StringComparison.Ordinal);
        Assert.Contains("identity", LaunchBudgetObservation.Violation(2, timings with { MaxLaunchDuration = timings.Kernel + TimeSpan.FromTicks(1) }, Limit), StringComparison.Ordinal);
    }

    /// <summary>The real batch loop records every emitted chunk, after its launch work and synchronization.</summary>
    [Fact]
    public void ActualBatchRunRecordsCountCoverageAndMaximumChunk()
    {
        using var cpu = Engine.Create(new EngineOptions { Accelerator = AcceleratorKind.Cpu });
        var plan = ChunkPlan.For(7, 8, 1, new EngineOptions { ChunkSize = 3 });
        using var buffers = new ChunkBuffers(cpu.IlgpuAccelerator);
        buffers.Allocate(plan.Size);
        var timer = new RunTimer();
        var emitted = new List<(int Cases, TimeSpan Inside)>();
        cpu.RunBatchLoop(plan, buffers, timer, cases =>
        {
            var start = Stopwatch.GetTimestamp();
            Thread.SpinWait(2000);
            emitted.Add((cases, Stopwatch.GetElapsedTime(start)));
        });
        var timings = timer.Timings();
        Assert.Equal([3, 3, 1], emitted.Select(launch => launch.Cases));
        Assert.Equal(emitted.Count, timings.LaunchCount);
        Assert.Equal(emitted.Sum(launch => launch.Cases), timings.CoveredCases);
        Assert.InRange(timings.MaxLaunchOrdinal, 1, emitted.Count);
        Assert.Equal(emitted[timings.MaxLaunchOrdinal - 1].Cases, timings.MaxLaunchCases);
        Assert.True(timings.MaxLaunchDuration >= emitted.Max(launch => launch.Inside));
        Assert.True(timings.MaxLaunchDuration > TimeSpan.Zero);
        Assert.True(timings.Kernel >= timings.MaxLaunchDuration);
        Assert.Null(LaunchBudgetObservation.Violation(plan.Count, timings, TimeSpan.MaxValue));
    }

    /// <summary>A successful synchronized launch is recorded before a later download failure.</summary>
    [Fact]
    public void DownloadFailureCannotEraseTheCompletedLaunchObservation()
    {
        using var cpu = Engine.Create(new EngineOptions { Accelerator = AcceleratorKind.Cpu });
        var plan = ChunkPlan.For(1, 8, 1, new EngineOptions());
        using var buffers = new ChunkBuffers(cpu.IlgpuAccelerator);
        _ = buffers.Output(Array.Empty<double>(), perCase: 1);
        buffers.Allocate(plan.Size);
        var timer = new RunTimer();
        _ = Assert.Throws<ArgumentException>(() => cpu.RunBatchLoop(plan, buffers, timer, _ => { }));
        var timings = timer.Timings();
        Assert.Equal(1, timings.LaunchCount);
        Assert.Equal(1, timings.CoveredCases);
        Assert.Equal(1, timings.MaxLaunchCases);
        Assert.Equal(1, timings.MaxLaunchOrdinal);
    }

    /// <summary>A launch that throws before synchronization succeeds contributes no success observation or kernel interval.</summary>
    [Fact]
    public void FailedLaunchIsNotReportedAsSuccessfullyCovered()
    {
        using var cpu = Engine.Create(new EngineOptions { Accelerator = AcceleratorKind.Cpu });
        var plan = ChunkPlan.For(1, 8, 1, new EngineOptions());
        using var buffers = new ChunkBuffers(cpu.IlgpuAccelerator);
        buffers.Allocate(plan.Size);
        var timer = new RunTimer();
        var primary = new InvalidOperationException("injected launch failure");
        Assert.Same(primary, Assert.Throws<InvalidOperationException>(() => cpu.RunBatchLoop(plan, buffers, timer, _ => throw primary)));
        Assert.Equal(0, timer.Timings().LaunchCount);
        Assert.Equal(0, timer.Timings().CoveredCases);
        Assert.Equal(TimeSpan.Zero, timer.Timings().Kernel);
    }

    /// <summary>Internal launch telemetry cannot add fields to the four existing public timing phases.</summary>
    [Fact]
    public void TelemetryPreservesTheFourPhaseTimingJsonAndConstructor()
    {
        var timer = new RunTimer();
        timer.RecordSuccessfulLaunch(1, TimeSpan.FromMilliseconds(2));
        var timings = timer.Timings();
        var constructor = typeof(RunTimings).GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Single(candidate => candidate.GetParameters().Length == 4);
        var originalPhases = constructor.GetParameters().Select(parameter => parameter.Name).Order(StringComparer.Ordinal);
        var serialized = JsonSerializer.SerializeToElement(timings).EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal);
        Assert.Equal(originalPhases, serialized);
    }
}