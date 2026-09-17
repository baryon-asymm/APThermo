using System.Text.RegularExpressions;

namespace APThermo.Docs.Tests;

/// <summary>
/// L3's command synopses (BOOT.md), read once from `src/Cli/API.md`'s `## Command line` block rather than copied
/// into a second, typed-in dictionary (the fourth documentation review's minor 6): which options take a value,
/// which are bare flags, and how many positional arguments a verb accepts. Before this task
/// `CommandLineExampleTests.cs` carried its own copy, narrower than the parser (`devices` accepts `--format` but
/// the copy did not declare it) and never caught the drift, since nothing compared the copy with its source.
/// </summary>
internal static class CommandSynopses
{
    private static readonly Regex Heading = new(@"^##\s+Command line\b", RegexOptions.Compiled);
    private static readonly Regex BracketGroup = new(@"\[([^\[\]]+)\]", RegexOptions.Compiled);
    private static readonly Regex LeadingVerb = new(@"^(\S+)", RegexOptions.Compiled);

    /// <summary>Every command synopsis declared by `src/Cli/API.md`'s `## Command line` block, keyed by the first token after `apthermo`.</summary>
    public static IReadOnlyDictionary<string, (int MaxPositional, string[] ValueOptions, string[] FlagOptions)> FromCliApi()
    {
        var path = Path.GetFullPath(Path.Combine(GuideDocuments.Root, "src", "Cli", "API.md"));
        var lines = GuideDocuments.Lines(path);

        var headingLine = Array.FindIndex(lines, l => Heading.IsMatch(l.Trim()));
        Assert.True(headingLine >= 0, $"{path}: no '## Command line' heading was found");

        var block = FindConsoleBlockAfter(lines, headingLine, path);
        var synopses = new Dictionary<string, (int MaxPositional, string[] ValueOptions, string[] FlagOptions)>(StringComparer.Ordinal);
        foreach (var raw in block)
        {
            var line = raw.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            Assert.True(line.StartsWith("$ apthermo ", StringComparison.Ordinal), $"{path}: this synopsis line does not start with '$ apthermo ': '{line}'");
            ParseLine(path, line["$ apthermo ".Length..], synopses);
        }

        Assert.True(synopses.Count > 0, $"{path}: no command synopsis was parsed from its '## Command line' block");
        return synopses;
    }

    private static string[] FindConsoleBlockAfter(string[] lines, int headingLine, string path)
    {
        foreach (var candidate in GuideDocuments.FencedBlocks(lines[headingLine..], path))
        {
            if (candidate.Info.Equals("console", StringComparison.OrdinalIgnoreCase))
            {
                return candidate.Body;
            }
        }

        Assert.Fail($"{path}: no ```console block follows the '## Command line' heading");
        return [];
    }

    /// <summary>
    /// One synopsis line, its verb followed by bracketed groups: `[--option value]` (a value option), `[--flag]` (a
    /// bare flag, one word), `[NAME]` (an optional positional, bounded) or `[more files...]` (an unbounded run of
    /// further positional arguments, its group containing "..."). Text outside every bracket, after the verb, is
    /// the command's required positional argument(s).
    /// </summary>
    private static void ParseLine(string path, string afterApthermo, Dictionary<string, (int, string[], string[])> synopses)
    {
        var verbMatch = LeadingVerb.Match(afterApthermo);
        Assert.True(verbMatch.Success, $"{path}: this synopsis line names no command: '{afterApthermo}'");
        var verb = verbMatch.Value;
        var rest = afterApthermo[verb.Length..];

        var maxPositional = 0;
        var valueOptions = new List<string>();
        var flagOptions = new List<string>();
        foreach (Match group in BracketGroup.Matches(rest))
        {
            maxPositional = ClassifyGroup(path, verb, group.Groups[1].Value, maxPositional, valueOptions, flagOptions);
        }

        var required = BracketGroup.Replace(rest, "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (maxPositional != int.MaxValue)
        {
            maxPositional += required.Length;
        }

        Assert.True(
            synopses.TryAdd(verb, (maxPositional, valueOptions.ToArray(), flagOptions.ToArray())),
            $"{path}: the command '{verb}' is declared more than once in the '## Command line' block");
    }

    private static int ClassifyGroup(string path, string verb, string content, int maxPositional, List<string> valueOptions, List<string> flagOptions)
    {
        var tokens = content.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        Assert.True(tokens.Length > 0, $"{path}: an empty bracket group in the synopsis of '{verb}'");

        if (!tokens[0].StartsWith("--", StringComparison.Ordinal))
        {
            return content.Contains("...", StringComparison.Ordinal) || maxPositional == int.MaxValue ? int.MaxValue : maxPositional + 1;
        }

        if (tokens.Length > 1)
        {
            valueOptions.Add(tokens[0]);
        }
        else
        {
            flagOptions.Add(tokens[0]);
        }

        return maxPositional;
    }
}
