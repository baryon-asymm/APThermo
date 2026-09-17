using System.Text.RegularExpressions;
using APThermo.Fixtures;

namespace APThermo.Docs.Tests;

/// <summary>
/// File discovery and line-oriented markdown reading shared by the levels: which documents each level reads, their
/// fenced code blocks, the samples' snippet regions, and the LF normalization every byte comparison in this node
/// goes through.
/// </summary>
internal static class GuideDocuments
{
    private static readonly Regex SnippetStart = new(@"^//\s*snippet-start:\s*(\w+)\s*$", RegexOptions.Compiled);

    /// <summary>The repository root (the Fixtures node finds it from its own source file).</summary>
    public static string Root => RepositoryPaths.Root;

    /// <summary>The documents that may carry C# snippet blocks: README.md, docs/guide/**/*.md and the package READMEs under docs/nuget/.</summary>
    public static IReadOnlyList<string> SnippetSources() =>
        Existing("README.md").Concat(MarkdownUnder("docs/guide")).Concat(MarkdownUnder("docs/nuget")).ToList();

    /// <summary>The guide pages of docs/guide/.</summary>
    public static IReadOnlyList<string> GuidePages() => MarkdownUnder("docs/guide");

    /// <summary>The package READMEs of docs/nuget/, which may carry no relative link (it breaks on nuget.org).</summary>
    public static IReadOnlyList<string> NugetReadmes() => MarkdownUnder("docs/nuget");

    /// <summary>
    /// The documents whose relative links must resolve: README.md, llms.txt and every markdown under docs/ except
    /// docs/protocol/templates — its placeholder links are deliberate and never resolve, so a check red on them by
    /// construction would be worse than absent (AGENTS.md §13). Every other document under docs/protocol (for
    /// example docs/protocol/prompts) is checked like any other page.
    /// </summary>
    public static IReadOnlyList<string> LinkedDocuments()
    {
        var templatesPrefix = Path.Combine(Root, "docs", "protocol", "templates") + Path.DirectorySeparatorChar;
        return Existing("README.md").Concat(Existing("llms.txt")).Concat(MarkdownUnder("docs")
            .Where(p => !p.StartsWith(templatesPrefix, StringComparison.Ordinal))).ToList();
    }

    /// <summary>Every file under samples/cli/, recursively, mapped to the schema name its top-level directory declares.</summary>
    public static IReadOnlyList<(string Path, string Schema)> CliDocuments()
    {
        var schemaOf = new Dictionary<string, string>(StringComparer.Ordinal) { ["problems"] = "input", ["states"] = "states" };
        var root = Path.GetFullPath(Path.Combine(Root, "samples", "cli"));
        if (!Directory.Exists(root))
        {
            return [];
        }

        var found = new List<(string Path, string Schema)>();
        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            var relative = Path.GetRelativePath(root, file);
            var topDirectory = relative.Split(Path.DirectorySeparatorChar, 2)[0];
            Assert.True(schemaOf.TryGetValue(topDirectory, out var schema), $"{file}: no schema is declared for samples/cli/{topDirectory}/");
            found.Add((file, schema!));
        }

        return found;
    }

    /// <summary>The lines of a file, both line-ending styles accepted (ReadAllLines strips them).</summary>
    public static string[] Lines(string path) => File.ReadAllLines(path);

    /// <summary>CRLF to LF: the form every byte comparison in this node goes through.</summary>
    public static string Lf(string text) => text.Replace("\r\n", "\n");

    /// <summary>The fenced code blocks of a markdown document: the info string, the body lines and the opening line index.</summary>
    public static IReadOnlyList<(string Info, string[] Body, int StartLine)> FencedBlocks(string[] lines)
    {
        var blocks = new List<(string Info, string[] Body, int StartLine)>();
        var opening = -1;
        var fenceChar = '\0';
        var fenceLength = 0;
        var body = new List<string>();
        for (var i = 0; i < lines.Length; i++)
        {
            if (opening < 0)
            {
                var trimmedStart = lines[i].TrimStart();
                var indent = lines[i].Length - trimmedStart.Length;
                if (indent <= 3 && (trimmedStart.StartsWith("```") || trimmedStart.StartsWith("~~~")))
                {
                    fenceChar = trimmedStart[0];
                    fenceLength = RunOf(trimmedStart, fenceChar);
                    opening = i;
                    body.Clear();
                }

                continue;
            }

            var closingCandidate = lines[i].Trim();
            if (closingCandidate.Length >= fenceLength && closingCandidate.All(c => c == fenceChar))
            {
                blocks.Add((InfoOf(lines[opening]), body.ToArray(), opening));
                opening = -1;
            }
            else
            {
                body.Add(lines[i]);
            }
        }

        return blocks;
    }

    /// <summary>The lines of a markdown document that lie outside fenced code blocks (fence delimiters included in the exclusion).</summary>
    public static IEnumerable<string> OutsideFences(string[] lines)
    {
        var mask = OutsideFenceMask(lines);
        for (var i = 0; i < lines.Length; i++)
        {
            if (mask[i])
            {
                yield return lines[i];
            }
        }
    }

    /// <summary>
    /// True at each line index that lies outside every fenced code block (fence delimiter lines counted as inside);
    /// the same walk <see cref="OutsideFences"/> does, exposed with indices for a caller that must report a line
    /// number (an inline code span outside a fence, for instance).
    /// </summary>
    public static bool[] OutsideFenceMask(string[] lines)
    {
        var mask = new bool[lines.Length];
        var fenceChar = '\0';
        var fenceLength = 0;
        for (var i = 0; i < lines.Length; i++)
        {
            if (fenceChar == '\0')
            {
                var trimmedStart = lines[i].TrimStart();
                var indent = lines[i].Length - trimmedStart.Length;
                if (indent <= 3 && (trimmedStart.StartsWith("```") || trimmedStart.StartsWith("~~~")))
                {
                    fenceChar = trimmedStart[0];
                    fenceLength = RunOf(trimmedStart, fenceChar);
                    continue;
                }

                mask[i] = true;
            }
            else if (lines[i].Trim().Length >= fenceLength && lines[i].Trim().All(c => c == fenceChar))
            {
                fenceChar = '\0';
            }
        }

        return mask;
    }

    /// <summary>
    /// The fenced block whose opening fence is exactly at <paramref name="line"/>, when one exists there. A marker
    /// (a snippet marker or a cli-document marker) is meant to be followed by a fence; a marker followed by prose
    /// instead has no block at that line, and a caller must report that in a message rather than read a default
    /// struct's null fields (D14, `SCRATCH/audit/review-docs-2.md`).
    /// </summary>
    public static bool TryFencedBlockAt(string[] lines, int line, out (string Info, string[] Body, int StartLine) block)
    {
        foreach (var candidate in FencedBlocks(lines))
        {
            if (candidate.StartLine == line)
            {
                block = candidate;
                return true;
            }
        }

        block = default;
        return false;
    }

    /// <summary>Whether a fence's info string names a C# block: `csharp`, `cs` or `c#`, any case (the root BOOT.md's Documentation bullet).</summary>
    public static bool IsCSharpFenceInfo(string info) =>
        info.Equals("csharp", StringComparison.OrdinalIgnoreCase)
        || info.Equals("cs", StringComparison.OrdinalIgnoreCase)
        || info.Equals("c#", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Every snippet region of the samples node's own source files: a name to its dedented body, the bytes between a
    /// <c>// snippet-start: name</c> line and the next <c>// snippet-end</c> line. A region may hold another
    /// region's markers nested inside it (the usings region sits above the class, the body region inside its
    /// method); each is read independently, by its own start and end.
    /// </summary>
    public static IReadOnlyDictionary<string, string> SnippetRegions()
    {
        var regions = new Dictionary<string, string>(StringComparer.Ordinal);
        var directory = Path.GetFullPath(Path.Combine(Root, "samples", "Samples"));
        foreach (var file in Directory.EnumerateFiles(directory, "*.cs", SearchOption.TopDirectoryOnly).Order(StringComparer.Ordinal))
        {
            ReadRegionsOf(file, regions);
        }

        return regions;
    }

    /// <summary>Reads a file's own, sequential (never nested) snippet regions into the shared dictionary.</summary>
    private static void ReadRegionsOf(string file, Dictionary<string, string> regions)
    {
        string? currentName = null;
        var body = new List<string>();
        foreach (var line in File.ReadAllLines(file))
        {
            if (currentName is null)
            {
                var start = SnippetStart.Match(line.Trim());
                if (start.Success)
                {
                    currentName = start.Groups[1].Value;
                    body = [];
                }

                continue;
            }

            if (line.Trim() == "// snippet-end")
            {
                Assert.True(!regions.ContainsKey(currentName), $"{file}: the snippet region '{currentName}' is declared more than once");
                regions[currentName] = Dedent(body);
                currentName = null;
                continue;
            }

            body.Add(line);
        }

        Assert.True(currentName is null, $"{file}: a '// snippet-start: {currentName}' with no matching '// snippet-end'");
    }

    private static string Dedent(IReadOnlyList<string> body)
    {
        var indent = body.Where(l => l.Trim().Length > 0).Select(l => l.Length - l.TrimStart().Length).DefaultIfEmpty(0).Min();
        return string.Join("\n", body.Select(l => l.Length >= indent ? l[indent..] : l));
    }

    private static IReadOnlyList<string> Existing(params string[] relativePaths) =>
        relativePaths.Select(p => Path.GetFullPath(Path.Combine(Root, p))).Where(File.Exists).ToList();

    private static IReadOnlyList<string> MarkdownUnder(string relativeDirectory)
    {
        var directory = Path.GetFullPath(Path.Combine(Root, relativeDirectory));
        if (!Directory.Exists(directory))
        {
            return [];
        }

        return Directory.EnumerateFiles(directory, "*.md", SearchOption.AllDirectories).Order(StringComparer.Ordinal).ToList();
    }

    private static int RunOf(string text, char c)
    {
        var n = 0;
        while (n < text.Length && text[n] == c)
        {
            n++;
        }

        return n;
    }

    private static string InfoOf(string openingLine)
    {
        var trimmedStart = openingLine.TrimStart();
        var fenceChar = trimmedStart[0];
        var info = trimmedStart[RunOf(trimmedStart, fenceChar)..];
        return info.Trim();
    }
}
