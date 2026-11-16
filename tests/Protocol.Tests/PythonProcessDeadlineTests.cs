using System.Globalization;
using System.Diagnostics;
using APThermo.Harness;
using System.Reflection;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace APThermo.Protocol.Tests;

/// <summary>Owned probes establish readiness before advancing the actual per-call deadline clock.</summary>
/// <param name="output">State/identity/timer observations, emitted before assertions.</param>
[Trait("Category", "EndToEnd")]
public sealed class PythonProcessDeadlineTests(ITestOutputHelper output)
{
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(3);

    /// <summary>Ordinary/nonzero output checks have the fixture guard, not a three-second setup race.</summary>
    /// <param name="exitCode">The probe's requested exit code.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    public async Task NormalExitAndAsciiOutputRemainExact(int exitCode)
    {
        var probe = new PythonProcessProbe();
        await using var cleanup = probe.ConfigureAwait(true);
        probe.Start("ascii", PythonFixtureBudget.Limit, exitCode);
        var run = await probe.CompletionAsync().ConfigureAwait(true);
        output.WriteLine(probe.Diagnostics);
        Assert.Equal(exitCode, run.ExitCode);
        Assert.Equal(PythonProcessProbe.ExpectedAsciiOutput, run.Output);
    }

    /// <summary>Python's UTF-8 bytes remain exact independently of host startup speed.</summary>
    [Fact]
    public async Task Utf8OutputIsDecodedWithoutPlatformSubstitution()
    {
        var probe = new PythonProcessProbe();
        await using var cleanup = probe.ConfigureAwait(true);
        probe.Start("normal", PythonFixtureBudget.Limit);
        var run = await probe.CompletionAsync().ConfigureAwait(true);
        output.WriteLine(probe.Diagnostics);
        Assert.Equal(PythonProcessProbe.ExpectedOutput, run.Output);
    }

    /// <summary>The actual timer expires only after live owned inherited-pipe state is established.</summary>
    /// <param name="mode">The inherited standard streams.</param>
    [Theory]
    [InlineData("stdout")]
    [InlineData("stderr")]
    [InlineData("both")]
    public async Task InheritedPipeCannotOutliveSuppliedDeadline(string mode)
    {
        var probe = new PythonProcessProbe();
        await using var cleanup = probe.ConfigureAwait(true);
        probe.Start(mode, Deadline);
        await probe.AcknowledgeAsync().ConfigureAwait(true);
        await probe.WaitForParentExitAsync().ConfigureAwait(true);
        RequireBeforeExpiry(probe);
        probe.AdvanceDeadline();
        var problem = await Assert.ThrowsAsync<XunitException>(probe.CompletionAsync).ConfigureAwait(true);
        output.WriteLine(probe.Diagnostics);
        Assert.Contains("did not finish", problem.Message, StringComparison.Ordinal);
    }

    /// <summary>A controlled four-second preparation history cannot consume the paused helper deadline.</summary>
    [Fact]
    public async Task DelayedReadinessDoesNotSpendTheHelperDeadline()
    {
        var probe = new PythonProcessProbe();
        await using var cleanup = probe.ConfigureAwait(true);
        using var formerReady = new CancellationTokenSource(TimeSpan.FromSeconds(2), probe.PreparationClock);
        using var formerHelper = new CancellationTokenSource(Deadline, probe.PreparationClock);
        probe.Start("both", Deadline);
        await probe.AcknowledgeAsync(() => probe.PreparationClock.Advance(TimeSpan.FromSeconds(4))).ConfigureAwait(true);
        await probe.WaitForParentExitAsync().ConfigureAwait(true);
        output.WriteLine(probe.Diagnostics);
        Assert.True(formerReady.IsCancellationRequested);
        Assert.True(formerHelper.IsCancellationRequested);
        RequireBeforeExpiry(probe);
        probe.AdvanceDeadline();
        _ = await Assert.ThrowsAsync<XunitException>(probe.CompletionAsync).ConfigureAwait(true);
        output.WriteLine(probe.Diagnostics);
    }

    /// <summary>A live parent also reaches the same production deadline/cleanup path after acknowledged ownership.</summary>
    [Fact]
    public async Task HangingParentIsADeadlineFailure()
    {
        var probe = new PythonProcessProbe();
        await using var cleanup = probe.ConfigureAwait(true);
        probe.Start("hang", Deadline);
        await probe.AcknowledgeAsync().ConfigureAwait(true);
        RequireBeforeExpiry(probe);
        probe.AdvanceDeadline();
        _ = await Assert.ThrowsAsync<XunitException>(probe.CompletionAsync).ConfigureAwait(true);
        await probe.WaitForParentExitAsync(requireSuccess: false).ConfigureAwait(true);
        output.WriteLine(probe.Diagnostics);
    }

    /// <summary>The helper keeps script, supplied deadline, owned PID and cleanup diagnostics.</summary>
    [Fact]
    public async Task TimeoutPreservesProcessAndCleanupContext()
    {
        var probe = new PythonProcessProbe();
        await using var cleanup = probe.ConfigureAwait(true);
        probe.Start("hang", Deadline);
        await probe.AcknowledgeAsync().ConfigureAwait(true);
        RequireBeforeExpiry(probe);
        probe.AdvanceDeadline();
        var problem = await Assert.ThrowsAsync<XunitException>(probe.CompletionAsync).ConfigureAwait(true);
        output.WriteLine(probe.Diagnostics);
        Assert.Contains(probe.Script, problem.Message, StringComparison.Ordinal);
        Assert.Contains(probe.ParentId.ToString(CultureInfo.InvariantCulture), problem.Message, StringComparison.Ordinal);
        Assert.Contains("cleanup", problem.Message, StringComparison.Ordinal);
        Assert.Contains(Deadline.TotalSeconds.ToString(CultureInfo.InvariantCulture), problem.Message, StringComparison.Ordinal);
    }

    /// <summary>The original three-argument entry still uses an actually firing System clock timer.</summary>
    [Fact]
    public async Task OriginalEntryUsesTheSystemClock()
    {
        var probe = new PythonProcessProbe();
        await using var cleanup = probe.ConfigureAwait(true);
        probe.Start("hang", TimeSpan.FromMilliseconds(1), systemClock: true);
        var problem = await Assert.ThrowsAsync<XunitException>(probe.CompletionAsync).ConfigureAwait(true);
        output.WriteLine(probe.Diagnostics);
        Assert.Contains("0.001 seconds", problem.Message, StringComparison.Ordinal);
        Assert.Empty(probe.Clock.DueTimes);
    }

    /// <summary>Wrong nonce, mode or protocol stage fails preparation rather than counting as helper timeout.</summary>
    /// <param name="field">The field deliberately corrupted in an otherwise valid state frame.</param>
    [Theory]
    [InlineData("nonce")]
    [InlineData("mode")]
    [InlineData("stage")]
    public void InvalidReadyStateIsAFixtureFailure(string field)
    {
        var frame = new PythonProbeFrame(field == "nonce" ? "wrong" : "owned", field == "mode" ? "stderr" : "stdout",
            field == "stage" ? "acknowledged" : "child-ready", 1, 2);
        _ = Assert.Throws<InvalidOperationException>(() => frame.Validate("owned", "stdout", "child-ready"));
    }

    /// <summary>A previously owned exited identity cannot satisfy the live precondition.</summary>
    [Fact]
    public async Task DeadIdentityIsAFixtureFailure()
    {
        var probe = new PythonProcessProbe();
        await using var cleanup = probe.ConfigureAwait(true);
        probe.Start("hang", Deadline);
        await probe.AcknowledgeAsync().ConfigureAwait(true);
        await probe.ReleaseAsync().ConfigureAwait(true);
        await probe.WaitForParentExitAsync().ConfigureAwait(true);
        output.WriteLine(probe.Diagnostics);
        _ = Assert.Throws<InvalidOperationException>(probe.RequireLiveChild);
        var run = await probe.CompletionAsync().ConfigureAwait(true);
        Assert.Equal(0, run.ExitCode);
    }

    /// <summary>A spent outer budget is a setup failure independently of the helper clock.</summary>
    [Fact]
    public void SpentBudgetIsAFixtureFailure() =>
        _ = Assert.Throws<InvalidOperationException>(() => PythonFixtureBudget.RequireRemaining(TimeSpan.Zero, "capture"));

    /// <summary>The real CTS arms the supplied interval and fires precisely on the controlled boundary.</summary>
    [Fact]
    public void ActualCtsUsesTheSuppliedClockInterval()
    {
        using var clock = new PythonDeadlineClock();
        using var deadline = new CancellationTokenSource(Deadline, clock);
        Assert.Equal(Deadline, Assert.Single(clock.DueTimes));
        clock.Advance(TimeSpan.FromSeconds(2));
        Assert.False(deadline.IsCancellationRequested);
        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.True(deadline.IsCancellationRequested);
        Assert.Equal(1, clock.Fired);
    }

    /// <summary>The real owned Python path retains a secondary cleanup failure alongside its timeout.</summary>
    [Fact]
    public async Task OwnedCleanupFailureRemainsSecondaryToPythonTimeout()
    {
        var probe = new PythonProcessProbe();
        await using var cleanup = probe.ConfigureAwait(true);
        var attempts = 0;
        void FailCleanup() { attempts++; throw new AggregateException("controlled owned cleanup failure"); }
        Task<DotnetProcessResult> RunOwned(ProcessStartInfo start, CancellationToken token, Action<int> started)
        {
            var type = typeof(OwnedProcess).Assembly.GetType("APThermo.Harness.OwnedProcessOperation", throwOnError: true)!;
            var constructor = type.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Single();
            var instance = constructor.Invoke([start, started, (Action)FailCleanup, null, null, null]);
            var method = type.GetMethod("RunAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
            return (Task<DotnetProcessResult>)method.Invoke(instance, [token])!;
        }
        probe.Start("hang", Deadline, controlledRun: RunOwned);
        await probe.AcknowledgeAsync().ConfigureAwait(true);
        RequireBeforeExpiry(probe);
        probe.AdvanceDeadline();
        var problem = await Assert.ThrowsAsync<XunitException>(probe.CompletionAsync).ConfigureAwait(true);
        Assert.Equal(1, attempts);
        Assert.Contains("controlled owned cleanup failure", Assert.IsType<string>(problem.Data["Cleanup"]), StringComparison.Ordinal);
        _ = Assert.IsType<byte[]>(problem.Data["StandardOutput"]);
        _ = Assert.IsType<byte[]>(problem.Data["StandardError"]);
        await probe.WaitForParentExitAsync(requireSuccess: false).ConfigureAwait(true);
    }

    private void RequireBeforeExpiry(PythonProcessProbe probe)
    {
        probe.Record("before-precondition-assertions");
        output.WriteLine(probe.Diagnostics);
        probe.RequireLiveChild();
        Assert.False(probe.CallCompleted);
        Assert.Equal(0, probe.Clock.Fired);
        Assert.Equal(Deadline, Assert.Single(probe.Clock.DueTimes));
        Assert.True(probe.Remaining > TimeSpan.Zero);
    }
}
