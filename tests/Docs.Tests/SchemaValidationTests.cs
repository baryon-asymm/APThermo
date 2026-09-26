using System.Text.Json;
using APThermo.Harness;

namespace APThermo.Docs.Tests;

/// <summary>
/// L5 (BOOT.md): every file under samples/cli/, walked recursively, validates against the schema its top-level
/// directory declares (`problems` against `input`, `states` against `states`; `GuideDocuments.CliDocuments` fails
/// loudly on any other top-level directory, so a document cannot escape validation by living somewhere new). A
/// `.jsonl` file is checked record by record; every other file is one JSON document. The schemas are read through
/// `apthermo schema &lt;name&gt;` in-process, so no copy lives here. Fails when no document exists under samples/cli/.
/// </summary>
public sealed class SchemaValidationTests
{
    /// <summary>Every document under samples cli validates against its schema.</summary>
    [Fact]
    public void EveryDocumentUnderSamplesCliValidatesAgainstItsSchema()
    {
        var documents = GuideDocuments.CliDocuments();
        Assert.True(documents.Count > 0, "no document was found under samples/cli/");

        var schemas = new Dictionary<string, JsonSchema>(StringComparer.Ordinal);
        foreach (var (path, schemaName) in documents)
        {
            if (!schemas.TryGetValue(schemaName, out var schema))
            {
                schema = Schema(schemaName);
                schemas[schemaName] = schema;
            }

            CheckDocument(path, schema);
        }
    }

    private static void CheckDocument(string path, JsonSchema schema)
    {
        if (path.EndsWith(".jsonl", StringComparison.OrdinalIgnoreCase))
        {
            CheckJsonLines(path, schema);
            return;
        }

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var errors = schema.Validate(document.RootElement);
        Assert.True(errors.Count == 0, $"{path} violates its schema:\n{string.Join("\n", errors)}");
    }

    private static void CheckJsonLines(string path, JsonSchema schema)
    {
        var line = 0;
        foreach (var text in File.ReadLines(path))
        {
            line++;
            CheckJsonLine(path, line, text, schema);
        }
    }

    private static void CheckJsonLine(string path, int line, string text, JsonSchema schema)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        using var record = JsonDocument.Parse(text);
        var errors = schema.Validate(record.RootElement);
        Assert.True(errors.Count == 0, $"{path}:{line} violates its schema:\n{string.Join("\n", errors)}");
    }

    /// <summary>The schema as `apthermo schema &lt;name&gt;` prints it: read in-process through the command line's tree contract.</summary>
    private static JsonSchema Schema(string name)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var code = Cli.Program.Run(["schema", name], output, error);
        Assert.True(code == 0, $"apthermo schema {name} exited with {code}: {error}");
        return JsonSchema.Parse(output.ToString());
    }
}
