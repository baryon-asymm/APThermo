using System.Diagnostics;
using System.Globalization;
using System.IO.Pipes;
using System.Reflection;
using System.Text.Json;

namespace APThermo.Fixtures.Tests.ProcessProbe;

/// <summary>Finite external process controlled exclusively by the parent's infrastructure tests.</summary>
internal static class Program
{
    public static Task<int> Main(string[] args) => (args.Length == 6 || args.Length == 9) && args[0] == "owner-parent"
        ? RunOwnerParentAsync(args) : Task.FromResult(RunNormal(args));

    private static int RunNormal(string[] args)
    {
        var normal = args.Length == 6;
        var ownerSubstitute = args.Length == 8 && args[0] == "controlled" && args[6] == "--channel" && args[7].Length != 0;
        if ((!normal && !ownerSubstitute) || Environment.GetEnvironmentVariable("PROBE_VALUE") != args[3])
        {
            return 79;
        }

        using var output = Console.OpenStandardOutput();
        using var error = Console.OpenStandardError();
        output.Write(File.ReadAllBytes(args[1]));
        error.Write(File.ReadAllBytes(args[2]));
        output.Flush();
        error.Flush();
        if (args[0] is "spawn" or "tree")
        {
            return Spawn(args);
        }

        WriteReady(args[5]);
        if (args[0] == "controlled")
        {
            var watch = Stopwatch.StartNew();
            while (!File.Exists(args[5] + ".release") && watch.Elapsed < TimeSpan.FromSeconds(20))
            {
                Thread.Sleep(10);
            }
        }
        if (args[0] == "block")
        {
            Thread.Sleep(TimeSpan.FromSeconds(20));
        }
        return int.Parse(args[4], CultureInfo.InvariantCulture);
    }

    private static async Task<int> RunOwnerParentAsync(string[] args)
    {
        using var self = Process.GetCurrentProcess();
        var connected = args.Length == 9 && args[6] == "--test-connected";
        using var job = connected ? CreateOwnerJob(args[1], args[2]) : null;
        using var server = connected ? new NamedPipeServerStream(args[2], PipeDirection.InOut, 1,
            PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly) : null;
        var connection = server?.WaitForConnectionAsync();
        var birth = OperatingSystem.IsWindows()
            ? checked((ulong)self.StartTime.ToUniversalTime().ToFileTimeUtc()) : LinuxBirth();
        var start = new ProcessStartInfo("dotnet") { UseShellExecute = false };
        start.ArgumentList.Add(args[1]);
        start.ArgumentList.Add("--channel");
        start.ArgumentList.Add(args[2]);
        start.ArgumentList.Add("--parent-pid");
        start.ArgumentList.Add(Environment.ProcessId.ToString(CultureInfo.InvariantCulture));
        start.ArgumentList.Add("--parent-birth");
        start.ArgumentList.Add(birth.ToString(CultureInfo.InvariantCulture));
        if (OperatingSystem.IsWindows()) { start.ArgumentList.Add("--job-name"); start.ArgumentList.Add(args[2]); }
        start.ArgumentList.Add("--test-parent-opened");
        start.ArgumentList.Add(args[3]);
        if (args.Length == 9)
        {
            start.ArgumentList.Add(args[6]); start.ArgumentList.Add(args[7]);
            start.ArgumentList.Add("--test-deadline-utc-ticks"); start.ArgumentList.Add(args[8]);
        }
        start.ArgumentList.Add("--");
        start.ArgumentList.Add("python");
        start.ArgumentList.Add("-c");
        start.ArgumentList.Add("import sys;open(sys.argv[1],'w').close()");
        start.ArgumentList.Add(args[4]);
        using var owner = Process.Start(start) ?? throw new InvalidOperationException("bootstrap did not start");
        await File.WriteAllTextAsync(args[5] + ".pending", owner.Id.ToString(CultureInfo.InvariantCulture)).ConfigureAwait(false);
        File.Move(args[5] + ".pending", args[5], overwrite: true);
        var watch = Stopwatch.StartNew();
        var aborted = false;
        while (!File.Exists(args[5] + ".release") && watch.Elapsed < TimeSpan.FromSeconds(20))
        {
            if (!aborted && server is not null && connection!.IsCompletedSuccessfully && File.Exists(args[5] + ".abort"))
            {
                await SendAbortAsync(server, long.Parse(args[8], CultureInfo.InvariantCulture)).ConfigureAwait(false);
                aborted = true;
            }
            await Task.Delay(10).ConfigureAwait(false);
        }
        return connection?.IsFaulted == true ? 80 : 0;
    }

    private static async Task SendAbortAsync(Stream server, long deadline)
    {
        var remaining = TimeSpan.FromTicks(deadline - DateTime.UtcNow.Ticks);
        if (remaining <= TimeSpan.Zero) { throw new TimeoutException("test deadline expired"); }
        using var timeout = new CancellationTokenSource(remaining);
        var ready = new byte[10];
        await server.ReadExactlyAsync(ready, timeout.Token).ConfigureAwait(false);
        if (ready[5] != 1) { throw new InvalidDataException("owner did not send READY"); }
        byte[] abort = [(byte)'A', (byte)'P', (byte)'T', (byte)'O', 1, 3, 0, 0, 0, 0];
        await server.WriteAsync(abort, timeout.Token).ConfigureAwait(false);
        await server.FlushAsync(timeout.Token).ConfigureAwait(false);
    }

    private static IDisposable? CreateOwnerJob(string assembly, string channel)
    {
        if (!OperatingSystem.IsWindows()) { return null; }
        var type = Assembly.LoadFrom(assembly).GetType("APThermo.Harness.ProcessOwner.WindowsJob", true)!;
        return (IDisposable)type.GetMethod("Create", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [channel])!;
    }

    private static ulong LinuxBirth()
    {
        var stat = File.ReadAllText("/proc/self/stat");
        return ulong.Parse(stat[(stat.LastIndexOf(')') + 2)..].Split(' ', StringSplitOptions.RemoveEmptyEntries)[19], CultureInfo.InvariantCulture);
    }

    private static void WriteReady(string marker)
    {
        using var process = Process.GetCurrentProcess();
        var parent = Environment.GetEnvironmentVariable("PROBE_PARENT_ID");
        string? linuxStart = null;
        if (OperatingSystem.IsLinux())
        {
            var stat = File.ReadAllText("/proc/self/stat");
            linuxStart = stat[(stat.LastIndexOf(')') + 2)..].Split(' ', StringSplitOptions.RemoveEmptyEntries)[19];
        }
        var identity = new
        {
            Environment.ProcessId,
            ParentProcessId = parent,
            StartTimeUtcTicks = process.StartTime.ToUniversalTime().Ticks,
            LinuxStartTicks = linuxStart,
        };
        File.WriteAllText(marker + ".identity.json", JsonSerializer.Serialize(identity));
        File.WriteAllText(marker + ".pending", Environment.ProcessId.ToString(CultureInfo.InvariantCulture));
        File.Move(marker + ".pending", marker, overwrite: true);
    }

    private static int Spawn(string[] args)
    {
        var start = new ProcessStartInfo("dotnet") { UseShellExecute = false };
        start.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
        start.ArgumentList.Add("block");
        start.Environment["PROBE_PARENT_ID"] = Environment.ProcessId.ToString(CultureInfo.InvariantCulture);
        foreach (var argument in args.Skip(1))
        {
            start.ArgumentList.Add(argument);
        }
        using var child = Process.Start(start) ?? throw new InvalidOperationException("child probe did not start");
        var watch = Stopwatch.StartNew();
        while (!File.Exists(args[5]) && watch.Elapsed < TimeSpan.FromSeconds(5))
        {
            Thread.Sleep(10);
        }
        WriteReady(args[5] + ".parent");
        if (args[0] == "tree")
        {
            Thread.Sleep(TimeSpan.FromSeconds(20));
        }
        return File.Exists(args[5]) ? 0 : 78;
    }
}
