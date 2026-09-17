using System.Text.Json;
using System.Text.RegularExpressions;
using APThermo.Harness;

namespace APThermo.Docs.Tests;

/// <summary>
/// L3's two JSON-document facts (BOOT.md), split from `CommandLineExampleTests.cs` to keep that type within the
/// code-shape size limit (root `BOOT.md`, Constraints: 400 lines of code). A JSON document shown next to prose, such
/// as a package README's example input, is tied to its `samples/cli/` file through a
/// `&lt;!-- cli-document: path --&gt;` marker: the following ```json fence must equal that file byte for byte, and the
/// shown document validates against the schema its top-level directory declares, the same way L5 validates the file
/// itself (`Every_marked_cli_document_equals_its_samples_cli_file_and_validates_against_its_schema`). A second fact
/// (MA1) requires that marker on every JSON fence of the guide (info `json`, first word, any case): before this task
/// a JSON fence with no marker at all went entirely unchecked
/// (`Every_json_fence_is_preceded_by_a_cli_document_marker`).
/// </summary>
public sealed class CliDocumentTests
{
    private static readonly Regex CliDocumentMarker = new(@"^<!--\s*cli-document:\s*([\w./-]+)\s*-->$", RegexOptions.Compiled);
    private static readonly Dictionary<string, string> SchemaOfTopDirectory = new(StringComparer.Ordinal) { ["problems"] = "input", ["states"] = "states" };

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
        Assert.True(GuideDocuments.TryFencedBlockAt(lines, j, file, out var block), $"{where}: the cli-document marker is not followed by a fenced code block");
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

    /// <summary>MA1: a shown JSON fence (info string, first word, any case) must carry a `&lt;!-- cli-document: path --&gt;` marker above it, the same rule L1 holds C# fences to.</summary>
    [Fact]
    public void Every_json_fence_is_preceded_by_a_cli_document_marker()
    {
        var fences = new List<(string File, int Line)>();
        foreach (var file in GuideDocuments.SnippetSources())
        {
            var lines = GuideDocuments.Lines(file);
            foreach (var block in GuideDocuments.FencedBlocks(lines, file))
            {
                if (GuideDocuments.IsJsonFenceInfo(block.Info))
                {
                    fences.Add((file, block.StartLine));
                }
            }
        }

        Assert.True(fences.Count > 0, "no json fence was found in README.md, docs/guide/*.md or docs/nuget/*.md");

        foreach (var (file, line) in fences)
        {
            CheckPrecededByCliDocumentMarker(file, line);
        }
    }

    private static void CheckPrecededByCliDocumentMarker(string file, int fenceLine)
    {
        var lines = GuideDocuments.Lines(file);
        var i = fenceLine - 1;
        while (i >= 0 && string.IsNullOrWhiteSpace(lines[i]))
        {
            i--;
        }

        Assert.True(
            i >= 0 && CliDocumentMarker.IsMatch(lines[i].Trim()),
            $"{file}:{fenceLine + 1}: this json fence is not preceded by a '<!-- cli-document: path -->' marker");
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
