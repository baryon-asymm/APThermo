using System.Globalization;
using System.Text.Json;

namespace AerospacePropellantThermodynamics.Fixtures;

/// <summary>Where a fixture came from: the package, the script and the data files, by version and hash.</summary>
public sealed record Provenance(
    string Package,
    string Version,
    string LibraryVersion,
    string Method,
    string Script,
    string ScriptSha256,
    string ThermoLibSha256,
    string TransLibSha256,
    string DataThermoSha256,
    string DataTransSha256,
    DateOnly GeneratedOn);

/// <summary>One reference case: its inputs and outputs by name, in SI, and its provenance.</summary>
public sealed record CeaCase(
    string Name,
    string Kind,
    JsonElement Inputs,
    JsonElement Outputs,
    Provenance Generator,
    string Path);

/// <summary>A fixture file that is not a fixture document of its kind.</summary>
public sealed class FixtureFormatException(string fileName, string field, string message)
    : Exception($"{fileName}: {field}: {message}")
{
    public string FileName { get; } = fileName;

    public string Field { get; } = field;
}

/// <summary>Reads fixture documents written by the generator scripts.</summary>
public static class CeaFixtures
{
    private static readonly string[] ProvenanceStrings =
    [
        "package", "version", "libraryVersion", "method", "script", "scriptSha256",
        "thermoLibSha256", "transLibSha256", "dataThermoSha256", "dataTransSha256",
    ];

    public static CeaCase Load(string path)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(File.ReadAllText(path));
        }
        catch (JsonException e)
        {
            throw new FixtureFormatException(path, "<document>", e.Message);
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new FixtureFormatException(path, "<document>", "the document is not a JSON object");
            }

            var header = Required(path, root, "case", JsonValueKind.Object);
            var name = Required(path, header, "case.name", "name", JsonValueKind.String).GetString()!;
            var kind = Required(path, header, "case.kind", "kind", JsonValueKind.String).GetString()!;
            var inputs = Required(path, header, "case.inputs", "inputs", JsonValueKind.Object);
            var generator = Required(path, root, "generator", JsonValueKind.Object);
            var outputs = Required(path, root, "outputs", JsonValueKind.Object);

            var directoryKind = System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(path)));
            if (!string.Equals(kind, directoryKind, StringComparison.Ordinal))
            {
                throw new FixtureFormatException(path, "case.kind", $"the kind '{kind}' does not match the directory '{directoryKind}'");
            }

            var strings = new string[ProvenanceStrings.Length];
            for (var k = 0; k < ProvenanceStrings.Length; k++)
            {
                strings[k] = Required(path, generator, "generator." + ProvenanceStrings[k], ProvenanceStrings[k], JsonValueKind.String).GetString()!;
            }

            var dateText = Required(path, generator, "generator.generatedOn", "generatedOn", JsonValueKind.String).GetString()!;
            if (!DateOnly.TryParseExact(dateText, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var generatedOn))
            {
                throw new FixtureFormatException(path, "generator.generatedOn", $"'{dateText}' is not a date of the form yyyy-MM-dd");
            }

            var provenance = new Provenance(
                strings[0], strings[1], strings[2], strings[3], strings[4], strings[5],
                strings[6], strings[7], strings[8], strings[9], generatedOn);
            return new CeaCase(name, kind, inputs.Clone(), outputs.Clone(), provenance, path);
        }
    }

    public static IReadOnlyList<CeaCase> LoadAll(string kind) =>
        FixtureFiles.Enumerate(kind).Select(Load).ToArray();

    private static JsonElement Required(string path, JsonElement parent, string field, JsonValueKind kind) =>
        Required(path, parent, field, field, kind);

    private static JsonElement Required(string path, JsonElement parent, string field, string property, JsonValueKind kind)
    {
        if (!parent.TryGetProperty(property, out var element))
        {
            throw new FixtureFormatException(path, field, "the field is missing");
        }

        if (element.ValueKind != kind)
        {
            throw new FixtureFormatException(path, field, $"expected {kind}, found {element.ValueKind}");
        }

        return element;
    }
}
