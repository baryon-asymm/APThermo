namespace APThermo.Harness;

/// <summary>
/// A snapshot file of "key value" lines: a tripwire, not a contract, the way <c>PublicSurface.approved.txt</c> is one
/// over the contract (BOOT.md; AGENTS.md §13). Blank lines and lines starting with <c>#</c> are comments and are
/// skipped; a line's key is everything before the file's delimiter: a tab when any line of the file holds one, else a
/// space; a line without it is skipped. Snapshots keyed by a fixture path use a space, since a path never holds one;
/// the command-line adapter's example names can (<c>"API.md input example 0"</c>), so the Cli tests node's snapshot is
/// tab-separated and its value holds a further tab. When the approved file is absent or empty, the delimiter is chosen
/// the same way from what this run is writing: a tab when any key holds a space or any value holds a tab, else a
/// space, so that the first-ever approval already separates a key that needs it. This node never rewrites the
/// approved file; it only ever writes the actual file beside it, and never over it.
/// </summary>
public sealed class ApprovedSnapshot
{
    private readonly string _approvedPath;
    private readonly string _actualPath;
    private readonly char? _approvedDelimiter;
    private readonly IReadOnlyDictionary<string, string> _approved;
    private readonly List<(string Key, string Value)> _actualPairs = [];
    private bool _dirty;

    private ApprovedSnapshot(string approvedPath, char? approvedDelimiter, IReadOnlyDictionary<string, string> approved)
    {
        _approvedPath = approvedPath;
        _actualPath = ActualPathOf(approvedPath);
        _approvedDelimiter = approvedDelimiter;
        _approved = approved;
    }

    /// <summary>Reads the approved file at <paramref name="approvedPath"/>; an absent or empty file is an empty snapshot.</summary>
    public static ApprovedSnapshot Load(string approvedPath)
    {
        if (!File.Exists(approvedPath))
        {
            return new ApprovedSnapshot(approvedPath, null, new Dictionary<string, string>(StringComparer.Ordinal));
        }

        var contentLines = File.ReadAllLines(approvedPath).Where(line => line.Length > 0 && line[0] != '#').ToList();
        if (contentLines.Count == 0)
        {
            return new ApprovedSnapshot(approvedPath, null, new Dictionary<string, string>(StringComparer.Ordinal));
        }

        var delimiter = contentLines.Any(line => line.Contains('\t')) ? '\t' : ' ';
        var approved = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var line in contentLines)
        {
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
        _actualPairs.Add((key, actualLine));
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
            var delimiter = _approvedDelimiter ?? DelimiterFor(_actualPairs);
            File.WriteAllLines(_actualPath, _actualPairs.Select(pair => pair.Key + delimiter + pair.Value));
        }

        return problem;
    }

    /// <summary>The approved keys among <paramref name="producedKeys"/> that no run produced, sorted ordinally.</summary>
    public IReadOnlyList<string> StaleKeys(IEnumerable<string> producedKeys)
    {
        var produced = new HashSet<string>(producedKeys, StringComparer.Ordinal);
        return _approved.Keys.Where(key => !produced.Contains(key)).OrderBy(key => key, StringComparer.Ordinal).ToArray();
    }

    /// <summary>
    /// The delimiter for a snapshot with no approved delimiter of its own (the approved file was absent or empty): a
    /// tab when a key so far holds a space or a value so far holds a tab (either would otherwise be swallowed into
    /// the wrong side of the split on the next load), else a space.
    /// </summary>
    private static char DelimiterFor(IReadOnlyList<(string Key, string Value)> pairs) =>
        pairs.Any(pair => pair.Key.Contains(' ') || pair.Value.Contains('\t')) ? '\t' : ' ';

    private static string ActualPathOf(string approvedPath)
    {
        var directory = Path.GetDirectoryName(approvedPath);
        var fileName = Path.GetFileName(approvedPath).Replace("approved", "actual", StringComparison.Ordinal);
        return directory is null ? fileName : Path.Combine(directory, fileName);
    }
}
