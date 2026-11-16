using System.Diagnostics;
using System.Net.Sockets;
using System.Reflection;
using APThermo.Harness;
using Xunit.Abstractions;

namespace APThermo.Fixtures.Tests;

/// <summary>Controls the timing and identity of setup connection failures.</summary>
[Trait("Category", "EndToEnd")]
public sealed class OwnerSocketRecoveryTests(ITestOutputHelper output) : IDisposable
{
    private readonly DotnetProcessGuardSession _session = new(output);

    /// <summary>Only an aborted pending connection after intentional close is expected.</summary>
    [Theory]
    [InlineData(true, SocketError.OperationAborted, false)]
    [InlineData(false, SocketError.OperationAborted, true)]
    [InlineData(false, SocketError.ConnectionRefused, false)]
    public async Task SetupConnectionFailureKeepsItsOrdering(bool preexisting, SocketError code, bool expected)
    {
        var (arguments, environment) = _session.Input("echo");
        var target = new ProcessStartInfo("python") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        var type = typeof(OwnedProcess).Assembly.GetType("APThermo.Harness.OwnedProcessOperation", true)!;
        var operation = type.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Single().Invoke([target, null, null, null, null, null]);
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var failure = new SocketException((int)code);
        if (preexisting) { completion.SetException(failure); }
        Func<CancellationToken, Task> wait = _ => completion.Task;
        var closed = false;
        Action afterClose = () => { closed = true; _ = completion.TrySetException(failure); };
        Action<Process> setup = owner => { Assert.True(owner.WaitForExit(5000)); throw new InvalidOperationException("controlled setup failure"); };
        Func<ProcessStartInfo, ProcessStartInfo> start = _ => ProbeStart(arguments, environment);
        Set(type, operation, "SetupConnectionOverride", wait);
        Set(type, operation, "SetupPipeClosed", afterClose);
        Set(type, operation, "SetupProbe", setup);
        Set(type, operation, "OwnerStartOverride", start);
        var pending = (Task<DotnetProcessResult>)type.GetMethod("RunAsync", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(operation, [CancellationToken.None])!;
        var problem = await Assert.ThrowsAsync<InvalidOperationException>(() => pending.WaitAsync(TimeSpan.FromSeconds(10))).ConfigureAwait(true);
        Assert.Equal("controlled setup failure", problem.Message);
        Assert.Equal(!preexisting, closed);
        if (expected) { Assert.Null(problem.Data["CleanupException"]); }
        else
        {
            var aggregate = Assert.IsType<AggregateException>(problem.Data["CleanupException"]);
            Assert.Same(failure, Assert.Single(aggregate.InnerExceptions));
        }
    }

    /// <summary>Disposes the owned finite probe session.</summary>
    public void Dispose() => _session.Dispose();

    private static void Set(Type type, object instance, string property, object value) =>
        type.GetProperty(property, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(instance, value);

    private ProcessStartInfo ProbeStart(IReadOnlyList<string> arguments, IReadOnlyDictionary<string, string> environment)
    {
        var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add(_session.AssemblyPath);
        foreach (var argument in arguments) { start.ArgumentList.Add(argument); }
        foreach (var pair in environment) { start.Environment[pair.Key] = pair.Value; }
        return start;
    }
}
