using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using APThermo.Harness;
using Xunit.Abstractions;

namespace APThermo.Fixtures.Tests;

/// <summary>Real finite probes for process deadlines, partial raw bytes and inherited pipe handles.</summary>
/// <param name="output">Owned-process diagnostics captured before teardown.</param>
[Trait("Category", "EndToEnd")]
public sealed class DotnetProcessGuardTests(ITestOutputHelper output) : IDisposable
{
    private readonly ITestOutputHelper _output = output;
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "apthermo process " + Guid.NewGuid().ToString("N"));
    private readonly string _assembly = DotnetProcess.BuiltAssemblyPath("APThermo.Fixtures.Tests.ProcessProbe", "tests", "Fixtures.Tests", "ProcessProbe");
    private readonly string _data = RepositoryPaths.Resolve("tests", "Fixtures.Tests", "ProcessProbe", "data");

    /// <summary>Stops only the fixture's recorded process and removes its private temporary files.</summary>
    public void Dispose()
    {
        StopRecordedProbe();
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    /// <summary>Both overloads preserve exact bytes, quoted/Unicode arguments, environment and nonzero exits.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CompletedProcessKeepsItsOriginalContract(bool explicitTimeout)
    {
        var (arguments, environment) = Input("echo");
        var result = explicitTimeout
            ? DotnetProcess.Run(_assembly, arguments, _directory, TimeSpan.FromSeconds(5), environment: environment)
            : DotnetProcess.Run(_assembly, arguments, _directory, environment);
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(_data, "case.json")));
        Assert.Equal(document.RootElement.GetProperty("exitCode").GetInt32(), result.ExitCode);
        Assert.Equal(File.ReadAllBytes(Path.Combine(_data, "stdout.bin")), result.Output.ToArray());
        Assert.Equal(File.ReadAllBytes(Path.Combine(_data, "stderr.bin")), result.Error.ToArray());
    }

    /// <summary>The deadline includes pipe drainage after the direct parent exits.</summary>
    [Theory]
    [InlineData("block")]
    [InlineData("spawn")]
    [InlineData("tree")]
    public async Task DeadlineRefusesProcessOrInheritedPipeHang(string mode)
    {
        var (arguments, environment) = Input(mode);
        var watch = Stopwatch.StartNew();
        var problem = Assert.Throws<TimeoutException>(() => DotnetProcess.Run(
            _assembly, arguments, _directory, TimeSpan.FromSeconds(2), environment: environment));
        var samples = mode == "tree"
            ? await ObserveOwnedProbesAsync(watch, TimeSpan.FromSeconds(2) + TimeSpan.FromSeconds(5), "ready.pid", "ready.pid.parent").ConfigureAwait(true)
            : mode == "block"
                ? await ObserveOwnedProbesAsync(watch, TimeSpan.FromSeconds(2) + TimeSpan.FromSeconds(5), "ready.pid").ConfigureAwait(true)
                : [];
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(10), $"deadline/cleanup/observation took {watch.Elapsed}");
        Assert.NotEmpty(Assert.IsType<byte[]>(problem.Data["StandardOutput"]));
        Assert.NotEmpty(Assert.IsType<byte[]>(problem.Data["StandardError"]));
        Assert.True(problem.Message.Length < 5000);
        if (mode == "tree")
        {
            Assert.False(samples[0].IsLive, "the live parent's child survived tree cleanup; " + samples[0].Diagnostics);
            Assert.False(samples[1].IsLive, "the live parent survived tree cleanup; " + samples[1].Diagnostics);
        }
        if (mode == "block")
        {
            Assert.False(samples[0].IsLive, "the timed-out parent survived cleanup; " + samples[0].Diagnostics);
        }
        if (mode is "spawn" or "tree")
        {
            Assert.True(File.Exists(Path.Combine(_directory, "ready.pid.parent")), "the direct parent did not exit before pipe timeout");
        }
    }

    /// <summary>Cancellation after a ready marker preserves the caller's token and all bytes written before it.</summary>
    [Fact]
    public async Task CancellationStopsAnAlreadyWritingProcess()
    {
        var (arguments, environment) = Input("block");
        using var cancellation = new CancellationTokenSource();
        var watch = Stopwatch.StartNew();
        var pending = Task.Run(() => DotnetProcess.Run(_assembly, arguments, _directory,
            TimeSpan.FromSeconds(10), environment, cancellation.Token));
        while (!File.Exists(Path.Combine(_directory, "ready.pid")) && watch.Elapsed < TimeSpan.FromSeconds(5))
        {
            await Task.Delay(10).ConfigureAwait(true);
        }
        Assert.True(File.Exists(Path.Combine(_directory, "ready.pid")), "probe did not reach its ready marker");
        var cleanupClock = Stopwatch.StartNew();
        await cancellation.CancelAsync().ConfigureAwait(true);
        var problem = await Assert.ThrowsAsync<OperationCanceledException>(async () => await pending.ConfigureAwait(true)).ConfigureAwait(true);
        var samples = await ObserveOwnedProbesAsync(cleanupClock, TimeSpan.FromSeconds(5), "ready.pid").ConfigureAwait(true);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(10), $"cancellation/observation took {watch.Elapsed}");
        Assert.Equal(cancellation.Token, problem.CancellationToken);
        Assert.False(samples[0].IsLive, "the cancelled parent survived cleanup; " + samples[0].Diagnostics);
        Assert.Equal(await File.ReadAllBytesAsync(Path.Combine(_data, "stdout.bin")).ConfigureAwait(true), Assert.IsType<byte[]>(problem.Data["StandardOutput"]));
        Assert.Equal(await File.ReadAllBytesAsync(Path.Combine(_data, "stderr.bin")).ConfigureAwait(true), Assert.IsType<byte[]>(problem.Data["StandardError"]));
    }

    /// <summary>Both live processes are observed before cancellation and neither can execute afterwards.</summary>
    [Fact]
    public async Task CancellationStopsAnAlreadyWritingProcessTree()
    {
        var (arguments, environment) = Input("tree");
        using var cancellation = new CancellationTokenSource();
        var watch = Stopwatch.StartNew();
        var pending = Task.Run(() => DotnetProcess.Run(_assembly, arguments, _directory,
            TimeSpan.FromSeconds(10), environment, cancellation.Token));
        while (!RecordedProbeIsRunning("ready.pid.parent") && watch.Elapsed < TimeSpan.FromSeconds(5))
        {
            await Task.Delay(10).ConfigureAwait(true);
        }
        var ready = File.Exists(Path.Combine(_directory, "ready.pid.parent"));
        var childWasLive = RecordedProbeIsRunning("ready.pid");
        var parentWasLive = RecordedProbeIsRunning("ready.pid.parent");
        var cleanupClock = Stopwatch.StartNew();
        await cancellation.CancelAsync().ConfigureAwait(true);
        var problem = await Assert.ThrowsAsync<OperationCanceledException>(async () => await pending.ConfigureAwait(true)).ConfigureAwait(true);
        var samples = await ObserveOwnedProbesAsync(cleanupClock, TimeSpan.FromSeconds(5), "ready.pid", "ready.pid.parent").ConfigureAwait(true);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(10), $"cancellation/observation took {watch.Elapsed}");
        Assert.True(ready, "the tree probe did not reach its ready marker");
        Assert.True(childWasLive, "the observation failed to detect the owned live child");
        Assert.True(parentWasLive, "the observation failed to detect the owned live parent");
        Assert.Equal(cancellation.Token, problem.CancellationToken);
        Assert.False(samples[0].IsLive, "the cancelled child survived cleanup; " + samples[0].Diagnostics);
        Assert.False(samples[1].IsLive, "the cancelled parent survived cleanup; " + samples[1].Diagnostics);
        Assert.NotEmpty(Assert.IsType<byte[]>(problem.Data["StandardOutput"]));
        Assert.NotEmpty(Assert.IsType<byte[]>(problem.Data["StandardError"]));
    }

    /// <summary>Readable messages stay bounded while exception data retains the complete raw prefix.</summary>
    [Fact]
    public void DiagnosticExcerptIsBoundedWithoutTruncatingRawEvidence()
    {
        var (arguments, environment) = Input("block");
        string[] expanded = [.. arguments];
        var fixture = File.ReadAllBytes(Path.Combine(_data, "stdout.bin"));
        byte[] raw = [.. Enumerable.Repeat(fixture, 32).SelectMany(bytes => bytes)];
        expanded[1] = Path.Combine(_directory, "large output.bin");
        File.WriteAllBytes(expanded[1], raw);
        var problem = Assert.Throws<TimeoutException>(() => DotnetProcess.Run(
            _assembly, expanded, _directory, TimeSpan.FromSeconds(2), environment: environment));
        Assert.Equal(raw, Assert.IsType<byte[]>(problem.Data["StandardOutput"]));
        Assert.True(problem.Message.Length < 5000);
        Assert.DoesNotContain("\0", problem.Message, StringComparison.Ordinal);
        Assert.Contains("\\u0000", problem.Message, StringComparison.Ordinal);
    }

    /// <summary>Pre-cancellation prevents a process from writing its launch marker.</summary>
    [Fact]
    public void CancellationBeforeTheCallPreventsLaunch()
    {
        var (arguments, environment) = Input("echo");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var problem = Assert.Throws<OperationCanceledException>(() => DotnetProcess.Run(
            _assembly, arguments, _directory, TimeSpan.FromSeconds(1), environment, cancellation.Token));
        Assert.Equal(cancellation.Token, problem.CancellationToken);
        Assert.Empty(Assert.IsType<byte[]>(problem.Data["StandardOutput"]));
        Assert.Empty(Assert.IsType<byte[]>(problem.Data["StandardError"]));
        Assert.False(File.Exists(Path.Combine(_directory, "ready.pid")));
    }

    /// <summary>Zero, negative and timer-unrepresentable deadlines fail before process launch.</summary>
    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    [InlineData(long.MaxValue)]
    public void InvalidDeadlineIsRefused(long ticks)
    {
        var (arguments, environment) = Input("echo");
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => DotnetProcess.Run(
            _assembly, arguments, _directory, TimeSpan.FromTicks(ticks), environment: environment));
        Assert.False(File.Exists(Path.Combine(_directory, "ready.pid")));
    }

    /// <summary>An explicit release follows the first live sample; held and spent-budget controls remain live.</summary>
    [Theory]
    [InlineData("release")]
    [InlineData("held")]
    [InlineData("spent")]
    public async Task ControlledObservationRequiresTerminalStateInsideItsOriginalBudget(string mode)
    {
        var (arguments, environment) = Input("controlled");
        using var cancellation = new CancellationTokenSource();
        var pending = Task.Run(() => DotnetProcess.Run(_assembly, arguments, _directory,
            TimeSpan.FromSeconds(10), environment, cancellation.Token));
        var marker = Path.Combine(_directory, "ready.pid");
        await WaitForOwnedReadyAsync(marker).ConfigureAwait(true);
        var clock = Stopwatch.StartNew();
        var budget = mode == "spent" ? TimeSpan.Zero : mode == "held" ? TimeSpan.FromMilliseconds(100) : TimeSpan.FromSeconds(5);
        var firstWasLive = false;
        var sample = await DotnetProcessObservation.WaitForTerminalAsync(marker, clock, budget, first =>
        {
            firstWasLive = first.IsLive;
            if (mode == "release")
            {
                File.WriteAllText(marker + ".release", "release after first live observation");
            }
        }).ConfigureAwait(true);
        var observedElapsed = clock.Elapsed;
        _output.WriteLine("Observation clock=" + observedElapsed.ToString("c", CultureInfo.InvariantCulture) + "; budget=" + budget.ToString("c", CultureInfo.InvariantCulture));
        _output.WriteLine(sample.Diagnostics);
        if (mode == "release")
        {
            _ = await pending.ConfigureAwait(true);
        }
        else
        {
            await cancellation.CancelAsync().ConfigureAwait(true);
            _ = await Assert.ThrowsAsync<OperationCanceledException>(async () => await pending.ConfigureAwait(true)).ConfigureAwait(true);
        }
        Assert.True(firstWasLive, "the controlled child was not live at its handshake");
        Assert.True(mode == "release" || observedElapsed >= budget, "the live control returned before its original observation budget expired");
        Assert.True(sample.IsLive == (mode != "release"), sample.Diagnostics);
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(10), sample.Diagnostics);
    }

    /// <summary>A completed owned process is terminal even at the first observation.</summary>
    [Fact]
    public void CompletedOwnedProbeIsTerminalAtFirstObservation()
    {
        var (arguments, environment) = Input("echo");
        _ = DotnetProcess.Run(_assembly, arguments, _directory, environment);
        var marker = Path.Combine(_directory, "ready.pid");
        Assert.True(File.Exists(marker), "the completed owned probe did not publish its ready marker");
        var sample = DotnetProcessObservation.Read(marker);
        Assert.True(sample.ProcessId > 0, "the completed owned probe identity was not read");
        Assert.False(sample.IsLive, sample.Diagnostics);
    }

    private static async Task WaitForOwnedReadyAsync(string marker)
    {
        var watch = Stopwatch.StartNew();
        while (!DotnetProcessObservation.Read(marker).IsLive && watch.Elapsed < TimeSpan.FromSeconds(5))
        {
            await Task.Delay(10).ConfigureAwait(true);
        }
        Assert.True(DotnetProcessObservation.Read(marker).IsLive, "the owned probe did not become ready");
    }

    private (IReadOnlyList<string> Arguments, IReadOnlyDictionary<string, string> Environment) Input(string mode)
    {
        _ = Directory.CreateDirectory(_directory);
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(_data, "case.json")));
        var value = document.RootElement.GetProperty("environmentValue").GetString()!;
        var exit = document.RootElement.GetProperty("exitCode").GetRawText();
        return ([mode, Path.Combine(_data, "stdout.bin"), Path.Combine(_data, "stderr.bin"), value, exit,
            Path.Combine(_directory, "ready.pid")], new Dictionary<string, string> { ["PROBE_VALUE"] = value, ["PROBE_PARENT_ID"] = Environment.ProcessId.ToString(CultureInfo.InvariantCulture) });
    }

    private bool RecordedProbeIsRunning(string name) => DotnetProcessObservation.Read(Path.Combine(_directory, name)).IsLive;

    private async Task<DotnetProcessSample[]> ObserveOwnedProbesAsync(Stopwatch clock, TimeSpan budget, params string[] names)
    {
        var samples = await Task.WhenAll(names.Select(name =>
            DotnetProcessObservation.WaitForTerminalAsync(Path.Combine(_directory, name), clock, budget))).ConfigureAwait(true);
        _output.WriteLine("Original clock=" + clock.Elapsed.ToString("c", CultureInfo.InvariantCulture) +
            "; total budget=" + budget.ToString("c", CultureInfo.InvariantCulture));
        foreach (var sample in samples)
        {
            _output.WriteLine(sample.Diagnostics);
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
        var marker = Path.Combine(_directory, name);
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
