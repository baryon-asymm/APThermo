using System.ComponentModel;
using System.Diagnostics;

namespace APThermo.Fixtures.Tests;

/// <summary>Documented partial tree-kill failures remain secondary to a bounded process interruption.</summary>
[Trait("Category", "EndToEnd")]
public sealed class DotnetCleanupFailureTests
{
    /// <summary>A completed ordinary process does not call the kill seam and keeps exact fixture bytes and exit code.</summary>
    [Fact]
    public async Task CompletedProcessDoesNotAttemptCleanup()
    {
        using var probe = new DotnetCleanupProbe("echo");
        var result = await probe.CompleteAsync(_ => throw new InvalidOperationException("normal completion must not call kill"),
            CancellationToken.None, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(true);
        Assert.Equal(probe.ExpectedExit, result.ExitCode);
        Assert.Equal(probe.ExpectedOutput, result.Output.ToArray());
        Assert.Equal(probe.ExpectedError, result.Error.ToArray());
    }

    /// <summary>Both deadline expiry and caller cancellation keep raw diagnostics when an owned kill reports a partial failure.</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task CleanupFailurePreservesPrimaryAndRawEvidence(bool callerCancelled, bool partialFailure)
    {
        using var probe = new DotnetCleanupProbe("block");
        await probe.ReadyAsync().ConfigureAwait(true);
        using var interruption = new CancellationTokenSource();
        await interruption.CancelAsync().ConfigureAwait(true);
        var caller = callerCancelled ? interruption.Token : CancellationToken.None;
        var secondary = new AggregateException("controlled partial tree kill", new Win32Exception(5));
        var attempts = 0;
        var clock = Stopwatch.StartNew();
        async Task Operation()
        {
            _ = await probe.CompleteAsync(process =>
            {
                Assert.Same(probe.Process, process);
                attempts++;
                process.Kill(entireProcessTree: true);
                if (partialFailure)
                {
                    throw secondary;
                }
            }, interruption.Token, caller).WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(true);
        }
        Exception primary = callerCancelled
            ? await Assert.ThrowsAsync<OperationCanceledException>(Operation).ConfigureAwait(true)
            : await Assert.ThrowsAsync<TimeoutException>(Operation).ConfigureAwait(true);
        Assert.Equal(1, attempts);
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(5), $"cleanup took {clock.Elapsed}");
        Assert.Equal(probe.ExpectedOutput, Assert.IsType<byte[]>(primary.Data["StandardOutput"]));
        Assert.Equal(probe.ExpectedError, Assert.IsType<byte[]>(primary.Data["StandardError"]));
        Assert.Contains("stdout=", primary.Message, StringComparison.Ordinal);
        Assert.Contains("stderr=", primary.Message, StringComparison.Ordinal);
        if (callerCancelled)
        {
            Assert.Equal(caller, Assert.IsType<OperationCanceledException>(primary).CancellationToken);
        }
        if (partialFailure)
        {
            Assert.Same(secondary, primary.Data["CleanupException"]);
            _ = Assert.IsType<Win32Exception>(Assert.Single(Assert.IsType<AggregateException>(primary.Data["CleanupException"]).InnerExceptions));
            Assert.Contains(nameof(AggregateException), primary.Message, StringComparison.Ordinal);
        }
        else
        {
            Assert.Null(primary.Data["CleanupException"]);
        }
    }
}
