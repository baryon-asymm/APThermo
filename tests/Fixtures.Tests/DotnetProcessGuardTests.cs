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
        using var deadline = new DotnetDeadlineClock();
        using var watchdog = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var pending = RunWithClock(arguments, environment, deadline);
        var marker = mode is "tree" or "spawn" ? "ready.pid.parent" : "ready.pid";
        while (!File.Exists(Path.Combine(_directory, marker))) { await Task.Delay(10, watchdog.Token).ConfigureAwait(true); }
        var watch = Stopwatch.StartNew();
        deadline.Advance(TimeSpan.FromSeconds(2));
        var problem = await Assert.ThrowsAsync<TimeoutException>(async () => await pending.WaitAsync(watchdog.Token).ConfigureAwait(true)).ConfigureAwait(true);
        var samples = mode is "tree" or "spawn"
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
        if (mode == "spawn")
        {
            Assert.False(samples[0].IsLive, "the exited parent's child survived cleanup; " + samples[0].Diagnostics);
            Assert.False(samples[1].IsLive, "the direct parent survived cleanup; " + samples[1].Diagnostics);
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

    /// <summary>A real cancelled owned process retains its token and bytes when disposal also fails.</summary>
    [Fact]
    public async Task OwnedCancellationPreservesPrimaryWhenCleanupFails()
    {
        var (arguments, environment) = Input("block");
        var start = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = _directory,
        };
        start.ArgumentList.Add(_assembly);
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
        while (!File.Exists(Path.Combine(_directory, "ready.pid")) && watch.Elapsed < TimeSpan.FromSeconds(5))
        {
            await Task.Delay(10).ConfigureAwait(true);
        }
        Assert.True(File.Exists(Path.Combine(_directory, "ready.pid")));
        await cancellation.CancelAsync().ConfigureAwait(true);
        var problem = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await pending.ConfigureAwait(true)).ConfigureAwait(true);
        Assert.Equal(cancellation.Token, problem.CancellationToken);
        Assert.Equal(await File.ReadAllBytesAsync(Path.Combine(_data, "stdout.bin")).ConfigureAwait(true), Assert.IsType<byte[]>(problem.Data["StandardOutput"]));
        Assert.Equal(await File.ReadAllBytesAsync(Path.Combine(_data, "stderr.bin")).ConfigureAwait(true), Assert.IsType<byte[]>(problem.Data["StandardError"]));
        var cleanup = Assert.IsType<AggregateException>(problem.Data["CleanupException"]);
        Assert.Contains(cleanup.Flatten().InnerExceptions, exception => exception is UnauthorizedAccessException);
        var samples = await ObserveOwnedProbesAsync(watch, TimeSpan.FromSeconds(10), "ready.pid").ConfigureAwait(true);
        Assert.False(samples[0].IsLive, samples[0].Diagnostics);
    }

    /// <summary>A disposal failure after a successful target is reported rather than treated as success.</summary>
    [Fact]
    public async Task OwnedCleanupFailureCannotBecomeSuccess()
    {
        var (arguments, environment) = Input("echo");
        var start = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = _directory,
        };
        start.ArgumentList.Add(_assembly);
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

    /// <summary>Four simultaneous owned processes preserve distinct streams and do not inherit each other's control channels.</summary>
    [Fact]
    public async Task FourConcurrentOwnersKeepStreamsAndChannelsSeparate()
    {
        _ = Directory.CreateDirectory(_directory);
        var release = Path.Combine(_directory, "release");
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var tasks = Enumerable.Range(0, 4).Select(index =>
        {
            var start = new ProcessStartInfo("python")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = _directory,
            };
            start.ArgumentList.Add("-c");
            start.ArgumentList.Add("import os,sys,time; open(sys.argv[1],'w').close(); " +
                "exec(\"while not os.path.exists(sys.argv[2]): time.sleep(0.01)\"); " +
                "sys.stdout.write(sys.argv[3]); sys.stderr.write(sys.argv[3])");
            start.ArgumentList.Add(Path.Combine(_directory, "ready-" + index));
            start.ArgumentList.Add(release);
            start.ArgumentList.Add("owner-" + index);
            return OwnedProcess.RunAsync(start, deadline.Token);
        }).ToArray();
        while (Enumerable.Range(0, 4).Any(index => !File.Exists(Path.Combine(_directory, "ready-" + index))))
        {
            await Task.Delay(10, deadline.Token).ConfigureAwait(true);
        }
        Assert.All(tasks, task => Assert.False(task.IsCompleted));
        await File.WriteAllTextAsync(release, string.Empty, deadline.Token).ConfigureAwait(true);
        var results = await Task.WhenAll(tasks).WaitAsync(deadline.Token).ConfigureAwait(true);
        for (var index = 0; index < results.Length; index++)
        {
            var expected = System.Text.Encoding.UTF8.GetBytes("owner-" + index);
            Assert.Equal(0, results[index].ExitCode);
            Assert.Equal(expected, results[index].Output.ToArray());
            Assert.Equal(expected, results[index].Error.ToArray());
        }
    }

    /// <summary>Release kills a live descendant that has closed both captured output streams.</summary>
    [Fact]
    public async Task ClosedStreamDescendantIsTerminatedOnRelease()
    {
        _ = Directory.CreateDirectory(_directory);
        var marker = Path.Combine(_directory, "closed-child.pid");
        var release = Path.Combine(_directory, "closed-release");
        var start = new ProcessStartInfo("python")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = _directory,
        };
        start.ArgumentList.Add("-c");
        start.ArgumentList.Add("import os,subprocess,sys,time; p=subprocess.Popen([sys.executable,'-c','import time;time.sleep(30)']," +
            "stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL);f=open(sys.argv[1]+'.tmp','w');f.write(str(p.pid));f.close();" +
            "os.replace(sys.argv[1]+'.tmp',sys.argv[1]);exec(\"while not os.path.exists(sys.argv[2]): time.sleep(0.01)\");" +
            "sys.stdout.buffer.write(bytes([0,65,255]));sys.stderr.buffer.write(bytes([1,66,254]))");
        start.ArgumentList.Add(marker);
        start.ArgumentList.Add(release);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var pending = OwnedProcess.RunAsync(start, deadline.Token);
        while (!File.Exists(marker)) { await Task.Delay(10, deadline.Token).ConfigureAwait(true); }
        var pid = int.Parse(await File.ReadAllTextAsync(marker, deadline.Token).ConfigureAwait(true), CultureInfo.InvariantCulture);
        using var child = Process.GetProcessById(pid);
        try
        {
            Assert.False(child.HasExited);
            await File.WriteAllTextAsync(release, string.Empty, deadline.Token).ConfigureAwait(true);
            var result = await pending.WaitAsync(deadline.Token).ConfigureAwait(true);
            await child.WaitForExitAsync(deadline.Token).ConfigureAwait(true);
            Assert.True(child.HasExited);
            Assert.Equal(0, result.ExitCode);
            Assert.Equal(new byte[] { 0, 65, 255 }, result.Output.ToArray());
            Assert.Equal(new byte[] { 1, 66, 254 }, result.Error.ToArray());
        }
        finally
        {
            if (!child.HasExited) { child.Kill(); }
            await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(true);
        }
    }

    /// <summary>A descendant that changes group within the session prevents false cleanup success.</summary>
    [Fact]
    public async Task LinuxMovedGroupCannotBeReportedAsClean()
    {
        if (!OperatingSystem.IsLinux()) { Assert.True(OperatingSystem.IsWindows()); return; }
        _ = Directory.CreateDirectory(_directory);
        var marker = Path.Combine(_directory, "moved.pid");
        var start = new ProcessStartInfo("python")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = _directory,
        };
        start.ArgumentList.Add("-c");
        start.ArgumentList.Add("import os,subprocess,sys; p=subprocess.Popen([sys.executable,'-c','import time;time.sleep(30)']," +
            "preexec_fn=os.setpgrp,stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL);f=open(sys.argv[1]+'.tmp','w');f.write(str(p.pid));f.close();os.replace(sys.argv[1]+'.tmp',sys.argv[1])");
        start.ArgumentList.Add(marker);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var pending = OwnedProcess.RunAsync(start, deadline.Token);
        while (!File.Exists(marker)) { await Task.Delay(10, deadline.Token).ConfigureAwait(true); }
        var pid = int.Parse(await File.ReadAllTextAsync(marker, deadline.Token).ConfigureAwait(true), CultureInfo.InvariantCulture);
        using var child = Process.GetProcessById(pid);
        try
        {
            Assert.False(child.HasExited);
            deadline.CancelAfter(TimeSpan.FromSeconds(2));
            var problem = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await pending.ConfigureAwait(true)).ConfigureAwait(true);
            Assert.False(child.HasExited, "the escaped group member should remain observable for this control");
            Assert.NotNull(problem.Data["CleanupException"]);
        }
        finally
        {
            if (!child.HasExited) { child.Kill(); }
            await child.WaitForExitAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(true);
        }
    }

    /// <summary>Control EOF terminates a real live descendant and preserves the caller cancellation.</summary>
    [Fact]
    public async Task ControlEofTerminatesLiveDescendant()
    {
        var (arguments, environment) = Input("tree");
        var start = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = _directory,
        };
        start.ArgumentList.Add(_assembly);
        foreach (var argument in arguments) { start.ArgumentList.Add(argument); }
        foreach (var (key, value) in environment) { start.Environment[key] = value; }
        using var cancellation = new CancellationTokenSource();
        using var watchdog = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var type = typeof(OwnedProcess).Assembly.GetType("APThermo.Harness.OwnedProcessOperation", throwOnError: true)!;
        var constructor = type.GetConstructors(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic).Single();
        var closed = 0;
        void CloseControl(Stream stream) { stream.Dispose(); closed++; }
        var instance = constructor.Invoke([start, null, null, (Action<Stream>)CloseControl, null, null]);
        var method = type.GetMethod("RunAsync", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        var pending = (Task<DotnetProcessResult>)method.Invoke(instance, [cancellation.Token])!;
        while (!File.Exists(Path.Combine(_directory, "ready.pid.parent")))
        {
            await Task.Delay(10, watchdog.Token).ConfigureAwait(true);
        }
        Assert.True(RecordedProbeIsRunning("ready.pid"));
        var clock = Stopwatch.StartNew();
        await cancellation.CancelAsync().ConfigureAwait(true);
        var problem = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await pending.WaitAsync(watchdog.Token).ConfigureAwait(true)).ConfigureAwait(true);
        Assert.Equal(cancellation.Token, problem.CancellationToken);
        Assert.Equal(1, closed);
        var samples = await ObserveOwnedProbesAsync(clock, TimeSpan.FromSeconds(5), "ready.pid", "ready.pid.parent").ConfigureAwait(true);
        Assert.False(samples[0].IsLive, samples[0].Diagnostics);
        Assert.False(samples[1].IsLive, samples[1].Diagnostics);
    }

    /// <summary>Cleanup drains a delayed actual binary read before capturing the interruption evidence.</summary>
    [Fact]
    public async Task InterruptionDrainsDelayedBinaryCapture()
    {
        _ = Directory.CreateDirectory(_directory);
        var first = Path.Combine(_directory, "first.ready");
        var second = Path.Combine(_directory, "second.ready");
        var release = Path.Combine(_directory, "release.read");
        var fixturePath = Path.Combine(_data, "stdout.bin");
        var fixture = await File.ReadAllBytesAsync(fixturePath).ConfigureAwait(true);
        var start = new ProcessStartInfo("python")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = _directory,
        };
        start.ArgumentList.Add("-c");
        start.ArgumentList.Add("import os,sys,time;d=open(sys.argv[1],'rb').read();" +
            "sys.stdout.buffer.write(d*1000);sys.stdout.buffer.flush();" +
            "sys.stderr.buffer.write(d);sys.stderr.buffer.flush();open(sys.argv[2],'w').close();" +
            "exec(\"while not os.path.exists(sys.argv[3]): time.sleep(0.01)\");" +
            "sys.stdout.buffer.write(d);sys.stdout.buffer.flush();open(sys.argv[4],'w').close();time.sleep(30)");
        foreach (var argument in new[] { fixturePath, first, release, second }) { start.ArgumentList.Add(argument); }
        using var cancellation = new CancellationTokenSource();
        using var watchdog = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var captureStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var drainStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var armed = 0;
        Task Capture()
        {
            if (Volatile.Read(ref armed) == 0) { return Task.CompletedTask; }
            _ = captureStarted.TrySetResult();
            return gate.Task;
        }
        void DrainWaiting() => _ = drainStarted.TrySetResult();
        var type = typeof(OwnedProcess).Assembly.GetType("APThermo.Harness.OwnedProcessOperation", throwOnError: true)!;
        var constructor = type.GetConstructors(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic).Single();
        var instance = constructor.Invoke([start, null, null, null, (Func<Task>)Capture, (Action)DrainWaiting]);
        var method = type.GetMethod("RunAsync", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        var pending = (Task<DotnetProcessResult>)method.Invoke(instance, [cancellation.Token])!;
        try
        {
            while (!File.Exists(first)) { await Task.Delay(10, watchdog.Token).ConfigureAwait(true); }
            Volatile.Write(ref armed, 1);
            await File.WriteAllTextAsync(release, string.Empty, watchdog.Token).ConfigureAwait(true);
            while (!File.Exists(second)) { await Task.Delay(10, watchdog.Token).ConfigureAwait(true); }
            await captureStarted.Task.WaitAsync(watchdog.Token).ConfigureAwait(true);
            await cancellation.CancelAsync().ConfigureAwait(true);
            await drainStarted.Task.WaitAsync(watchdog.Token).ConfigureAwait(true);
            Assert.False(pending.IsCompleted, "interrupted call returned before its pending read was drained");
            _ = gate.TrySetResult();
            var problem = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await pending.WaitAsync(watchdog.Token).ConfigureAwait(true)).ConfigureAwait(true);
            Assert.Equal(cancellation.Token, problem.CancellationToken);
            byte[] expected = [.. Enumerable.Repeat(fixture, 1001).SelectMany(bytes => bytes)];
            Assert.Equal(expected, Assert.IsType<byte[]>(problem.Data["StandardOutput"]));
            Assert.Equal(fixture, Assert.IsType<byte[]>(problem.Data["StandardError"]));
        }
        finally { _ = gate.TrySetResult(); }
    }

    /// <summary>Job assignment failure prevents target execution; Linux ABORT prevents it after READY.</summary>
    [Fact]
    public async Task SetupFailureOrAbortPreventsTargetStart()
    {
        _ = Directory.CreateDirectory(_directory);
        var channel = "apt-test-" + Guid.NewGuid().ToString("N");
        var marker = Path.Combine(_directory, "must-not-start");
        using var pipe = new System.IO.Pipes.NamedPipeServerStream(channel, System.IO.Pipes.PipeDirection.InOut, 1,
            System.IO.Pipes.PipeTransmissionMode.Byte, System.IO.Pipes.PipeOptions.Asynchronous | System.IO.Pipes.PipeOptions.CurrentUserOnly);
        var start = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = _directory,
        };
        start.ArgumentList.Add(DotnetProcess.BuiltAssemblyPath("APThermo.Harness.ProcessOwner", "tests", "Harness", "ProcessOwner"));
        start.ArgumentList.Add("--channel");
        start.ArgumentList.Add(channel);
        if (OperatingSystem.IsWindows()) { start.ArgumentList.Add("--job-name"); start.ArgumentList.Add("missing-" + channel); }
        start.ArgumentList.Add("--");
        start.ArgumentList.Add("python");
        start.ArgumentList.Add("-c");
        start.ArgumentList.Add("import sys;open(sys.argv[1],'w').close()");
        start.ArgumentList.Add(marker);
        using var owner = Process.Start(start) ?? throw new InvalidOperationException("test bootstrap did not start");
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        try
        {
            await pipe.WaitForConnectionAsync(deadline.Token).ConfigureAwait(true);
            var header = new byte[10];
            await pipe.ReadExactlyAsync(header, deadline.Token).ConfigureAwait(true);
            Assert.Equal("APTO", System.Text.Encoding.ASCII.GetString(header, 0, 4));
            Assert.Equal(1, header[4]);
            var length = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(6));
            Assert.True(length <= 4096);
            var payload = new byte[length];
            await pipe.ReadExactlyAsync(payload, deadline.Token).ConfigureAwait(true);
            if (OperatingSystem.IsWindows())
            {
                Assert.Equal(6, header[5]);
                Assert.Equal(5, payload.Length);
                Assert.Equal(1, payload[0]);
            }
            else
            {
                Assert.Equal(1, header[5]);
                Assert.Empty(payload);
                byte[] abort = [(byte)'A', (byte)'P', (byte)'T', (byte)'O', 1, 3, 0, 0, 0, 0];
                await pipe.WriteAsync(abort, deadline.Token).ConfigureAwait(true);
                await pipe.FlushAsync(deadline.Token).ConfigureAwait(true);
            }
            await owner.WaitForExitAsync(deadline.Token).ConfigureAwait(true);
            Assert.False(File.Exists(marker));
        }
        finally
        {
            if (!owner.HasExited) { owner.Kill(entireProcessTree: true); }
            await owner.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(true);
        }
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
    public async Task DiagnosticExcerptIsBoundedWithoutTruncatingRawEvidence()
    {
        var (arguments, environment) = Input("block");
        string[] expanded = [.. arguments];
        var fixture = await File.ReadAllBytesAsync(Path.Combine(_data, "stdout.bin")).ConfigureAwait(true);
        byte[] raw = [.. Enumerable.Repeat(fixture, 32).SelectMany(bytes => bytes)];
        expanded[1] = Path.Combine(_directory, "large output.bin");
        await File.WriteAllBytesAsync(expanded[1], raw).ConfigureAwait(true);
        using var deadline = new DotnetDeadlineClock();
        using var watchdog = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var pending = RunWithClock(expanded, environment, deadline);
        while (!File.Exists(Path.Combine(_directory, "ready.pid"))) { await Task.Delay(10, watchdog.Token).ConfigureAwait(true); }
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

    private Task<DotnetProcessResult> RunWithClock(IReadOnlyList<string> arguments, IReadOnlyDictionary<string, string> environment,
        TimeProvider clock)
    {
        var method = typeof(DotnetProcess).GetMethod("RunControlledAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        return (Task<DotnetProcessResult>)method.Invoke(null,
            [_assembly, arguments, _directory, TimeSpan.FromSeconds(2), environment, clock, null, CancellationToken.None])!;
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
