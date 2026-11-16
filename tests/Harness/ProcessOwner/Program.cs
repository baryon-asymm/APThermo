using System.ComponentModel;
using System.Diagnostics;
using System.IO.Pipes;
using System.Security.Principal;

namespace APThermo.Harness.ProcessOwner;

/// <summary>Creates owned scope before launching the requested executable.</summary>
internal static class Program
{
    private static async Task<int> Main(string[] arguments)
    {
        var (channel, job, command) = Parse(arguments);
        using var pipe = new NamedPipeClientStream(".", channel, PipeDirection.InOut,
            PipeOptions.Asynchronous, TokenImpersonationLevel.None, HandleInheritability.None);
        await pipe.ConnectAsync().ConfigureAwait(false);
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
        return await StartTargetAsync(pipe, job, command).ConfigureAwait(false);
    }

    private static async Task<int> StartTargetAsync(Stream pipe, string? job, string[] command)
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

    private static (string Channel, string? Job, string[] Command) Parse(string[] arguments)
    {
        if (arguments.Length < 4 || arguments[0] != "--channel") { throw new ArgumentException("missing control channel", nameof(arguments)); }
        var index = 2;
        string? job = null;
        if (arguments[index] == "--job-name")
        {
            if (arguments.Length < 6) { throw new ArgumentException("missing job name", nameof(arguments)); }
            job = arguments[index + 1];
            index += 2;
        }
        return arguments[index] != "--" || index + 1 >= arguments.Length
            ? throw new ArgumentException("missing target", nameof(arguments))
            : (arguments[1], job, arguments[(index + 1)..]);
    }
}
