using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using APThermo.Fixtures;
using APThermo.Harness;

namespace APThermo.Docs.Tests;

/// <summary>
/// L3 (BOOT.md): every `apthermo …` invocation of README.md, docs/guide/*.md or docs/nuget/*.md is accounted for,
/// wherever it appears — the single line of a fence, any line of a fence (not the opening line alone), or an inline
/// code span in prose. A `rocket`, `equilibrium` or `states` verb with at least one more token is a runnable
/// example: it must pin `--accelerator cpu`, name exactly one committed document under samples/cli/, and deliver no
/// output to a file (`--output`/`--format` absent); a verb with nothing after it (`` `apthermo rocket` ``, naming
/// the command in prose) is a bare mention, not an invocation, and is skipped. A verb from the declared synopsis
/// list (`species`, `devices`, `schema`, `--help`, `--version`) is counted but not run, except the two deterministic
/// forms `apthermo species --find H2O` and `apthermo schema input`, which are run and approved like a runnable
/// example. Any other verb is a documentation defect — a misspelled command — and fails naming it. A runnable
/// example runs in-process through the command line's tree contract with exit code 0, and its delivered document,
/// `run` cut, equals the approved file keyed by the input document's name (the two deterministic synopses key by
/// their own name instead, `schema input`'s undergoing no `run` cut, since its document carries no `run` property).
/// Fails when no invocation exists anywhere in the guide, when a runnable example (or a deterministic synopsis) and
/// its approved file do not name each other, and when a fenced block carries more than one non-empty line together
/// with an `apthermo …` invocation among them — that form is not supported (a synopsis with placeholders belongs in
/// prose, never a fence), so it is a documentation defect, not a silently skipped block (AGENTS.md §13).
///
/// A second fact (`Every_marked_cli_document_equals_its_samples_cli_file_and_validates_against_its_schema`) checks a
/// JSON document shown next to prose, such as a package README's example input: a `&lt;!-- cli-document: path --&gt;`
/// marker names its samples/cli/ file, the following ```json fence must equal that file byte for byte, and the
/// shown document is validated against the schema its top-level directory declares, the same way L5 validates the
/// file itself.
/// </summary>
public sealed class CommandLineExampleTests
{
    private static readonly HashSet<string> RunnableVerbs = new(StringComparer.Ordinal) { "rocket", "equilibrium", "states" };
    private static readonly HashSet<string> SynopsisVerbs = new(StringComparer.Ordinal) { "species", "devices", "schema", "--help", "--version" };
    private static readonly Regex InlineSpan = new(@"`(apthermo\s[^`]*)`", RegexOptions.Compiled);
    private static readonly Regex CliDocumentMarker = new(@"^<!--\s*cli-document:\s*([\w./-]+)\s*-->$", RegexOptions.Compiled);
    private static readonly Dictionary<string, string> SchemaOfTopDirectory = new(StringComparer.Ordinal) { ["problems"] = "input", ["states"] = "states" };

    [Fact]
    public void Every_command_line_invocation_is_a_checked_example_or_a_declared_synopsis()
    {
        var pages = GuideDocuments.SnippetSources();
        var invocations = FenceInvocationsOf(pages).Concat(InlineInvocationsOf(pages)).ToList();
        Assert.True(invocations.Count > 0, "no 'apthermo …' invocation was found in README.md, docs/guide/*.md or docs/nuget/*.md");

        var runnableKeys = new List<string>();
        foreach (var (file, line, command) in invocations)
        {
            Classify($"{file}:{line + 1}", command, runnableKeys);
        }

        CheckApprovedFilesMatch(runnableKeys);
    }

    /// <summary>
    /// Every line of every fence (not the opening line alone, D4/X4 of `SCRATCH/audit/review-docs-2.md`) whose
    /// content, the leading `$ ` stripped, starts with `apthermo `. A fence carrying such a line together with any
    /// other non-empty line — before or after it — is not a supported form: this node's BOOT.md records that a page
    /// shows only the command, never the delivered document inline.
    /// </summary>
    private static List<(string File, int Line, string Command)> FenceInvocationsOf(IReadOnlyList<string> pages)
    {
        var found = new List<(string File, int Line, string Command)>();
        foreach (var page in pages)
        {
            var lines = GuideDocuments.Lines(page);
            foreach (var block in GuideDocuments.FencedBlocks(lines))
            {
                var nonEmpty = block.Body
                    .Select((text, index) => (Text: StripDollar(text), Index: index))
                    .Where(entry => !string.IsNullOrWhiteSpace(entry.Text))
                    .ToArray();
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

    private static string StripDollar(string line)
    {
        var trimmed = line.Trim();
        return trimmed.StartsWith("$ ", StringComparison.Ordinal) ? trimmed["$ ".Length..] : trimmed;
    }

    /// <summary>Classifies one invocation: a runnable example, a bare mention, a declared synopsis, or a documentation defect.</summary>
    private static void Classify(string where, string command, List<string> runnableKeys)
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

            var (key, reason) = TryRunnableKey(tokens[1..]);
            Assert.True(key is not null, $"{where}: '{command}' is not a runnable example: {reason}");
            runnableKeys.Add(key!);
            CheckExample(where, tokens[1..], key!);
            return;
        }

        if (SynopsisVerbs.Contains(verb))
        {
            if (tokens.Length == 4 && verb == "species" && tokens[2] == "--find" && tokens[3] == "H2O")
            {
                RunAndApprove(where, tokens[1..], "species-find-h2o", cutRun: true);
                runnableKeys.Add("species-find-h2o");
            }
            else if (tokens.Length == 3 && verb == "schema" && tokens[2] == "input")
            {
                RunAndApprove(where, tokens[1..], "schema-input", cutRun: false);
                runnableKeys.Add("schema-input");
            }

            return;
        }

        Assert.Fail($"{where}: '{command}' names no command of the declared list (rocket, equilibrium, states, species, devices, schema, --help, --version)");
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
        var approvedPath = ApprovedPathOf(key);
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

    /// <summary>No runnable example (or deterministic synopsis) without an approved file, and no approved file without one (D1).</summary>
    private static void CheckApprovedFilesMatch(IReadOnlyList<string> runnableKeys)
    {
        var directory = Path.GetDirectoryName(ApprovedPathOf("x"))!;
        var approvedKeys = Directory.Exists(directory)
            ? Directory.EnumerateFiles(directory, "*.approved.json")
                .Select(p => Path.GetFileName(p)[..^".approved.json".Length]).ToList()
            : [];

        var missing = runnableKeys.Except(approvedKeys, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        var orphaned = approvedKeys.Except(runnableKeys, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        Assert.True(missing.Count == 0, $"no approved/cli file for the runnable example(s): {string.Join(", ", missing)}");
        Assert.True(orphaned.Count == 0, $"approved/cli file(s) with no matching runnable example: {string.Join(", ", orphaned)}");
    }

    private static string ApprovedPathOf(string key) => RepositoryPaths.Resolve("tests", "Docs.Tests", "approved", "cli", key + ".approved.json");

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

    [Fact]
    public void Every_marked_cli_document_equals_its_samples_cli_file_and_validates_against_its_schema()
    {
        var markers = new List<(string File, int Line, string RelativePath)>();
        foreach (var file in GuideDocuments.SnippetSources())
        {
            var lines = GuideDocuments.Lines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                var match = CliDocumentMarker.Match(lines[i].Trim());
                if (match.Success)
                {
                    markers.Add((file, i, match.Groups[1].Value));
                }
            }
        }

        Assert.True(markers.Count > 0, "no '<!-- cli-document: path -->' marker was found in README.md, docs/guide/*.md or docs/nuget/*.md");

        var schemas = new Dictionary<string, JsonSchema>(StringComparer.Ordinal);
        foreach (var (file, line, relativePath) in markers)
        {
            CheckCliDocumentMarker(file, line, relativePath, schemas);
        }
    }

    private static void CheckCliDocumentMarker(string file, int line, string relativePath, Dictionary<string, JsonSchema> schemas)
    {
        var where = $"{file}:{line + 1}";
        var lines = GuideDocuments.Lines(file);

        var j = line + 1;
        while (j < lines.Length && string.IsNullOrWhiteSpace(lines[j]))
        {
            j++;
        }

        Assert.True(j < lines.Length, $"{where}: the cli-document marker is not followed by a fenced code block");
        Assert.True(GuideDocuments.TryFencedBlockAt(lines, j, out var block), $"{where}: the cli-document marker is not followed by a fenced code block");
        Assert.True(
            block.Info.Equals("json", StringComparison.OrdinalIgnoreCase),
            $"{where}: a cli-document marker must be followed by a ```json block, found a ```{block.Info} block");

        var cliPath = Path.GetFullPath(Path.Combine(GuideDocuments.Root, "samples", "cli", relativePath));
        Assert.True(File.Exists(cliPath), $"{where}: 'samples/cli/{relativePath}' does not exist");

        var quoted = GuideDocuments.Lf(string.Join("\n", block.Body));
        var expected = GuideDocuments.Lf(string.Join("\n", GuideDocuments.Lines(cliPath)));
        Assert.True(expected.Equals(quoted, StringComparison.Ordinal), $"{where}: the shown document differs from samples/cli/{relativePath}");

        var topDirectory = relativePath.Split('/', 2)[0];
        Assert.True(SchemaOfTopDirectory.TryGetValue(topDirectory, out var schemaName), $"{where}: no schema is declared for samples/cli/{topDirectory}/");

        if (!schemas.TryGetValue(schemaName!, out var schema))
        {
            schema = Schema(schemaName!);
            schemas[schemaName!] = schema;
        }

        using var document = JsonDocument.Parse(quoted);
        var errors = schema.Validate(document.RootElement);
        Assert.True(errors.Count == 0, $"{where}: the shown document violates its schema:\n{string.Join("\n", errors)}");
    }

    /// <summary>The schema as `apthermo schema &lt;name&gt;` prints it: read in-process through the command line's tree contract.</summary>
    private static JsonSchema Schema(string name)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var code = APThermo.Cli.Program.Run(["schema", name], output, error);
        Assert.True(code == 0, $"apthermo schema {name} exited with {code}: {error}");
        return JsonSchema.Parse(output.ToString());
    }
}
