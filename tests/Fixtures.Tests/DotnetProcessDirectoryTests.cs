using System.Diagnostics;
using APThermo.Harness;
using Xunit.Abstractions;

namespace APThermo.Fixtures.Tests;

/// <summary>Real owned process controls for temporary-directory retirement.</summary>
[Trait("Category", "EndToEnd")]
public sealed class DotnetProcessDirectoryTests(ITestOutputHelper output) : IDisposable
{
    private readonly DotnetProcessGuardSession _session = new(output);
    private Process? _child;

    /// <summary>A real Windows CWD sharing violation clears after controlled child exit.</summary>
    [Fact]
    public async Task SharingViolationClearsAfterOwnedChildRelease()
    {
        var marker = StartControlledChild();
        await DotnetProcessGuardSession.WaitForOwnedReadyAsync(marker).ConfigureAwait(true);
        Assert.False(_child!.HasExited);
        if (!OperatingSystem.IsWindows())
        {
            DotnetProcessGuardSession.DeleteOwnedDirectory(_session.DirectoryPath, DotnetProcess.CleanupTimeout);
            Assert.False(Directory.Exists(_session.DirectoryPath));
            Assert.False(_child.HasExited);
            return;
        }
        var observations = 0;
        DotnetProcessGuardSession.DeleteOwnedDirectory(_session.DirectoryPath, DotnetProcess.CleanupTimeout, () =>
        {
            observations++;
            File.WriteAllText(marker + ".release", "release");
            Assert.True(_child.WaitForExit(5000), "the owned child did not exit after release");
        });
        Assert.Equal(1, observations);
        Assert.True(_child.HasExited);
        Assert.False(Directory.Exists(_session.DirectoryPath));
    }

    /// <summary>A held Windows CWD remains an explicit error after the finite budget.</summary>
    [Fact]
    public async Task HeldSharingViolationRemainsAnErrorAtTheDeadline()
    {
        var marker = StartControlledChild();
        await DotnetProcessGuardSession.WaitForOwnedReadyAsync(marker).ConfigureAwait(true);
        Assert.False(_child!.HasExited);
        if (!OperatingSystem.IsWindows())
        {
            DotnetProcessGuardSession.DeleteOwnedDirectory(_session.DirectoryPath, DotnetProcess.CleanupTimeout);
            Assert.False(Directory.Exists(_session.DirectoryPath));
            Assert.False(_child.HasExited);
            return;
        }
        var budget = TimeSpan.FromMilliseconds(150);
        var watch = Stopwatch.StartNew();
        var error = Assert.Throws<IOException>(() => DotnetProcessGuardSession.DeleteOwnedDirectory(_session.DirectoryPath, budget));
        Assert.Equal(unchecked((int)0x80070020), error.HResult);
        Assert.True(watch.Elapsed >= budget, "a live sharing violation was not observed through the deadline");
        Assert.False(_child.HasExited);
        Assert.True(Directory.Exists(_session.DirectoryPath));
    }

    /// <summary>A non-sharing I/O error is propagated without invoking the retry observation.</summary>
    [Fact]
    public void NonSharingIoErrorIsNotRetriedOrIgnored()
    {
        _ = _session.Input("echo");
        var file = Path.Combine(_session.DirectoryPath, "not-a-directory");
        File.WriteAllText(file, "owned test file");
        var observations = 0;
        var error = Assert.ThrowsAny<IOException>(() => DotnetProcessGuardSession.DeleteOwnedDirectory(file,
            DotnetProcess.CleanupTimeout, () => observations++));
        Assert.NotEqual(unchecked((int)0x80070020), error.HResult);
        Assert.Equal(0, observations);
        Assert.True(File.Exists(file));
    }
    /// <summary>A missing nested path is not mistaken for absence of the still-present owned root.</summary>
    [Fact]
    public void NestedDisappearanceIsNotIgnoredWhileOwnedRootExists()
    {
        _ = _session.Input("echo");
        var absent = Path.Combine(_session.DirectoryPath, "already-absent");
        DotnetProcessGuardSession.DeleteOwnedDirectory(absent, DotnetProcess.CleanupTimeout);
        Assert.False(Directory.Exists(absent));
        _ = Assert.Throws<DirectoryNotFoundException>(() => DotnetProcessGuardSession.DeleteOwnedDirectory(
            _session.DirectoryPath, DotnetProcess.CleanupTimeout, deleteDirectory: _ => throw new DirectoryNotFoundException("nested vanished")));
        Assert.True(Directory.Exists(_session.DirectoryPath));
    }
    /// <summary>Stops only this test's child and retires its private directory.</summary>
    public void Dispose()
    {
        if (_child is not null)
        {
            if (!_child.HasExited) { _child.Kill(entireProcessTree: true); }
            Assert.True(_child.WaitForExit(5000), "the owned child did not terminate during teardown");
            _child.Dispose();
        }
        _session.Dispose();
    }

    private string StartControlledChild()
    {
        var (arguments, environment) = _session.Input("controlled");
        var marker = Path.Combine(_session.DirectoryPath, "directory-lock.pid");
        var start = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = _session.DirectoryPath,
        };
        start.ArgumentList.Add(_session.AssemblyPath);
        foreach (var argument in arguments.Take(arguments.Count - 1)) { start.ArgumentList.Add(argument); }
        start.ArgumentList.Add(marker);
        foreach (var (key, value) in environment) { start.Environment[key] = value; }
        _child = Process.Start(start) ?? throw new InvalidOperationException("owned child did not start");
        return marker;
    }
}
