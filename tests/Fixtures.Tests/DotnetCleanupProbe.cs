using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using APThermo.Harness;

namespace APThermo.Fixtures.Tests;

/// <summary>One owned finite process and its real capture readers for cleanup-failure controls.</summary>
internal sealed class DotnetCleanupProbe : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "apthermo cleanup " + Guid.NewGuid().ToString("N"));
    private readonly object _output;
    private readonly object _error;
    private readonly string _marker;

    internal Process Process { get; }
    internal byte[] ExpectedOutput { get; }
    internal byte[] ExpectedError { get; }
    internal int ExpectedExit { get; }

    internal DotnetCleanupProbe(string mode)
    {
        _ = Directory.CreateDirectory(_directory);
        _marker = Path.Combine(_directory, "ready.pid");
        var data = RepositoryPaths.Resolve("tests", "Fixtures.Tests", "ProcessProbe", "data");
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(data, "case.json")));
        var environment = document.RootElement.GetProperty("environmentValue").GetString()!;
        ExpectedExit = document.RootElement.GetProperty("exitCode").GetInt32();
        ExpectedOutput = File.ReadAllBytes(Path.Combine(data, "stdout.bin"));
        ExpectedError = File.ReadAllBytes(Path.Combine(data, "stderr.bin"));
        var start = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = _directory,
        };
        foreach (var argument in new[]
        {
            DotnetProcess.BuiltAssemblyPath("APThermo.Fixtures.Tests.ProcessProbe", "tests", "Fixtures.Tests", "ProcessProbe"),
            mode, Path.Combine(data, "stdout.bin"), Path.Combine(data, "stderr.bin"), environment,
            ExpectedExit.ToString(CultureInfo.InvariantCulture), _marker,
        })
        {
            start.ArgumentList.Add(argument);
        }
        start.Environment["PROBE_VALUE"] = environment;
        start.Environment["PROBE_PARENT_ID"] = Environment.ProcessId.ToString(CultureInfo.InvariantCulture);
        Process = Process.Start(start) ?? throw new InvalidOperationException("the owned cleanup probe did not start");
        var capture = typeof(DotnetProcess).Assembly.GetType("APThermo.Harness.ProcessOutput", throwOnError: true)!;
        _output = Activator.CreateInstance(capture, Process.StandardOutput.BaseStream)!;
        _error = Activator.CreateInstance(capture, Process.StandardError.BaseStream)!;
    }

    internal async Task ReadyAsync()
    {
        var clock = Stopwatch.StartNew();
        while (!File.Exists(_marker) && !Process.HasExited && clock.Elapsed < TimeSpan.FromSeconds(5))
        {
            await Task.Delay(10).ConfigureAwait(true);
        }
        Assert.True(File.Exists(_marker) && !Process.HasExited, "the owned finite probe did not become ready");
        Assert.Equal(Process.Id, DotnetProcessObservation.Read(_marker).ProcessId);
    }

    internal Task<DotnetProcessResult> CompleteAsync(Action<Process> kill, CancellationToken linked, CancellationToken caller)
    {
        var completion = typeof(DotnetProcess).GetMethod("CompleteAsync", BindingFlags.NonPublic | BindingFlags.Static)!;
        return (Task<DotnetProcessResult>)completion.Invoke(null, [Process, _output, _error, kill, linked, caller])!;
    }

    public void Dispose()
    {
        try
        {
            if (!Process.HasExited)
            {
                Process.Kill(entireProcessTree: true);
            }
            _ = Process.WaitForExit(5000);
        }
        catch (Exception problem) when (problem is InvalidOperationException or Win32Exception)
        {
            // Only this owned finite probe can have exited during teardown.
        }
        ((IDisposable)_output).Dispose();
        ((IDisposable)_error).Dispose();
        Process.Dispose();
        Directory.Delete(_directory, recursive: true);
    }
}
