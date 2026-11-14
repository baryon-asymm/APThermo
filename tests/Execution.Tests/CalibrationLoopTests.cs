using APThermo.Execution.Chunks;

namespace APThermo.Execution.Tests;

/// <summary>The actual production batch loop must still upload, compute, download and measure current work on a hit.</summary>
[Collection(EngineFixture.CollectionName)]
public sealed class CalibrationLoopTests
{
    /// <summary>Reuse changes only scheduling; current callbacks, current results and current R09 timings remain mandatory.</summary>
    [Fact]
    public void AHitPerformsEveryCurrentCaseAndRecordsItsOwnLaunches()
    {
        using var scenario = new CalibrationScenario();
        var work = new LoopWork();
        var original = Enumerable.Range(0, scenario.Plan.Count).ToArray();
        var (coldValues, coldTimings) = work.Run(scenario, (int[])original.Clone());
        Assert.Equal(original.Select(value => value + 1), coldValues);
        Assert.Equal(1, coldTimings.FirstLaunchCases);
        Assert.False(coldTimings.ReusedPrefix);
        // R12, 2026-11-15: real Stopwatch partitions are not a deterministic prefix oracle.
        work.Train(scenario, original);
        var previousCalls = work.Calls;
        var (hitValues, hitTimings) = work.Run(scenario, (int[])original.Clone());
        Assert.Equal(original.Select(value => value + 2), hitValues);
        Assert.Equal(15, hitTimings.FirstLaunchCases);
        Assert.True(hitTimings.ReusedPrefix);
        Assert.Equal(previousCalls + hitTimings.LaunchCount, work.Calls);
        Assert.Equal(original.Length, hitTimings.CoveredCases);
        Assert.True(hitTimings.Kernel > TimeSpan.Zero);
        Assert.True(hitTimings.MaxLaunchDuration > TimeSpan.Zero);
        Assert.NotSame(coldTimings, hitTimings);
        Assert.Equal(31, hitTimings.CoveredCases);
    }

    /// <summary>InputOutput's output B cannot masquerade as initial A; a cloned original A is a legitimate hit.</summary>
    [Fact]
    public void InitialSeedIdentityIsCapturedBeforeDownloadMutatesIt()
    {
        using var scenario = new CalibrationScenario();
        var work = new LoopWork();
        var original = Enumerable.Range(0, scenario.Plan.Count).ToArray();
        var seedA = (int[])original.Clone();
        var (coldValues, _) = work.Run(scenario, seedA);
        Assert.Equal(original.Select(value => value + 1), seedA);
        var (_, newInputTimings) = work.Run(scenario, (int[])coldValues.Clone());
        Assert.Equal(1, newInputTimings.FirstLaunchCases);
        Assert.False(newInputTimings.ReusedPrefix);
        work.Train(scenario, original);
        var (_, originalTimings) = work.Run(scenario, (int[])original.Clone());
        Assert.True(originalTimings.ReusedPrefix);
        Assert.Equal(15, originalTimings.FirstLaunchCases);
    }

    /// <summary>A slower historical partition has an exact full boundary of ten, proving fifteen is not a universal host invariant.</summary>
    [Fact]
    public void AControlledNonDoublingPartitionDrivesTheActualFirstLaunch()
    {
        using var scenario = new CalibrationScenario();
        var work = new LoopWork();
        var original = Enumerable.Range(0, scenario.Plan.Count).ToArray();
        work.Train(scenario, original, nonDoubling: true);
        var (values, timings) = work.Run(scenario, (int[])original.Clone());
        Assert.True(timings.ReusedPrefix);
        Assert.Equal(10, timings.FirstLaunchCases);
        Assert.Equal(original.Select(value => value + 1), values);
        Assert.Equal(work.Calls, timings.LaunchCount);
        Assert.Equal(original.Length, timings.CoveredCases);
        Assert.True(timings.Kernel > TimeSpan.Zero);
    }

    /// <summary>A late assembly failure leaves no entry even after the loop has completed and disposed its buffers.</summary>
    [Fact]
    public void SuccessfulLoopIsNotPublishedBeforeResultAssemblyCommits()
    {
        using var scenario = new CalibrationScenario();
        var work = new LoopWork();
        var primary = new InvalidOperationException("injected late result assembly");
        Assert.Same(primary, Assert.Throws<InvalidOperationException>(() =>
            work.Run(scenario, new int[scenario.Plan.Count], primary)));
        var (_, nextTimings) = work.Run(scenario, new int[scenario.Plan.Count]);
        Assert.False(nextTimings.ReusedPrefix);
        Assert.Equal(1, nextTimings.FirstLaunchCases);
    }

    /// <summary>Only the first successful current launch owns the first-case and reuse labels.</summary>
    [Fact]
    public void FirstLaunchTelemetryIsEmptyUntilSuccessAndCannotBeOverwritten()
    {
        var timer = new RunTimer();
        Assert.Equal(0, timer.Timings().FirstLaunchCases);
        Assert.False(timer.Timings().ReusedPrefix);
        timer.RecordSuccessfulLaunch(15, TimeSpan.FromMilliseconds(3), reusedPrefix: true);
        timer.RecordSuccessfulLaunch(16, TimeSpan.FromMilliseconds(4), reusedPrefix: false);
        Assert.Equal(15, timer.Timings().FirstLaunchCases);
        Assert.True(timer.Timings().ReusedPrefix);
        Assert.Equal(31, timer.Timings().CoveredCases);
        Assert.Equal(TimeSpan.FromMilliseconds(7), timer.Timings().Kernel);
        var diagnostic = LaunchBudgetObservation.Describe(timer.Timings());
        Assert.Contains("firstLaunchCases=15", diagnostic, StringComparison.Ordinal);
        Assert.Contains("reusedPrefix=True", diagnostic, StringComparison.Ordinal);
    }

    private sealed class LoopWork
    {
        private ChunkBuffer<int>? _values;
        private int _generation;
        internal LoopWork()
        {
            Launcher = Launch;
        }
        internal Action<int> Launcher { get; }
        internal int Calls { get; private set; }

        internal (int[] Values, RunTimings Timings) Run(CalibrationScenario scenario, int[] values, Exception? assemblyFailure = null)
        {
            _generation++;
            using var buffers = new ChunkBuffers(scenario.Cpu.IlgpuAccelerator);
            _values = buffers.InputOutput(values, perCase: 1);
            var identity = buffers.InputIdentity();
            buffers.Allocate(scenario.Plan.Size);
            var views = new CalibrationViews(0, 2, default);
            using var calibration = scenario.Cache.Begin(Launcher, scenario.Tables, scenario.Plan, identity, views);
            var timer = new RunTimer();
            BatchRun.Execute(scenario.Session, scenario.Plan, buffers, timer, Launcher, calibration);
            if (assemblyFailure is not null)
            {
                throw assemblyFailure;
            }

            var result = (values, timer.Timings());
            calibration.Complete();
            return result;
        }

        /// <summary>A controlled completed observation, independent of the current host's Stopwatch/GC/load.</summary>
        internal void Train(CalibrationScenario scenario, int[] initial, bool nonDoubling = false)
        {
            using var buffers = new ChunkBuffers(scenario.Cpu.IlgpuAccelerator);
            _ = buffers.InputOutput(initial, perCase: 1);
            var identity = buffers.InputIdentity();
            using var run = scenario.Cache.Begin(Launcher, scenario.Tables, scenario.Plan, identity, scenario.Views);
            if (nonDoubling)
            {
                // A previous repeat can start at two; one slower launch changes its later partition.
                run.ObserveSuccessfulLaunch(2, TimeSpan.FromMilliseconds(10));
                run.ObserveSuccessfulLaunch(4, TimeSpan.FromMilliseconds(400));
                run.ObserveSuccessfulLaunch(4, TimeSpan.FromMilliseconds(10));
                run.ObserveSuccessfulLaunch(8, TimeSpan.FromMilliseconds(10));
                run.ObserveSuccessfulLaunch(13, TimeSpan.FromMilliseconds(10));
            }
            else
            {
                foreach (var cases in new[] { 1, 2, 4, 8, 16 })
                {
                    run.ObserveSuccessfulLaunch(cases, TimeSpan.FromMilliseconds(5));
                }
            }

            run.Complete();
        }

        private void Launch(int cases)
        {
            Calls++;
            var view = _values!.View;
            for (var index = 0; index < cases; index++)
            {
                view[index] += _generation;
            }
        }
    }
}