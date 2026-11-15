using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using Xunit.Sdk;

namespace APThermo.Protocol.Tests;

/// <summary>
/// Runs Python with one deadline spanning process exit and both UTF-8 streams. Interrupted calls retain a test
/// failure while a separate bounded cleanup attempts owned-tree termination and observes stopped readers.
/// </summary>
internal static class PythonProcess
{
    private static readonly TimeSpan CleanupTimeout = TimeSpan.FromSeconds(5);

    /// <summary>Runs a Python script; its supplied timeout covers exit and both streams, including inherited pipes.</summary>
    public static Task<PythonRun> RunAsync(string script, IReadOnlyList<string> arguments, TimeSpan timeout) =>
        RunAsync(script, arguments, timeout, TimeProvider.System);

    internal static async Task<PythonRun> RunAsync(string script, IReadOnlyList<string> arguments, TimeSpan timeout, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        using var deadline = new CancellationTokenSource(timeout, clock);
        using var process = Start(StartInfo(script, arguments));
        var output = process.StandardOutput.ReadToEndAsync(deadline.Token);
        var error = process.StandardError.ReadToEndAsync(deadline.Token);
        var completion = Task.WhenAll(process.WaitForExitAsync(deadline.Token), output, error);
        try
        {
            await completion.WaitAsync(deadline.Token).ConfigureAwait(true);
            return new PythonRun(process.ExitCode, await output.ConfigureAwait(true) + await error.ConfigureAwait(true));
        }
        catch (OperationCanceledException) when (deadline.IsCancellationRequested)
        {
            var cleanup = await CleanupAsync(process, completion).ConfigureAwait(true);
            var seconds = timeout.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture);
            var problem = new XunitException($"{script} did not finish within {seconds} seconds; python PID {process.Id}; {cleanup}");
            problem.Data["Cleanup"] = cleanup;
            throw problem;
        }
    }

    private static ProcessStartInfo StartInfo(string script, IReadOnlyList<string> arguments)
    {
        var start = new ProcessStartInfo("python")
        {
            WorkingDirectory = Tree.Root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            UseShellExecute = false,
        };
        foreach (var argument in (string[])["-X", "utf8", script, .. arguments])
        {
            start.ArgumentList.Add(argument);
        }
        return start;
    }

    private static Process Start(ProcessStartInfo start)
    {
        try
        {
            var process = Process.Start(start);
            Assert.NotNull(process);
            return process;
        }
        catch (Win32Exception problem)
        {
            throw new XunitException($"python was not found on the path ({problem.Message}); the tool levels need Python 3.8+ (tests/Protocol.Tests/BOOT.md)");
        }
    }

    private static async Task<string> CleanupAsync(Process process, Task completion)
    {
        using var deadline = new CancellationTokenSource(CleanupTimeout);
        var kill = Task.Run(() => AttemptTreeKill(process));
        var cleanup = Task.WhenAll(kill, ObserveCompletionAsync(completion),
            ObserveCompletionAsync(process.WaitForExitAsync(deadline.Token)));
        try
        {
            var outcomes = await cleanup.WaitAsync(deadline.Token).ConfigureAwait(true);
            return "cleanup: " + string.Join("; ", outcomes);
        }
        catch (OperationCanceledException) when (deadline.IsCancellationRequested)
        {
            var observedKill = kill.IsCompletedSuccessfully ? await kill.ConfigureAwait(true) : "owned-tree kill still pending";
            return $"cleanup: {observedKill}; five-second cleanup deadline reached; readers cancelled";
        }
        catch (Exception problem) when (problem is IOException or InvalidOperationException or Win32Exception or AggregateException)
        {
            return "cleanup: " + problem;
        }
        finally
        {
            ObserveLateFault(cleanup);
        }
    }

    private static string AttemptTreeKill(Process process) => CaptureTreeKill(() =>
    {
        if (!process.HasExited)
        {
            process.Kill(entireProcessTree: true);
        }
    });

    private static string CaptureTreeKill(Action kill)
    {
        try
        {
            kill();
            return "owned-tree kill attempted when parent was live";
        }
        catch (Exception problem) when (problem is InvalidOperationException or Win32Exception or AggregateException)
        {
            return "owned-tree kill: " + problem;
        }
    }

    private static async Task<string> ObserveCompletionAsync(Task completion)
    {
        try
        {
            await completion.ConfigureAwait(true);
            return "completion observed";
        }
        catch (Exception problem) when (problem is OperationCanceledException or IOException or ObjectDisposedException)
        {
            return "completion observed: " + problem.GetType().Name;
        }
    }

    private static void ObserveLateFault(Task completion) =>
        _ = completion.ContinueWith(static failed => { _ = failed.Exception; }, CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
}
