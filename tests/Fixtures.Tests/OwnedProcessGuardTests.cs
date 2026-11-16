using System.Diagnostics;
using System.Globalization;
using APThermo.Harness;
using Xunit.Abstractions;

namespace APThermo.Fixtures.Tests;

/// <summary>Real owned-process scope, control and cleanup probes.</summary>
/// <param name="output">Owned-process diagnostics captured before teardown.</param>
[Trait("Category", "EndToEnd")]
public sealed class OwnedProcessGuardTests(ITestOutputHelper output) : IDisposable
{
    private readonly DotnetProcessGuardSession _session = new(output);

    /// <summary>Releases the private test session.</summary>
    public void Dispose() => _session.Dispose();

    /// <summary>Four simultaneous owned processes preserve distinct streams and do not inherit each other's control channels.</summary>
    [Fact]
    public async Task FourConcurrentOwnersKeepStreamsAndChannelsSeparate()
    {
        _ = Directory.CreateDirectory(_session.DirectoryPath);
        var release = Path.Combine(_session.DirectoryPath, "release");
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var tasks = Enumerable.Range(0, 4).Select(index =>
        {
            var start = new ProcessStartInfo("python")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = _session.DirectoryPath,
            };
            start.ArgumentList.Add("-c");
            start.ArgumentList.Add("import os,sys,time; open(sys.argv[1],'w').close(); " +
                "exec(\"while not os.path.exists(sys.argv[2]): time.sleep(0.01)\"); " +
                "sys.stdout.write(sys.argv[3]); sys.stderr.write(sys.argv[3])");
            start.ArgumentList.Add(Path.Combine(_session.DirectoryPath, "ready-" + index));
            start.ArgumentList.Add(release);
            start.ArgumentList.Add("owner-" + index);
            return OwnedProcess.RunAsync(start, deadline.Token);
        }).ToArray();
        while (Enumerable.Range(0, 4).Any(index => !File.Exists(Path.Combine(_session.DirectoryPath, "ready-" + index))))
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
        _ = Directory.CreateDirectory(_session.DirectoryPath);
        var marker = Path.Combine(_session.DirectoryPath, "closed-child.pid");
        var release = Path.Combine(_session.DirectoryPath, "closed-release");
        var start = new ProcessStartInfo("python")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = _session.DirectoryPath,
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
        _ = Directory.CreateDirectory(_session.DirectoryPath);
        var marker = Path.Combine(_session.DirectoryPath, "moved.pid");
        var start = new ProcessStartInfo("python")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = _session.DirectoryPath,
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
        var (arguments, environment) = _session.Input("tree");
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
        using var watchdog = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var type = typeof(OwnedProcess).Assembly.GetType("APThermo.Harness.OwnedProcessOperation", throwOnError: true)!;
        var constructor = type.GetConstructors(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic).Single();
        var closed = 0;
        void CloseControl(Stream stream) { stream.Dispose(); closed++; }
        var instance = constructor.Invoke([start, null, null, (Action<Stream>)CloseControl, null, null]);
        var method = type.GetMethod("RunAsync", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        var pending = (Task<DotnetProcessResult>)method.Invoke(instance, [cancellation.Token])!;
        while (!File.Exists(Path.Combine(_session.DirectoryPath, "ready.pid.parent")))
        {
            await Task.Delay(10, watchdog.Token).ConfigureAwait(true);
        }
        Assert.True(_session.RecordedProbeIsRunning("ready.pid"));
        var clock = Stopwatch.StartNew();
        await cancellation.CancelAsync().ConfigureAwait(true);
        var problem = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await pending.WaitAsync(watchdog.Token).ConfigureAwait(true)).ConfigureAwait(true);
        Assert.Equal(cancellation.Token, problem.CancellationToken);
        Assert.Equal(1, closed);
        var samples = await _session.ObserveOwnedProbesAsync(clock, TimeSpan.FromSeconds(5), "ready.pid", "ready.pid.parent").ConfigureAwait(true);
        Assert.False(samples[0].IsLive, samples[0].Diagnostics);
        Assert.False(samples[1].IsLive, samples[1].Diagnostics);
    }

    /// <summary>Cleanup drains a delayed actual binary read before capturing the interruption evidence.</summary>
    [Fact]
    public async Task InterruptionDrainsDelayedBinaryCapture()
    {
        _ = Directory.CreateDirectory(_session.DirectoryPath);
        var first = Path.Combine(_session.DirectoryPath, "first.ready");
        var second = Path.Combine(_session.DirectoryPath, "second.ready");
        var release = Path.Combine(_session.DirectoryPath, "release.read");
        var fixturePath = Path.Combine(_session.DataPath, "stdout.bin");
        var fixture = await File.ReadAllBytesAsync(fixturePath).ConfigureAwait(true);
        var start = new ProcessStartInfo("python")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = _session.DirectoryPath,
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
        _ = Directory.CreateDirectory(_session.DirectoryPath);
        var channel = "apt-test-" + Guid.NewGuid().ToString("N");
        var marker = Path.Combine(_session.DirectoryPath, "must-not-start");
        using var pipe = new System.IO.Pipes.NamedPipeServerStream(channel, System.IO.Pipes.PipeDirection.InOut, 1,
            System.IO.Pipes.PipeTransmissionMode.Byte, System.IO.Pipes.PipeOptions.Asynchronous | System.IO.Pipes.PipeOptions.CurrentUserOnly);
        var start = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = _session.DirectoryPath,
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

}
