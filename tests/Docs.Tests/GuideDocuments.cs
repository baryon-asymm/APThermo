using APThermo.Fixtures;

namespace APThermo.Docs.Tests;

/// <summary>
/// File discovery and line-oriented markdown reading shared by the levels: which documents each level reads, their
/// fenced code blocks, and the LF normalization every byte comparison in this node goes through.
/// </summary>
internal static class GuideDocuments
{
    /// <summary>The repository root (the Fixtures node finds it from its own source file).</summary>
    public static string Root => RepositoryPaths.Root;

    /// <summary>
    /// The scenario classes of the samples node, one per entry of <c>APThermo.Samples.Program.Scenarios</c>: the
    /// argument name to its class (one file per scenario, named after it).
    /// </summary>
    public static string ScenarioClass(string scenario) => scenario switch
    {
        "rocket" or "RocketSolve" => "RocketSolve",
        "batch" or "BatchSolve" => "BatchSolve",
        "equilibrium" or "EquilibriumSolve" => "EquilibriumSolve",
        "states" or "StatesSolve" => "StatesSolve",
        _ => throw new InvalidOperationException($"no scenario class is known for '{scenario}'"),
    };

    /// <summary>The documents that may carry C# snippet blocks: README.md, docs/guide/**/*.md and the package READMEs under docs/nuget/.</summary>
    public static IReadOnlyList<string> SnippetSources() =>
        Existing("README.md").Concat(MarkdownUnder("docs/guide")).Concat(MarkdownUnder("docs/nuget")).ToList();

    /// <summary>The guide pages of docs/guide/.</summary>
    public static IReadOnlyList<string> GuidePages() => MarkdownUnder("docs/guide");

    /// <summary>
    /// The documents whose relative links must resolve: README.md, llms.txt and every markdown under docs/ except
    /// docs/protocol — the protocol kit's templates carry deliberate &lt;placeholder&gt; links that never resolve, so a
    /// check red on them by construction would be worse than absent (AGENTS.md §13).
    /// </summary>
    public static IReadOnlyList<string> LinkedDocuments()
    {
        var protocolPrefix = Path.Combine(Root, "docs", "protocol") + Path.DirectorySeparatorChar;
        return Existing("README.md").Concat(Existing("llms.txt")).Concat(MarkdownUnder("docs")
            .Where(p => !p.StartsWith(protocolPrefix, StringComparison.Ordinal))).ToList();
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
            if (opening >= 0)
            {
                var trimmed = lines[i].Trim();
                if (trimmed.Length >= fenceLength && trimmed.All(c => c == fenceChar))
                {
                    blocks.Add((InfoOf(lines[opening]), body.ToArray(), opening));
                    opening = -1;
                }
                else
                {
                    body.Add(lines[i]);
                }
            }
            else
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
            }
        }

        return blocks;
    }

    /// <summary>The lines of a markdown document that lie outside fenced code blocks (fence delimiters included in the exclusion).</summary>
    public static IEnumerable<string> OutsideFences(string[] lines)
    {
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

                yield return lines[i];
            }
            else if (lines[i].Trim().Length >= fenceLength && lines[i].Trim().All(c => c == fenceChar))
            {
                fenceChar = '\0';
            }
        }
    }

    private static IReadOnlyList<string> Existing(params string[] relativePaths) =>
        relativePaths.Select(p => Path.GetFullPath(Path.Combine(Root, p))).Where(File.Exists).ToList();

    private static IReadOnlyList<string> MarkdownUnder(string relativeDirectory)
    {
        var directory = Path.GetFullPath(Path.Combine(Root, relativeDirectory));
        if (!Directory.Exists(directory))
        {
            return Array.Empty<string>();
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
        var char_ = trimmedStart[0];
        var info = trimmedStart.Substring(RunOf(trimmedStart, char_));
        return info.Trim();
    }
}
