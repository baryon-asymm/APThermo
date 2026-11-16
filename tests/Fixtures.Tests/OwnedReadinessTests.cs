using System.ComponentModel;
using System.Diagnostics;
using APThermo.Harness;
using Xunit.Abstractions;

namespace APThermo.Fixtures.Tests;

/// <summary>Readiness reporting preserves actual owned-process startup failures.</summary>
[Trait("Category", "EndToEnd")]
public sealed class OwnedReadinessTests(ITestOutputHelper output) : IDisposable
{
    private readonly DotnetProcessGuardSession _session = new(output);

    /// <summary>An actual failed target launch surfaces its original error before readiness expires.</summary>
    [Fact]
    public async Task EarlyTargetFailureIsNotReadinessTimeout()
    {
        _ = Directory.CreateDirectory(_session.DirectoryPath);
        var marker = Path.Combine(_session.DirectoryPath, "never-ready");
        var start = new ProcessStartInfo(Path.Combine(_session.DirectoryPath, "missing-executable"))
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = _session.DirectoryPath,
        };
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var pending = OwnedProcess.RunAsync(start, timeout.Token);
        var watch = Stopwatch.StartNew();
        var reported = await Assert.ThrowsAsync<Win32Exception>(() =>
            DotnetProcessGuardSession.WaitForMarkerOrFailureAsync(marker, pending, watch)).ConfigureAwait(true);
        var original = await Assert.ThrowsAsync<Win32Exception>(() => pending).ConfigureAwait(true);
        Assert.Same(original, reported);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(5));
        Assert.False(File.Exists(marker));
    }

    /// <summary>Removes the isolated test files.</summary>
    public void Dispose() => _session.Dispose();
}
