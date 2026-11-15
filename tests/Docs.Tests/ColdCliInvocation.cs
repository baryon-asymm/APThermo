using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using APThermo.Fixtures;
using APThermo.Harness;

namespace APThermo.Docs.Tests;

internal sealed record ColdCliSettings(string Mode, string AssemblyPath, string WorkingDirectory);
internal sealed record ColdCliProcess(string AssemblyPath, string WorkingDirectory, IReadOnlyList<string> Arguments, double TimeoutSeconds, double CleanupSeconds);
internal sealed record ColdCliInput(string Path, string Sha256);
internal sealed record ColdCliContext(int Ordinal, string Source, string Mode, ColdCliProcess Process, IReadOnlyList<ColdCliInput> Inputs);

internal sealed class ColdCliInvocation(ColdCliSettings settings, Func<IReadOnlyList<string>, CliRun> runner, Action<string> output)
{
    internal const string ContextKey = "Docs.ColdCliInvocation";
    internal const string OutcomeKey = "Docs.ColdCliOutcome";
    private int _ordinal;

    internal CliRun Run(string where, IReadOnlyList<string> args)
    {
        var started = Stopwatch.GetTimestamp();
        var process = new ColdCliProcess(settings.AssemblyPath, settings.WorkingDirectory, args,
            DotnetProcess.DefaultTimeout.TotalSeconds, DotnetProcess.CleanupTimeout.TotalSeconds);
        var context = new ColdCliContext(Interlocked.Increment(ref _ordinal), where, settings.Mode, process, []);
        BestEffort(() => context = context with { Process = process with { Arguments = [.. args] } });
        BestEffort(() => context = context with { Inputs = InputsOf(args) });
        var startRecord = string.Empty;
        BestEffort(() =>
        {
            startRecord = JsonSerializer.Serialize(new { Phase = "start", Context = context, StartedTimestamp = started, ElapsedSeconds = Stopwatch.GetElapsedTime(started).TotalSeconds });
            output(startRecord);
        });
        try
        {
            var result = runner(args);
            BestEffort(() => output(JsonSerializer.Serialize(new
            {
                Phase = "finish",
                Context = context,
                StartedTimestamp = started,
                ElapsedSeconds = Stopwatch.GetElapsedTime(started).TotalSeconds,
                ExitCode = result.Code,
            })));
            return result;
        }
        catch (Exception problem)
        {
            BestEffort(() => problem.Data[ContextKey] = startRecord);
            BestEffort(() =>
            {
                var record = JsonSerializer.Serialize(new
                {
                    Phase = "failure",
                    Context = context,
                    StartedTimestamp = started,
                    ElapsedSeconds = Stopwatch.GetElapsedTime(started).TotalSeconds,
                    ExceptionType = problem.GetType().FullName,
                });
                BestEffort(() => problem.Data[OutcomeKey] = record);
                output(record);
            });
            throw;
        }
    }

    private static List<ColdCliInput> InputsOf(IReadOnlyList<string> args)
    {
        var root = Path.GetFullPath(RepositoryPaths.Resolve("samples", "cli")) + Path.DirectorySeparatorChar;
        var found = new List<ColdCliInput>();
        foreach (var token in args.Where(token => !token.StartsWith('-')))
        {
            var path = Path.GetFullPath(Path.Combine(RepositoryPaths.Root, token));
            if (path.StartsWith(root, StringComparison.Ordinal) && File.Exists(path))
            {
                found.Add(new ColdCliInput(path, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)))));
            }
        }
        return found;
    }

    private static void BestEffort(Action diagnostic)
    {
        // Execute inline, capturing diagnostic faults as task outcomes without replacing the runner's failure.
        using var attempt = new Task(diagnostic, CancellationToken.None, TaskCreationOptions.DenyChildAttach);
        attempt.RunSynchronously(TaskScheduler.Default);
        _ = attempt.Exception;
    }
}
