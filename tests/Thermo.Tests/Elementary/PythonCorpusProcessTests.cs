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
        var script = "import os, subprocess, sys, time\n" +
            "print('parent-out', flush=True); print('parent-err', file=sys.stderr, flush=True)\n" +
            "p=subprocess.Popen([sys.executable,'-c','import time; time.sleep(20)'])\n" +
            "open(sys.argv[1], 'w').write(str(p.pid))\n" +
            (parentExits ? "sys.exit(0)\n" : "time.sleep(20)\n");
        using var clock = new CorpusDeadlineClock();
        var launched = new TaskCompletionSource<Process>(TaskCreationOptions.RunContinuationsAsynchronously);
        var scriptPath = Path.Combine(directory, "probe.py");
        await File.WriteAllTextAsync(scriptPath, script).ConfigureAwait(true);
        var pending = PythonCorpusProcess.RunAsync(scriptPath, [marker], TimeSpan.FromMinutes(5), clock, launched.SetResult);
        using var watchdog = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        while (!File.Exists(marker))
        {
            await Task.Delay(10, watchdog.Token).ConfigureAwait(true);
        }
        var pid = int.Parse(await File.ReadAllTextAsync(marker, watchdog.Token).ConfigureAwait(true), CultureInfo.InvariantCulture);
        using var child = Process.GetProcessById(pid);
        try
        {
            Assert.False(child.HasExited);
            var parent = await launched.Task.WaitAsync(watchdog.Token).ConfigureAwait(true);
            if (parentExits)
            {
                try
                {
                    await parent.WaitForExitAsync(watchdog.Token).ConfigureAwait(true);
                }
                catch (InvalidOperationException) when (pending.IsCompleted)
                {
                    Assert.Fail("runner completed after parent exit while the child held inherited streams");
                }
                Assert.False(pending.IsCompleted, "runner completed after parent exit while the child held inherited streams");
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
            if (!child.HasExited) { child.Kill(entireProcessTree: true); }
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await child.WaitForExitAsync(cleanup.Token).ConfigureAwait(true);
            if (!pending.IsCompleted && launched.Task.IsCompletedSuccessfully)
            {
                var parent = await launched.Task.ConfigureAwait(true);
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
    }

    private static ProcessStartInfo Start(string script)
    {
        var start = new ProcessStartInfo("python") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        start.ArgumentList.Add("-c");
        start.ArgumentList.Add(script);
        return start;
    }
}
