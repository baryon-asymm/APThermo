using System.Text.Json;

namespace AerospacePropellantThermodynamics.Fixtures.Tests;

/// <summary>L1: the tolerance table loads, covers the fields the fixtures report, and compares as documented.</summary>
public sealed class ToleranceTableTests
{
    private static readonly string[] NotCompared = ["station", "index", "moleFractions", "converged"];

    [Fact]
    public void The_table_loads_with_a_derivation_for_every_field()
    {
        var table = ToleranceTable.Load();
        Assert.NotEmpty(table.Fields);
        foreach (var field in table.Fields)
        {
            Assert.False(string.IsNullOrWhiteSpace(table.Derivation(field)), field);
            var tolerance = table.For(field);
            Assert.True(tolerance.Absolute >= 0.0 && tolerance.Relative >= 0.0, field);
        }
    }

    [Fact]
    public void Unknown_fields_are_reported_by_name()
    {
        var table = ToleranceTable.Load();
        var e = Assert.Throws<KeyNotFoundException>(() => table.For("noSuchField"));
        Assert.Contains("noSuchField", e.Message, StringComparison.Ordinal);
        Assert.Throws<KeyNotFoundException>(() => table.Matches("noSuchField", 1.0, 1.0));
    }

    [Fact]
    public void Matches_adds_the_absolute_and_the_relative_part()
    {
        var table = ToleranceTable.Load();
        var t = table.For("temperature");
        const double expected = 3000.0;
        var budget = t.Absolute + t.Relative * expected;
        Assert.True(table.Matches("temperature", expected, expected + 0.9 * budget));
        Assert.True(table.Matches("temperature", expected, expected - 0.9 * budget));
        Assert.False(table.Matches("temperature", expected, expected + 1.1 * budget));
        Assert.False(table.Matches("temperature", expected, expected - 1.1 * budget));
    }

    /// <summary>The list of compared fields is read from the fixtures themselves, so a new output field without a tolerance is caught here.</summary>
    [Theory]
    [InlineData("rocket")]
    [InlineData("tp")]
    [InlineData("hp")]
    [InlineData("sp")]
    public void Every_state_field_of_the_fixtures_has_a_tolerance(string kind)
    {
        var table = ToleranceTable.Load();
        var fields = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var c in CeaFixtures.LoadAll(kind))
        {
            var states = kind == "rocket"
                ? c.Outputs.GetProperty("stations").EnumerateArray()
                : new[] { c.Outputs }.AsEnumerable();
            foreach (var state in states)
            {
                foreach (var property in state.EnumerateObject())
                {
                    if (property.Value.ValueKind == JsonValueKind.Number && !NotCompared.Contains(property.Name))
                    {
                        fields.Add(property.Name);
                    }
                }
            }
        }

        Assert.NotEmpty(fields);
        var missing = fields.Where(f => !table.Fields.Contains(f)).ToArray();
        Assert.True(missing.Length == 0, $"fields without a tolerance: {string.Join(", ", missing)}");
    }
}
