using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text.Json;

namespace APThermo.Fixtures.Tests.ProcessProbe;

/// <summary>Finite external process controlled exclusively by the parent's infrastructure tests.</summary>
internal static class Program
{
    public static int Main(string[] args)
    {
        if (args.Length == 6 && args[0] == "owner-parent") { return RunOwnerParent(args); }
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

    private static int RunOwnerParent(string[] args)
    {
        using var self = Process.GetCurrentProcess();
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
        start.ArgumentList.Add("--");
        start.ArgumentList.Add("python");
        start.ArgumentList.Add("-c");
        start.ArgumentList.Add("import sys;open(sys.argv[1],'w').close()");
        start.ArgumentList.Add(args[4]);
        using var owner = Process.Start(start) ?? throw new InvalidOperationException("bootstrap did not start");
        File.WriteAllText(args[5] + ".pending", owner.Id.ToString(CultureInfo.InvariantCulture));
        File.Move(args[5] + ".pending", args[5], overwrite: true);
        var watch = Stopwatch.StartNew();
        while (!File.Exists(args[5] + ".release") && watch.Elapsed < TimeSpan.FromSeconds(20)) { Thread.Sleep(10); }
        return 0;
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
