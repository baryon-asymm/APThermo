using System.ComponentModel;
using System.Diagnostics;

namespace APThermo.Fixtures.Tests;

/// <summary>Actual owned-process cleanup failures remain secondary to bounded interruption.</summary>
[Trait("Category", "EndToEnd")]
public sealed class DotnetCleanupFailureTests
{
    /// <summary>A completed target preserves fixture bytes and exit code through owned execution.</summary>
    [Fact]
    public async Task CompletedProcessKeepsOutputAndExit()
    {
        using var probe = new DotnetCleanupProbe("echo");
        using var clock = new DotnetDeadlineClock();
        var result = await probe.RunAsync(clock, null, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(15)).ConfigureAwait(true);
        Assert.Equal(probe.ExpectedExit, result.ExitCode);
        Assert.Equal(probe.ExpectedOutput, result.Output.ToArray());
        Assert.Equal(probe.ExpectedError, result.Error.ToArray());
    }

    /// <summary>Controlled deadlines and cancellation retain raw evidence even when real-path disposal fails.</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task CleanupFailurePreservesPrimaryAndRawEvidence(bool callerCancelled, bool partialFailure)
    {
        using var probe = new DotnetCleanupProbe("block");
        using var clock = new DotnetDeadlineClock();
        using var caller = new CancellationTokenSource();
        var secondary = new AggregateException("controlled partial cleanup", new Win32Exception(5));
        var attempts = 0;
        void Cleanup() { attempts++; if (partialFailure) { throw secondary; } }
        var pending = probe.RunAsync(clock, Cleanup, caller.Token);
        await probe.ReadyAsync().ConfigureAwait(true);
        Assert.False(pending.IsCompleted);
        var watch = Stopwatch.StartNew();
        if (callerCancelled) { await caller.CancelAsync().ConfigureAwait(true); }
        else { clock.Advance(TimeSpan.FromSeconds(2)); }
        Exception primary = callerCancelled
            ? await Assert.ThrowsAsync<OperationCanceledException>(async () => await pending.WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(true)).ConfigureAwait(true)
            : await Assert.ThrowsAsync<TimeoutException>(async () => await pending.WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(true)).ConfigureAwait(true);
        await probe.WaitForExitAsync().ConfigureAwait(true);
        Assert.Equal(1, attempts);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(10), $"cleanup took {watch.Elapsed}");
        Assert.Equal(probe.ExpectedOutput, Assert.IsType<byte[]>(primary.Data["StandardOutput"]));
        Assert.Equal(probe.ExpectedError, Assert.IsType<byte[]>(primary.Data["StandardError"]));
        Assert.Contains("stdout=", primary.Message, StringComparison.Ordinal);
        Assert.Contains("stderr=", primary.Message, StringComparison.Ordinal);
        if (callerCancelled) { Assert.Equal(caller.Token, Assert.IsType<OperationCanceledException>(primary).CancellationToken); }
        if (partialFailure)
        {
            var captured = Assert.IsType<AggregateException>(primary.Data["CleanupException"]);
            Assert.Contains(secondary, captured.InnerExceptions);
        }
        else { Assert.Null(primary.Data["CleanupException"]); }
    }
}
