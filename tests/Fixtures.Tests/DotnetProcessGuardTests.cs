using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using APThermo.Harness;
using Xunit.Abstractions;

namespace APThermo.Fixtures.Tests;

/// <summary>Real finite probes for deadlines, cancellation and raw process evidence.</summary>
/// <param name="output">Owned-process diagnostics captured before teardown.</param>
[Trait("Category", "EndToEnd")]
public sealed class DotnetProcessGuardTests(ITestOutputHelper output) : IDisposable
{
    private readonly DotnetProcessGuardSession _session = new(output);

    /// <summary>Releases the private test session.</summary>
    public void Dispose() => _session.Dispose();

    /// <summary>Both overloads preserve exact bytes, quoted/Unicode arguments, environment and nonzero exits.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CompletedProcessKeepsItsOriginalContract(bool explicitTimeout)
    {
        var (arguments, environment) = _session.Input("echo");
        var result = explicitTimeout
            ? DotnetProcess.Run(_session.AssemblyPath, arguments, _session.DirectoryPath, TimeSpan.FromSeconds(5), environment: environment)
            : DotnetProcess.Run(_session.AssemblyPath, arguments, _session.DirectoryPath, environment);
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(_session.DataPath, "case.json")));
        Assert.Equal(document.RootElement.GetProperty("exitCode").GetInt32(), result.ExitCode);
        Assert.Equal(File.ReadAllBytes(Path.Combine(_session.DataPath, "stdout.bin")), result.Output.ToArray());
        Assert.Equal(File.ReadAllBytes(Path.Combine(_session.DataPath, "stderr.bin")), result.Error.ToArray());
    }

    /// <summary>The deadline includes pipe drainage after the direct parent exits.</summary>
    [Theory]
    [InlineData("block")]
    [InlineData("spawn")]
    [InlineData("tree")]
    public async Task DeadlineRefusesProcessOrInheritedPipeHang(string mode)
    {
        var (arguments, environment) = _session.Input(mode);
        using var deadline = new DotnetDeadlineClock();
        using var watchdog = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var pending = _session.RunWithClock(arguments, environment, deadline);
        var marker = mode is "tree" or "spawn" ? "ready.pid.parent" : "ready.pid";
        while (!File.Exists(Path.Combine(_session.DirectoryPath, marker))) { await Task.Delay(10, watchdog.Token).ConfigureAwait(true); }
        var watch = Stopwatch.StartNew();
        deadline.Advance(TimeSpan.FromSeconds(2));
        var problem = await Assert.ThrowsAsync<TimeoutException>(async () => await pending.WaitAsync(watchdog.Token).ConfigureAwait(true)).ConfigureAwait(true);
        var samples = mode is "tree" or "spawn"
            ? await _session.ObserveOwnedProbesAsync(watch, TimeSpan.FromSeconds(2) + TimeSpan.FromSeconds(5), "ready.pid", "ready.pid.parent").ConfigureAwait(true)
            : mode == "block"
                ? await _session.ObserveOwnedProbesAsync(watch, TimeSpan.FromSeconds(2) + TimeSpan.FromSeconds(5), "ready.pid").ConfigureAwait(true)
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
        if (mode == "spawn")
        {
            Assert.False(samples[0].IsLive, "the exited parent's child survived cleanup; " + samples[0].Diagnostics);
            Assert.False(samples[1].IsLive, "the direct parent survived cleanup; " + samples[1].Diagnostics);
        }
        if (mode is "spawn" or "tree")
        {
            Assert.True(File.Exists(Path.Combine(_session.DirectoryPath, "ready.pid.parent")), "the direct parent did not exit before pipe timeout");
        }
    }

    /// <summary>Cancellation after a ready marker preserves the caller's token and all bytes written before it.</summary>
    [Fact]
    public async Task CancellationStopsAnAlreadyWritingProcess()
    {
        var (arguments, environment) = _session.Input("block");
        using var cancellation = new CancellationTokenSource();
        var watch = Stopwatch.StartNew();
        var pending = Task.Run(() => DotnetProcess.Run(_session.AssemblyPath, arguments, _session.DirectoryPath,
            TimeSpan.FromSeconds(10), environment, cancellation.Token));
        while (!File.Exists(Path.Combine(_session.DirectoryPath, "ready.pid")) && watch.Elapsed < TimeSpan.FromSeconds(5))
        {
            await Task.Delay(10).ConfigureAwait(true);
        }
        Assert.True(File.Exists(Path.Combine(_session.DirectoryPath, "ready.pid")), "probe did not reach its ready marker");
        var cleanupClock = Stopwatch.StartNew();
        await cancellation.CancelAsync().ConfigureAwait(true);
        var problem = await Assert.ThrowsAsync<OperationCanceledException>(async () => await pending.ConfigureAwait(true)).ConfigureAwait(true);
        var samples = await _session.ObserveOwnedProbesAsync(cleanupClock, TimeSpan.FromSeconds(5), "ready.pid").ConfigureAwait(true);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(10), $"cancellation/observation took {watch.Elapsed}");
        Assert.Equal(cancellation.Token, problem.CancellationToken);
        Assert.False(samples[0].IsLive, "the cancelled parent survived cleanup; " + samples[0].Diagnostics);
        Assert.Equal(await File.ReadAllBytesAsync(Path.Combine(_session.DataPath, "stdout.bin")).ConfigureAwait(true), Assert.IsType<byte[]>(problem.Data["StandardOutput"]));
        Assert.Equal(await File.ReadAllBytesAsync(Path.Combine(_session.DataPath, "stderr.bin")).ConfigureAwait(true), Assert.IsType<byte[]>(problem.Data["StandardError"]));
    }

    /// <summary>A real cancelled owned process retains its token and bytes when disposal also fails.</summary>
    [Fact]
    public async Task OwnedCancellationPreservesPrimaryWhenCleanupFails()
    {
        var (arguments, environment) = _session.Input("block");
        var start = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = _session.DirectoryPath,
        };
        start.ArgumentList.Add(_session.AssemblyPath);
        foreach (var argument in arguments) { start.ArgumentList.Add(argument); }
        foreach (var (key, value) in environment) { start.Environment[key] = value; }
        using var cancellation = new CancellationTokenSource();
        var type = typeof(OwnedProcess).Assembly.GetType("APThermo.Harness.OwnedProcessOperation", throwOnError: true)!;
        var constructor = type.GetConstructors(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic).Single();
        Action failCleanup = () => throw new UnauthorizedAccessException("injected cleanup failure");
        var instance = constructor.Invoke([start, null, failCleanup, null, null, null]);
        var method = type.GetMethod("RunAsync", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        var pending = (Task<DotnetProcessResult>)method.Invoke(instance, [cancellation.Token])!;
        var watch = Stopwatch.StartNew();
        while (!File.Exists(Path.Combine(_session.DirectoryPath, "ready.pid")) && watch.Elapsed < TimeSpan.FromSeconds(5))
        {
            await Task.Delay(10).ConfigureAwait(true);
        }
        Assert.True(File.Exists(Path.Combine(_session.DirectoryPath, "ready.pid")));
        await cancellation.CancelAsync().ConfigureAwait(true);
        var problem = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await pending.ConfigureAwait(true)).ConfigureAwait(true);
        Assert.Equal(cancellation.Token, problem.CancellationToken);
        Assert.Equal(await File.ReadAllBytesAsync(Path.Combine(_session.DataPath, "stdout.bin")).ConfigureAwait(true), Assert.IsType<byte[]>(problem.Data["StandardOutput"]));
        Assert.Equal(await File.ReadAllBytesAsync(Path.Combine(_session.DataPath, "stderr.bin")).ConfigureAwait(true), Assert.IsType<byte[]>(problem.Data["StandardError"]));
        var cleanup = Assert.IsType<AggregateException>(problem.Data["CleanupException"]);
        Assert.Contains(cleanup.Flatten().InnerExceptions, exception => exception is UnauthorizedAccessException);
        var samples = await _session.ObserveOwnedProbesAsync(watch, TimeSpan.FromSeconds(10), "ready.pid").ConfigureAwait(true);
        Assert.False(samples[0].IsLive, samples[0].Diagnostics);
    }

    /// <summary>A disposal failure after a successful target is reported rather than treated as success.</summary>
    [Fact]
    public async Task OwnedCleanupFailureCannotBecomeSuccess()
    {
        var (arguments, environment) = _session.Input("echo");
        var start = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = _session.DirectoryPath,
        };
        start.ArgumentList.Add(_session.AssemblyPath);
        foreach (var argument in arguments) { start.ArgumentList.Add(argument); }
        foreach (var (key, value) in environment) { start.Environment[key] = value; }
        var type = typeof(OwnedProcess).Assembly.GetType("APThermo.Harness.OwnedProcessOperation", throwOnError: true)!;
        var constructor = type.GetConstructors(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic).Single();
        Action failCleanup = () => throw new UnauthorizedAccessException("injected cleanup failure");
        var instance = constructor.Invoke([start, null, failCleanup, null, null, null]);
        var method = type.GetMethod("RunAsync", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var pending = (Task<DotnetProcessResult>)method.Invoke(instance, [deadline.Token])!;
        var problem = await Assert.ThrowsAsync<AggregateException>(async () => await pending.ConfigureAwait(true)).ConfigureAwait(true);
        Assert.Contains(problem.Flatten().InnerExceptions, exception => exception is UnauthorizedAccessException);
    }

    /// <summary>Both live processes are observed before cancellation and neither can execute afterwards.</summary>
    [Fact]
    public async Task CancellationStopsAnAlreadyWritingProcessTree()
    {
        var (arguments, environment) = _session.Input("tree");
        using var cancellation = new CancellationTokenSource();
        var watch = Stopwatch.StartNew();
        var pending = Task.Run(() => DotnetProcess.Run(_session.AssemblyPath, arguments, _session.DirectoryPath,
            TimeSpan.FromSeconds(10), environment, cancellation.Token));
        while (!_session.RecordedProbeIsRunning("ready.pid.parent") && watch.Elapsed < TimeSpan.FromSeconds(5))
        {
            await Task.Delay(10).ConfigureAwait(true);
        }
        var ready = File.Exists(Path.Combine(_session.DirectoryPath, "ready.pid.parent"));
        var childWasLive = _session.RecordedProbeIsRunning("ready.pid");
        var parentWasLive = _session.RecordedProbeIsRunning("ready.pid.parent");
        var cleanupClock = Stopwatch.StartNew();
        await cancellation.CancelAsync().ConfigureAwait(true);
        var problem = await Assert.ThrowsAsync<OperationCanceledException>(async () => await pending.ConfigureAwait(true)).ConfigureAwait(true);
        var samples = await _session.ObserveOwnedProbesAsync(cleanupClock, TimeSpan.FromSeconds(5), "ready.pid", "ready.pid.parent").ConfigureAwait(true);
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
    public async Task DiagnosticExcerptIsBoundedWithoutTruncatingRawEvidence()
    {
        var (arguments, environment) = _session.Input("block");
        string[] expanded = [.. arguments];
        var fixture = await File.ReadAllBytesAsync(Path.Combine(_session.DataPath, "stdout.bin")).ConfigureAwait(true);
        byte[] raw = [.. Enumerable.Repeat(fixture, 32).SelectMany(bytes => bytes)];
        expanded[1] = Path.Combine(_session.DirectoryPath, "large output.bin");
        await File.WriteAllBytesAsync(expanded[1], raw).ConfigureAwait(true);
        using var deadline = new DotnetDeadlineClock();
        using var watchdog = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var pending = _session.RunWithClock(expanded, environment, deadline);
        while (!File.Exists(Path.Combine(_session.DirectoryPath, "ready.pid"))) { await Task.Delay(10, watchdog.Token).ConfigureAwait(true); }
        deadline.Advance(TimeSpan.FromSeconds(2));
        var problem = await Assert.ThrowsAsync<TimeoutException>(async () => await pending.WaitAsync(watchdog.Token).ConfigureAwait(true)).ConfigureAwait(true);
        Assert.Equal(raw, Assert.IsType<byte[]>(problem.Data["StandardOutput"]));
        Assert.True(problem.Message.Length < 5000);
        Assert.DoesNotContain("\0", problem.Message, StringComparison.Ordinal);
        Assert.Contains("\\u0000", problem.Message, StringComparison.Ordinal);
    }

    /// <summary>Pre-cancellation prevents a process from writing its launch marker.</summary>
    [Fact]
    public void CancellationBeforeTheCallPreventsLaunch()
    {
        var (arguments, environment) = _session.Input("echo");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var problem = Assert.Throws<OperationCanceledException>(() => DotnetProcess.Run(
            _session.AssemblyPath, arguments, _session.DirectoryPath, TimeSpan.FromSeconds(1), environment, cancellation.Token));
        Assert.Equal(cancellation.Token, problem.CancellationToken);
        Assert.Empty(Assert.IsType<byte[]>(problem.Data["StandardOutput"]));
        Assert.Empty(Assert.IsType<byte[]>(problem.Data["StandardError"]));
        Assert.False(File.Exists(Path.Combine(_session.DirectoryPath, "ready.pid")));
    }

    /// <summary>Zero, negative and timer-unrepresentable deadlines fail before process launch.</summary>
    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    [InlineData(long.MaxValue)]
    public void InvalidDeadlineIsRefused(long ticks)
    {
        var (arguments, environment) = _session.Input("echo");
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => DotnetProcess.Run(
            _session.AssemblyPath, arguments, _session.DirectoryPath, TimeSpan.FromTicks(ticks), environment: environment));
        Assert.False(File.Exists(Path.Combine(_session.DirectoryPath, "ready.pid")));
    }

    /// <summary>An explicit release follows the first live sample; held and spent-budget controls remain live.</summary>
    [Theory]
    [InlineData("release")]
    [InlineData("held")]
    [InlineData("spent")]
    public async Task ControlledObservationRequiresTerminalStateInsideItsOriginalBudget(string mode)
    {
        var (arguments, environment) = _session.Input("controlled");
        using var cancellation = new CancellationTokenSource();
        var pending = Task.Run(() => DotnetProcess.Run(_session.AssemblyPath, arguments, _session.DirectoryPath,
            TimeSpan.FromSeconds(10), environment, cancellation.Token));
        var marker = Path.Combine(_session.DirectoryPath, "ready.pid");
        await DotnetProcessGuardSession.WaitForOwnedReadyAsync(marker).ConfigureAwait(true);
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
        _session.Output.WriteLine("Observation clock=" + observedElapsed.ToString("c", CultureInfo.InvariantCulture) + "; budget=" + budget.ToString("c", CultureInfo.InvariantCulture));
        _session.Output.WriteLine(sample.Diagnostics);
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
        var (arguments, environment) = _session.Input("echo");
        _ = DotnetProcess.Run(_session.AssemblyPath, arguments, _session.DirectoryPath, environment);
        var marker = Path.Combine(_session.DirectoryPath, "ready.pid");
        Assert.True(File.Exists(marker), "the completed owned probe did not publish its ready marker");
        var sample = DotnetProcessObservation.Read(marker);
        Assert.True(sample.ProcessId > 0, "the completed owned probe identity was not read");
        Assert.False(sample.IsLive, sample.Diagnostics);
    }

}
