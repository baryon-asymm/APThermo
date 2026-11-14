namespace APThermo.Protocol.Tests;

/// <summary>Optional struct defaults are not null references in a readable signature snapshot.</summary>
public sealed class SurfaceDefaultsTests
{
    /// <summary>Metadata null for a nonnullable value type means default; real nullable/reference null stays null.</summary>
    [Fact]
    public void OptionalDefaultsKeepTheirCompiledTypeMeaning()
    {
        _ = SignatureExample<string>.DefaultArguments();
        (string Header, IEnumerable<Type> Types)[] groups = [("fixture", [typeof(SignatureExample<string>)])];
        var text = SurfaceTests.DescribeTypes("first", "second", "third", groups, MemberAccessibility.IsPublic);
        Assert.Contains("token = default", text, StringComparison.Ordinal);
        Assert.Contains("text = null", text, StringComparison.Ordinal);
        Assert.Contains("number = null", text, StringComparison.Ordinal);
    }
}
