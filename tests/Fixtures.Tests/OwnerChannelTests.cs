using System.Buffers.Binary;
using System.Diagnostics;
using System.IO.Pipes;
using System.Reflection;
using APThermo.Harness;

namespace APThermo.Fixtures.Tests;

/// <summary>Real owner-channel framing and handle-inheritance controls.</summary>
[Trait("Category", "EndToEnd")]
public sealed class OwnerChannelTests
{
    /// <summary>EOF and a malformed frame before START cannot run the requested target.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InvalidControlBeforeStartCannotLaunch(bool malformed)
    {
        var channel = "apt-test-" + Guid.NewGuid().ToString("N");
        var marker = Path.Combine(Path.GetTempPath(), channel + ".marker");
        using var job = CreateJob(channel);
        using var pipe = CreatePipe(channel);
        using var owner = StartOwner(channel, marker);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        try
        {
            await pipe.WaitForConnectionAsync(deadline.Token).ConfigureAwait(true);
            var (kind, payload) = await ReadFrameAsync(pipe, deadline.Token).ConfigureAwait(true);
            Assert.Equal(1, kind);
            Assert.Empty(payload);
            if (malformed)
            {
                byte[] invalid = [(byte)'B', (byte)'A', (byte)'D', (byte)'!', 1, 2, 0, 0, 0, 0];
                await pipe.WriteAsync(invalid, deadline.Token).ConfigureAwait(true);
                await pipe.FlushAsync(deadline.Token).ConfigureAwait(true);
            }
            else { await pipe.DisposeAsync().ConfigureAwait(true); }
            await owner.WaitForExitAsync(deadline.Token).ConfigureAwait(true);
            Assert.NotEqual(0, owner.ExitCode);
            Assert.False(File.Exists(marker));
        }
        finally
        {
            if (!owner.HasExited) { owner.Kill(entireProcessTree: true); }
            await owner.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(true);
            if (File.Exists(marker)) { File.Delete(marker); }
        }
    }

    /// <summary>Actual Windows job-open or Linux session-creation failure happens before target launch.</summary>
    [Fact]
    public async Task NativeOwnershipFailurePreventsTarget()
    {
        var channel = "apt-test-" + Guid.NewGuid().ToString("N");
        var marker = Path.Combine(Path.GetTempPath(), channel + ".marker");
        using var pipe = CreatePipe(channel);
        var start = new ProcessStartInfo(OperatingSystem.IsLinux() ? "python" : "dotnet")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        if (OperatingSystem.IsLinux())
        {
            start.ArgumentList.Add("-c");
            start.ArgumentList.Add("import os,sys;os.setpgid(0,0);os.execvp('dotnet',sys.argv[1:])");
            start.ArgumentList.Add("dotnet");
        }
        start.ArgumentList.Add(DotnetProcess.BuiltAssemblyPath("APThermo.Harness.ProcessOwner", "tests", "Harness", "ProcessOwner"));
        start.ArgumentList.Add("--channel");
        start.ArgumentList.Add(channel);
        if (OperatingSystem.IsWindows()) { start.ArgumentList.Add("--job-name"); start.ArgumentList.Add("missing-" + channel); }
        start.ArgumentList.Add("--");
        start.ArgumentList.Add("python");
        start.ArgumentList.Add("-c");
        start.ArgumentList.Add("import sys;open(sys.argv[1],'w').close()");
        start.ArgumentList.Add(marker);
        using var owner = Process.Start(start) ?? throw new InvalidOperationException("owner did not start");
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        try
        {
            await pipe.WaitForConnectionAsync(deadline.Token).ConfigureAwait(true);
            var (kind, payload) = await ReadFrameAsync(pipe, deadline.Token).ConfigureAwait(true);
            Assert.Equal(6, kind);
            Assert.Equal(5, payload.Length);
            Assert.Equal(1, payload[0]);
            await owner.WaitForExitAsync(deadline.Token).ConfigureAwait(true);
            Assert.NotEqual(0, owner.ExitCode);
            Assert.False(File.Exists(marker));
        }
        finally
        {
            if (!owner.HasExited) { owner.Kill(entireProcessTree: true); }
            await owner.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(true);
            if (File.Exists(marker)) { File.Delete(marker); }
        }
    }

    /// <summary>A live target holds neither its owner's control endpoint nor its Windows job handle.</summary>
    [Fact]
    public async Task LiveTargetDoesNotInheritOwnerHandles()
    {
        var channel = "apt-test-" + Guid.NewGuid().ToString("N");
        var marker = Path.Combine(Path.GetTempPath(), channel + ".marker");
        using var job = CreateJob(channel);
        using var pipe = CreatePipe(channel);
        using var owner = StartOwner(channel, marker);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        Process? target = null;
        try
        {
            await pipe.WaitForConnectionAsync(deadline.Token).ConfigureAwait(true);
            var (ready, readyPayload) = await ReadFrameAsync(pipe, deadline.Token).ConfigureAwait(true);
            Assert.Equal(1, ready);
            Assert.Empty(readyPayload);
            byte[] start = [(byte)'A', (byte)'P', (byte)'T', (byte)'O', 1, 2, 0, 0, 0, 0];
            await pipe.WriteAsync(start, deadline.Token).ConfigureAwait(true);
            await pipe.FlushAsync(deadline.Token).ConfigureAwait(true);
            var (started, payload) = await ReadFrameAsync(pipe, deadline.Token).ConfigureAwait(true);
            Assert.Equal(8, started);
            Assert.Equal(4, payload.Length);
            target = Process.GetProcessById(BinaryPrimitives.ReadInt32LittleEndian(payload));
            while (!File.Exists(marker)) { await Task.Delay(10, deadline.Token).ConfigureAwait(true); }
            Assert.False(target.HasExited);
            owner.Kill();
            await owner.WaitForExitAsync(deadline.Token).ConfigureAwait(true);
            var probe = new byte[1];
            Assert.Equal(0, await pipe.ReadAsync(probe, deadline.Token).ConfigureAwait(true));
            Assert.False(target.HasExited);
            if (OperatingSystem.IsWindows())
            {
                job!.Dispose();
                await target.WaitForExitAsync(deadline.Token).ConfigureAwait(true);
                Assert.True(target.HasExited);
            }
        }
        finally
        {
            if (!owner.HasExited) { owner.Kill(entireProcessTree: true); }
            await owner.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(true);
            if (target is not null)
            {
                if (!target.HasExited) { target.Kill(entireProcessTree: true); }
                await target.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(true);
                target.Dispose();
            }
            if (File.Exists(marker)) { File.Delete(marker); }
        }
    }

    private static IDisposable? CreateJob(string name)
    {
        if (!OperatingSystem.IsWindows()) { return null; }
        var type = Assembly.Load("APThermo.Harness.ProcessOwner").GetType("APThermo.Harness.ProcessOwner.WindowsJob", throwOnError: true)!;
        var create = type.GetMethod("Create", BindingFlags.Static | BindingFlags.NonPublic)!;
        return (IDisposable)create.Invoke(null, [name])!;
    }

    private static NamedPipeServerStream CreatePipe(string channel) => new(channel, PipeDirection.InOut, 1,
        PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

    private static Process StartOwner(string channel, string marker)
    {
        var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add(DotnetProcess.BuiltAssemblyPath("APThermo.Harness.ProcessOwner", "tests", "Harness", "ProcessOwner"));
        start.ArgumentList.Add("--channel");
        start.ArgumentList.Add(channel);
        if (OperatingSystem.IsWindows()) { start.ArgumentList.Add("--job-name"); start.ArgumentList.Add(channel); }
        start.ArgumentList.Add("--");
        start.ArgumentList.Add("python");
        start.ArgumentList.Add("-c");
        start.ArgumentList.Add("import sys,time;open(sys.argv[1],'w').close();time.sleep(30)");
        start.ArgumentList.Add(marker);
        return Process.Start(start) ?? throw new InvalidOperationException("owner did not start");
    }

    private static async Task<(byte Kind, byte[] Payload)> ReadFrameAsync(Stream pipe, CancellationToken token)
    {
        var header = new byte[10];
        await pipe.ReadExactlyAsync(header, token).ConfigureAwait(true);
        Assert.Equal("APTO", System.Text.Encoding.ASCII.GetString(header, 0, 4));
        Assert.Equal(1, header[4]);
        var length = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(6));
        Assert.True(length <= 4096);
        var payload = new byte[length];
        await pipe.ReadExactlyAsync(payload, token).ConfigureAwait(true);
        return (header[5], payload);
    }
}
