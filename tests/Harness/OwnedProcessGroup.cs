using System.Globalization;

namespace APThermo.Harness;

/// <summary>Observes live members of the owner's Linux session without signalling a numeric PID.</summary>
internal static class OwnedProcessGroup
{
    internal static bool HasLiveMembers(int identity)
    {
        foreach (var directory in Directory.EnumerateDirectories("/proc"))
        {
            if (!int.TryParse(Path.GetFileName(directory), NumberStyles.None, CultureInfo.InvariantCulture, out _)) { continue; }
            string stat;
            try { stat = File.ReadAllText(Path.Combine(directory, "stat")); }
            catch (Exception problem) when (problem is FileNotFoundException or DirectoryNotFoundException) { continue; }
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
