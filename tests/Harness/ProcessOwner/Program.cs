using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO.Pipes;
using System.Security.Principal;

namespace APThermo.Harness.ProcessOwner;

/// <summary>Creates owned scope before launching the requested executable.</summary>
internal static class Program
{
    private static async Task<int> Main(string[] arguments)
    {
        var (channel, parent, birth, job, marker, beforeOpen, pending, connected, eofGate, deadline, command) = Parse(arguments);
        using var pipe = new NamedPipeClientStream(".", channel, PipeDirection.InOut,
            PipeOptions.Asynchronous, TokenImpersonationLevel.None, HandleInheritability.None);
        await TestGateAsync(beforeOpen, deadline).ConfigureAwait(false);
        using (var lifetime = ParentLifetime.Open(parent, birth))
        {
            if (marker is not null) { PublishMarker(marker); }
            if (pending is null && connected is null) { await ConnectWhileParentLivesAsync(pipe, lifetime).ConfigureAwait(false); }
            else { await ConnectWithGatesAsync(pipe, lifetime, pending, connected, deadline).ConfigureAwait(false); }
        }
        try
        {
            NativeOwner.Establish(job);
        }
        catch (Exception problem) when (problem is Win32Exception or InvalidOperationException or PlatformNotSupportedException)
        {
            await SendErrorAsync(pipe, 1, problem).ConfigureAwait(false);
            return 1;
        }
        await Wire.SendAsync(pipe, Wire.Ready, ReadOnlyMemory<byte>.Empty, CancellationToken.None).ConfigureAwait(false);
        var (firstKind, firstPayload) = await Wire.ReadAsync(pipe, CancellationToken.None).ConfigureAwait(false);
        if (firstKind == Wire.Abort && firstPayload.Length == 0) { return 0; }
        if (firstKind != Wire.Start || firstPayload.Length != 0)
        {
            await SendErrorAsync(pipe, 3, new InvalidDataException("expected START or ABORT")).ConfigureAwait(false);
            return 1;
        }
        return await StartTargetAsync(pipe, job, command, eofGate, deadline).ConfigureAwait(false);
    }

    private static Task ConnectWhileParentLivesAsync(NamedPipeClientStream pipe, ParentLifetime lifetime) =>
        ConnectWithGatesAsync(pipe, lifetime, null, null, 0);

    private static async Task ConnectWithGatesAsync(NamedPipeClientStream pipe, ParentLifetime lifetime, string? pending, string? connected, long deadline)
    {
        using var stop = new CancellationTokenSource();
        var connect = pipe.ConnectAsync(stop.Token);
        var death = lifetime.WaitForExitAsync(stop.Token);
        try
        {
            await TestGateAsync(pending, deadline).ConfigureAwait(false);
            if (await Task.WhenAny(connect, death).ConfigureAwait(false) == death)
            {
                await death.ConfigureAwait(false);
                throw new InvalidOperationException("parent exited before owner connected");
            }
            await connect.ConfigureAwait(false);
            await TestGateAsync(connected, deadline).ConfigureAwait(false);
            if (!lifetime.IsLive()) { throw new InvalidOperationException("parent exited during connection"); }
        }
        finally
        {
            await stop.CancelAsync().ConfigureAwait(false);
            try { await ObserveAsync(connect, stop.Token).ConfigureAwait(false); }
            finally { await ObserveAsync(death, stop.Token).ConfigureAwait(false); }
        }
    }

    private static async Task ObserveAsync(Task task, CancellationToken stop)
    {
        try { await task.ConfigureAwait(false); }
        catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
    }

    private static async Task<int> StartTargetAsync(Stream pipe, string? job, string[] command, string? eofGate, long deadline)
    {
        var start = new ProcessStartInfo(command[0]) { UseShellExecute = false };
        foreach (var argument in command.Skip(1)) { start.ArgumentList.Add(argument); }
        Process? target;
        try { target = Process.Start(start); }
        catch (Exception problem) when (problem is Win32Exception or InvalidOperationException or ArgumentException)
        {
            await SendErrorAsync(pipe, 2, problem).ConfigureAwait(false);
            return 1;
        }
        if (target is null)
        {
            await SendErrorAsync(pipe, 2, new InvalidOperationException("target did not start")).ConfigureAwait(false);
            return 1;
        }
        using (target)
        {
            try
            {
                await Wire.SendAsync(pipe, Wire.Started, Wire.Code(target.Id), CancellationToken.None).ConfigureAwait(false);
                NativeOwner.CloseOutputs();
                var control = Wire.ReadAsync(pipe, CancellationToken.None);
                var exit = target.WaitForExitAsync();
                if (await Task.WhenAny(control, exit).ConfigureAwait(false) == exit)
                {
                    await exit.ConfigureAwait(false);
                    await Wire.SendAsync(pipe, Wire.Exit, Wire.Code(target.ExitCode), CancellationToken.None).ConfigureAwait(false);
                }
                var (kind, payload) = await control.ConfigureAwait(false);
                if (payload.Length != 0 || kind is not (Wire.Release or Wire.Terminate or Wire.Abort))
                {
                    await SendErrorAsync(pipe, 3, new InvalidDataException("unexpected control message")).ConfigureAwait(false);
                }
            }
            catch (EndOfStreamException)
            {
                await TestGateAsync(eofGate, deadline).ConfigureAwait(false);
            }
            catch (Exception problem) when (problem is IOException or InvalidDataException or ObjectDisposedException)
            {
                // A lost control channel terminates the owned scope below.
            }
            finally
            {
                NativeOwner.Terminate(job);
            }
            return 0;
        }
    }

    private static async Task SendErrorAsync(Stream pipe, byte stage, Exception problem)
    {
        var data = new byte[5];
        data[0] = stage;
        Wire.Code(problem is Win32Exception native ? native.NativeErrorCode : problem.HResult).CopyTo(data, 1);
        await Wire.SendAsync(pipe, Wire.Error, data, CancellationToken.None).ConfigureAwait(false);
    }

    private static void PublishMarker(string marker)
    {
        var temporary = marker + ".pending";
        File.WriteAllText(temporary, "opened");
        File.Move(temporary, marker, overwrite: true);
    }

    private static async Task TestGateAsync(string? gate, long deadline)
    {
        if (gate is null) { return; }
        var remaining = TimeSpan.FromTicks(deadline - DateTime.UtcNow.Ticks);
        if (remaining <= TimeSpan.Zero) { throw new TimeoutException("test gate deadline expired"); }
        PublishMarker(gate + ".ready");
        using var timeout = new CancellationTokenSource(remaining);
        while (!File.Exists(gate + ".release")) { await Task.Delay(10, timeout.Token).ConfigureAwait(false); }
    }

    private static string? ReadTestOption(string[] arguments, ref int index, string option)
    {
        if (index >= arguments.Length || arguments[index] != option) { return null; }
        if (index + 1 >= arguments.Length || arguments[index + 1].Length == 0) { throw new ArgumentException("missing test gate"); }
        var value = arguments[index + 1];
        index += 2;
        return value;
    }

    private static (string Channel, int Parent, ulong Birth, string? Job, string? Marker, string? BeforeOpen, string? Pending, string? Connected, string? EofGate, long Deadline, string[] Command) Parse(string[] arguments)
    {
        if (arguments.Length < 8 || arguments[0] != "--channel" || arguments[2] != "--parent-pid" ||
            arguments[4] != "--parent-birth" || !int.TryParse(arguments[3], NumberStyles.None, CultureInfo.InvariantCulture, out var parent) ||
            parent <= 0 || !ulong.TryParse(arguments[5], NumberStyles.None, CultureInfo.InvariantCulture, out var birth))
        {
            throw new ArgumentException("missing or invalid owner identity", nameof(arguments));
        }
        var index = 6;
        string? job = null;
        if (arguments[index] == "--job-name")
        {
            if (arguments.Length < 10) { throw new ArgumentException("missing job name", nameof(arguments)); }
            job = arguments[index + 1];
            index += 2;
        }
        string? marker = null;
        if (index < arguments.Length && arguments[index] == "--test-parent-opened")
        {
            if (index + 1 >= arguments.Length || arguments[index + 1].Length == 0)
            {
                throw new ArgumentException("missing test marker path", nameof(arguments));
            }
            marker = arguments[index + 1];
            index += 2;
        }
        var beforeOpen = ReadTestOption(arguments, ref index, "--test-before-parent-open");
        var pending = ReadTestOption(arguments, ref index, "--test-connect-pending");
        var connected = ReadTestOption(arguments, ref index, "--test-connected");
        var eofGate = ReadTestOption(arguments, ref index, "--test-control-eof");
        var deadlineText = ReadTestOption(arguments, ref index, "--test-deadline-utc-ticks");
        var validDeadline = long.TryParse(deadlineText, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedDeadline) && parsedDeadline > 0;
        var deadline = (beforeOpen is not null || pending is not null || connected is not null || eofGate is not null) && !validDeadline
            ? throw new ArgumentException("missing test deadline") : parsedDeadline;
        return index >= arguments.Length || arguments[index] != "--" || index + 1 >= arguments.Length
            ? throw new ArgumentException("missing target", nameof(arguments))
            : (arguments[1], parent, birth, job, marker, beforeOpen, pending, connected, eofGate, deadline, arguments[(index + 1)..]);
    }
}
