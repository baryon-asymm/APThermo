using System.Text.Json;
using APThermo.Harness;

namespace APThermo.Fixtures.Tests;

/// <summary>Whole-entry finite-domain and unchanged-budget controls for delivered JSON results.</summary>
public sealed class JsonComparisonGuardTests
{
    /// <summary>Invalid finite-domain output cannot exploit infinity arithmetic; ordinary budgets retain their boundary.</summary>
    [Theory]
    [InlineData("1", "1e400", false)]
    [InlineData("1e400", "1", false)]
    [InlineData("1e400", "-1e400", false)]
    [InlineData("1e400", "1e400", false)]
    [InlineData("1", "2", false)]
    [InlineData("1", "1.0000000001", true)]
    [InlineData("1", "1.000000002", false)]
    [InlineData("0", "-0", true)]
    [InlineData("1.7976931348623157e308", "1.7976931348623155e308", true)]
    [InlineData("1.7976931348623157e308", "-1.7976931348623157e308", false)]
    public void NumericResultsRespectTheirFiniteDomain(string approved, string actual, bool matches)
    {
        using var expected = JsonDocument.Parse(approved);
        using var delivered = JsonDocument.Parse(actual);
        Assert.Equal(matches, JsonFieldComparison.Mismatch(expected.RootElement, delivered.RootElement, "$") is null);
    }

    /// <summary>The complete recursive entry point names the exact nested field that overflowed.</summary>
    [Fact]
    public void NestedNonfiniteResultNamesItsJsonPath()
    {
        using var approved = JsonDocument.Parse("""{"records":[{"value":1}]}""");
        using var actual = JsonDocument.Parse("""{"records":[{"value":1e400}]}""");
        var problem = JsonFieldComparison.Mismatch(approved.RootElement, actual.RootElement, "$");
        Assert.NotNull(problem);
        Assert.Contains("$.records[0].value", problem, StringComparison.Ordinal);
    }
}
