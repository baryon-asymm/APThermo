using System.Globalization;
using System.Text.Json;

namespace APThermo.Fixtures;

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

            var provenance = ReadProvenance(path, generator);
            return new CeaCase(name, kind, inputs.Clone(), outputs.Clone(), provenance, path);
        }
    }

    public static IReadOnlyList<CeaCase> LoadAll(string kind) =>
        FixtureFiles.Enumerate(kind).Select(Load).ToArray();

    /// <summary>
    /// The <c>generator</c> block, field by field: each argument reads its own named field of <paramref name="generator"/>
    /// (the root's named-construction condition on a declared wide constructor), rather than an index into a side array
    /// whose order had to be kept in step with <see cref="Provenance"/>'s parameter order by hand.
    /// </summary>
    private static Provenance ReadProvenance(string path, JsonElement generator)
    {
        string Text(string property) => Required(path, generator, "generator." + property, property, JsonValueKind.String).GetString()!;

        DateOnly Date()
        {
            var text = Text("generatedOn");
            if (!DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                throw new FixtureFormatException(path, "generator.generatedOn", $"'{text}' is not a date of the form yyyy-MM-dd");
            }

            return date;
        }

        return new Provenance(
            Package: Text("package"), Version: Text("version"), LibraryVersion: Text("libraryVersion"), Method: Text("method"),
            Script: Text("script"), ScriptSha256: Text("scriptSha256"), ThermoLibSha256: Text("thermoLibSha256"),
            TransLibSha256: Text("transLibSha256"), DataThermoSha256: Text("dataThermoSha256"),
            DataTransSha256: Text("dataTransSha256"), GeneratedOn: Date());
    }

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
