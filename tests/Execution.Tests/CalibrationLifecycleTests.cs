using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using APThermo.Execution.Calibration;
using APThermo.Execution.Chunks;

namespace APThermo.Execution.Tests;

/// <summary>Failure, lifecycle and weak ownership controls independent of CUDA timing.</summary>
[Collection(EngineFixture.CollectionName)]
public sealed class CalibrationLifecycleTests
{
    /// <summary>Primary upload/launch/download faults invalidate, preserve the primary, and attempt every cleanup.</summary>
    [Theory]
    [InlineData("upload")]
    [InlineData("launch")]
    [InlineData("download")]
    [InlineData("cleanup")]
    public void EveryLoopFailurePreventsPublication(string stage)
    {
        using var scenario = new CalibrationScenario();
        scenario.Train();
        using var calibration = scenario.Begin();
        using var buffers = new ChunkBuffers(scenario.Cpu.IlgpuAccelerator);
        var primary = new InvalidOperationException("injected " + stage);
        var first = new FaultBuffer(stage, primary);
        var last = new FaultBuffer("none", primary);
        var inventory = (List<IChunkBuffer>)typeof(ChunkBuffers).GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single(field => field.FieldType == typeof(List<IChunkBuffer>)).GetValue(buffers)!;
        inventory.Add(first);
        inventory.Add(last);
        buffers.Allocate(scenario.Plan.Size);
        Action<int> launch = stage == "launch" ? _ => throw primary : _ => { };
        var caught = Assert.Throws<InvalidOperationException>(() =>
            BatchRun.Execute(scenario.Session, scenario.Plan, buffers, new RunTimer(), launch, calibration));
        Assert.Same(primary, caught);
        Assert.True(first.Disposed);
        Assert.True(last.Disposed);
        calibration.Dispose();
        using var next = scenario.Begin();
        Assert.Equal(1, next.FirstChunkCases());
    }

    /// <summary>Every owning lifecycle boundary cancels an otherwise reusable entry, including pre-pipeline guards.</summary>
    [Theory]
    [InlineData("guard")]
    [InlineData("upload")]
    [InlineData("probe")]
    [InlineData("kernel-clear")]
    [InlineData("table-dispose")]
    [InlineData("lost")]
    [InlineData("dispose")]
    public void OwnerBoundariesClearTheSessionEntry(string boundary)
    {
        using var scenario = new CalibrationScenario();
        var cache = scenario.Session.Calibration;
        Publish(cache, scenario);
        switch (boundary)
        {
            case "guard":
                _ = Assert.Throws<ArgumentNullException>(() => scenario.Cpu.Run(scenario.Tables, (RocketBatch)null!));
                break;
            case "upload":
                using (scenario.Cpu.Upload(scenario.Tables.Species)) { }
                break;
            case "probe":
                Assert.Empty(scenario.Cpu.ProbeMath([]));
                break;
            case "kernel-clear":
                scenario.Cpu.Launchers.Clear();
                break;
            case "table-dispose":
                scenario.Tables.Dispose();
                break;
            case "lost":
                scenario.Cpu.MarkLost(new AcceleratorUnavailableException("injected loss"));
                break;
            case "dispose":
                scenario.Cpu.Dispose();
                break;
            default:
                throw new ArgumentException(boundary, nameof(boundary));
        }

        using var next = cache.Begin(scenario.Launcher, scenario.Tables, scenario.Plan, "input-A", scenario.Views);
        Assert.Equal(1, next.FirstChunkCases());
    }

    /// <summary>A still-reachable cache cannot retain its original host array, device buffer, tables or delegate.</summary>
    [Fact]
    public void PublishedEntryRetainsOnlyWeakWorkloadIdentities()
    {
        var (cache, weak) = MakeRetiredEntryWithLivePositiveControl();
        for (var attempt = 0; attempt < 3 && weak.Any(reference => reference.IsAlive); attempt++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        Assert.All(weak, reference => Assert.False(reference.IsAlive));
        GC.KeepAlive(cache);
    }

    /// <summary>The production loop rechecks the pending prefix after upload, before its first physical launch.</summary>
    [Fact]
    public void ExpiryDuringUploadRestoresTheColdPhysicalLaunch()
    {
        using var scenario = new CalibrationScenario();
        scenario.Train();
        using var calibration = scenario.Begin();
        using var buffers = new ChunkBuffers(scenario.Cpu.IlgpuAccelerator);
        var clockBuffer = new ExpiringUpload(scenario.Clock);
        var inventory = (List<IChunkBuffer>)typeof(ChunkBuffers).GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single(field => field.FieldType == typeof(List<IChunkBuffer>)).GetValue(buffers)!;
        inventory.Add(clockBuffer);
        buffers.Allocate(scenario.Plan.Size);
        var emitted = new List<int>();
        var timer = new RunTimer();
        BatchRun.Execute(scenario.Session, scenario.Plan, buffers, timer, emitted.Add, calibration);
        Assert.Equal(1, emitted[0]);
        Assert.Equal([15, 1], clockBuffer.Uploaded.Take(2));
        Assert.False(timer.Timings().ReusedPrefix);
        Assert.Equal(1, timer.Timings().FirstLaunchCases);
    }

    private static void Publish(LaunchCalibrationCache cache, CalibrationScenario scenario)
    {
        using var run = cache.Begin(scenario.Launcher, scenario.Tables, scenario.Plan, "input-A", scenario.Views);
        foreach (var cases in new[] { 1, 2, 4, 8, 16 })
        {
            run.ObserveSuccessfulLaunch(cases, TimeSpan.Zero);
        }

        run.Complete();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (LaunchCalibrationCache Cache, WeakReference[] Weak) MakeRetiredEntryWithLivePositiveControl()
    {
        using var scenario = new CalibrationScenario();
        using var buffers = new ChunkBuffers(scenario.Cpu.IlgpuAccelerator);
        var input = new int[scenario.Plan.Count];
        var buffer = buffers.Input(input, perCase: 1);
        var target = new LauncherTarget();
        Action<int> launcher = target.Launch;
        var identity = buffers.InputIdentity();
        buffers.Allocate(scenario.Plan.Size);
        using (var run = scenario.Cache.Begin(launcher, scenario.Tables, scenario.Plan, identity, scenario.Views))
        {
            foreach (var cases in new[] { 1, 2, 4, 8, 16 })
            {
                run.ObserveSuccessfulLaunch(cases, TimeSpan.Zero);
            }

            run.Complete();
        }

        object[] owners = [input, buffer, scenario.Tables, launcher, target];
        var weak = owners.Select(owner => new WeakReference(owner)).ToArray();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        Assert.All(weak, reference => Assert.True(reference.IsAlive));
        GC.KeepAlive(owners);
        return (scenario.Cache, weak);
    }

    private sealed class ExpiringUpload(CalibrationClock clock) : IChunkBuffer
    {
        internal List<int> Uploaded { get; } = [];
        public long BytesPerCase => 0;
        public long ElementsPerCase => 0;
        public bool AppendIdentity(IncrementalHash hash) => true;
        public void Allocate(ILGPU.Runtime.Accelerator accelerator, int chunk) { }
        public void UploadChunk(int offset, int length)
        {
            Uploaded.Add(length);
            if (Uploaded.Count == 1)
            {
                clock.Advance(TimeSpan.FromMilliseconds(501));
            }
        }

        public void DownloadChunk(int offset, int length) { }
        public void Dispose() { }
    }

    private sealed class LauncherTarget
    {
        internal void Launch(int cases)
        {
            GC.KeepAlive(this);
            GC.KeepAlive(cases);
        }
    }

    private sealed class FaultBuffer(string stage, Exception primary) : IChunkBuffer
    {
        internal bool Disposed { get; private set; }
        public long BytesPerCase => 0;
        public long ElementsPerCase => 0;
        public bool AppendIdentity(IncrementalHash hash) => true;
        public void Allocate(ILGPU.Runtime.Accelerator accelerator, int chunk) { }
        public void UploadChunk(int offset, int length) => Fail("upload");
        public void DownloadChunk(int offset, int length) => Fail("download");
        public void Dispose()
        {
            Disposed = true;
            Fail("cleanup");
        }

        private void Fail(string current)
        {
            if (stage == current)
            {
                throw primary;
            }
        }
    }
}