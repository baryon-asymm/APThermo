using System.Text;
using System.Text.Json;
using APThermo.Fixtures;
using APThermo.Harness;

namespace APThermo.Docs.Tests;

/// <summary>
/// L3 (BOOT.md): every `apthermo …` invocation shown in a fenced block of README.md, docs/guide/*.md or
/// docs/nuget/*.md (not console fences alone: any fence, since a page may show a synopsis in a plain or `json`
/// fence too) is accounted for. A runnable example is a single line, pins `--accelerator cpu`, names exactly one
/// committed document under samples/cli/, and delivers no output to a file (`--output` and `--format` absent); it
/// runs in-process through the command line's tree contract with exit code 0, and its delivered document, with the
/// top-level `run` property cut out, equals the approved file keyed by the input document's name. Every other
/// `apthermo` invocation is a declared synopsis: counted, but not run (a command without a committed input, for
/// example `apthermo devices`, is always a synopsis by this rule; a page shows no output next to its command today,
/// so the whole delivered document is the thing approved, not a line shown on the page). Fails when no invocation
/// exists anywhere in the guide, and when a runnable example and an approved file do not name each other (a
/// missing file and an orphan both fail; AGENTS.md §13).
/// </summary>
public sealed class CommandLineExampleTests
{
    [Fact]
    public void Every_command_line_invocation_is_a_checked_example_or_a_declared_synopsis()
    {
        var invocations = InvocationsOf(GuideDocuments.SnippetSources());
        Assert.True(invocations.Count > 0, "no 'apthermo …' invocation was found in a fenced block of README.md, docs/guide/*.md or docs/nuget/*.md");

        var runnableKeys = new List<string>();
        foreach (var (file, line, command) in invocations)
        {
            var key = RunnableKeyOf(command);
            if (key is not null)
            {
                runnableKeys.Add(key);
                CheckExample(file, line, command, key);
            }
        }

        CheckApprovedFilesMatch(runnableKeys);
    }

    /// <summary>Every fenced block of the guide whose first non-empty line is an apthermo invocation, the leading `$ ` stripped.</summary>
    private static List<(string File, int Line, string Command)> InvocationsOf(IReadOnlyList<string> pages)
    {
        var found = new List<(string File, int Line, string Command)>();
        foreach (var page in pages)
        {
            var lines = GuideDocuments.Lines(page);
            foreach (var block in GuideDocuments.FencedBlocks(lines))
            {
                var nonEmpty = block.Body.Where(l => !string.IsNullOrWhiteSpace(l)).ToArray();
                if (nonEmpty.Length != 1)
                {
                    continue; // a synopsis with shown output is not yet a supported form (this node's BOOT.md records the choice)
                }

                var command = StripDollar(nonEmpty[0]);
                if (command.StartsWith("apthermo ", StringComparison.Ordinal))
                {
                    found.Add((page, block.StartLine, command));
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

    /// <summary>
    /// The input document's key (its file name without extension) when the invocation is runnable: `--accelerator
    /// cpu`, no `--output` or `--format`, and exactly one existing samples/cli/ document among its tokens. Null
    /// otherwise: a declared synopsis.
    /// </summary>
    private static string? RunnableKeyOf(string command)
    {
        var args = command.Split(' ', StringSplitOptions.RemoveEmptyEntries)[1..];
        if (OptionValue(args, "--accelerator") != "cpu" || OptionValue(args, "--output") is not null || OptionValue(args, "--format") is not null)
        {
            return null;
        }

        var inputs = InputDocumentsOf(args);
        return inputs.Count == 1 ? Path.GetFileNameWithoutExtension(inputs[0]) : null;
    }

    private static void CheckExample(string file, int line, string command, string key)
    {
        var where = $"{file}:{line + 1}";
        var args = command.Split(' ', StringSplitOptions.RemoveEmptyEntries)[1..];
        var input = InputDocumentsOf(args)[0];
        args = args.Select(a => a == input ? Path.GetFullPath(Path.Combine(GuideDocuments.Root, input)) : a).ToArray();

        var output = new StringWriter();
        var error = new StringWriter();
        var code = APThermo.Cli.Program.Run(args, output, error);
        Assert.True(code == 0, $"{where}: the example exited with {code}: {error}");

        var document = output.ToString();
        using (var parsed = JsonDocument.Parse(document))
        {
            Assert.True(parsed.RootElement.ValueKind == JsonValueKind.Object, $"{where}: the example delivered no JSON object to standard output");
        }

        var cut = RunPropertyCut.Bytes(Encoding.UTF8.GetBytes(document), key);
        var approvedPath = ApprovedPathOf(key);
        var actual = GuideDocuments.Lf(Encoding.UTF8.GetString(cut));
        var approved = GuideDocuments.Lf(File.ReadAllText(approvedPath));
        if (!approved.Equals(actual, StringComparison.Ordinal))
        {
            var actualPath = Path.Combine(Path.GetDirectoryName(approvedPath)!, key + ".actual.json");
            File.WriteAllText(actualPath, actual, new UTF8Encoding(false));
            Assert.Fail($"{where}: the delivered document differs from its approved file: approved {approvedPath}, actual {actualPath}");
        }
    }

    /// <summary>No runnable example without an approved file, and no approved file without a runnable example (D1).</summary>
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
}
