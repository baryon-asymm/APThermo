using System.Text.Json;
using APThermo.Harness;

namespace APThermo.Fixtures.Tests;

/// <summary>Negative and positive controls for the shared reference/schema/snapshot infrastructure.</summary>
public sealed class OracleGuardTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "apthermo-oracle-guards-" + Guid.NewGuid().ToString("N"));

    /// <summary>Removes only the private temporary directory created by this fixture.</summary>
    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    /// <summary>An unconventional name cannot make an actual write overwrite its approved input.</summary>
    [Theory]
    [InlineData("baseline.txt", "baseline.actual.txt")]
    [InlineData("Bits.approved.txt", "Bits.actual.txt")]
    [InlineData("prefix.approved.name.approved.txt", "prefix.approved.name.actual.txt")]
    [InlineData("baseline.APPROVED.txt", "baseline.APPROVED.actual.txt")]
    public void SnapshotNamesPreserveApprovedBytes(string approvedName, string actualName)
    {
        var approved = Write(approvedName, "case expected\n");
        var snapshot = ApprovedSnapshot.Load(approved);
        Assert.NotNull(snapshot.Problem("case", "actual", ["temperature=300"]));
        Assert.Equal("case expected\n", File.ReadAllText(approved));
        Assert.Equal("case actual", Assert.Single(File.ReadAllLines(Path.Combine(_directory, actualName))));
        var dump = Assert.Single(Directory.GetFiles(_directory, "*.fields.txt"));
        Assert.Equal("temperature=300", Assert.Single(File.ReadAllLines(dump)));
    }

    /// <summary>Draft-2020-style reference siblings remain constraints on the same instance.</summary>
    [Fact]
    public void ReferenceSiblingsAreAppliedToTheSameValue()
    {
        var schema = JsonSchema.Parse("""{"$defs":{"number":{"type":"number"}},"$ref":"#/$defs/number","minimum":10}""");
        using var rejected = JsonDocument.Parse("5");
        using var accepted = JsonDocument.Parse("10");
        Assert.NotEmpty(schema.Validate(rejected.RootElement));
        Assert.Empty(schema.Validate(accepted.RootElement));
    }

    /// <summary>Unknown keywords cannot hide in a schema branch that this particular instance never visits.</summary>
    [Theory]
    [InlineData("""{"properties":{"unused":{"maximum":1}}}""")]
    [InlineData("""{"$defs":{"unused":{"maximum":1}}}""")]
    [InlineData("""{"items":{"maximum":1}}""")]
    [InlineData("""{"additionalProperties":{"maximum":1}}""")]
    [InlineData("""{"oneOf":[{"type":"string"},{"maximum":1}]}""")]
    [InlineData("""{"anyOf":[{"type":"string"},{"maximum":1}]}""")]
    public void ParseAuditsEverySchemaBranch(string text)
    {
        var exception = Assert.Throws<InvalidOperationException>(() => JsonSchema.Parse(text));
        Assert.Contains("maximum", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>A broken local reference is refused even in an unused definition.</summary>
    [Fact]
    public void ParseRejectsAnUnusedBrokenReference()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => JsonSchema.Parse("""{"$defs":{"unused":{"$ref":"#/$defs/missing"}}}"""));
        Assert.Contains("reference", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>The finite supported subset refuses a reference cycle before validation can recurse forever.</summary>
    [Fact]
    public void ParseRejectsReferenceCycles()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => JsonSchema.Parse("""{"$defs":{"self":{"$ref":"#/$defs/self"}}}"""));
        Assert.Contains("cyclic", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Local JSON Pointer segments decode escaped slash and tilde characters.</summary>
    [Fact]
    public void LocalPointersDecodeEscapedSegments()
    {
        var schema = JsonSchema.Parse("""{"$defs":{"a/b~c":{"type":"number"}},"$ref":"#/$defs/a~1b~0c"}""");
        using var instance = JsonDocument.Parse("2");
        Assert.Empty(schema.Validate(instance.RootElement));
    }

    /// <summary>Boolean schema boxes have their standard accepting or rejecting meaning.</summary>
    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    public void BooleanSchemasAreSupported(string text, bool accepts)
    {
        var schema = JsonSchema.Parse(text);
        using var instance = JsonDocument.Parse("null");
        Assert.Equal(accepts, schema.Validate(instance.RootElement).Count == 0);
    }

    /// <summary>Objects in enum values are instance data, not schema boxes to audit.</summary>
    [Fact]
    public void EnumObjectsAreNotMistakenForSchemas()
    {
        var schema = JsonSchema.Parse("""{"enum":[{"unsupported":true}]}""");
        using var instance = JsonDocument.Parse("""{"unsupported":true}""");
        Assert.Empty(schema.Validate(instance.RootElement));
    }

    /// <summary>An overflowing JSON tolerance is not a valid finite comparison budget.</summary>
    [Theory]
    [InlineData("absolute")]
    [InlineData("relative")]
    public void OverflowedTolerancesAreRejected(string property)
    {
        var absolute = property == "absolute" ? "1e309" : "0";
        var relative = property == "relative" ? "1e309" : "0";
        var text = "{\"fields\":{\"value\":{\"absolute\":" + absolute + ",\"relative\":" + relative + ",\"derivation\":\"guard control\"}}}";
        var path = Write("bad-tolerances.json", text);
        var exception = Assert.Throws<FixtureFormatException>(() => ToleranceTable.Load(path));
        Assert.Contains("value." + property, exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Exact zero remains a legitimate finite tolerance.</summary>
    [Fact]
    public void ExactZeroToleranceRemainsValid()
    {
        var path = Write("exact-tolerances.json", """{"fields":{"value":{"absolute":0,"relative":0,"derivation":"exact control"}}}""");
        var table = ToleranceTable.Load(path);
        Assert.True(table.Matches("value", 3, 3));
        Assert.False(table.Matches("value", 3, 4));
    }

    /// <summary>Nonfinite reference or actual values cannot be admitted through a finite table.</summary>
    [Theory]
    [InlineData(double.PositiveInfinity, 3)]
    [InlineData(double.NegativeInfinity, 3)]
    [InlineData(3, double.PositiveInfinity)]
    [InlineData(double.NaN, double.NaN)]
    public void NonfiniteComparedValuesDoNotMatch(double expected, double actual)
    {
        var path = Write("finite-tolerances.json", """{"fields":{"value":{"absolute":0,"relative":1,"derivation":"finite control"}}}""");
        Assert.False(ToleranceTable.Load(path).Matches("value", expected, actual));
    }

    private string Write(string name, string text)
    {
        _ = Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, name);
        File.WriteAllText(path, text);
        return path;
    }
}
