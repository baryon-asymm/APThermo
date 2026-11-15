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
        if (args.Length != 6 || Environment.GetEnvironmentVariable("PROBE_VALUE") != args[3])
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
