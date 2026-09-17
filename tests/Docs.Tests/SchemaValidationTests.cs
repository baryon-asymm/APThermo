using System.Text.Json;
using APThermo.Harness;

namespace APThermo.Docs.Tests;

/// <summary>
/// L5 (BOOT.md): every document under samples/cli/problems/ validates against the `input` schema and every record of
/// a file under samples/cli/states/ (`json` array, object or `jsonl`) against `states`. The schemas are read through
/// `apthermo schema <name>` in-process, so no copy lives here. Passes vacuously while no sample document exists.
/// </summary>
public sealed class SchemaValidationTests
{
    [Fact]
    public void Every_problem_document_validates_against_the_input_schema()
    {
        var schema = Schema("input");
        foreach (var path in Under(Path.Combine("samples", "cli", "problems"), "*.json"))
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var errors = schema.Validate(document.RootElement);
            Assert.True(errors.Count == 0, $"{path} violates the input schema:\n{string.Join("\n", errors)}");
        }
    }

    [Fact]
    public void Every_states_file_validates_against_the_states_schema()
    {
        var schema = Schema("states");
        foreach (var path in Under(Path.Combine("samples", "cli", "states"), "*"))
        {
            if (path.EndsWith(".jsonl", StringComparison.Ordinal))
            {
                var line = 0;
                foreach (var text in File.ReadLines(path))
                {
                    line++;
                    if (string.IsNullOrWhiteSpace(text))
                    {
                        continue;
                    }

                    using var record = JsonDocument.Parse(text);
                    var errors = schema.Validate(record.RootElement);
                    Assert.True(errors.Count == 0, $"{path}:{line} violates the states schema:\n{string.Join("\n", errors)}");
                }

                continue;
            }

            if (!path.EndsWith(".json", StringComparison.Ordinal))
            {
                continue; // neither json nor jsonl: not a states document
            }

            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var fileErrors = schema.Validate(document.RootElement);
            Assert.True(fileErrors.Count == 0, $"{path} violates the states schema:\n{string.Join("\n", fileErrors)}");
        }
    }

    /// <summary>The schema as `apthermo schema <name>` prints it: read in-process through the command line's tree contract.</summary>
    private static JsonSchema Schema(string name)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var code = APThermo.Cli.Program.Run(["schema", name], output, error);
        Assert.True(code == 0, $"apthermo schema {name} exited with {code}: {error}");
        return JsonSchema.Parse(output.ToString());
    }

    private static IEnumerable<string> Under(string relativeDirectory, string pattern)
    {
        var root = Path.GetFullPath(Path.Combine(GuideDocuments.Root, relativeDirectory));
        if (!Directory.Exists(root))
        {
            yield break; // no sample documents yet: nothing to check
        }

        foreach (var file in Directory.EnumerateFiles(root, pattern).Order(StringComparer.Ordinal))
        {
            yield return file;
        }
    }
}
