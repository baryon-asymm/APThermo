using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using Xunit.Sdk;

namespace APThermo.Protocol.Tests;

/// <summary>Owned finite probes prove that a Python deadline includes its redirected streams.</summary>
[Trait("Category", "EndToEnd")]
public sealed class PythonProcessDeadlineTests
{
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan OuterBound = TimeSpan.FromSeconds(16);

    /// <summary>Normal and nonzero exits retain exact ASCII text, stdout before stderr.</summary>
    /// <param name="exitCode">The exit code requested from the independent probe.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    public async Task NormalExitAndAsciiOutputRemainExact(int exitCode)
    {
        var probe = new PythonProcessProbe();
        await using var cleanup = probe.ConfigureAwait(true);
        var run = await PythonProcess.RunAsync(probe.Script, probe.Arguments("ascii", exitCode), Deadline)
            .WaitAsync(OuterBound).ConfigureAwait(true);
        Assert.Equal(exitCode, run.ExitCode);
        Assert.Equal(PythonProcessProbe.ExpectedAsciiOutput, run.Output);
    }

    /// <summary>Python's explicit UTF-8 byte output is decoded as UTF-8 on each platform.</summary>
    [Fact]
    public async Task Utf8OutputIsDecodedWithoutPlatformSubstitution()
    {
        var probe = new PythonProcessProbe();
        await using var cleanup = probe.ConfigureAwait(true);
        var run = await PythonProcess.RunAsync(probe.Script, probe.Arguments("normal"), Deadline)
            .WaitAsync(OuterBound).ConfigureAwait(true);
        Assert.Equal(PythonProcessProbe.ExpectedOutput, run.Output);
    }

    /// <summary>An exited parent cannot leave an inherited output pipe outside the supplied deadline.</summary>
    /// <param name="mode">Which redirected streams the child inherits.</param>
    [Theory]
    [InlineData("stdout")]
    [InlineData("stderr")]
    [InlineData("both")]
    public async Task InheritedPipeCannotOutliveSuppliedDeadline(string mode)
    {
        var probe = new PythonProcessProbe();
        await using var cleanup = probe.ConfigureAwait(true);
        var clock = Stopwatch.StartNew();
        var call = PythonProcess.RunAsync(probe.Script, probe.Arguments(mode), Deadline);
        await probe.AcknowledgeAsync().ConfigureAwait(true);
        await probe.WaitForParentExitAsync().ConfigureAwait(true);
        Assert.True(probe.ChildIsRunning, "the owned child must hold the selected pipe after parent exit");
        var problem = await Assert.ThrowsAsync<XunitException>(() => call.WaitAsync(OuterBound)).ConfigureAwait(true);
        Assert.Contains("did not finish", problem.Message, StringComparison.Ordinal);
        Assert.True(clock.Elapsed < Deadline + TimeSpan.FromSeconds(6), $"process and stream deadline took {clock.Elapsed}");
    }

    /// <summary>A genuinely live parent still fails at the same supplied deadline.</summary>
    [Fact]
    public async Task HangingParentIsADeadlineFailure()
    {
        var probe = new PythonProcessProbe();
        await using var cleanup = probe.ConfigureAwait(true);
        var call = PythonProcess.RunAsync(probe.Script, probe.Arguments("hang"), Deadline);
        await probe.AcknowledgeAsync().ConfigureAwait(true);
        var problem = await Assert.ThrowsAsync<XunitException>(() => call.WaitAsync(OuterBound)).ConfigureAwait(true);
        Assert.Contains("did not finish", problem.Message, StringComparison.Ordinal);
        await probe.WaitForParentExitAsync().ConfigureAwait(true);
    }

    /// <summary>The actual kill filter keeps a documented partial-kill failure as secondary detail.</summary>
    [Fact]
    public void DocumentedTreeKillFailureRemainsSecondary()
    {
        var invoked = false;
        var normal = CaptureKill(() => invoked = true);
        Assert.True(invoked);
        Assert.Contains("attempted", normal, StringComparison.Ordinal);
        var partial = new AggregateException("injected partial tree kill", new IOException("owned child was not terminated"));
        var failure = CaptureKill(() => throw partial);
        Assert.Contains(nameof(AggregateException), failure, StringComparison.Ordinal);
        Assert.Contains(partial.InnerException!.Message, failure, StringComparison.Ordinal);
    }

    private static string CaptureKill(Action kill)
    {
        var method = typeof(PythonProcess).GetMethod("CaptureTreeKill", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        return Assert.IsType<string>(method.Invoke(null, [kill]));
    }

    /// <summary>A timeout identifies the actual owned process, script, supplied deadline and cleanup attempt.</summary>
    [Fact]
    public async Task TimeoutPreservesProcessAndCleanupContext()
    {
        var probe = new PythonProcessProbe();
        await using var cleanup = probe.ConfigureAwait(true);
        var call = PythonProcess.RunAsync(probe.Script, probe.Arguments("hang"), Deadline);
        await probe.AcknowledgeAsync().ConfigureAwait(true);
        var problem = await Assert.ThrowsAsync<XunitException>(() => call.WaitAsync(OuterBound)).ConfigureAwait(true);
        Assert.Contains(probe.Script, problem.Message, StringComparison.Ordinal);
        Assert.Contains(probe.ParentId.ToString(CultureInfo.InvariantCulture), problem.Message, StringComparison.Ordinal);
        Assert.Contains("cleanup", problem.Message, StringComparison.Ordinal);
        Assert.Contains(Deadline.TotalSeconds.ToString(CultureInfo.InvariantCulture), problem.Message, StringComparison.Ordinal);
    }
}
