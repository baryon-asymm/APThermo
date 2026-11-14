using System.Diagnostics;
using System.Globalization;
using System.Reflection;

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

        File.WriteAllText(args[5], Environment.ProcessId.ToString(CultureInfo.InvariantCulture));
        if (args[0] == "block")
        {
            Thread.Sleep(TimeSpan.FromSeconds(20));
        }
        return int.Parse(args[4], CultureInfo.InvariantCulture);
    }

    private static int Spawn(string[] args)
    {
        var start = new ProcessStartInfo("dotnet") { UseShellExecute = false };
        start.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
        start.ArgumentList.Add("block");
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
        File.WriteAllText(args[5] + ".parent", Environment.ProcessId.ToString(CultureInfo.InvariantCulture));
        if (args[0] == "tree")
        {
            Thread.Sleep(TimeSpan.FromSeconds(20));
        }
        return File.Exists(args[5]) ? 0 : 78;
    }
}
