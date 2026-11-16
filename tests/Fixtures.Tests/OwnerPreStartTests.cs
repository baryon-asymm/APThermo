using System.Diagnostics;
using System.IO.Pipes;
using System.Reflection;
using APThermo.Harness;
using Xunit.Abstractions;

namespace APThermo.Fixtures.Tests;

/// <summary>Controls interruption before any START can be sent to an unconnected bootstrap.</summary>
[Trait("Category", "EndToEnd")]
public sealed class OwnerPreStartTests(ITestOutputHelper output) : IDisposable
{
    private readonly DotnetProcessGuardSession _session = new(output);

    /// <summary>The retained bootstrap is reaped when either a deadline or caller cancellation fires before connect.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InterruptedUnconnectedOwnerIsReaped(bool deadline)
    {
        var (arguments, environment) = _session.Input("controlled");
        var marker = Path.Combine(_session.DirectoryPath, "ready.pid");
        var targetMarker = Path.Combine(_session.DirectoryPath, "target-ran");
        var target = new ProcessStartInfo("python")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = _session.DirectoryPath,
        };
        target.ArgumentList.Add("-c");
        target.ArgumentList.Add("import sys;open(sys.argv[1],'w').close()");
        target.ArgumentList.Add(targetMarker);
        var type = typeof(OwnedProcess).Assembly.GetType("APThermo.Harness.OwnedProcessOperation", throwOnError: true)!;
        var constructor = type.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Single();
        var operation = constructor.Invoke([target, null, null, null, null, null]);
        Func<ProcessStartInfo, ProcessStartInfo> substitute = original => ProbeStart(arguments, environment, original.ArgumentList[2]);
        type.GetProperty("OwnerStartOverride", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(operation, substitute);
        var method = type.GetMethod("RunAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        using var interruption = new CancellationTokenSource();
        var pending = (Task<DotnetProcessResult>)method.Invoke(operation, [interruption.Token])!;
        await DotnetProcessGuardSession.WaitForOwnedReadyAsync(marker).ConfigureAwait(true);
        var identity = DotnetProcessObservation.Read(marker);
        using var owner = Process.GetProcessById(identity.ProcessId);
        try
        {
            Assert.False(owner.HasExited);
            if (deadline) { interruption.CancelAfter(TimeSpan.FromMilliseconds(50)); }
            else { await interruption.CancelAsync().ConfigureAwait(true); }
            var problem = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
                await pending.WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(true)).ConfigureAwait(true);
            Assert.Equal(interruption.Token, problem.CancellationToken);
            Assert.Equal(await File.ReadAllBytesAsync(Path.Combine(_session.DataPath, "stdout.bin")).ConfigureAwait(true), Assert.IsType<byte[]>(problem.Data["StandardOutput"]));
            Assert.Equal(await File.ReadAllBytesAsync(Path.Combine(_session.DataPath, "stderr.bin")).ConfigureAwait(true), Assert.IsType<byte[]>(problem.Data["StandardError"]));
            Assert.True(owner.HasExited, "the bootstrap survived interruption before START");
            Assert.False(File.Exists(targetMarker));
            Assert.Null(problem.Data["CleanupException"]);
        }
        finally
        {
            if (!owner.HasExited) { owner.Kill(entireProcessTree: true); }
            await owner.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(true);
        }
    }

    /// <summary>The real bootstrap suspended before managed startup is reaped on cancellation.</summary>
    [Fact]
    public async Task RealBootstrapStoppedBeforeConnectIsReaped()
    {
        _ = Directory.CreateDirectory(_session.DirectoryPath);
        var marker = Path.Combine(_session.DirectoryPath, "real-target-ran");
        var target = MarkerTarget(marker);
        var type = typeof(OwnedProcess).Assembly.GetType("APThermo.Harness.OwnedProcessOperation", throwOnError: true)!;
        var constructor = type.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Single();
        bool? connectedAtCleanup = null;
        Action<Stream> observe = stream => connectedAtCleanup = ((NamedPipeServerStream)stream).IsConnected;
        var operation = constructor.Invoke([target, null, null, observe, null, null]);
        Func<ProcessStartInfo, ProcessStartInfo> suspend = start =>
        {
            start.Environment["DOTNET_DefaultDiagnosticPortSuspend"] = "1";
            return start;
        };
        var launched = new TaskCompletionSource<Process>(TaskCreationOptions.RunContinuationsAsynchronously);
        Action<Process> capture = process => launched.SetResult(Process.GetProcessById(process.Id));
        type.GetProperty("OwnerStartOverride", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(operation, suspend);
        type.GetProperty("OwnerLaunched", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(operation, capture);
        var method = type.GetMethod("RunAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        using var interruption = new CancellationTokenSource();
        var pending = (Task<DotnetProcessResult>)method.Invoke(operation, [interruption.Token])!;
        using var owner = await launched.Task.WaitAsync(TimeSpan.FromSeconds(15)).ConfigureAwait(true);
        try
        {
            Assert.False(owner.HasExited);
            await interruption.CancelAsync().ConfigureAwait(true);
            var problem = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
                await pending.WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(true)).ConfigureAwait(true);
            Assert.Equal(interruption.Token, problem.CancellationToken);
            Assert.False(connectedAtCleanup, "the bootstrap connected before cancellation");
            Assert.True(owner.HasExited, "the real pre-connect bootstrap survived cancellation");
            Assert.False(File.Exists(marker));
            Assert.Null(problem.Data["CleanupException"]);
        }
        finally
        {
            if (!owner.HasExited) { owner.Kill(entireProcessTree: true); }
            await owner.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(true);
        }
    }

    /// <summary>The normal public runner reports a timeout when its actual bootstrap is suspended before connect.</summary>
    [Fact]
    public async Task PublicRunnerTimeoutStopsPreConnectBootstrap()
    {
        var (arguments, environment) = _session.Input("controlled");
        var variables = new Dictionary<string, string>(environment)
        {
            ["DOTNET_DefaultDiagnosticPortSuspend"] = "1",
        };
        var problem = await Assert.ThrowsAsync<TimeoutException>(() => Task.Run(() => DotnetProcess.Run(
            _session.AssemblyPath, arguments, _session.DirectoryPath, TimeSpan.FromMilliseconds(500), variables))).ConfigureAwait(true);
        _ = Assert.IsType<byte[]>(problem.Data["StandardOutput"]);
        _ = Assert.IsType<byte[]>(problem.Data["StandardError"]);
        Assert.Null(problem.Data["CleanupException"]);
        Assert.False(File.Exists(Path.Combine(_session.DirectoryPath, "ready.pid")));
    }

    /// <summary>A START that reached a real target and child uses scope cleanup if writing then fails.</summary>
    [Fact]
    public async Task AmbiguousStartWritePreservesScopeCleanup()
    {
        var (arguments, environment) = _session.Input("tree");
        var marker = Path.Combine(_session.DirectoryPath, "ready.pid");
        var target = ProbeStart(arguments, environment, null);
        var type = typeof(OwnedProcess).Assembly.GetType("APThermo.Harness.OwnedProcessOperation", throwOnError: true)!;
        var constructor = type.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Single();
        var operation = constructor.Invoke([target, null, null, null, null, null]);
        var gate = Path.Combine(_session.DirectoryPath, "eof-gate");
        var gateDeadline = DateTime.UtcNow.AddSeconds(15);
        Func<ProcessStartInfo, ProcessStartInfo> ownerStart = start =>
        {
            var index = start.ArgumentList.IndexOf("--");
            start.ArgumentList.Insert(index, "--test-control-eof");
            start.ArgumentList.Insert(index + 1, gate);
            start.ArgumentList.Insert(index + 2, "--test-deadline-utc-ticks");
            start.ArgumentList.Insert(index + 3, gateDeadline.Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture));
            return start;
        };
        type.GetProperty("OwnerStartOverride", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(operation, ownerStart);
        Func<Stream, CancellationToken, Task> write = async (stream, token) =>
        {
            byte[] frame = [(byte)'A', (byte)'P', (byte)'T', (byte)'O', 1, 2, 0, 0, 0, 0];
            await stream.WriteAsync(frame, token).ConfigureAwait(false);
            await stream.FlushAsync(token).ConfigureAwait(false);
            var header = new byte[10];
            await stream.ReadExactlyAsync(header, token).ConfigureAwait(false);
            Assert.Equal(8, header[5]);
            Assert.Equal(4U, System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(6)));
            var payload = new byte[4];
            await stream.ReadExactlyAsync(payload, token).ConfigureAwait(false);
            await DotnetProcessGuardSession.WaitForOwnedReadyAsync(marker).ConfigureAwait(false);
            await DotnetProcessGuardSession.WaitForOwnedReadyAsync(marker + ".parent").ConfigureAwait(false);
            throw new InvalidOperationException("controlled ambiguous START write");
        };
        type.GetProperty("StartWriteOverride", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(operation, write);
        var method = type.GetMethod("RunAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var pending = (Task<DotnetProcessResult>)method.Invoke(operation, [timeout.Token])!;
        try
        {
            var problem = await Assert.ThrowsAsync<InvalidOperationException>(async () => await pending.ConfigureAwait(true)).ConfigureAwait(true);
            Assert.Equal("controlled ambiguous START write", problem.Message);
            Assert.Null(problem.Data["CleanupException"]);
            Assert.False(DotnetProcessObservation.Read(marker).IsLive, "the target's child survived scope cleanup");
            Assert.False(DotnetProcessObservation.Read(marker + ".parent").IsLive, "the target survived scope cleanup");
            var stdout = await File.ReadAllBytesAsync(Path.Combine(_session.DataPath, "stdout.bin")).ConfigureAwait(true);
            var stderr = await File.ReadAllBytesAsync(Path.Combine(_session.DataPath, "stderr.bin")).ConfigureAwait(true);
            Assert.Equal(stdout.Concat(stdout), Assert.IsType<byte[]>(problem.Data["StandardOutput"]));
            Assert.Equal(stderr.Concat(stderr), Assert.IsType<byte[]>(problem.Data["StandardError"]));
        }
        finally { await File.WriteAllTextAsync(gate + ".release", "release").ConfigureAwait(true); }
    }

    /// <summary>Stops any retained finite probe and removes its private directory.</summary>
    public void Dispose() => _session.Dispose();

    private ProcessStartInfo MarkerTarget(string marker)
    {
        var target = new ProcessStartInfo("python")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = _session.DirectoryPath,
        };
        target.ArgumentList.Add("-c");
        target.ArgumentList.Add("import sys;open(sys.argv[1],'w').close()");
        target.ArgumentList.Add(marker);
        return target;
    }

    private ProcessStartInfo ProbeStart(IReadOnlyList<string> arguments, IReadOnlyDictionary<string, string> environment, string? channel)
    {
        var start = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = _session.DirectoryPath,
        };
        start.ArgumentList.Add(_session.AssemblyPath);
        foreach (var argument in arguments) { start.ArgumentList.Add(argument); }
        if (channel is not null)
        {
            start.ArgumentList.Add("--channel");
            start.ArgumentList.Add(channel);
        }
        foreach (var pair in environment) { start.Environment[pair.Key] = pair.Value; }
        return start;
    }
}
