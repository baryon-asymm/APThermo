using System.Diagnostics;
using System.Globalization;

namespace APThermo.Thermo.Tests.Elementary;

/// <summary>Real Python processes exercise the corpus runner's deadline and inherited pipes.</summary>
[Trait("Category", "EndToEnd")]
public sealed class PythonCorpusProcessTests
{
    /// <summary>A normally completed command preserves its stdout and stderr.</summary>
    [Fact]
    public async Task CompletedPythonReturnsBothStreams()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var (exitCode, output) = await PythonCorpusProcess.RunAsync(Start("import sys; print('out'); print('err', file=sys.stderr)"),
            deadline.Token).ConfigureAwait(true);
        Assert.Equal(0, exitCode);
        Assert.Contains("out", output, StringComparison.Ordinal);
        Assert.Contains("err", output, StringComparison.Ordinal);
    }

    /// <summary>Both a live tree and an exited parent with inherited pipes retain output and finish within cleanup.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeadlineCoversParentAndInheritedPipes(bool parentExits)
    {
        var directory = Path.Combine(Path.GetTempPath(), "apthermo-corpus-" + Guid.NewGuid().ToString("N"));
        _ = Directory.CreateDirectory(directory);
        var marker = Path.Combine(directory, "child.pid");
        var release = Path.Combine(directory, "release");
        var script = "import os, subprocess, sys, time\n" +
            "print('parent-out', flush=True); print('parent-err', file=sys.stderr, flush=True)\n" +
            "p=subprocess.Popen([sys.executable,'-c','import time; time.sleep(20)'])\n" +
            "tmp=sys.argv[1]+'.tmp'; open(tmp, 'w').write(str(p.pid)); os.replace(tmp,sys.argv[1])\n" +
            (parentExits ? "while not os.path.exists(sys.argv[2]): time.sleep(0.01)\nsys.exit(0)\n" : "time.sleep(20)\n");
        using var clock = new CorpusDeadlineClock();
        var launched = new TaskCompletionSource<Process>(TaskCreationOptions.RunContinuationsAsynchronously);
        var scriptPath = Path.Combine(directory, "probe.py");
        await File.WriteAllTextAsync(scriptPath, script).ConfigureAwait(true);
        var pending = PythonCorpusProcess.RunAsync(scriptPath, [marker, release], TimeSpan.FromMinutes(5), clock, launched.SetResult);
        using var watchdog = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        Process? child = null;
        try
        {
            var parent = await AwaitStartedAsync(launched.Task, pending, watchdog.Token).ConfigureAwait(true);
            await WaitForMarkerAsync(marker, pending, watchdog.Token).ConfigureAwait(true);
            var pid = int.Parse(await File.ReadAllTextAsync(marker, watchdog.Token).ConfigureAwait(true), CultureInfo.InvariantCulture);
            child = Process.GetProcessById(pid);
            Assert.False(child.HasExited);
            if (parentExits)
            {
                await File.WriteAllTextAsync(release, "release", watchdog.Token).ConfigureAwait(true);
                await AssertParentWaitsAsync(parent, pending, watchdog.Token).ConfigureAwait(true);
                Assert.False(child.HasExited, "the descendant exited before the parent-exited check");
            }
            var watch = Stopwatch.StartNew();
            clock.Expire();
            var finished = await Task.WhenAny(pending, Task.Delay(TimeSpan.FromSeconds(10))).ConfigureAwait(true);
            Assert.Same(pending, finished);
            var problem = await Assert.ThrowsAsync<TimeoutException>(async () => await pending.ConfigureAwait(true)).ConfigureAwait(true);
            Assert.True(watch.Elapsed < TimeSpan.FromSeconds(10));
            Assert.Contains("parent-out", Assert.IsType<string>(problem.Data["StandardOutput"]), StringComparison.Ordinal);
            Assert.Contains("parent-err", Assert.IsType<string>(problem.Data["StandardError"]), StringComparison.Ordinal);
            await child.WaitForExitAsync(watchdog.Token).ConfigureAwait(true);
            Assert.True(child.HasExited, "the owned descendant survived cleanup");
            Assert.Contains("owned-scope cleanup attempted", problem.Message, StringComparison.Ordinal);
        }
        finally
        {
            clock.Expire();
            await StopProbeAsync(child, pending, launched.Task, directory).ConfigureAwait(true);
            child?.Dispose();
        }
    }

    /// <summary>A callback failure is surfaced without waiting for the readiness watchdog.</summary>
    [Fact]
    public async Task StartCallbackFailureIsReportedWithoutWaitingForWatchdog()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var started = new TaskCompletionSource<Process>(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = PythonCorpusProcess.RunAsync(Start("import time; time.sleep(20)"), deadline.Token,
            _ => throw new InvalidOperationException("controlled start callback failure"));
        var error = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await AwaitStartedAsync(started.Task, pending, deadline.Token).ConfigureAwait(true)).ConfigureAwait(true);
        Assert.Equal("controlled start callback failure", error.Message);
        Assert.False(deadline.IsCancellationRequested);
    }

    private static async Task<Process> AwaitStartedAsync(Task<Process> started, Task pending, CancellationToken token)
    {
        var completed = await Task.WhenAny(started, pending).WaitAsync(token).ConfigureAwait(true);
        if (completed == started) { return await started.ConfigureAwait(true); }
        await pending.ConfigureAwait(true);
        throw new InvalidOperationException("python process finished before its start notification");
    }

    private static async Task WaitForMarkerAsync(string marker, Task pending, CancellationToken token)
    {
        while (!File.Exists(marker))
        {
            if (pending.IsCompleted)
            {
                await pending.ConfigureAwait(true);
                throw new InvalidOperationException("python process finished before child readiness");
            }
            await Task.Delay(10, token).ConfigureAwait(true);
        }
    }

    private static async Task AssertParentWaitsAsync(Process parent, Task pending, CancellationToken watchdog)
    {
        try
        {
            await parent.WaitForExitAsync(watchdog).ConfigureAwait(true);
        }
        catch (InvalidOperationException) when (pending.IsCompleted)
        {
            Assert.Fail("runner completed after parent exit while the child held inherited streams");
        }
        Assert.False(pending.IsCompleted, "runner completed after parent exit while the child held inherited streams");
    }

    private static async Task StopProbeAsync(Process? child, Task pending, Task<Process> launched, string directory)
    {
        using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        if (child is not null)
        {
            if (!child.HasExited) { child.Kill(entireProcessTree: true); }
            await child.WaitForExitAsync(cleanup.Token).ConfigureAwait(true);
        }
        if (!pending.IsCompleted && launched.IsCompletedSuccessfully)
        {
            var parent = await launched.ConfigureAwait(true);
            try
            {
                if (!parent.HasExited) { parent.Kill(entireProcessTree: true); }
            }
            catch (InvalidOperationException)
            {
                // The owned parent completed concurrently with fixture cleanup.
            }
        }
        Directory.Delete(directory, recursive: true);
    }

    private static ProcessStartInfo Start(string script)
    {
        var start = new ProcessStartInfo("python") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        start.ArgumentList.Add("-c");
        start.ArgumentList.Add(script);
        return start;
    }
}
