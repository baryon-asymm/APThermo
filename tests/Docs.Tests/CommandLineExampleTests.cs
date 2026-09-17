using System.Text;
using System.Text.Json;
using APThermo.Fixtures;
using APThermo.Harness;

namespace APThermo.Docs.Tests;

/// <summary>
/// L3 (BOOT.md): every command-line example of the guide — a fenced `console` block whose first line is
/// `$ apthermo …` — names its input document under samples/cli/, runs in-process through the command line's tree
/// contract with exit code 0, and delivers its approved JSON document with the top-level `run` property cut as the
/// command line's tests cut it. Examples pin `--accelerator cpu`, so the approved bytes hold on a CUDA machine and
/// on CI alike. Passes vacuously while no guide page carries an example.
/// </summary>
public sealed class CommandLineExampleTests
{
    [Fact]
    public void Every_command_line_example_runs_and_matches_its_approved_document()
    {
        var examples = new List<(string File, int Line, string Command)>();
        foreach (var page in GuideDocuments.GuidePages())
        {
            var lines = GuideDocuments.Lines(page);
            foreach (var block in GuideDocuments.FencedBlocks(lines))
            {
                if (!block.Info.Equals("console", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var nonEmpty = block.Body.Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
                Assert.True(
                    nonEmpty.Count == 1,
                    $"{page}:{block.StartLine + 1}: a command-line example is one '$ apthermo …' line, found {nonEmpty.Count}");

                var first = nonEmpty[0];
                Assert.True(
                    first.StartsWith("$ apthermo", StringComparison.Ordinal),
                    $"{page}:{block.StartLine + 1}: the example's line must be '$ apthermo …', found '{first}'");

                examples.Add((page, block.StartLine, first["$ ".Length..].Trim()));
            }
        }

        if (examples.Count == 0)
        {
            return; // no guide page carries an example yet: nothing to check
        }

        foreach (var (file, line, command) in examples)
        {
            CheckExample(file, line, command);
        }
    }

    private static void CheckExample(string file, int line, string command)
    {
        var where = $"{file}:{line + 1}";
        var tokens = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        Assert.True(tokens[0] == "apthermo", $"{where}: the example must run apthermo");

        var args = tokens[1..];
        Assert.True(OptionValue(args, "--accelerator") == "cpu", $"{where}: examples pin '--accelerator cpu', so the approved bytes hold on every machine");
        Assert.True(OptionValue(args, "--output") is null, $"{where}: the example must deliver its document to standard output, not a file");
        Assert.True(OptionValue(args, "--format") is null, $"{where}: the example must deliver JSON, the command line's default");

        var input = InputDocumentOf(args, where);
        var key = Path.GetFileNameWithoutExtension(input);
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
        var approvedPath = RepositoryPaths.Resolve("tests", "Docs.Tests", "approved", "cli", key + ".approved.json");
        var actual = GuideDocuments.Lf(Encoding.UTF8.GetString(cut));
        if (!File.Exists(approvedPath))
        {
            Assert.Fail($"{where}: the approved document is missing: {approvedPath}");
        }

        var approved = GuideDocuments.Lf(File.ReadAllText(approvedPath));
        if (!approved.Equals(actual, StringComparison.Ordinal))
        {
            var actualPath = Path.Combine(Path.GetDirectoryName(approvedPath)!, key + ".actual.json");
            File.WriteAllText(actualPath, actual, new UTF8Encoding(false));
            Assert.Fail($"{where}: the delivered document differs from its approved file: approved {approvedPath}, actual {actualPath}");
        }
    }

    /// <summary>The option's value, or null when the option is absent (or dangling).</summary>
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

    /// <summary>The example's input document: the one argument that is a committed file under samples/cli/.</summary>
    private static string InputDocumentOf(IReadOnlyList<string> args, string where)
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

        Assert.True(
            found.Count == 1,
            $"{where}: the example must name exactly one input document under samples/cli/, found: {string.Join(", ", found)}");
        return found[0];
    }
}
