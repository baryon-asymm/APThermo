using System.Diagnostics;
using System.Text;
using APThermo.Harness;

namespace APThermo.Thermo.Tests.Elementary;

/// <summary>Runs the corpus script with one deadline over process exit and both streams.</summary>
internal static class PythonCorpusProcess
{
    internal static Task<(int ExitCode, string Output)> RunAsync(string script, IReadOnlyList<string> arguments, TimeSpan timeout) =>
        RunAsync(script, arguments, timeout, TimeProvider.System);

    internal static async Task<(int ExitCode, string Output)> RunAsync(string script, IReadOnlyList<string> arguments, TimeSpan timeout,
        TimeProvider clock, Action<Process>? started = null)
    {
        var start = new ProcessStartInfo("python") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var argument in (string[])["-X", "utf8", script, .. arguments]) { start.ArgumentList.Add(argument); }
        using var deadline = new CancellationTokenSource(timeout, clock);
        return await RunAsync(start, deadline.Token, started).ConfigureAwait(false);
    }

    internal static async Task<(int ExitCode, string Output)> RunAsync(ProcessStartInfo start, CancellationToken deadline,
        Action<Process>? started = null)
    {
        try
        {
            var result = await OwnedProcess.RunAsync(start, deadline, started is null ? null : pid => started(Process.GetProcessById(pid))).ConfigureAwait(false);
            return (result.ExitCode, Encoding.UTF8.GetString(result.Output.Span) + Encoding.UTF8.GetString(result.Error.Span));
        }
        catch (OperationCanceledException interrupted) when (deadline.IsCancellationRequested)
        {
            var problem = new TimeoutException("python process exceeded its deadline; owned-scope cleanup attempted");
            problem.Data["StandardOutput"] = Encoding.UTF8.GetString(interrupted.Data["StandardOutput"] as byte[] ?? []);
            problem.Data["StandardError"] = Encoding.UTF8.GetString(interrupted.Data["StandardError"] as byte[] ?? []);
            if (interrupted.Data["CleanupException"] is { } cleanup) { problem.Data["CleanupException"] = cleanup; }
            throw problem;
        }
    }
}
