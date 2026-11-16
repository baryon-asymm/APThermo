using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using APThermo.Harness;
using Xunit.Abstractions;

namespace APThermo.Fixtures.Tests;

/// <summary>Owns one process-probe directory, observations and deterministic teardown.</summary>
internal sealed class DotnetProcessGuardSession(ITestOutputHelper output) : IDisposable
{
    internal ITestOutputHelper Output { get; } = output;
    internal string DirectoryPath { get; } = Path.Combine(Path.GetTempPath(), "apthermo process " + Guid.NewGuid().ToString("N"));
    internal string AssemblyPath { get; } = DotnetProcess.BuiltAssemblyPath("APThermo.Fixtures.Tests.ProcessProbe", "tests", "Fixtures.Tests", "ProcessProbe");
    internal string DataPath { get; } = RepositoryPaths.Resolve("tests", "Fixtures.Tests", "ProcessProbe", "data");

    /// <summary>Stops only the fixture's recorded process and removes its private temporary files.</summary>
    public void Dispose()
    {
        StopRecordedProbe();
        if (Directory.Exists(DirectoryPath))
        {
            Directory.Delete(DirectoryPath, recursive: true);
        }
    }

    internal static async Task WaitForOwnedReadyAsync(string marker)
    {
        var watch = Stopwatch.StartNew();
        while (!DotnetProcessObservation.Read(marker).IsLive && watch.Elapsed < TimeSpan.FromSeconds(5))
        {
            await Task.Delay(10).ConfigureAwait(true);
        }
        Assert.True(DotnetProcessObservation.Read(marker).IsLive, "the owned probe did not become ready");
    }

    internal Task<DotnetProcessResult> RunWithClock(IReadOnlyList<string> arguments, IReadOnlyDictionary<string, string> environment,
        TimeProvider clock)
    {
        var start = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = DirectoryPath,
        };
        start.ArgumentList.Add(AssemblyPath);
        foreach (var argument in arguments) { start.ArgumentList.Add(argument); }
        foreach (var (key, value) in environment) { start.Environment[key] = value; }
        var method = typeof(DotnetProcess).GetMethod("RunControlledAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        return (Task<DotnetProcessResult>)method.Invoke(null,
            [start, TimeSpan.FromSeconds(2), clock, null, CancellationToken.None])!;
    }

    internal (IReadOnlyList<string> Arguments, IReadOnlyDictionary<string, string> Environment) Input(string mode)
    {
        _ = Directory.CreateDirectory(DirectoryPath);
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(DataPath, "case.json")));
        var value = document.RootElement.GetProperty("environmentValue").GetString()!;
        var exit = document.RootElement.GetProperty("exitCode").GetRawText();
        return ([mode, Path.Combine(DataPath, "stdout.bin"), Path.Combine(DataPath, "stderr.bin"), value, exit,
            Path.Combine(DirectoryPath, "ready.pid")], new Dictionary<string, string> { ["PROBE_VALUE"] = value, ["PROBE_PARENT_ID"] = Environment.ProcessId.ToString(CultureInfo.InvariantCulture) });
    }

    internal bool RecordedProbeIsRunning(string name) => DotnetProcessObservation.Read(Path.Combine(DirectoryPath, name)).IsLive;

    internal async Task<DotnetProcessSample[]> ObserveOwnedProbesAsync(Stopwatch clock, TimeSpan budget, params string[] names)
    {
        var samples = await Task.WhenAll(names.Select(name =>
            DotnetProcessObservation.WaitForTerminalAsync(Path.Combine(DirectoryPath, name), clock, budget))).ConfigureAwait(true);
        Output.WriteLine("Original clock=" + clock.Elapsed.ToString("c", CultureInfo.InvariantCulture) +
            "; total budget=" + budget.ToString("c", CultureInfo.InvariantCulture));
        foreach (var sample in samples)
        {
            Output.WriteLine(sample.Diagnostics);
        }
        return samples;
    }

    private void StopRecordedProbe()
    {
        foreach (var name in new[] { "ready.pid", "ready.pid.parent" })
        {
            StopRecordedProbe(name);
        }
    }

    private void StopRecordedProbe(string name)
    {
        var marker = Path.Combine(DirectoryPath, name);
        var sample = DotnetProcessObservation.Read(marker);
        if (!sample.IsLive)
        {
            return;
        }
        try
        {
            using var process = Process.GetProcessById(sample.ProcessId);
            var confirmed = DotnetProcessObservation.Read(marker);
            if (!confirmed.IsLive || confirmed.ProcessId != process.Id)
            {
                return;
            }
            process.Kill(entireProcessTree: true);
            _ = process.WaitForExit(5000);
        }
        catch (Exception problem) when (problem is ArgumentException or InvalidOperationException or Win32Exception)
        {
            // The finite probe may already have ended before cleanup.
        }
    }
}
