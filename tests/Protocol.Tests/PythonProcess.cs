using System.ComponentModel;
using System.Diagnostics;
using Xunit.Sdk;

namespace APThermo.Protocol.Tests;

/// <summary>The outcome of one Python script run as a process: its exit code and everything it wrote, standard output first.</summary>
/// <param name="ExitCode">The exit code of the script.</param>
/// <param name="Output">The standard output followed by the standard error.</param>
internal sealed record PythonRun(int ExitCode, string Output);

/// <summary>
/// The one place this node starts Python: <c>python -X utf8 &lt;script&gt; &lt;arguments&gt;</c> from the tree root, both output
/// streams read to the end, a deadline after which the process tree is killed. Python missing from the path and a missed
/// deadline are failures of the calling test, never a skip (this node's BOOT.md, Lint and Tool self-tests levels).
/// </summary>
internal static class PythonProcess
{
    /// <summary>Runs <paramref name="script"/> (a path relative to the tree root) with <paramref name="arguments"/> and returns
    /// what it did; fails the calling test when Python is not on the path or the script outlives <paramref name="timeout"/>.</summary>
    public static async Task<PythonRun> RunAsync(string script, IReadOnlyList<string> arguments, TimeSpan timeout)
    {
        var start = new ProcessStartInfo("python")
        {
            WorkingDirectory = Tree.Root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in (string[])["-X", "utf8", script, .. arguments])
        {
            start.ArgumentList.Add(argument);
        }

        Process? process;
        try
        {
            process = Process.Start(start);
        }
        catch (Win32Exception e)
        {
            throw new XunitException($"python was not found on the path ({e.Message}); the tool levels need Python 3.8+ (tests/Protocol.Tests/BOOT.md)");
        }

        Assert.NotNull(process);
        using (process)
        {
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            using var deadline = new CancellationTokenSource(timeout);
            try
            {
                await process.WaitForExitAsync(deadline.Token).ConfigureAwait(true);
            }
            catch (OperationCanceledException)
            {
                process.Kill(entireProcessTree: true);
                throw new XunitException($"{script} did not finish within {timeout.TotalMinutes:0.#} minutes");
            }

            return new PythonRun(process.ExitCode, await output.ConfigureAwait(true) + await error.ConfigureAwait(true));
        }
    }
}
