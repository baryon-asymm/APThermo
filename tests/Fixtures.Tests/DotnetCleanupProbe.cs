using System.Globalization;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using APThermo.Harness;

namespace APThermo.Fixtures.Tests;

/// <summary>One configured target used through the actual owned process runner.</summary>
internal sealed class DotnetCleanupProbe : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "apthermo cleanup " + Guid.NewGuid().ToString("N"));
    private readonly string _marker;
    private readonly string _assembly;
    private readonly string[] _arguments;
    private readonly Dictionary<string, string> _environment;
    private Process? _process;
    internal byte[] ExpectedOutput { get; }
    internal byte[] ExpectedError { get; }
    internal int ExpectedExit { get; }

    internal DotnetCleanupProbe(string mode)
    {
        _ = Directory.CreateDirectory(_directory);
        _marker = Path.Combine(_directory, "ready.pid");
        var data = RepositoryPaths.Resolve("tests", "Fixtures.Tests", "ProcessProbe", "data");
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(data, "case.json")));
        var value = document.RootElement.GetProperty("environmentValue").GetString()!;
        ExpectedExit = document.RootElement.GetProperty("exitCode").GetInt32();
        ExpectedOutput = File.ReadAllBytes(Path.Combine(data, "stdout.bin"));
        ExpectedError = File.ReadAllBytes(Path.Combine(data, "stderr.bin"));
        _assembly = DotnetProcess.BuiltAssemblyPath("APThermo.Fixtures.Tests.ProcessProbe", "tests", "Fixtures.Tests", "ProcessProbe");
        _arguments = [mode, Path.Combine(data, "stdout.bin"), Path.Combine(data, "stderr.bin"), value,
            ExpectedExit.ToString(CultureInfo.InvariantCulture), _marker];
        _environment = new Dictionary<string, string> { ["PROBE_VALUE"] = value };
    }

    internal async Task ReadyAsync()
    {
        using var watchdog = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        while (!File.Exists(_marker)) { await Task.Delay(10, watchdog.Token).ConfigureAwait(true); }
        _process = Process.GetProcessById(DotnetProcessObservation.Read(_marker).ProcessId);
    }

    internal Task<DotnetProcessResult> RunAsync(TimeProvider clock, Action? cleanupProbe, CancellationToken caller)
    {
        var method = typeof(DotnetProcess).GetMethod("RunControlledAsync", BindingFlags.NonPublic | BindingFlags.Static)!;
        return (Task<DotnetProcessResult>)method.Invoke(null, [_assembly, _arguments, Path.GetTempPath(), TimeSpan.FromSeconds(2),
            _environment, clock, cleanupProbe, caller])!;
    }

    internal async Task WaitForExitAsync()
    {
        if (_process is not null) { await _process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(true); }
    }

    public void Dispose()
    {
        _process?.Dispose();
        Directory.Delete(_directory, recursive: true);
    }
}
