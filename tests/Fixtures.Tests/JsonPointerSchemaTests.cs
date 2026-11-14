using System.Text.Json;
using APThermo.Harness;

namespace APThermo.Fixtures.Tests;

/// <summary>URI-fragment decoding must not select a similarly spelled but different schema.</summary>
public sealed class JsonPointerSchemaTests
{
    /// <summary>The wrong-target canary distinguishes percent decoding from literal lookup and from double decoding.</summary>
    [Theory]
    [InlineData("#/$defs/%61", false)]
    [InlineData("#/$defs/%2561", true)]
    public void PercentDecodingSelectsTheRightSchemaExactlyOnce(string reference, bool conforms)
    {
        var schema = JsonSchema.Parse(JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["$defs"] = new Dictionary<string, bool> { ["a"] = false, ["%61"] = true },
            ["$ref"] = reference,
        }));
        using var instance = JsonDocument.Parse("1");
        Assert.Equal(conforms, schema.Validate(instance.RootElement).Count == 0);
    }

    /// <summary>Valid escapes preserve literal percent/tilde/slash, encoded Unicode/reserved characters and plus.</summary>
    [Theory]
    [InlineData("a~2", "#/$defs/a~02")]
    [InlineData("a/b~c", "#/$defs/a~1b~0c")]
    [InlineData("a/b", "#/$defs/a%7E1b")]
    [InlineData("~1", "#/$defs/~01")]
    [InlineData("a", "#%2F$defs%2Fa")]
    [InlineData("%FF", "#/$defs/%25FF")]
    [InlineData("%2F", "#/$defs/%252F")]
    [InlineData("%", "#/$defs/%25")]
    [InlineData("a b", "#/$defs/a%20b")]
    [InlineData("a+b", "#/$defs/a+b")]
    [InlineData("a#b", "#/$defs/a%23b")]
    [InlineData("a[0]", "#/$defs/a%5B0%5D")]
    [InlineData("π", "#/$defs/%CF%80")]
    [InlineData("😺", "#/$defs/%F0%9F%98%BA")]
    [InlineData("\"", "#/$defs/%22")]
    [InlineData("\0", "#/$defs/%00")]
    [InlineData("\\", "#/$defs/%5C")]
    public void ValidObjectReferencesRemainSupported(string property, string reference)
    {
        var schema = JsonSchema.Parse(Schema(property, reference));
        using var instance = JsonDocument.Parse("1");
        Assert.Empty(schema.Validate(instance.RootElement));
    }

    /// <summary>Malformed forms cannot become valid merely because their raw spelling is a real object property.</summary>
    [Theory]
    [InlineData("#/$defs/a~2")]
    [InlineData("#/$defs/a~")]
    [InlineData("#/$defs/a%7E2")]
    [InlineData("#/$defs/%")]
    [InlineData("#/$defs/%6")]
    [InlineData("#/$defs/%GG")]
    [InlineData("#/$defs/%FF")]
    [InlineData("#/$defs/%80")]
    [InlineData("#/$defs/%C3")]
    [InlineData("#/$defs/%C3%28")]
    [InlineData("#/$defs/%C0%AF")]
    [InlineData("#/$defs/%ED%A0%80")]
    [InlineData("#/$defs/%F4%90%80%80")]
    [InlineData("#/$defs/has space")]
    [InlineData("#/$defs/hash#")]
    [InlineData("#/$defs/raw\\backslash")]
    public void InvalidFragmentsFailBeforeLiteralLookup(string reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        var rawProperty = reference[(reference.LastIndexOf('/') + 1)..];
        _ = Assert.Throws<InvalidOperationException>(() => JsonSchema.Parse(Schema(rawProperty, reference)));
    }

    /// <summary>Percent-encoded slashes delimit tokens; a plus is never form-decoded into a space.</summary>
    [Fact]
    public void UriDecodingPrecedesSplittingWithoutFormOrUnicodeNormalization()
    {
        const string text = """{"$defs":{"a":{"$defs":{"b":false}},"a%2F$defs%2Fb":true,"a+b":true,"a b":false,"é":false,"é":true},"$ref":"#/$defs/a%2F$defs%2Fb"}""";
        using var instance = JsonDocument.Parse("1");
        Assert.NotEmpty(JsonSchema.Parse(text).Validate(instance.RootElement));
        Assert.Empty(JsonSchema.Parse(text.Replace("#/$defs/a%2F$defs%2Fb", "#/$defs/a+b", StringComparison.Ordinal)).Validate(instance.RootElement));
        Assert.Empty(JsonSchema.Parse(text.Replace("#/$defs/a%2F$defs%2Fb", "#/$defs/e%CC%81", StringComparison.Ordinal)).Validate(instance.RootElement));
    }

    private static string Schema(string property, string reference) => JsonSerializer.Serialize(new Dictionary<string, object>
    {
        ["$defs"] = new Dictionary<string, bool> { [property] = true },
        ["$ref"] = reference,
    });
}
