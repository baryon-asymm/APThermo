namespace AerospacePropellantThermodynamics.Harness;

/// <summary>
/// A snapshot file of "key value" lines: a tripwire, not a contract, the way <c>PublicSurface.approved.txt</c> is one
/// over the contract (BOOT.md; AGENTS.md §13). Blank lines and lines starting with <c>#</c> are comments and are
/// skipped; a line's key is everything before its first tab, or, when the line holds no tab, everything before its
/// first space. The recorded snapshots of the tree use both conventions: a fixture path never holds a space, so five
/// of them separate the key from the hash with one; the front door's Cli node's example names can (<c>"API.md input
/// example 0"</c>), so its lines are tab-separated and its value itself holds a further tab. This node never rewrites
/// the approved file; it only ever writes the actual file beside it, and never over it.
/// </summary>
public sealed class ApprovedSnapshot
{
    private readonly string _approvedPath;
    private readonly string _actualPath;
    private readonly char _delimiter;
    private readonly IReadOnlyDictionary<string, string> _approved;
    private readonly List<string> _actualLines = [];
    private bool _dirty;

    private ApprovedSnapshot(string approvedPath, char delimiter, IReadOnlyDictionary<string, string> approved)
    {
        _approvedPath = approvedPath;
        _actualPath = ActualPathOf(approvedPath);
        _delimiter = delimiter;
        _approved = approved;
    }

    /// <summary>Reads the approved file at <paramref name="approvedPath"/>; an absent file is an empty snapshot.</summary>
    public static ApprovedSnapshot Load(string approvedPath)
    {
        if (!File.Exists(approvedPath))
        {
            return new ApprovedSnapshot(approvedPath, ' ', new Dictionary<string, string>(StringComparer.Ordinal));
        }

        var lines = File.ReadAllLines(approvedPath);
        var delimiter = lines.Any(line => line.Length > 0 && line[0] != '#' && line.Contains('\t')) ? '\t' : ' ';
        var approved = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var line in lines)
        {
            if (line.Length == 0 || line[0] == '#')
            {
                continue;
            }

            var at = line.IndexOf(delimiter);
            if (at < 0)
            {
                continue;
            }

            approved[line[..at]] = line[(at + 1)..];
        }

        return new ApprovedSnapshot(approvedPath, delimiter, approved);
    }

    /// <summary>
    /// Compares <paramref name="actualLine"/>, the value this run computed for <paramref name="key"/>, with the approved
    /// one. Null when they agree; otherwise the problem, naming the key and how to approve, and the pair is kept for the
    /// actual file, written beside the approved one (never over it) from this call on, so that it always ends complete
    /// through the last key a caller passed.
    /// </summary>
    public string? Problem(string key, string actualLine)
    {
        _actualLines.Add(key + _delimiter + actualLine);
        string? problem = null;
        if (!_approved.TryGetValue(key, out var recorded))
        {
            problem = $"{key}: not in {_approvedPath}. A new case is approved like a changed one: " +
                      $"review {_actualPath} and, once it looks right, replace {_approvedPath} with it in the same commit.";
        }
        else if (recorded != actualLine)
        {
            problem = $"{key}: {_approvedPath} records {recorded}, this run gives {actualLine}. A decomposition, a rename or a " +
                      $"reordering of code must move no line here; if the change is an intended one, name it in the commit and " +
                      $"replace {_approvedPath} with {_actualPath}.";
        }

        if (problem is not null)
        {
            _dirty = true;
        }

        if (_dirty)
        {
            File.WriteAllLines(_actualPath, _actualLines);
        }

        return problem;
    }

    /// <summary>The approved keys among <paramref name="producedKeys"/> that no run produced, sorted ordinally.</summary>
    public IReadOnlyList<string> StaleKeys(IEnumerable<string> producedKeys)
    {
        var produced = new HashSet<string>(producedKeys, StringComparer.Ordinal);
        return _approved.Keys.Where(key => !produced.Contains(key)).OrderBy(key => key, StringComparer.Ordinal).ToArray();
    }

    private static string ActualPathOf(string approvedPath)
    {
        var directory = Path.GetDirectoryName(approvedPath);
        var fileName = Path.GetFileName(approvedPath).Replace("approved", "actual", StringComparison.Ordinal);
        return directory is null ? fileName : Path.Combine(directory, fileName);
    }
}
