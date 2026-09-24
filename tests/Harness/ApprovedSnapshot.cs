using System.Text;

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

    /// <summary>
    /// The approved path for a snapshot named <paramref name="baseName"/> under <paramref name="directory"/>: the root
    /// BOOT.md's platform constraint keeps one record per platform, <c>&lt;baseName&gt;.approved.txt</c> everywhere but
    /// Linux and <c>&lt;baseName&gt;.linux.approved.txt</c> on Linux, so that a difference the two accelerators'
    /// C runtimes round differently stays visible to the bit on both rather than being averaged away by a shared
    /// tolerance. This is the one place that picks between them: every consumer's Bits level calls it instead of
    /// repeating the platform check.
    /// </summary>
    public static string ApprovedPathFor(string directory, string baseName) =>
        Path.Combine(directory, OperatingSystem.IsLinux() ? $"{baseName}.linux.approved.txt" : $"{baseName}.approved.txt");

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
    /// <param name="fields">
    /// Optional: the ordered field values the caller's <see cref="BitHash"/> folded into <paramref name="actualLine"/>
    /// (its <see cref="BitHash.Fields"/>). Passed and the key is a problem, they are written as one line each to a
    /// per-case dump file beside the actual file (BOOT.md, "a field dump is a caller's opt-in") — a caller with no
    /// fields to offer (a composite line over more than one hash, for instance) simply omits the argument, and gets the
    /// same key/value problem as before.
    /// </param>
    /// <param name="key">The snapshot key this run computed a value for.</param>
    /// <param name="actualLine">The value this run computed for <paramref name="key"/>.</param>
    /// <returns>Null when the value agrees with the approved one; otherwise a message naming the problem.</returns>
    public string? Problem(string key, string actualLine, IReadOnlyList<string>? fields = null)
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
            if (fields is not null)
            {
                WriteFieldDump(key, fields);
            }
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
        return [.. _approved.Keys.Where(key => !produced.Contains(key)).OrderBy(key => key, StringComparer.Ordinal)];
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

    /// <summary>
    /// One field per line, in the order <paramref name="fields"/> holds them, into a file beside the actual file: its
    /// name is the actual file's own name with <paramref name="key"/> (sanitized) and <c>.fields.txt</c> appended, so a
    /// glob over <c>*.fields.txt</c> beside <c>*.actual.txt</c> finds every differing case's dump. Git-ignored like the
    /// actual file, and (like it) never read back by this node: a human or CI diffs it against a reference dump.
    /// </summary>
    private void WriteFieldDump(string key, IReadOnlyList<string> fields)
    {
        var directory = Path.GetDirectoryName(_actualPath);
        var baseName = Path.GetFileNameWithoutExtension(_actualPath);
        var fileName = $"{baseName}.{Sanitize(key)}.fields.txt";
        var path = directory is null ? fileName : Path.Combine(directory, fileName);
        File.WriteAllLines(path, fields);
    }

    /// <summary>A key turned into a safe file-name fragment: every character but a letter, a digit, '-', '_' or '.' becomes '_'.</summary>
    private static string Sanitize(string key)
    {
        var builder = new StringBuilder(key.Length);
        foreach (var c in key)
        {
            _ = builder.Append(char.IsLetterOrDigit(c) || c is '-' or '_' or '.' ? c : '_');
        }

        return builder.ToString();
    }
}
