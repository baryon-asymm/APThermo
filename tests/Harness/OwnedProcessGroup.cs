using System.Globalization;

namespace APThermo.Harness;

/// <summary>Observes live members of the owner's Linux session without signalling a numeric PID.</summary>
internal static class OwnedProcessGroup
{
    internal static bool HasLiveMembers(int identity) =>
        HasLiveMembers(identity, Directory.EnumerateDirectories("/proc"), File.ReadAllText);

    internal static bool HasLiveMembers(int identity, IEnumerable<string> directories, Func<string, string> readStat)
    {
        foreach (var directory in directories)
        {
            if (!int.TryParse(Path.GetFileName(directory), NumberStyles.None, CultureInfo.InvariantCulture, out _)) { continue; }
            string stat;
            try { stat = readStat(Path.Combine(directory, "stat")); }
            catch (IOException problem) when (problem is FileNotFoundException or DirectoryNotFoundException || problem.HResult == 3) { continue; }
            var end = stat.LastIndexOf(')');
            if (end < 0) { throw new InvalidDataException("malformed Linux process identity"); }
            var fields = stat[(end + 2)..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length < 4 || !int.TryParse(fields[3], CultureInfo.InvariantCulture, out var session))
            {
                throw new InvalidDataException("malformed Linux process group");
            }
            if (session == identity && fields[0] is not ("Z" or "X" or "x")) { return true; }
        }
        return false;
    }
}
