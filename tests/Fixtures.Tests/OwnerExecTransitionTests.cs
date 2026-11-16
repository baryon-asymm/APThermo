using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using APThermo.Harness;
using Xunit.Abstractions;

namespace APThermo.Fixtures.Tests;

/// <summary>Linux bootstrap identity controls around a real same-PID exec transition.</summary>
[Trait("Category", "EndToEnd")]
public sealed class OwnerExecTransitionTests(ITestOutputHelper output) : IDisposable
{
    private readonly DotnetProcessGuardSession _session = new(output);

    /// <summary>The pre-exec argv is not mistaken for the bootstrap, nor signalled without validation.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DelayedExecPreservesBootstrapIdentity(bool cancel)
    {
        if (!OperatingSystem.IsLinux()) { Assert.True(OperatingSystem.IsWindows()); return; }
        _ = Directory.CreateDirectory(_session.DirectoryPath);
        var ready = Path.Combine(_session.DirectoryPath, "proxy.ready");
        var targetMarker = Path.Combine(_session.DirectoryPath, "target.ready");
        var target = new ProcessStartInfo("python3") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        target.ArgumentList.Add("-c");
        target.ArgumentList.Add("import sys;open(sys.argv[1],'w').close()");
        target.ArgumentList.Add(targetMarker);
        var type = typeof(OwnedProcess).Assembly.GetType("APThermo.Harness.OwnedProcessOperation", true)!;
        var cleanup = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Action<Stream> control = _ => cleanup.TrySetResult();
        var operation = type.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Single().Invoke([target, null, null, control, null, null]);
        string? channel = null;
        var opened = Path.Combine(_session.DirectoryPath, "owner.opened");
        Func<ProcessStartInfo, ProcessStartInfo> proxy = start => { channel = start.ArgumentList[2]; return Proxy(start, ready, opened); };
        Process? captured = null;
        Action<Process> launched = owner => { captured = Process.GetProcessById(owner.Id); _ = captured.SafeHandle; };
        type.GetProperty("OwnerStartOverride", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(operation, proxy);
        type.GetProperty("OwnerLaunched", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(operation, launched);
        using var cancellation = new CancellationTokenSource();
        var pending = (Task<DotnetProcessResult>)type.GetMethod("RunAsync", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(operation, [cancellation.Token])!;
        Exception? primary = null;
        try
        {
            await WaitForMarkerAsync(ready, pending).ConfigureAwait(true);
            Assert.NotNull(captured);
            Assert.False(captured.HasExited);
            if (cancel)
            {
                await cancellation.CancelAsync().ConfigureAwait(true);
                await cleanup.Task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(true);
                Assert.False(captured.HasExited, "unverified pre-exec process was signalled");
            }
            await File.WriteAllTextAsync(ready + ".release", "release").ConfigureAwait(true);
            if (cancel)
            {
                await WaitForMarkerAsync(ready + ".released", pending).ConfigureAwait(true);
                var problem = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending.WaitAsync(TimeSpan.FromSeconds(10))).ConfigureAwait(true);
                Assert.Equal(cancellation.Token, problem.CancellationToken);
                Assert.Null(problem.Data["CleanupException"]);
                Assert.False(File.Exists(targetMarker));
            }
            else
            {
                var result = await pending.WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(true);
                Assert.Equal(0, result.ExitCode);
                Assert.True(File.Exists(targetMarker));
            }
        }
        catch (Exception problem) { primary = problem; throw; }
        finally
        {
            try { await CleanupProxyAsync(ready, opened, channel, captured).ConfigureAwait(true); }
            catch (Exception problem) when (primary is not null) { primary.Data["TestCleanupException"] = problem; }
        }
    }

    /// <summary>Removes the isolated files after the owned process ends.</summary>
    public void Dispose() => _session.Dispose();

    private static ProcessStartInfo Proxy(ProcessStartInfo original, string marker, string opened)
    {
        var index = original.ArgumentList.IndexOf("--");
        original.ArgumentList.Insert(index, "--test-parent-opened");
        original.ArgumentList.Insert(index + 1, opened);
        var start = new ProcessStartInfo("python3")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = original.WorkingDirectory,
        };
        foreach (var pair in original.Environment) { start.Environment[pair.Key] = pair.Value; }
        start.Environment["APT_EXEC_ARGS"] = JsonSerializer.Serialize(new[] { original.FileName }.Concat(original.ArgumentList));
        start.Environment["APT_EXEC_MARKER"] = marker;
        start.ArgumentList.Add("-c");
        start.ArgumentList.Add("import os,json,time; p=os.environ.pop('APT_EXEC_MARKER'); open(p,'w').close(); t=time.monotonic()+15;\nwhile not os.path.exists(p+'.release') and time.monotonic()<t: time.sleep(.01)\nopen(p+'.released','w').close(); a=json.loads(os.environ.pop('APT_EXEC_ARGS')); os.execvp(a[0],a)");
        return start;
    }

    private static async Task CleanupProxyAsync(string marker, string opened, string? channel, Process? process)
    {
        await File.WriteAllTextAsync(marker + ".release", "release").ConfigureAwait(false);
        if (process is null) { return; }
        using (process)
        {
            try { await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false); }
            catch (TimeoutException)
            {
                if (!File.Exists(opened) || channel is null) { throw new InvalidOperationException("cannot verify proxy exec identity for cleanup"); }
                var type = Assembly.Load("APThermo.Harness.ProcessOwner").GetType("APThermo.Harness.ProcessOwner.LinuxProcessIdentity", true)!;
                using var identity = (IDisposable)type.GetMethod("Open", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [process.Id])!;
                _ = type.GetMethod("RequireChannel", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(identity, [channel]);
                _ = type.GetMethod("Send", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(identity, [9]);
                await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            }
        }
    }

    private static async Task WaitForMarkerAsync(string marker, Task pending)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!File.Exists(marker))
        {
            if (pending.IsCompleted) { await pending.ConfigureAwait(false); throw new InvalidOperationException("owner ended before proxy readiness"); }
            await Task.Delay(10, deadline.Token).ConfigureAwait(false);
        }
    }
}
