using System.Diagnostics;
using System.Globalization;
using APThermo.Harness;

namespace APThermo.Execution.Tests;

/// <summary>
/// All cores (BOOT.md, `AcceleratorChoice.CpuDeviceFor`; the audit's F3): the CPU accelerator is sized for
/// <see cref="Environment.ProcessorCount"/>, not ILGPU's fixed 16-thread default. <see cref="Environment.ProcessorCount"/> is
/// read once, at process start, so each processor count this class proves is a fresh <c>dotnet test</c> process with
/// <c>DOTNET_PROCESSOR_COUNT</c> set: this same test class acts as its own worker, a marker environment variable
/// distinguishing the two roles, so no separate executable is needed.
/// </summary>
public sealed class AllCoresLayoutTests
{
    private const string WorkerOutputVariable = "APTHERMO_ALLCORES_WORKER_OUTPUT";

    /// <summary>
    /// Reports this process's CPU-engine thread count and a small rocket batch's result hash to the file
    /// <see cref="WorkerOutputVariable"/> names, when spawned as a worker; otherwise a no-op, so this fact still passes when
    /// the ordinary suite runs it directly.
    /// </summary>
    [Fact]
    public void AllCoresLayoutWorker()
    {
        var output = Environment.GetEnvironmentVariable(WorkerOutputVariable);
        if (output is null)
        {
            return;
        }

        using var engine = Engine.Create(new EngineOptions { Accelerator = AcceleratorKind.Cpu });
        var family = FixtureBatches.RocketFamilies(EngineFixture.SharedDatabase)[0];
        using var tables = engine.Upload(family.Table, family.Transport);
        var result = engine.Run(tables, family.Batch());
        var hash = new BitHash();
        foreach (var figures in result.Figures)
        {
            _ = hash.Add(figures.SpecificImpulse).Add(figures.CharacteristicVelocity).Add(figures.ThrustCoefficient);
        }

        File.WriteAllLines(output, [engine.Accelerator.ThreadsOrMultiprocessors.ToString(CultureInfo.InvariantCulture), hash.ToHex()]);
    }

    /// <summary>
    /// At 4, 12, 16 and 64 processors, each its own process, the reported thread count is the documented layout (4, 12, 16
    /// and 64: <see cref="AcceleratorChoice.CpuDeviceFor"/>'s own formula, so a change to one without the other reddens this
    /// fact) and the batch's result hash is the same at every count (BOOT.md, "results do not depend on the thread count").
    /// 12 is the coordinator's own example of a multiprocessor-count layout (4, 1, 3): the one
    /// <see cref="AcceleratorChoice.CpuDeviceFor"/> used to under-report as 8 by keeping the multiprocessor count fixed at 1.
    /// </summary>
    [Fact]
    [Trait("Category", "EndToEnd")]
    public void TheCpuEngineReportsTheDocumentedLayoutAtEveryProcessorCountAndResultsDoNotMove()
    {
        var expectedThreads = new Dictionary<int, int> { [4] = 4, [12] = 12, [16] = 16, [64] = 64 };
        string? hash = null;
        foreach (var (processorCount, threads) in expectedThreads)
        {
            var (reportedThreads, reportedHash) = RunWorker(processorCount);
            Assert.Equal(threads, reportedThreads);
            hash ??= reportedHash;
            Assert.Equal(hash, reportedHash);
        }
    }

    /// <summary>
    /// <see cref="AcceleratorChoice.CpuDeviceFor"/>'s thread total, in this process, at every processor count of the
    /// coordinator's list (BOOT.md, "All cores"): exact whenever the count is a multiple of 4 (or 2 or 3, below one full
    /// group of 4), and otherwise the largest multiple of 4 not above the count — 1 (the ILGPU floor of 2 threads exceeds
    /// it) and 6 (not a multiple of 4) are the two counts of this list with no exact layout, and are asserted against that
    /// documented fallback instead of equality.
    /// </summary>
    [Fact]
    public void TheAllCoresLayoutMatchesEveryProcessorCountOrTheDocumentedFallback()
    {
        var counts = new[] { 1, 2, 3, 4, 6, 8, 12, 16, 20, 24, 32, 48, 64, 128 };

        // The two counts of the coordinator's list with no exact layout, and why: 1 falls below the ILGPU floor of 2
        // threads (a warp needs at least 2), so the layout exceeds it instead of falling back under it; 6 is not a
        // multiple of 4, so the layout is the largest multiple of 4 not above it.
        var reasons = new Dictionary<int, string>
        {
            [1] = "below the ILGPU floor of 2 threads per warp, so the layout exceeds the count instead of falling under it",
            [6] = "not a multiple of 4, so the layout is the largest multiple of 4 not above the count",
        };
        var fallbackTotals = new Dictionary<int, int> { [1] = 2, [6] = 4 };

        foreach (var count in counts)
        {
            var total = AcceleratorChoice.CpuDeviceFor(count).NumThreads;
            if (fallbackTotals.TryGetValue(count, out var fallbackTotal))
            {
                Assert.True(total == fallbackTotal, $"{count} threads: expected the documented fallback {fallbackTotal} ({reasons[count]}), got {total}.");
            }
            else
            {
                Assert.Equal(count, total);
            }
        }
    }

    /// <summary>Spawns this same test assembly as a worker, under <c>DOTNET_PROCESSOR_COUNT</c>, and reads back what it reported.</summary>
    private static (int Threads, string Hash) RunWorker(int processorCount)
    {
        var output = Path.GetTempFileName();
        try
        {
            var start = new ProcessStartInfo("dotnet")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            start.ArgumentList.Add("test");
            start.ArgumentList.Add(typeof(AllCoresLayoutTests).Assembly.Location);
            start.ArgumentList.Add("--filter");
            start.ArgumentList.Add("FullyQualifiedName~" + nameof(AllCoresLayoutWorker));
            start.Environment["DOTNET_PROCESSOR_COUNT"] = processorCount.ToString(CultureInfo.InvariantCulture);
            start.Environment[WorkerOutputVariable] = output;
            using var process = Process.Start(start) ?? throw new InvalidOperationException("dotnet did not start");
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            process.WaitForExit();
            Assert.True(process.ExitCode == 0, $"the worker at {processorCount} processors exited {process.ExitCode}: {stdout.Result} {stderr.Result}");
            var lines = File.ReadAllLines(output);
            return (int.Parse(lines[0], CultureInfo.InvariantCulture), lines[1]);
        }
        finally
        {
            File.Delete(output);
        }
    }
}
