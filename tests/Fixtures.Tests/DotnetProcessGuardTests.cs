using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using APThermo.Harness;

namespace APThermo.Fixtures.Tests;

/// <summary>Real finite probes for process deadlines, partial raw bytes and inherited pipe handles.</summary>
[Trait("Category", "EndToEnd")]
public sealed class DotnetProcessGuardTests : IDisposable
{
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
    public void DeadlineRefusesProcessOrInheritedPipeHang(string mode)
    {
        var (arguments, environment) = Input(mode);
        var watch = Stopwatch.StartNew();
        var problem = Assert.Throws<TimeoutException>(() => DotnetProcess.Run(
            _assembly, arguments, _directory, TimeSpan.FromSeconds(2), environment: environment));
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(10), $"deadline/cleanup took {watch.Elapsed}");
        Assert.NotEmpty(Assert.IsType<byte[]>(problem.Data["StandardOutput"]));
        Assert.NotEmpty(Assert.IsType<byte[]>(problem.Data["StandardError"]));
        Assert.True(problem.Message.Length < 5000);
        if (mode == "tree")
        {
            Assert.False(RecordedProbeIsRunning("ready.pid"), "the live parent's child survived tree cleanup");
            Assert.False(RecordedProbeIsRunning("ready.pid.parent"), "the live parent survived tree cleanup");
        }
        if (mode == "block")
        {
            Assert.False(RecordedProbeIsRunning("ready.pid"), "the timed-out parent survived cleanup");
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
        var pending = Task.Run(() => DotnetProcess.Run(_assembly, arguments, _directory,
            TimeSpan.FromSeconds(10), environment, cancellation.Token));
        var watch = Stopwatch.StartNew();
        while (!File.Exists(Path.Combine(_directory, "ready.pid")) && watch.Elapsed < TimeSpan.FromSeconds(5))
        {
            await Task.Delay(10).ConfigureAwait(true);
        }
        Assert.True(File.Exists(Path.Combine(_directory, "ready.pid")), "probe did not reach its ready marker");
        await cancellation.CancelAsync().ConfigureAwait(true);
        var problem = await Assert.ThrowsAsync<OperationCanceledException>(async () => await pending.ConfigureAwait(true)).ConfigureAwait(true);
        Assert.Equal(cancellation.Token, problem.CancellationToken);
        Assert.False(RecordedProbeIsRunning("ready.pid"), "the cancelled parent survived cleanup");
        Assert.Equal(await File.ReadAllBytesAsync(Path.Combine(_data, "stdout.bin")).ConfigureAwait(true), Assert.IsType<byte[]>(problem.Data["StandardOutput"]));
        Assert.Equal(await File.ReadAllBytesAsync(Path.Combine(_data, "stderr.bin")).ConfigureAwait(true), Assert.IsType<byte[]>(problem.Data["StandardError"]));
    }

    /// <summary>Both live processes are observed before cancellation and neither can execute afterwards.</summary>
    [Fact]
    public async Task CancellationStopsAnAlreadyWritingProcessTree()
    {
        var (arguments, environment) = Input("tree");
        using var cancellation = new CancellationTokenSource();
        var pending = Task.Run(() => DotnetProcess.Run(_assembly, arguments, _directory,
            TimeSpan.FromSeconds(10), environment, cancellation.Token));
        var watch = Stopwatch.StartNew();
        while (!RecordedProbeIsRunning("ready.pid.parent") && watch.Elapsed < TimeSpan.FromSeconds(5))
        {
            await Task.Delay(10).ConfigureAwait(true);
        }
        var ready = File.Exists(Path.Combine(_directory, "ready.pid.parent"));
        var childWasLive = RecordedProbeIsRunning("ready.pid");
        var parentWasLive = RecordedProbeIsRunning("ready.pid.parent");
        await cancellation.CancelAsync().ConfigureAwait(true);
        var problem = await Assert.ThrowsAsync<OperationCanceledException>(async () => await pending.ConfigureAwait(true)).ConfigureAwait(true);
        Assert.True(ready, "the tree probe did not reach its ready marker");
        Assert.True(childWasLive, "the observation failed to detect the owned live child");
        Assert.True(parentWasLive, "the observation failed to detect the owned live parent");
        Assert.Equal(cancellation.Token, problem.CancellationToken);
        Assert.False(RecordedProbeIsRunning("ready.pid"), "the cancelled child survived cleanup");
        Assert.False(RecordedProbeIsRunning("ready.pid.parent"), "the cancelled parent survived cleanup");
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

    private (IReadOnlyList<string> Arguments, IReadOnlyDictionary<string, string> Environment) Input(string mode)
    {
        _ = Directory.CreateDirectory(_directory);
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(_data, "case.json")));
        var value = document.RootElement.GetProperty("environmentValue").GetString()!;
        var exit = document.RootElement.GetProperty("exitCode").GetRawText();
        return ([mode, Path.Combine(_data, "stdout.bin"), Path.Combine(_data, "stderr.bin"), value, exit,
            Path.Combine(_directory, "ready.pid")], new Dictionary<string, string> { ["PROBE_VALUE"] = value });
    }

    private bool RecordedProbeIsRunning(string name)
    {
        var marker = Path.Combine(_directory, name);
        if (!File.Exists(marker) || !int.TryParse(File.ReadAllText(marker), NumberStyles.None, CultureInfo.InvariantCulture, out var pid))
        {
            return false;
        }
        if (OperatingSystem.IsLinux())
        {
            return LinuxProbeIsRunning(pid);
        }
        try
        {
            using var process = Process.GetProcessById(pid);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static bool LinuxProbeIsRunning(int pid)
    {
        try
        {
            var stat = File.ReadAllText("/proc/" + pid.ToString(CultureInfo.InvariantCulture) + "/stat");
            var stateIndex = stat.LastIndexOf(')') + 2;
            if (stateIndex < 2 || stateIndex >= stat.Length)
            {
                throw new InvalidDataException("the owned probe's Linux process stat is malformed");
            }
            // An unreaped zombie has exited; a fresh Process.HasExited can still report false.
            // Stopped, sleeping and uninterruptible processes remain live and must fail cleanup.
            return stat[stateIndex] is not ('Z' or 'X' or 'x');
        }
        catch (IOException problem) when (problem is FileNotFoundException or DirectoryNotFoundException)
        {
            return false;
        }
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
        if (!File.Exists(marker) || !int.TryParse(File.ReadAllText(marker), NumberStyles.None, CultureInfo.InvariantCulture, out var pid))
        {
            return;
        }
        try
        {
            using var process = Process.GetProcessById(pid);
            process.Kill(entireProcessTree: true);
            _ = process.WaitForExit(5000);
        }
        catch (Exception problem) when (problem is ArgumentException or InvalidOperationException or Win32Exception)
        {
            // The finite probe may already have ended before cleanup.
        }
    }
}
