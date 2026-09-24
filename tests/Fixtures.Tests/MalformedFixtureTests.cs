namespace APThermo.Fixtures.Tests;

/// <summary>L1: a document that is not a fixture of its kind fails the loader with the file name and the field.</summary>
public sealed class MalformedFixtureTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "apthermo-fixtures-" + Guid.NewGuid().ToString("N"));

    /// <summary>Removes the temporary directory this fixture wrote its documents into.</summary>
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
        _ = Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "case.json");
        File.WriteAllText(path, text);
        return path;
    }

    /// <summary>A complete document loads.</summary>
    [Fact]
    public void ACompleteDocumentLoads()
    {
        var path = Write("tp", $$$"""{"case": {"name": "x", "kind": "tp", "inputs": {"a": 1}}, {{{Generator}}}, "outputs": {"b": 2}}""");
        var c = CeaFixtures.Load(path);
        Assert.Equal("x", c.Name);
        Assert.Equal("tp", c.Kind);
        Assert.Equal(new DateOnly(2026, 9, 12), c.Generator.GeneratedOn);
        Assert.Equal(2, c.Outputs.GetProperty("b").GetInt32());
    }

    /// <summary>A missing field names the file and the field.</summary>
    [Fact]
    public void AMissingFieldNamesTheFileAndTheField()
    {
        var path = Write("tp", $$$"""{"case": {"name": "x", "kind": "tp", "inputs": {}}, {{{Generator}}}}""");
        var e = Assert.Throws<FixtureFormatException>(() => CeaFixtures.Load(path));
        Assert.Equal(path, e.FileName);
        Assert.Equal("outputs", e.Field);
        Assert.Contains(path, e.Message, StringComparison.Ordinal);
    }

    /// <summary>A missing provenance field is named.</summary>
    [Fact]
    public void AMissingProvenanceFieldIsNamed()
    {
        var path = Write("tp", """{"case": {"name": "x", "kind": "tp", "inputs": {}}, "generator": {"package": "cea"}, "outputs": {}}""");
        var e = Assert.Throws<FixtureFormatException>(() => CeaFixtures.Load(path));
        Assert.Equal("generator.version", e.Field);
    }

    /// <summary>A kind that does not match its directory is rejected.</summary>
    [Fact]
    public void AKindThatDoesNotMatchItsDirectoryIsRejected()
    {
        var path = Write("hp", $$$"""{"case": {"name": "x", "kind": "tp", "inputs": {}}, {{{Generator}}}, "outputs": {}}""");
        var e = Assert.Throws<FixtureFormatException>(() => CeaFixtures.Load(path));
        Assert.Equal("case.kind", e.Field);
    }

    /// <summary>Text that is not json is rejected with the file name.</summary>
    [Fact]
    public void TextThatIsNotJsonIsRejectedWithTheFileName()
    {
        var path = Write("tp", "not json");
        var e = Assert.Throws<FixtureFormatException>(() => CeaFixtures.Load(path));
        Assert.Equal(path, e.FileName);
    }

    /// <summary>A tolerance without a derivation is rejected.</summary>
    [Fact]
    public void AToleranceWithoutADerivationIsRejected()
    {
        var path = Write("tolerances", """{"fields": {"temperature": {"absolute": 0.1, "relative": 0.0}}}""");
        var e = Assert.Throws<FixtureFormatException>(() => ToleranceTable.Load(path));
        Assert.Equal("temperature.derivation", e.Field);
    }

    /// <summary>A negative tolerance is rejected.</summary>
    [Fact]
    public void ANegativeToleranceIsRejected()
    {
        var path = Write("tolerances", """{"fields": {"temperature": {"absolute": -0.1, "relative": 0.0, "derivation": "x"}}}""");
        var e = Assert.Throws<FixtureFormatException>(() => ToleranceTable.Load(path));
        Assert.Equal("temperature.absolute", e.Field);
    }
}
