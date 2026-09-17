using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using APThermo.Fixtures;
using APThermo.Harness;

namespace APThermo.Docs.Tests;

/// <summary>
/// L3 (BOOT.md): every `apthermo …` invocation of README.md, docs/guide/*.md or docs/nuget/*.md is accounted for,
/// wherever it appears — the single line of a fence, any line of a fence (not the opening line alone, its findings
/// D4/X4, fixed in `657410d`), or an inline code span in prose. A `rocket`, `equilibrium` or `states` verb with at
/// least one more token is a runnable example: it must pin `--accelerator cpu`, name exactly one committed document
/// under samples/cli/, and deliver no output to a file (`--output`/`--format` absent); a verb with nothing after it
/// (`` `apthermo rocket` ``, naming the command in prose) is a bare mention, not an invocation, and is skipped. The
/// two verbs whose output depends on the machine or the release — `devices` and `--version` — are counted but never
/// run (root `BOOT.md`, Delivery: Documentation, the exceptions to "its shown output is approved"). Every other
/// declared verb — `species`, `schema`, `--help` — is run and approved whenever it is a full invocation, not a bare
/// mention (ma10: before this task only two fixed forms of `species`/`schema` ran, and `--help` never did, though
/// none of the three has output that depends on the machine or the release). Any other verb is a documentation
/// defect — a misspelled command — and fails naming it. Every declared verb's tokens are checked against its own
/// synopsis (`src/Cli/API.md`, "## Command line"): an option the synopsis does not list, or more positional
/// arguments than it allows, fails naming the invocation (N8). A runnable example (`species`/`schema`/`rocket`/
/// `equilibrium`/`states`, JSON) runs in-process through the command line's tree contract with exit code 0, and its
/// delivered document, `run` cut (except a document that carries no `run` property, such as `schema`'s), equals the
/// approved file keyed by the invocation itself — the input document's name for `rocket`/`equilibrium`/`states`,
/// plus a suffix for any option beyond the mandatory `--accelerator cpu` (D10: two invocations of one input with
/// different options must not collide on one approved file), or `verb` plus its own tokens for `species`/`schema`.
/// `--help` runs the same way but is plain text, not JSON, approved as such. Fails when no invocation exists
/// anywhere in the guide, when a runnable example and its approved file do not name each other, and when a fenced
/// block carries more than one non-empty line together with an `apthermo …` invocation among them — that form is
/// not supported (a synopsis with placeholders belongs in prose, never a fence), so it is a documentation defect,
/// not a silently skipped block (AGENTS.md §13). A line naming `apthermo ` that survives prompt-stripping (`$ `,
/// `> `, `PS> `, `PS C:\…> `) without starting with `apthermo ` fails too (N4): an unrecognised prompt style must
/// not silently hide an invocation from every check above.
///
/// The two JSON-document facts — a shown document matching its `samples/cli/` file, and every JSON fence carrying
/// that marker (MA1) — live in `CliDocumentTests.cs`, next to each other since both read a `cli-document` marker.
/// </summary>
public sealed class CommandLineExampleTests
{
    private static readonly HashSet<string> RunnableVerbs = new(StringComparer.Ordinal) { "rocket", "equilibrium", "states" };
    private static readonly HashSet<string> DeclaredOnlyVerbs = new(StringComparer.Ordinal) { "devices", "--version" };
    private static readonly HashSet<string> SynopsisVerbs = new(StringComparer.Ordinal) { "species", "devices", "schema", "--help", "--version" };
    private static readonly Regex InlineSpan = new(@"`(apthermo\s[^`]*)`", RegexOptions.Compiled);
    private static readonly Regex Prompt = new(@"^(?:PS(?:\s+\S+)?>\s+|\$\s+|>\s+)", RegexOptions.Compiled);

    /// <summary>
    /// Every declared verb's own synopsis (`src/Cli/API.md`, "## Command line"), read once here rather than a second
    /// time by every invocation: which options take a value, which are bare flags, and how many positional
    /// arguments (an input document, a schema name, …) the verb accepts. N8: a verb whose invocation carries an
    /// option or a positional argument its own synopsis does not declare is a documentation defect, not a silent
    /// pass — `apthermo devices --outptu x` used to reach no check at all, since `devices` is never run.
    /// </summary>
    private static readonly Dictionary<string, (int MaxPositional, string[] ValueOptions, string[] FlagOptions)> Synopsis =
        new(StringComparer.Ordinal)
        {
            ["rocket"] = (1, ["--output", "--format", "--accelerator", "--database", "--threshold", "--mass-tolerance"], []),
            ["equilibrium"] = (1, ["--output", "--format", "--accelerator", "--database", "--threshold", "--mass-tolerance"], []),
            ["states"] = (int.MaxValue, ["--output", "--format", "--accelerator", "--database", "--threshold", "--mass-tolerance"], ["--transport"]),
            ["species"] = (0, ["--find", "--database", "--output", "--format"], []),
            ["devices"] = (0, ["--output"], []),
            ["schema"] = (1, ["--output"], []),
            ["--help"] = (0, [], []),
            ["--version"] = (0, [], []),
        };

    [Fact]
    public void Every_command_line_invocation_is_a_checked_example_or_a_declared_synopsis()
    {
        var pages = GuideDocuments.SnippetSources();
        var invocations = FenceInvocationsOf(pages).Concat(InlineInvocationsOf(pages)).ToList();
        Assert.True(invocations.Count > 0, "no 'apthermo …' invocation was found in README.md, docs/guide/*.md or docs/nuget/*.md");

        var runnableKeys = new List<ApprovedKey>();
        foreach (var (file, line, command) in invocations)
        {
            Classify($"{file}:{line + 1}", command, runnableKeys);
        }

        CheckApprovedFilesMatch(runnableKeys);
    }

    /// <summary>
    /// Every line of every fence (not the opening line alone, its findings D4/X4, fixed in `657410d`) whose
    /// content, a leading shell prompt stripped, starts with `apthermo `. A fence carrying such a line together with
    /// any other non-empty line — before or after it — is not a supported form: this node's BOOT.md records that a
    /// page shows only the command, never the delivered document inline. A line that names `apthermo ` but does not
    /// start with it even after stripping a known prompt (`$ `, `> `, `PS> `, `PS C:\…> `) fails naming the page and
    /// line (N4): an unrecognised prompt style must not silently hide an invocation.
    /// </summary>
    private static List<(string File, int Line, string Command)> FenceInvocationsOf(IReadOnlyList<string> pages)
    {
        var found = new List<(string File, int Line, string Command)>();
        foreach (var page in pages)
        {
            var lines = GuideDocuments.Lines(page);
            foreach (var block in GuideDocuments.FencedBlocks(lines, page))
            {
                var nonEmpty = block.Body
                    .Select((text, index) => (Text: StripPrompt(text), Index: index))
                    .Where(entry => !string.IsNullOrWhiteSpace(entry.Text))
                    .ToArray();

                var unrecognized = nonEmpty
                    .Where(entry => !entry.Text.StartsWith("apthermo ", StringComparison.Ordinal) && entry.Text.Contains("apthermo ", StringComparison.Ordinal))
                    .ToArray();
                if (unrecognized.Length > 0)
                {
                    Assert.Fail(
                        $"{page}:{block.StartLine + 1 + unrecognized[0].Index}: this line names 'apthermo' but is not "
                            + $"recognised as an invocation after stripping a known prompt ('$ ', '> ', 'PS> ', 'PS C:\\…> '): "
                            + $"'{unrecognized[0].Text}'");
                }

                var invocationEntries = nonEmpty.Where(entry => entry.Text.StartsWith("apthermo ", StringComparison.Ordinal)).ToArray();
                if (invocationEntries.Length == 0)
                {
                    continue;
                }

                Assert.True(
                    nonEmpty.Length == 1,
                    $"{page}:{block.StartLine + 1}: a fenced block carrying an 'apthermo …' invocation must hold no "
                        + $"other non-empty line (show only the command, in its own single-line fence; a synopsis "
                        + $"with placeholders belongs in prose or a link to the command line's API.md): "
                        + $"{invocationEntries.Length} invocation line(s) among {nonEmpty.Length} non-empty line(s)");

                found.Add((page, block.StartLine + 1 + invocationEntries[0].Index, invocationEntries[0].Text));
            }
        }

        return found;
    }

    /// <summary>Every backtick-delimited inline code span, outside every fence, whose content starts with `apthermo `.</summary>
    private static List<(string File, int Line, string Command)> InlineInvocationsOf(IReadOnlyList<string> pages)
    {
        var found = new List<(string File, int Line, string Command)>();
        foreach (var page in pages)
        {
            var lines = GuideDocuments.Lines(page);
            var mask = GuideDocuments.OutsideFenceMask(lines);
            for (var i = 0; i < lines.Length; i++)
            {
                if (!mask[i])
                {
                    continue;
                }

                foreach (Match match in InlineSpan.Matches(lines[i]))
                {
                    found.Add((page, i, match.Groups[1].Value.Trim()));
                }
            }
        }

        return found;
    }

    /// <summary>A leading shell prompt stripped: `$ `, a bare `> `, `PS> `, or `PS C:\…> ` (N4). Unrecognised text is returned unchanged.</summary>
    private static string StripPrompt(string line)
    {
        var trimmed = line.Trim();
        var match = Prompt.Match(trimmed);
        return match.Success ? trimmed[match.Length..] : trimmed;
    }

    /// <summary>Classifies one invocation: a runnable example, a bare mention, a declared synopsis, or a documentation defect.</summary>
    private static void Classify(string where, string command, List<ApprovedKey> runnableKeys)
    {
        var tokens = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        Assert.True(tokens.Length >= 2 && tokens[0] == "apthermo", $"{where}: '{command}' names no command after 'apthermo'");

        var verb = tokens[1];
        if (RunnableVerbs.Contains(verb))
        {
            if (tokens.Length == 2)
            {
                return; // a bare mention, e.g. "the `apthermo rocket` command" — not a full invocation
            }

            ValidateAgainstSynopsis(where, verb, tokens[2..]);

            var (baseKey, reason) = TryRunnableKey(tokens[1..]);
            Assert.True(baseKey is not null, $"{where}: '{command}' is not a runnable example: {reason}");
            var input = InputDocumentsOf(tokens[1..])[0];
            var key = RunnableKeyOf(baseKey!, tokens[2..], input);
            runnableKeys.Add(new ApprovedKey(key, "json"));
            CheckExample(where, tokens[1..], key);
            return;
        }

        Assert.True(SynopsisVerbs.Contains(verb), $"{where}: '{command}' names no command of the declared list (rocket, equilibrium, states, species, devices, schema, --help, --version)");

        ValidateAgainstSynopsis(where, verb, tokens[2..]);

        if (DeclaredOnlyVerbs.Contains(verb))
        {
            return; // devices, --version: output depends on the machine or the release (root BOOT.md, Delivery: Documentation)
        }

        if (verb == "--help")
        {
            RunAndApproveText(where, tokens[1..], "help");
            runnableKeys.Add(new ApprovedKey("help", "txt"));
            return;
        }

        if (tokens.Length == 2)
        {
            return; // a bare mention, e.g. "the `apthermo species` command" — not a full invocation
        }

        var syntheticKey = KeyOf(tokens[1..]);
        RunAndApprove(where, tokens[1..], syntheticKey, cutRun: verb != "schema");
        runnableKeys.Add(new ApprovedKey(syntheticKey, "json"));
    }

    /// <summary>
    /// Every declared verb's tokens checked against its own synopsis (N8): an option not in the synopsis' value or
    /// flag options fails as unknown, a value option with no following token fails as incomplete, and more
    /// positional arguments than the synopsis allows fails too. `=`-form options (`--output=x`) carry their value
    /// inline, so no following token is consumed for them.
    /// </summary>
    private static void ValidateAgainstSynopsis(string where, string verb, IReadOnlyList<string> argsAfterVerb)
    {
        if (!Synopsis.TryGetValue(verb, out var synopsis))
        {
            return;
        }

        var positional = 0;
        for (var i = 0; i < argsAfterVerb.Count; i++)
        {
            var token = argsAfterVerb[i];
            if (!token.StartsWith('-'))
            {
                positional++;
                Assert.True(
                    positional <= synopsis.MaxPositional,
                    $"{where}: 'apthermo {verb} …' names more positional argument(s) than its declared synopsis allows: '{token}'");
                continue;
            }

            var name = token.Split('=', 2)[0];
            var takesValue = synopsis.ValueOptions.Contains(name, StringComparer.Ordinal);
            Assert.True(
                takesValue || synopsis.FlagOptions.Contains(name, StringComparer.Ordinal),
                $"{where}: 'apthermo {verb} …' names an option '{name}' its declared synopsis does not have");

            if (takesValue && !token.Contains('='))
            {
                Assert.True(i + 1 < argsAfterVerb.Count, $"{where}: 'apthermo {verb} …': option '{name}' needs a value");
                i++;
            }
        }
    }

    /// <summary>
    /// The input document's key (its file name without extension) when the invocation is runnable, or null with the
    /// reason it is not: a misspelled option, an input not committed under samples/cli/, or delivery to a file
    /// instead of standard output.
    /// </summary>
    private static (string? Key, string Reason) TryRunnableKey(IReadOnlyList<string> argsAfterApthermo)
    {
        if (OptionValue(argsAfterApthermo, "--output") is not null)
        {
            return (null, "delivers to a file through --output instead of standard output");
        }

        if (OptionValue(argsAfterApthermo, "--format") is not null)
        {
            return (null, "delivers through --format instead of the default JSON to standard output");
        }

        if (OptionValue(argsAfterApthermo, "--accelerator") != "cpu")
        {
            return (null, "does not pin '--accelerator cpu'");
        }

        var inputs = InputDocumentsOf(argsAfterApthermo);
        if (inputs.Count == 0)
        {
            return (null, "names no document committed under samples/cli/");
        }

        if (inputs.Count > 1)
        {
            return (null, $"names more than one committed document under samples/cli/ ({string.Join(", ", inputs)})");
        }

        return (Path.GetFileNameWithoutExtension(inputs[0]), "");
    }

    /// <summary>
    /// D10: the approval key must identify the whole invocation, not only its input file, so two invocations of one
    /// input with different options never collide on one approved file. The mandatory `--accelerator cpu` and the
    /// input token itself carry no distinguishing information (every runnable example pins the former and names the
    /// latter once); every other token is folded into the key, sanitized to a filesystem-safe suffix. An invocation
    /// with no such extra token keeps the bare input-file key, so today's approved file names are unchanged.
    /// </summary>
    private static string RunnableKeyOf(string baseName, IReadOnlyList<string> argsAfterVerb, string inputToken)
    {
        var extras = new List<string>();
        for (var i = 0; i < argsAfterVerb.Count; i++)
        {
            var token = argsAfterVerb[i];
            if (token == inputToken)
            {
                continue;
            }

            var name = token.Split('=', 2)[0];
            if (name == "--accelerator")
            {
                if (!token.Contains('='))
                {
                    i++; // skip its value token ("cpu"); pinned on every runnable example, so it never distinguishes one
                }

                continue;
            }

            extras.Add(Sanitize(token));
        }

        return extras.Count == 0 ? baseName : baseName + "-" + string.Join("-", extras);
    }

    /// <summary>The approval key of a `species`/`schema` invocation: its verb, then each of its own tokens sanitized (e.g. `species --find H2O` → `species-find-h2o`).</summary>
    private static string KeyOf(IReadOnlyList<string> tokensFromVerb) =>
        string.Join("-", tokensFromVerb.Select(Sanitize));

    private static string Sanitize(string token) =>
        Regex.Replace(token.TrimStart('-'), @"[^A-Za-z0-9.]+", "-").Trim('-').ToLowerInvariant();

    private static void CheckExample(string where, IReadOnlyList<string> argsAfterApthermo, string key)
    {
        var input = InputDocumentsOf(argsAfterApthermo)[0];
        var args = argsAfterApthermo.Select(a => a == input ? Path.GetFullPath(Path.Combine(GuideDocuments.Root, input)) : a).ToArray();
        RunAndApprove(where, args, key, cutRun: true);
    }

    /// <summary>Runs `apthermo &lt;args&gt;` in-process and compares its delivered document, `run` cut when requested, with the approved file keyed by <paramref name="key"/>.</summary>
    private static void RunAndApprove(string where, IReadOnlyList<string> args, string key, bool cutRun)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var code = APThermo.Cli.Program.Run(args.ToArray(), output, error);
        Assert.True(code == 0, $"{where}: 'apthermo {string.Join(' ', args)}' exited with {code}: {error}");

        var document = output.ToString();
        using (var parsed = JsonDocument.Parse(document))
        {
            Assert.True(parsed.RootElement.ValueKind == JsonValueKind.Object, $"{where}: 'apthermo {string.Join(' ', args)}' delivered no JSON object to standard output");
        }

        var bytes = cutRun ? RunPropertyCut.Bytes(Encoding.UTF8.GetBytes(document), key) : Encoding.UTF8.GetBytes(document);
        var approvedPath = ApprovedPathOf(key, "json");
        var actual = GuideDocuments.Lf(Encoding.UTF8.GetString(bytes));
        var actualPath = Path.Combine(Path.GetDirectoryName(approvedPath)!, key + ".actual.json");

        if (!File.Exists(approvedPath))
        {
            File.WriteAllText(actualPath, actual, new UTF8Encoding(false));
            Assert.Fail($"{where}: the approved file for '{key}' is missing: {approvedPath} (actual written to {actualPath})");
        }

        var approved = GuideDocuments.Lf(File.ReadAllText(approvedPath));
        if (!approved.Equals(actual, StringComparison.Ordinal))
        {
            File.WriteAllText(actualPath, actual, new UTF8Encoding(false));
            Assert.Fail($"{where}: the delivered document differs from its approved file: approved {approvedPath}, actual {actualPath}");
        }
    }

    /// <summary>
    /// Runs `apthermo &lt;args&gt;` in-process and compares its plain-text output (not JSON, no `run` cut) with the
    /// approved file keyed by <paramref name="key"/> — the form `--help`'s usage text takes.
    /// </summary>
    private static void RunAndApproveText(string where, IReadOnlyList<string> args, string key)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var code = APThermo.Cli.Program.Run(args.ToArray(), output, error);
        Assert.True(code == 0, $"{where}: 'apthermo {string.Join(' ', args)}' exited with {code}: {error}");

        var actual = GuideDocuments.Lf(output.ToString());
        var approvedPath = ApprovedPathOf(key, "txt");
        var actualPath = Path.Combine(Path.GetDirectoryName(approvedPath)!, key + ".actual.txt");

        if (!File.Exists(approvedPath))
        {
            File.WriteAllText(actualPath, actual, new UTF8Encoding(false));
            Assert.Fail($"{where}: the approved file for '{key}' is missing: {approvedPath} (actual written to {actualPath})");
        }

        var approved = GuideDocuments.Lf(File.ReadAllText(approvedPath));
        if (!approved.Equals(actual, StringComparison.Ordinal))
        {
            File.WriteAllText(actualPath, actual, new UTF8Encoding(false));
            Assert.Fail($"{where}: the delivered text differs from its approved file: approved {approvedPath}, actual {actualPath}");
        }
    }

    /// <summary>No runnable example without an approved file, and no approved file without one (D1).</summary>
    private static void CheckApprovedFilesMatch(IReadOnlyList<ApprovedKey> runnableKeys)
    {
        var directory = Path.GetDirectoryName(ApprovedPathOf("x", "json"))!;
        var approvedKeys = Directory.Exists(directory)
            ? Directory.EnumerateFiles(directory, "*.approved.*")
                .Select(p => Path.GetFileName(p))
                .Where(n => n.EndsWith(".approved.json", StringComparison.Ordinal) || n.EndsWith(".approved.txt", StringComparison.Ordinal))
                .Select(SplitApprovedFileName)
                .ToList()
            : [];

        var missing = runnableKeys.Except(approvedKeys).OrderBy(k => k.Key, StringComparer.Ordinal).ToList();
        var orphaned = approvedKeys.Except(runnableKeys).OrderBy(k => k.Key, StringComparer.Ordinal).ToList();
        Assert.True(missing.Count == 0, $"no approved/cli file for the runnable example(s): {string.Join(", ", missing.Select(Describe))}");
        Assert.True(orphaned.Count == 0, $"approved/cli file(s) with no matching runnable example: {string.Join(", ", orphaned.Select(Describe))}");
    }

    private static ApprovedKey SplitApprovedFileName(string fileName) =>
        fileName.EndsWith(".approved.json", StringComparison.Ordinal)
            ? new ApprovedKey(fileName[..^".approved.json".Length], "json")
            : new ApprovedKey(fileName[..^".approved.txt".Length], "txt");

    private static string Describe(ApprovedKey key) => key.Extension == "json" ? key.Key : $"{key.Key} ({key.Extension})";

    private static string ApprovedPathOf(string key, string extension) => RepositoryPaths.Resolve("tests", "Docs.Tests", "approved", "cli", key + ".approved." + extension);

    private static string? OptionValue(IReadOnlyList<string> args, string name)
    {
        for (var i = 0; i + 1 < args.Count; i++)
        {
            if (args[i] == name)
            {
                return args[i + 1];
            }
        }

        return null;
    }

    /// <summary>Every token of the invocation that names an existing committed file under samples/cli/.</summary>
    private static IReadOnlyList<string> InputDocumentsOf(IReadOnlyList<string> args)
    {
        var cliRoot = Path.GetFullPath(Path.Combine(GuideDocuments.Root, "samples", "cli")) + Path.DirectorySeparatorChar;
        var found = new List<string>();
        foreach (var token in args)
        {
            if (token.StartsWith('-'))
            {
                continue;
            }

            var full = Path.GetFullPath(Path.Combine(GuideDocuments.Root, token));
            if (full.StartsWith(cliRoot, StringComparison.Ordinal) && File.Exists(full))
            {
                found.Add(token);
            }
        }

        return found;
    }

    /// <summary>An approved file's key together with its extension ("json" or "txt", the latter for `--help`'s plain-text usage).</summary>
    private readonly record struct ApprovedKey(string Key, string Extension);
}
