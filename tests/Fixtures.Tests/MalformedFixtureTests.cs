namespace AerospacePropellantThermodynamics.Fixtures.Tests;

/// <summary>L1: a document that is not a fixture of its kind fails the loader with the file name and the field.</summary>
public sealed class MalformedFixtureTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "apthermo-fixtures-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private const string Generator = """
        "generator": {
          "package": "cea", "version": "3.3.4", "libraryVersion": "3.3.4", "method": "cea-package",
          "script": "x.py", "scriptSha256": "0", "thermoLibSha256": "0", "transLibSha256": "0",
          "dataThermoSha256": "0", "dataTransSha256": "0", "generatedOn": "2026-09-12"
        }
        """;

    private string Write(string kind, string text)
    {
        var directory = Path.Combine(_root, kind);
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "case.json");
        File.WriteAllText(path, text);
        return path;
    }

    [Fact]
    public void A_complete_document_loads()
    {
        var path = Write("tp", $$$"""{"case": {"name": "x", "kind": "tp", "inputs": {"a": 1}}, {{{Generator}}}, "outputs": {"b": 2}}""");
        var c = CeaFixtures.Load(path);
        Assert.Equal("x", c.Name);
        Assert.Equal("tp", c.Kind);
        Assert.Equal(new DateOnly(2026, 9, 12), c.Generator.GeneratedOn);
        Assert.Equal(2, c.Outputs.GetProperty("b").GetInt32());
    }

    [Fact]
    public void A_missing_field_names_the_file_and_the_field()
    {
        var path = Write("tp", $$$"""{"case": {"name": "x", "kind": "tp", "inputs": {}}, {{{Generator}}}}""");
        var e = Assert.Throws<FixtureFormatException>(() => CeaFixtures.Load(path));
        Assert.Equal(path, e.FileName);
        Assert.Equal("outputs", e.Field);
        Assert.Contains(path, e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_missing_provenance_field_is_named()
    {
        var path = Write("tp", """{"case": {"name": "x", "kind": "tp", "inputs": {}}, "generator": {"package": "cea"}, "outputs": {}}""");
        var e = Assert.Throws<FixtureFormatException>(() => CeaFixtures.Load(path));
        Assert.Equal("generator.version", e.Field);
    }

    [Fact]
    public void A_kind_that_does_not_match_its_directory_is_rejected()
    {
        var path = Write("hp", $$$"""{"case": {"name": "x", "kind": "tp", "inputs": {}}, {{{Generator}}}, "outputs": {}}""");
        var e = Assert.Throws<FixtureFormatException>(() => CeaFixtures.Load(path));
        Assert.Equal("case.kind", e.Field);
    }

    [Fact]
    public void Text_that_is_not_json_is_rejected_with_the_file_name()
    {
        var path = Write("tp", "not json");
        var e = Assert.Throws<FixtureFormatException>(() => CeaFixtures.Load(path));
        Assert.Equal(path, e.FileName);
    }

    [Fact]
    public void A_tolerance_without_a_derivation_is_rejected()
    {
        var path = Write("tolerances", """{"fields": {"temperature": {"absolute": 0.1, "relative": 0.0}}}""");
        var e = Assert.Throws<FixtureFormatException>(() => ToleranceTable.Load(path));
        Assert.Equal("temperature.derivation", e.Field);
    }

    [Fact]
    public void A_negative_tolerance_is_rejected()
    {
        var path = Write("tolerances", """{"fields": {"temperature": {"absolute": -0.1, "relative": 0.0, "derivation": "x"}}}""");
        var e = Assert.Throws<FixtureFormatException>(() => ToleranceTable.Load(path));
        Assert.Equal("temperature.absolute", e.Field);
    }
}
