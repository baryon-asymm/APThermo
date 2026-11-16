using System.Diagnostics;
using System.Globalization;
using System.IO.Pipes;
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
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActualParentDeathAfterOpenStopsBootstrap(bool raceConnection)
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
            using var server = raceConnection ? new NamedPipeServerStream(channel, PipeDirection.InOut, 1,
                PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly) : null;
            var connection = server?.WaitForConnectionAsync(deadline.Token);
            await File.WriteAllTextAsync(ready + ".release", "release", deadline.Token).ConfigureAwait(true);
            await parent.WaitForExitAsync(deadline.Token).ConfigureAwait(true);
            if (server is not null)
            {
                await server.DisposeAsync().ConfigureAwait(true);
                try { await connection!.ConfigureAwait(true); }
                catch (Exception problem) when (problem is IOException or ObjectDisposedException or OperationCanceledException) { }
            }
            await owner.WaitForExitAsync(deadline.Token).ConfigureAwait(true);
            Assert.False(File.Exists(target));
        }
        finally
        {
            if (!parent.HasExited) { await File.WriteAllTextAsync(ready + ".release", "release").ConfigureAwait(true); }
            await parent.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(true);
            if (owner is not null) { await StopOwnerAsync(owner, channel).ConfigureAwait(true); }
        }
    }

    /// <summary>Releases the isolated files.</summary>
    public void Dispose() => _session.Dispose();

    private Process StartParent(string channel, string opened, string target, string ready)
    {
        var start = new ProcessStartInfo("dotnet") { UseShellExecute = false };
        start.ArgumentList.Add(_session.AssemblyPath);
        start.ArgumentList.Add("owner-parent");
        start.ArgumentList.Add(DotnetProcess.BuiltAssemblyPath("APThermo.Harness.ProcessOwner", "tests", "Harness", "ProcessOwner"));
        start.ArgumentList.Add(channel);
        start.ArgumentList.Add(opened);
        start.ArgumentList.Add(target);
        start.ArgumentList.Add(ready);
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
