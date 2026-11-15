using System.Diagnostics;

namespace APThermo.Protocol.Tests;

/// <summary>One real outer watchdog from fixture construction through process completion.</summary>
internal sealed class PythonFixtureBudget : IDisposable
{
    internal static readonly TimeSpan Limit = TimeSpan.FromSeconds(30);
    private readonly CancellationTokenSource _deadline = new(Limit);
    private readonly Stopwatch _elapsed = Stopwatch.StartNew();
    public CancellationToken Token => _deadline.Token;
    public TimeSpan Remaining => Limit > _elapsed.Elapsed ? Limit - _elapsed.Elapsed : TimeSpan.Zero;

    public void Require(string stage)
    {
        RequireRemaining(Remaining, stage);
        if (_deadline.IsCancellationRequested)
        {
            throw new InvalidOperationException($"fixture budget exhausted during {stage}; elapsed={_elapsed.Elapsed}");
        }
    }

    internal static void RequireRemaining(TimeSpan remaining, string stage)
    {
        if (remaining <= TimeSpan.Zero)
        {
            throw new InvalidOperationException($"fixture budget exhausted during {stage}");
        }
    }

    public async Task<T> WaitAsync<T>(Task<T> task, string stage)
    {
        Require(stage);
        try
        {
            return await task.WaitAsync(Token).ConfigureAwait(true);
        }
        catch (OperationCanceledException) when (Token.IsCancellationRequested)
        {
            throw new InvalidOperationException($"fixture watchdog during {stage}; elapsed={_elapsed.Elapsed}");
        }
    }

    public async Task WaitAsync(Task task, string stage)
    {
        Require(stage);
        try
        {
            await task.WaitAsync(Token).ConfigureAwait(true);
        }
        catch (OperationCanceledException) when (Token.IsCancellationRequested)
        {
            throw new InvalidOperationException($"fixture watchdog during {stage}; elapsed={_elapsed.Elapsed}");
        }
    }

    public void Dispose() => _deadline.Dispose();
}
