using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using APThermo.Harness;
using Xunit.Abstractions;

namespace APThermo.Fixtures.Tests;

/// <summary>Actual bootstrap lifetime controls using a separate parent that launches it.</summary>
[Trait("Category", "EndToEnd")]
public sealed class OwnerLifetimeBootstrapTests(ITestOutputHelper output) : IDisposable
{
    private readonly DotnetProcessGuardSession _session = new(output);

    /// <summary>Parent death after identity open stops a bootstrap, also when connection races death.</summary>
    [Fact]
    public async Task ActualParentDeathAfterOpenStopsBootstrap()
    {
        _ = Directory.CreateDirectory(_session.DirectoryPath);
        var channel = "apt-parent-" + Guid.NewGuid().ToString("N");
        var ready = Path.Combine(_session.DirectoryPath, "parent.ready");
        var opened = Path.Combine(_session.DirectoryPath, "opened.ready");
        var target = Path.Combine(_session.DirectoryPath, "target.ready");
        using var job = CreateJob(channel);
        using var parent = StartParent(channel, opened, target, ready);
        Process? owner = null;
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        try
        {
            await WaitForMarkerAsync(ready, deadline.Token).ConfigureAwait(true);
            owner = Process.GetProcessById(int.Parse(await File.ReadAllTextAsync(ready, deadline.Token).ConfigureAwait(true), CultureInfo.InvariantCulture));
            _ = owner.SafeHandle;
            await WaitForMarkerAsync(opened, deadline.Token).ConfigureAwait(true);
            Assert.False(parent.HasExited);
            Assert.False(owner.HasExited);
            await File.WriteAllTextAsync(ready + ".release", "release", deadline.Token).ConfigureAwait(true);
            await parent.WaitForExitAsync(deadline.Token).ConfigureAwait(true);
            await owner.WaitForExitAsync(deadline.Token).ConfigureAwait(true);
            Assert.False(File.Exists(target));
        }
        finally
        {
            if (!parent.HasExited) { await File.WriteAllTextAsync(ready + ".release", "release").ConfigureAwait(true); }
            try { await parent.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(true); }
            finally { if (owner is not null) { await StopOwnerAsync(owner, channel).ConfigureAwait(true); } }
        }
    }

    /// <summary>Real launching-parent exit is ordered before open, before connect, or after connect.</summary>
    [Theory]
    [InlineData("--test-before-parent-open")]
    [InlineData("--test-connect-pending")]
    [InlineData("--test-connected")]
    public async Task ActualParentDeathAtControlledStageFailsClosed(string option)
    {
        _ = Directory.CreateDirectory(_session.DirectoryPath);
        var channel = "apt-stage-" + Guid.NewGuid().ToString("N");
        var ready = Path.Combine(_session.DirectoryPath, "parent.ready");
        var opened = Path.Combine(_session.DirectoryPath, "opened.ready");
        var target = Path.Combine(_session.DirectoryPath, "target.ready");
        var gate = Path.Combine(_session.DirectoryPath, "stage");
        var expires = DateTime.UtcNow.AddSeconds(15);
        using var deadline = new CancellationTokenSource(expires - DateTime.UtcNow);
        using var parent = StartParent(channel, opened, target, ready, [option, gate, expires.Ticks.ToString(CultureInfo.InvariantCulture)]);
        Process? owner = null;
        try
        {
            await WaitForMarkerAsync(ready, deadline.Token).ConfigureAwait(true);
            owner = Process.GetProcessById(int.Parse(await File.ReadAllTextAsync(ready, deadline.Token).ConfigureAwait(true), CultureInfo.InvariantCulture));
            _ = owner.SafeHandle;
            await WaitForMarkerAsync(gate + ".ready", deadline.Token).ConfigureAwait(true);
            Assert.Equal(option != "--test-before-parent-open", File.Exists(opened));
            Assert.False(parent.HasExited);
            await File.WriteAllTextAsync(ready + ".release", "release", deadline.Token).ConfigureAwait(true);
            await parent.WaitForExitAsync(deadline.Token).ConfigureAwait(true);
            await File.WriteAllTextAsync(gate + ".release", "release", deadline.Token).ConfigureAwait(true);
            await owner.WaitForExitAsync(deadline.Token).ConfigureAwait(true);
            Assert.False(File.Exists(target));
        }
        finally
        {
            await File.WriteAllTextAsync(gate + ".release", "release").ConfigureAwait(true);
            if (!parent.HasExited) { await File.WriteAllTextAsync(ready + ".release", "release").ConfigureAwait(true); }
            try { await parent.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(true); }
            finally { if (owner is not null) { await StopOwnerAsync(owner, channel).ConfigureAwait(true); } }
        }
    }

    /// <summary>A connected live parent can release the gate and explicitly ABORT without launching target.</summary>
    [Fact]
    public async Task LiveParentCanAbortAfterConnectedGate()
    {
        _ = Directory.CreateDirectory(_session.DirectoryPath);
        var channel = "apt-positive-" + Guid.NewGuid().ToString("N");
        var ready = Path.Combine(_session.DirectoryPath, "parent.ready");
        var opened = Path.Combine(_session.DirectoryPath, "opened.ready");
        var target = Path.Combine(_session.DirectoryPath, "target.ready");
        var gate = Path.Combine(_session.DirectoryPath, "stage");
        var expires = DateTime.UtcNow.AddSeconds(15);
        using var deadline = new CancellationTokenSource(expires - DateTime.UtcNow);
        using var parent = StartParent(channel, opened, target, ready, ["--test-connected", gate, expires.Ticks.ToString(CultureInfo.InvariantCulture)]);
        Process? owner = null;
        try
        {
            await WaitForMarkerAsync(ready, deadline.Token).ConfigureAwait(true);
            owner = Process.GetProcessById(int.Parse(await File.ReadAllTextAsync(ready, deadline.Token).ConfigureAwait(true), CultureInfo.InvariantCulture));
            _ = owner.SafeHandle;
            await WaitForMarkerAsync(gate + ".ready", deadline.Token).ConfigureAwait(true);
            Assert.False(parent.HasExited);
            await File.WriteAllTextAsync(gate + ".release", "release", deadline.Token).ConfigureAwait(true);
            await File.WriteAllTextAsync(ready + ".abort", "abort", deadline.Token).ConfigureAwait(true);
            await owner.WaitForExitAsync(deadline.Token).ConfigureAwait(true);
            Assert.Equal(0, owner.ExitCode);
            Assert.False(parent.HasExited);
            Assert.False(File.Exists(target));
        }
        finally
        {
            await File.WriteAllTextAsync(gate + ".release", "release").ConfigureAwait(true);
            if (!parent.HasExited) { await File.WriteAllTextAsync(ready + ".release", "release").ConfigureAwait(true); }
            try { await parent.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(true); }
            finally { if (owner is not null) { await StopOwnerAsync(owner, channel).ConfigureAwait(true); } }
        }
    }

    /// <summary>Releases the isolated files.</summary>
    public void Dispose() => _session.Dispose();

    private Process StartParent(string channel, string opened, string target, string ready, IReadOnlyList<string>? gateArguments = null)
    {
        var start = new ProcessStartInfo("dotnet") { UseShellExecute = false };
        start.ArgumentList.Add(_session.AssemblyPath);
        start.ArgumentList.Add("owner-parent");
        start.ArgumentList.Add(DotnetProcess.BuiltAssemblyPath("APThermo.Harness.ProcessOwner", "tests", "Harness", "ProcessOwner"));
        start.ArgumentList.Add(channel);
        start.ArgumentList.Add(opened);
        start.ArgumentList.Add(target);
        start.ArgumentList.Add(ready);
        if (gateArguments is not null) { foreach (var argument in gateArguments) { start.ArgumentList.Add(argument); } }
        return Process.Start(start) ?? throw new InvalidOperationException("parent did not start");
    }

    private static async Task WaitForMarkerAsync(string marker, CancellationToken token)
    {
        while (!File.Exists(marker)) { await Task.Delay(10, token).ConfigureAwait(false); }
    }

    private static IDisposable? CreateJob(string channel)
    {
        if (!OperatingSystem.IsWindows()) { return null; }
        var type = Assembly.Load("APThermo.Harness.ProcessOwner").GetType("APThermo.Harness.ProcessOwner.WindowsJob", true)!;
        return (IDisposable)type.GetMethod("Create", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [channel])!;
    }

    private static async Task StopOwnerAsync(Process owner, string channel)
    {
        using (owner)
        {
            if (!owner.HasExited && OperatingSystem.IsWindows()) { owner.Kill(); }
            else if (!owner.HasExited)
            {
                var type = Assembly.Load("APThermo.Harness.ProcessOwner").GetType("APThermo.Harness.ProcessOwner.LinuxProcessIdentity", true)!;
                using var identity = (IDisposable)type.GetMethod("Open", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [owner.Id])!;
                _ = type.GetMethod("RequireChannel", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(identity, [channel]);
                _ = type.GetMethod("Send", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(identity, [9]);
            }
            await owner.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(true);
        }
    }
}
