using System.Diagnostics;
using System.IO.Pipes;
using System.Reflection;
using APThermo.Harness;
using Xunit.Abstractions;

namespace APThermo.Fixtures.Tests;

/// <summary>Identity and preflight controls for the process owner's pre-connect phase.</summary>
[Trait("Category", "EndToEnd")]
public sealed class OwnerIdentityTests(ITestOutputHelper output) : IDisposable
{
    private readonly DotnetProcessGuardSession _session = new(output);
    private static readonly Assembly OwnerAssembly = Assembly.Load("APThermo.Harness.ProcessOwner");
    private static readonly Type Lifetime = OwnerAssembly.GetType("APThermo.Harness.ProcessOwner.ParentLifetime", true)!;

    /// <summary>An incorrect birth identity is rejected by the actual lifetime observer.</summary>
    [Fact]
    public void WrongBirthFailsClosed()
    {
        var birth = (ulong)Lifetime.GetMethod("SelfBirth", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, null)!;
        var failure = Assert.Throws<TargetInvocationException>(() => Open(Environment.ProcessId, birth + 1));
        _ = Assert.IsType<InvalidOperationException>(failure.InnerException);
    }

    /// <summary>The real bootstrap rejects a wrong birth or absent parent before connecting or starting a target.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BootstrapRejectsInvalidParent(bool missing)
    {
        var birth = (ulong)Lifetime.GetMethod("SelfBirth", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, null)!;
        var marker = Path.Combine(Path.GetTempPath(), "apt-invalid-" + Guid.NewGuid().ToString("N"));
        var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, RedirectStandardError = true };
        start.ArgumentList.Add(DotnetProcess.BuiltAssemblyPath("APThermo.Harness.ProcessOwner", "tests", "Harness", "ProcessOwner"));
        start.ArgumentList.Add("--channel");
        start.ArgumentList.Add("apt-absent-" + Guid.NewGuid().ToString("N"));
        start.ArgumentList.Add("--parent-pid");
        start.ArgumentList.Add((missing ? int.MaxValue : Environment.ProcessId).ToString(System.Globalization.CultureInfo.InvariantCulture));
        start.ArgumentList.Add("--parent-birth");
        start.ArgumentList.Add((birth + 1).ToString(System.Globalization.CultureInfo.InvariantCulture));
        start.ArgumentList.Add("--");
        start.ArgumentList.Add("python");
        start.ArgumentList.Add("-c");
        start.ArgumentList.Add("import sys;open(sys.argv[1],'w').close()");
        start.ArgumentList.Add(marker);
        using var owner = Process.Start(start) ?? throw new InvalidOperationException();
        await owner.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(true);
        Assert.NotEqual(0, owner.ExitCode);
        Assert.False(File.Exists(marker));
    }

    /// <summary>Unavailable safe signaling and cancellation during preflight never launch the owner.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedPreflightNeverSpawns(bool cancel)
    {
        if (!OperatingSystem.IsLinux()) { Assert.True(OperatingSystem.IsWindows()); return; }
        var target = new ProcessStartInfo("python") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        var type = typeof(OwnedProcess).Assembly.GetType("APThermo.Harness.OwnedProcessOperation", true)!;
        var operation = type.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Single().Invoke([target, null, null, null, null, null]);
        using var cancellation = new CancellationTokenSource();
        var spawned = false;
        var launched = false;
        Action<Process> observeLaunch = _ => launched = true;
        Func<bool> capability = () => { if (cancel) { cancellation.Cancel(); } return cancel; };
        Func<ProcessStartInfo, ProcessStartInfo> start = info => { spawned = true; return info; };
        type.GetProperty("CapabilityOverride", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(operation, capability);
        type.GetProperty("OwnerStartOverride", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(operation, start);
        type.GetProperty("OwnerLaunched", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(operation, observeLaunch);
        var task = (Task<DotnetProcessResult>)type.GetMethod("RunAsync", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(operation, [cancellation.Token])!;
        if (cancel)
        {
            var problem = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task).ConfigureAwait(true);
            Assert.Equal(cancellation.Token, problem.CancellationToken);
        }
        else { _ = await Assert.ThrowsAsync<PlatformNotSupportedException>(() => task).ConfigureAwait(true); }
        Assert.False(spawned);
        Assert.False(launched);
    }

    /// <summary>The connected monitor observes the death of the exact retained parent identity.</summary>
    [Fact]
    public async Task ParentDeathInterruptsPendingConnection()
    {
        var (arguments, environment) = _session.Input("controlled");
        var marker = Path.Combine(_session.DirectoryPath, "ready.pid");
        var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, WorkingDirectory = _session.DirectoryPath };
        start.ArgumentList.Add(_session.AssemblyPath);
        foreach (var argument in arguments) { start.ArgumentList.Add(argument); }
        foreach (var pair in environment) { start.Environment[pair.Key] = pair.Value; }
        using var parent = Process.Start(start) ?? throw new InvalidOperationException();
        try
        {
            await DotnetProcessGuardSession.WaitForOwnedReadyAsync(marker).ConfigureAwait(true);
            var birth = Birth(parent);
            using var lifetime = (IDisposable)Open(parent.Id, birth);
            using var pipe = new NamedPipeClientStream(".", "apt-absent-" + Guid.NewGuid().ToString("N"), PipeDirection.InOut, PipeOptions.Asynchronous);
            var program = OwnerAssembly.GetType("APThermo.Harness.ProcessOwner.Program", true)!;
            var pending = (Task)program.GetMethod("ConnectWhileParentLivesAsync", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [pipe, lifetime])!;
            await File.WriteAllTextAsync(marker + ".release", "release").ConfigureAwait(true);
            await parent.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(true);
            _ = await Assert.ThrowsAsync<InvalidOperationException>(() => pending.WaitAsync(TimeSpan.FromSeconds(5))).ConfigureAwait(true);
            _ = Assert.ThrowsAny<TargetInvocationException>(() => Open(parent.Id, birth));
        }
        finally
        {
            if (!parent.HasExited) { await File.WriteAllTextAsync(marker + ".release", "release").ConfigureAwait(true); }
            await parent.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(true);
        }
    }

    /// <summary>An owner already exited before connecting leaves no spurious pipe cleanup failure.</summary>
    [Fact]
    public async Task SetupFailureAfterOwnerExitDoesNotReportPipeFailure()
    {
        var (arguments, environment) = _session.Input("echo");
        var target = new ProcessStartInfo("python") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        var type = typeof(OwnedProcess).Assembly.GetType("APThermo.Harness.OwnedProcessOperation", true)!;
        var operation = type.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Single().Invoke([target, null, null, null, null, null]);
        Func<ProcessStartInfo, ProcessStartInfo> substitute = _ =>
        {
            var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
            start.ArgumentList.Add(_session.AssemblyPath);
            foreach (var argument in arguments) { start.ArgumentList.Add(argument); }
            foreach (var pair in environment) { start.Environment[pair.Key] = pair.Value; }
            return start;
        };
        Action<Process> fail = owner => { Assert.True(owner.WaitForExit(5000)); throw new InvalidOperationException("controlled exited owner"); };
        type.GetProperty("OwnerStartOverride", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(operation, substitute);
        type.GetProperty("SetupProbe", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(operation, fail);
        var pending = (Task<DotnetProcessResult>)type.GetMethod("RunAsync", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(operation, [CancellationToken.None])!;
        var problem = await Assert.ThrowsAsync<InvalidOperationException>(() => pending.WaitAsync(TimeSpan.FromSeconds(10))).ConfigureAwait(true);
        Assert.Equal("controlled exited owner", problem.Message);
        Assert.Null(problem.Data["CleanupException"]);
        Assert.Equal(await File.ReadAllBytesAsync(Path.Combine(_session.DataPath, "stdout.bin")).ConfigureAwait(true), Assert.IsType<byte[]>(problem.Data["StandardOutput"]));
        Assert.Equal(await File.ReadAllBytesAsync(Path.Combine(_session.DataPath, "stderr.bin")).ConfigureAwait(true), Assert.IsType<byte[]>(problem.Data["StandardError"]));
    }

    /// <summary>An actual bootstrap exits by rendezvous after post-spawn setup fails, before START.</summary>
    [Fact]
    public async Task SetupFailureRendezvousStopsRealBootstrap()
    {
        var marker = Path.Combine(Path.GetTempPath(), "apt-setup-" + Guid.NewGuid().ToString("N"));
        var target = new ProcessStartInfo("python") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        target.ArgumentList.Add("-c");
        target.ArgumentList.Add("import sys;open(sys.argv[1],'w').close()");
        target.ArgumentList.Add(marker);
        var type = typeof(OwnedProcess).Assembly.GetType("APThermo.Harness.OwnedProcessOperation", true)!;
        var operation = type.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Single().Invoke([target, null, null, null, null, null]);
        Process? captured = null;
        string? channel = null;
        Func<ProcessStartInfo, ProcessStartInfo> remember = info => { channel = info.ArgumentList[2]; return info; };
        Action<Process> fail = owner => { captured = Process.GetProcessById(owner.Id); _ = captured.SafeHandle; throw new InvalidOperationException("controlled identity failure"); };
        type.GetProperty("OwnerStartOverride", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(operation, remember);
        type.GetProperty("SetupProbe", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(operation, fail);
        var pending = (Task<DotnetProcessResult>)type.GetMethod("RunAsync", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(operation, [CancellationToken.None])!;
        try
        {
            var problem = await Assert.ThrowsAsync<InvalidOperationException>(() => pending.WaitAsync(TimeSpan.FromSeconds(10))).ConfigureAwait(true);
            Assert.Equal("controlled identity failure", problem.Message);
            Assert.Null(problem.Data["CleanupException"]);
            Assert.Empty(Assert.IsType<byte[]>(problem.Data["StandardOutput"]));
            _ = Assert.IsType<byte[]>(problem.Data["StandardError"]);
            Assert.NotNull(captured);
            Assert.True(captured.HasExited);
            Assert.False(File.Exists(marker));
        }
        finally { if (captured is not null) { await StopOwnedProbeAsync(captured, channel!).ConfigureAwait(true); } }
    }

    /// <summary>A wrong channel cannot signal a retained Linux identity.</summary>
    [Fact]
    public async Task LinuxNonceMismatchDoesNotSignal()
    {
        if (!OperatingSystem.IsLinux()) { Assert.True(OperatingSystem.IsWindows()); return; }
        var (arguments, environment) = _session.Input("controlled");
        var marker = Path.Combine(_session.DirectoryPath, "ready.pid");
        var channel = "apt-test-" + Guid.NewGuid().ToString("N");
        var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, WorkingDirectory = _session.DirectoryPath };
        start.ArgumentList.Add(_session.AssemblyPath);
        foreach (var argument in arguments) { start.ArgumentList.Add(argument); }
        start.ArgumentList.Add("--channel");
        start.ArgumentList.Add(channel);
        foreach (var pair in environment) { start.Environment[pair.Key] = pair.Value; }
        using var child = Process.Start(start) ?? throw new InvalidOperationException();
        try
        {
            await DotnetProcessGuardSession.WaitForOwnedReadyAsync(marker).ConfigureAwait(true);
            var identityType = OwnerAssembly.GetType("APThermo.Harness.ProcessOwner.LinuxProcessIdentity", true)!;
            using var identity = (IDisposable)identityType.GetMethod("Open", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [child.Id])!;
            var validate = identityType.GetMethod("RequireChannel", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var failure = Assert.Throws<TargetInvocationException>(() => validate.Invoke(identity, [channel + "-wrong"]));
            _ = Assert.IsType<InvalidOperationException>(failure.InnerException);
            Assert.False(child.HasExited);
            _ = validate.Invoke(identity, [channel]);
            Assert.True((bool)identityType.GetMethod("Send", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(identity, [9])!);
            await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(true);
            Assert.False((bool)identityType.GetMethod("Send", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(identity, [0])!);
        }
        finally
        {
            if (!child.HasExited) { await File.WriteAllTextAsync(marker + ".release", "release").ConfigureAwait(true); }
            await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(true);
        }
    }

    /// <summary>Releases the isolated test directory.</summary>
    public void Dispose() => _session.Dispose();

    private static async Task StopOwnedProbeAsync(Process process, string channel)
    {
        using (process)
        {
            if (!process.HasExited && OperatingSystem.IsWindows()) { process.Kill(); }
            else if (!process.HasExited)
            {
                var type = OwnerAssembly.GetType("APThermo.Harness.ProcessOwner.LinuxProcessIdentity", true)!;
                using var identity = (IDisposable)type.GetMethod("Open", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [process.Id])!;
                _ = type.GetMethod("RequireChannel", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(identity, [channel]);
                _ = type.GetMethod("Send", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(identity, [9]);
            }
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(true);
        }
    }

    private static object Open(int pid, ulong birth) => Lifetime.GetMethod("Open", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [pid, birth])!;

    private static ulong Birth(Process process)
    {
        if (OperatingSystem.IsWindows()) { return checked((ulong)process.StartTime.ToUniversalTime().ToFileTimeUtc()); }
        var stat = File.ReadAllText($"/proc/{process.Id}/stat");
        return ulong.Parse(stat[(stat.LastIndexOf(')') + 2)..].Split(' ', StringSplitOptions.RemoveEmptyEntries)[19], System.Globalization.CultureInfo.InvariantCulture);
    }
}
