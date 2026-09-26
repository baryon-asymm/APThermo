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
    /// At 4, 16 and 64 processors, each its own process, the reported thread count is the documented layout (4, 16 and 64:
    /// <see cref="AcceleratorChoice.CpuDeviceFor"/>'s own formula, so a change to one without the other reddens this fact) and
    /// the batch's result hash is the same at every count (BOOT.md, "results do not depend on the thread count").
    /// </summary>
    [Fact]
    public void TheCpuEngineReportsTheDocumentedLayoutAtEveryProcessorCountAndResultsDoNotMove()
    {
        var expectedThreads = new Dictionary<int, int> { [4] = 4, [16] = 16, [64] = 64 };
        string? hash = null;
        foreach (var (processorCount, threads) in expectedThreads)
        {
            var (reportedThreads, reportedHash) = RunWorker(processorCount);
            Assert.Equal(threads, reportedThreads);
            hash ??= reportedHash;
            Assert.Equal(hash, reportedHash);
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
