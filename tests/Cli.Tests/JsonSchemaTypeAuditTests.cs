using APThermo.Harness;

namespace APThermo.Cli.Tests;

/// <summary>Schema audit guards apply before an instance chooses a branch.</summary>
public sealed class JsonSchemaTypeAuditTests
{
    /// <summary>Parsing validates type names in branches no instance ever visits.</summary>
    [Theory]
    [InlineData("""{"properties":{"unused":{"type":"numbre"}}}""")]
    [InlineData("""{"$defs":{"unused":{"type":["number","numbre"]}}}""")]
    [InlineData("""{"type":[]}""")]
    [InlineData("""{"type":["number","number"]}""")]
    public void UnknownTypeInUnvisitedBranchFailsAtParse(string schema) =>
        _ = Assert.Throws<InvalidOperationException>(() => JsonSchema.Parse(schema));
}
