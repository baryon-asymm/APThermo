using System.ComponentModel;
using System.Diagnostics;
using System.IO.Pipes;
using APThermo.Harness.ProcessOwner;

namespace APThermo.Harness;

/// <summary>Runs a process inside a scope established before its creation.</summary>
public static class OwnedProcess
{
    /// <summary>Runs the supplied executable and preserves its raw streams under the caller's deadline.</summary>
    /// <param name="target">Executable, structured arguments, environment and working directory.</param>
    /// <param name="interruption">The caller's already-armed deadline or cancellation.</param>
    /// <param name="started">Optional notification of the actual target PID, not the owner PID.</param>
    /// <returns>The target exit code and exact output and error bytes.</returns>
    public static Task<DotnetProcessResult> RunAsync(ProcessStartInfo target, CancellationToken interruption, Action<int>? started = null)
    {
        ArgumentNullException.ThrowIfNull(target);
        interruption.ThrowIfCancellationRequested();
        return target.UseShellExecute || !target.RedirectStandardOutput || !target.RedirectStandardError ||
            target.RedirectStandardInput || target.Arguments.Length != 0
            ? throw new ArgumentException("the target must use structured arguments, inherited stdin and redirected output", nameof(target))
            : new OwnedProcessOperation(target, started).RunAsync(interruption);
    }
}

/// <summary>One isolated owner, control channel and captured target output.</summary>
internal sealed class OwnedProcessOperation(ProcessStartInfo target, Action<int>? started, Action? cleanupProbe = null, Action<Stream>? controlProbe = null, Func<Task>? captureGate = null, Action? drainWaiting = null)
{
    private readonly string _name = "apt-" + Guid.NewGuid().ToString("N");
    private readonly ProcessStartInfo _target = target;
    private readonly Action<int>? _started = started;
    private bool _startMayHaveBeenSent;
    private LinuxProcessIdentity? _linuxOwner;
    private readonly List<Exception> _secondary = [];

    internal Func<ProcessStartInfo, ProcessStartInfo>? OwnerStartOverride { get; set; }
    internal Func<Stream, CancellationToken, Task>? StartWriteOverride { get; set; }
    internal Action<Process>? OwnerLaunched { get; set; }
    internal Func<bool>? CapabilityOverride { get; set; }
    internal Action<Process>? SetupProbe { get; set; }

    internal async Task<DotnetProcessResult> RunAsync(CancellationToken interruption)
    {
        var secondary = _secondary;
        DotnetProcessResult? result = null;
        Exception? primary = null;
        try { result = await RunResourcesAsync(secondary, interruption).ConfigureAwait(false); }
        catch (Exception problem) when (Record(problem, null)) { primary = problem; }
        if (primary is not null)
        {
            if (secondary.Count != 0) { primary.Data["CleanupException"] = new AggregateException(secondary); }
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(primary).Throw();
        }
        return secondary.Count != 0 ? throw new AggregateException("process cleanup failed", secondary) : result!;
    }

    private async Task<DotnetProcessResult> RunResourcesAsync(List<Exception> secondary, CancellationToken interruption)
    {
        using var probeScope = new OwnedResource<OwnedCleanupProbe>(() => new OwnedCleanupProbe(cleanupProbe), secondary);
        using var jobScope = new OwnedResource<WindowsJob>(() => OperatingSystem.IsWindows() ? WindowsJob.Create(_name) : null, secondary);
        using var pipeScope = new OwnedResource<NamedPipeServerStream>(() => new NamedPipeServerStream(_name, PipeDirection.InOut, 1,
            PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly), secondary);
        if (OperatingSystem.IsLinux())
        {
            if (CapabilityOverride?.Invoke() == false) { throw new PlatformNotSupportedException("safe Linux signalling is unavailable"); }
            LinuxProcessIdentity.Probe();
        }
        var parentBirth = ParentLifetime.SelfBirth();
        interruption.ThrowIfCancellationRequested();
        using var ownerScope = new OwnedResource<Process>(() => Process.Start(SelectOwnerStart(parentBirth)) ??
            throw new InvalidOperationException("process owner did not start"), secondary);
        return await RunAcquiredAsync(ownerScope.Value!, pipeScope.Value!, jobScope.Value, secondary, interruption).ConfigureAwait(false);
    }

    private async Task<DotnetProcessResult> RunAcquiredAsync(Process owner, NamedPipeServerStream pipe,
        WindowsJob? job, List<Exception> secondary, CancellationToken interruption)
    {
        OwnedResource<LinuxProcessIdentity>? identityScope = null;
        OwnedResource<ProcessOutput>? outputScope = null;
        OwnedResource<ProcessOutput>? errorScope = null;
        var setupComplete = false;
        try
        {
            _ = owner.SafeHandle;
            outputScope = new OwnedResource<ProcessOutput>(() => new ProcessOutput(owner.StandardOutput.BaseStream, captureGate), secondary);
            errorScope = new OwnedResource<ProcessOutput>(() => new ProcessOutput(owner.StandardError.BaseStream), secondary);
            SetupProbe?.Invoke(owner);
            identityScope = new OwnedResource<LinuxProcessIdentity>(() => CaptureLinuxOwner(owner), secondary);
            _linuxOwner = identityScope.Value;
            setupComplete = true;
            return await RunAttachedAsync(pipe, owner, job, outputScope.Value!, errorScope.Value!, interruption).ConfigureAwait(false);
        }
        catch (Exception problem) when (!setupComplete)
        {
            outputScope ??= RecoverOutput(owner, false, secondary);
            errorScope ??= RecoverOutput(owner, true, secondary);
            await RecoverSetupAsync(pipe, owner, outputScope?.Value, errorScope?.Value, secondary).ConfigureAwait(false);
            problem.Data["StandardOutput"] = outputScope?.Value?.Snapshot() ?? [];
            problem.Data["StandardError"] = errorScope?.Value?.Snapshot() ?? [];
            throw;
        }
        finally
        {
            identityScope?.Dispose();
            errorScope?.Dispose();
            outputScope?.Dispose();
        }
    }

    private async Task<DotnetProcessResult> RunAttachedAsync(NamedPipeServerStream pipe, Process owner, WindowsJob? job,
        ProcessOutput output, ProcessOutput error, CancellationToken interruption)
    {
        var authorizedStop = false;
        try
        {
            OwnerLaunched?.Invoke(owner);
            var exitCode = await RunOwnedAsync(pipe, owner, output, error, interruption).ConfigureAwait(false);
            await Wire.SendAsync(pipe, Wire.Release, ReadOnlyMemory<byte>.Empty, interruption).ConfigureAwait(false);
            authorizedStop = true;
            job?.Stop();
            await owner.WaitForExitAsync(interruption).ConfigureAwait(false);
            if (OperatingSystem.IsLinux() && owner.ExitCode != 137)
            {
                throw new InvalidOperationException($"process owner exited without group SIGKILL: {owner.ExitCode}");
            }
            await WaitForScopeEmptyAsync(owner.Id, job, interruption).ConfigureAwait(false);
            return new DotnetProcessResult(exitCode, output.Snapshot(), error.Snapshot());
        }
        catch (Exception problem)
        {
            try
            {
                var cleanup = await CleanupAsync(pipe, owner, job, output, error, authorizedStop).ConfigureAwait(false);
                if (cleanup is not null) { _secondary.Add(cleanup); }
            }
            catch (Exception cleanup) when (Record(cleanup, _secondary)) { }
            problem.Data["StandardOutput"] = output.Snapshot();
            problem.Data["StandardError"] = error.Snapshot();
            throw;
        }
    }

    private static OwnedResource<ProcessOutput>? RecoverOutput(Process owner, bool error, List<Exception> secondary)
    {
        try
        {
            return new OwnedResource<ProcessOutput>(() => new ProcessOutput(error
                ? owner.StandardError.BaseStream : owner.StandardOutput.BaseStream), secondary);
        }
        catch (Exception problem) when (Record(problem, secondary)) { return null; }
    }

    private static async Task RecoverSetupAsync(NamedPipeServerStream pipe, Process owner, ProcessOutput? output,
        ProcessOutput? error, List<Exception> secondary)
    {
        using var timeout = new CancellationTokenSource(DotnetProcess.CleanupTimeout);
        try
        {
            var connection = pipe.WaitForConnectionAsync(timeout.Token);
            var exited = owner.WaitForExitAsync(timeout.Token);
            var first = await Task.WhenAny(connection, exited).ConfigureAwait(false);
            if (first == connection) { await connection.ConfigureAwait(false); }
            await pipe.DisposeAsync().ConfigureAwait(false);
            if (first != connection)
            {
                try { await connection.ConfigureAwait(false); }
                catch (Exception problem) when (problem is ObjectDisposedException or OperationCanceledException) { }
            }
            await exited.ConfigureAwait(false);
        }
        catch (Exception problem) when (Record(problem, secondary)) { }
        try
        {
            var streams = new[] { output?.Completion, error?.Completion }.OfType<Task>();
            await Task.WhenAll(streams).WaitAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (Exception problem) when (Record(problem, secondary)) { }
    }

    private static bool Record(Exception problem, List<Exception>? failures)
    {
        failures?.Add(problem);
        return true;
    }

    private async Task<int> RunOwnedAsync(NamedPipeServerStream pipe, Process owner, ProcessOutput output,
        ProcessOutput error, CancellationToken interruption)
    {
        var connect = pipe.WaitForConnectionAsync(interruption);
        var ownerExit = owner.WaitForExitAsync(CancellationToken.None);
        if (await Task.WhenAny(connect, ownerExit).WaitAsync(interruption).ConfigureAwait(false) == ownerExit)
        {
            throw new InvalidOperationException("process owner exited before connecting");
        }
        await connect.ConfigureAwait(false);
        _ = await ExpectAsync(pipe, Wire.Ready, 0, interruption).ConfigureAwait(false);
        interruption.ThrowIfCancellationRequested();
        _startMayHaveBeenSent = true;
        if (StartWriteOverride is null) { await Wire.SendAsync(pipe, Wire.Start, ReadOnlyMemory<byte>.Empty, interruption).ConfigureAwait(false); }
        else { await StartWriteOverride(pipe, interruption).ConfigureAwait(false); }
        var pidBytes = await ExpectAsync(pipe, Wire.Started, sizeof(int), interruption).ConfigureAwait(false);
        var pid = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(pidBytes);
        if (pid <= 0) { throw new InvalidDataException("invalid target PID"); }
        _started?.Invoke(pid);
        var status = await ExpectAsync(pipe, Wire.Exit, sizeof(int), interruption).ConfigureAwait(false);
        var exitCode = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(status);
        var drained = Task.WhenAll(output.Completion, error.Completion);
        if (await Task.WhenAny(drained, ownerExit).WaitAsync(interruption).ConfigureAwait(false) == ownerExit)
        {
            throw new InvalidOperationException("process owner exited before authorized cleanup");
        }
        await drained.WaitAsync(interruption).ConfigureAwait(false);
        return exitCode;
    }

    private static async Task<byte[]> ExpectAsync(Stream pipe, byte expected, int length, CancellationToken token)
    {
        var (kind, payload) = await Wire.ReadAsync(pipe, token).ConfigureAwait(false);
        if (kind == Wire.Error && payload.Length == 5)
        {
            var code = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(1));
            if (payload[0] == 2) { throw new Win32Exception(code, "target process could not start"); }
            throw new InvalidOperationException($"process owner failed at stage {payload[0]} (error {code})");
        }
        return kind != expected || payload.Length != length
            ? throw new InvalidDataException($"expected process-owner message {expected}") : payload;
    }

    private ProcessStartInfo SelectOwnerStart(ulong parentBirth)
    {
        var start = OwnerStart(parentBirth);
        return OwnerStartOverride is null ? start : OwnerStartOverride(start);
    }

    private ProcessStartInfo OwnerStart(ulong parentBirth)
    {
        var path = DotnetProcess.BuiltAssemblyPath("APThermo.Harness.ProcessOwner", "tests", "Harness", "ProcessOwner");
        var start = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = _target.WorkingDirectory,
        };
        start.Environment.Clear();
        foreach (var pair in _target.Environment) { start.Environment[pair.Key] = pair.Value; }
        start.ArgumentList.Add(path);
        start.ArgumentList.Add("--channel");
        start.ArgumentList.Add(_name);
        start.ArgumentList.Add("--parent-pid");
        start.ArgumentList.Add(Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        start.ArgumentList.Add("--parent-birth");
        start.ArgumentList.Add(parentBirth.ToString(System.Globalization.CultureInfo.InvariantCulture));
        if (OperatingSystem.IsWindows()) { start.ArgumentList.Add("--job-name"); start.ArgumentList.Add(_name); }
        start.ArgumentList.Add("--");
        start.ArgumentList.Add(_target.FileName);
        foreach (var argument in _target.ArgumentList) { start.ArgumentList.Add(argument); }
        return start;
    }

    private LinuxProcessIdentity? CaptureLinuxOwner(Process owner)
    {
        if (!OperatingSystem.IsLinux()) { return null; }
        var identity = LinuxProcessIdentity.Open(owner.Id);
        try { identity.RequireChannel(_name); return identity; }
        catch { identity.Dispose(); throw; }
    }

    private static async Task WaitForScopeEmptyAsync(int identity, WindowsJob? job, CancellationToken token)
    {
        while (OperatingSystem.IsWindows() ? job!.ActiveProcesses() != 0 : OwnedProcessGroup.HasLiveMembers(identity))
        {
            await Task.Delay(10, token).ConfigureAwait(false);
        }
    }

    private async Task<Exception?> StopBeforeStartAsync(NamedPipeServerStream pipe, Process owner, WindowsJob? job, CancellationToken token)
    {
        Exception? closeFailure = null;
        try { await pipe.DisposeAsync().ConfigureAwait(false); }
        catch (Exception problem) when (Record(problem, null)) { closeFailure = problem; }
        try
        {
            if (OperatingSystem.IsLinux()) { _ = _linuxOwner!.Send(9); }
            else if (!owner.HasExited) { owner.Kill(); }
        }
        catch (InvalidOperationException) when (owner.HasExited) { }
        job?.Stop();
        await owner.WaitForExitAsync(token).ConfigureAwait(false);
        if (job is not null) { await WaitForScopeEmptyAsync(owner.Id, job, token).ConfigureAwait(false); }
        return closeFailure;
    }

    private async Task<Exception?> CleanupAsync(NamedPipeServerStream pipe, Process owner, WindowsJob? job,
        ProcessOutput output, ProcessOutput error, bool stopping)
    {
        using var timeout = new CancellationTokenSource(DotnetProcess.CleanupTimeout);
        Exception? controlFailure = null;
        try
        {
            controlProbe?.Invoke(pipe);
            if (!_startMayHaveBeenSent)
            {
                controlFailure = await StopBeforeStartAsync(pipe, owner, job, timeout.Token).ConfigureAwait(false);
            }
            else if (!stopping && pipe.IsConnected)
            {
                try { await Wire.SendAsync(pipe, Wire.Terminate, ReadOnlyMemory<byte>.Empty, timeout.Token).ConfigureAwait(false); }
                catch (Exception problem) when (Record(problem, null)) { controlFailure = problem; }
            }
            if (_startMayHaveBeenSent)
            {
                if (job is not null) { job.Stop(); }
                else if (controlFailure is not null || !pipe.IsConnected) { await pipe.DisposeAsync().ConfigureAwait(false); }
                await owner.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
                await WaitForScopeEmptyAsync(owner.Id, job, timeout.Token).ConfigureAwait(false);
            }
        }
        catch (Exception problem) when (Record(problem, null))
        {
            controlFailure = controlFailure is null ? problem : new AggregateException(controlFailure, problem);
        }
        try
        {
            var drainage = Task.WhenAll(output.Completion, error.Completion);
            if (!drainage.IsCompleted) { drainWaiting?.Invoke(); }
            await drainage.WaitAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (Exception problem) when (Record(problem, null))
        {
            controlFailure = controlFailure is null ? problem : new AggregateException(controlFailure, problem);
        }
        return controlFailure;
    }

}

/// <summary>Disposes one owned resource and records any secondary failure.</summary>
internal sealed class OwnedResource<T>(Func<T?> factory, List<Exception> failures) : IDisposable where T : class, IDisposable
{
    internal T? Value { get; } = factory();

    public void Dispose()
    {
        try { Value?.Dispose(); }
        catch (Exception problem) when (RecordFailure(problem)) { }
    }

    private bool RecordFailure(Exception problem)
    {
        failures.Add(problem);
        return true;
    }
}


/// <summary>Allows a per-operation test to observe a failing disposal without global state.</summary>
internal sealed class OwnedCleanupProbe(Action? action) : IDisposable
{
    public void Dispose() => action?.Invoke();
}
