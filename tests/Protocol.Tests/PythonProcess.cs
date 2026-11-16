using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using APThermo.Harness;
using Xunit.Sdk;

namespace APThermo.Protocol.Tests;

/// <summary>
/// Runs Python with one deadline spanning process exit and both UTF-8 streams. Interrupted calls retain a test
/// failure while a separate bounded cleanup attempts owned-tree termination and observes stopped readers.
/// </summary>
internal static class PythonProcess
{
    /// <summary>Runs a Python script; its supplied timeout covers exit and both streams, including inherited pipes.</summary>
    public static Task<PythonRun> RunAsync(string script, IReadOnlyList<string> arguments, TimeSpan timeout) =>
        RunAsync(script, arguments, timeout, TimeProvider.System);

    internal static async Task<PythonRun> RunAsync(string script, IReadOnlyList<string> arguments, TimeSpan timeout, TimeProvider clock,
        Func<ProcessStartInfo, CancellationToken, Action<int>, Task<DotnetProcessResult>>? controlledRun = null)
    {
        ArgumentNullException.ThrowIfNull(clock);
        using var deadline = new CancellationTokenSource(timeout, clock);
        var pid = 0;
        try
        {
            var start = StartInfo(script, arguments);
            void Started(int value) => pid = value;
            var result = controlledRun is null
                ? await OwnedProcess.RunAsync(start, deadline.Token, Started).ConfigureAwait(true)
                : await controlledRun(start, deadline.Token, Started).ConfigureAwait(true);
            return new PythonRun(result.ExitCode, Encoding.UTF8.GetString(result.Output.Span) + Encoding.UTF8.GetString(result.Error.Span));
        }
        catch (OperationCanceledException interrupted) when (deadline.IsCancellationRequested)
        {
            var seconds = timeout.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture);
            var cleanup = interrupted.Data["CleanupException"] is { } failure ? "cleanup: " + failure : "owned-scope cleanup attempted";
            var problem = new XunitException($"{script} did not finish within {seconds} seconds; python PID {pid}; {cleanup}");
            problem.Data["Cleanup"] = cleanup;
            problem.Data["StandardOutput"] = interrupted.Data["StandardOutput"];
            problem.Data["StandardError"] = interrupted.Data["StandardError"];
            throw problem;
        }
        catch (Win32Exception problem)
        {
            throw new XunitException($"python was not found on the path ({problem.Message}); the tool levels need Python 3.8+ (tests/Protocol.Tests/BOOT.md)");
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

}
