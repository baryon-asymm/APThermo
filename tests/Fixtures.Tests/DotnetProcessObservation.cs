using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

namespace APThermo.Fixtures.Tests;

internal sealed record DotnetProcessSample(bool IsLive, int ProcessId, string Diagnostics);

internal static class DotnetProcessObservation
{
    private const int LinuxNoSuchProcess = 3;

    internal static DotnetProcessSample Read(string marker)
    {
        if (!File.Exists(marker))
        {
            return new DotnetProcessSample(false, 0, marker + ": marker absent");
        }
        using var document = JsonDocument.Parse(File.ReadAllText(marker + ".identity.json"));
        var identity = document.RootElement;
        var pid = identity.GetProperty("ProcessId").GetInt32();
        var recordedPid = int.Parse(File.ReadAllText(marker), CultureInfo.InvariantCulture);
        if (pid != recordedPid || pid <= 0)
        {
            throw new InvalidDataException("the owned probe's PID and identity disagree");
        }
        var description = marker + ": expected=" + identity.GetRawText();
        return OperatingSystem.IsLinux() ? ReadLinux(pid, identity, description) : ReadProcess(pid, identity, description);
    }

    internal static async Task<DotnetProcessSample> WaitForTerminalAsync(string marker, Stopwatch clock, TimeSpan budget,
        Action<DotnetProcessSample>? firstObservation = null)
    {
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentOutOfRangeException.ThrowIfLessThan(budget, TimeSpan.Zero);
        var sample = Read(marker);
        firstObservation?.Invoke(sample);
        while (sample.IsLive && clock.Elapsed < budget)
        {
            var remaining = budget - clock.Elapsed;
            if (remaining > TimeSpan.Zero)
            {
                await Task.Delay(remaining < TimeSpan.FromMilliseconds(10) ? remaining : TimeSpan.FromMilliseconds(10)).ConfigureAwait(false);
            }
            sample = Read(marker);
        }
        return sample.IsLive ? Read(marker) : sample;
    }

    internal static bool IsMissingLinuxProcess(IOException problem) =>
        problem is FileNotFoundException or DirectoryNotFoundException || problem.HResult == LinuxNoSuchProcess;

    private static DotnetProcessSample ReadLinux(int pid, JsonElement identity, string description)
    {
        string stat;
        try
        {
            stat = File.ReadAllText("/proc/" + pid.ToString(CultureInfo.InvariantCulture) + "/stat");
        }
        catch (IOException problem) when (IsMissingLinuxProcess(problem))
        {
            return new DotnetProcessSample(false, pid, description + "; observed=absent");
        }
        var delimiter = stat.LastIndexOf(')');
        if (delimiter < 0)
        {
            throw new InvalidDataException("the owned probe's Linux process stat has no command delimiter");
        }
        var fields = stat[(delimiter + 2)..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (fields.Length <= 19)
        {
            throw new InvalidDataException("the owned probe's Linux process stat is incomplete");
        }
        var sameIdentity = fields[19] == identity.GetProperty("LinuxStartTicks").GetString();
        var live = sameIdentity && fields[0] is not ("Z" or "X" or "x");
        return new DotnetProcessSample(live, pid, description + "; observed=" + stat);
    }

    private static DotnetProcessSample ReadProcess(int pid, JsonElement identity, string description)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            try
            {
                _ = process.SafeHandle;
            }
            catch (InvalidOperationException) when (process.HasExited)
            {
                return new DotnetProcessSample(false, pid, description + "; observed=exited before handle acquisition");
            }
            if (process.HasExited)
            {
                return new DotnetProcessSample(false, pid, description + "; observed=exited");
            }
            var start = process.StartTime.ToUniversalTime().Ticks;
            var sameIdentity = start == identity.GetProperty("StartTimeUtcTicks").GetInt64();
            return new DotnetProcessSample(sameIdentity, pid, description + "; observedState=live; sameIdentity=" + sameIdentity + "; observedStartUtcTicks=" + start.ToString(CultureInfo.InvariantCulture));
        }
        catch (ArgumentException)
        {
            return new DotnetProcessSample(false, pid, description + "; observed=absent");
        }
    }
}
