using System.Text.Json;

namespace APThermo.Fixtures.Tests;

/// <summary>L1: the tolerance table loads, covers the fields the fixtures report, and compares as documented.</summary>
public sealed class ToleranceTableTests
{
    private static readonly string[] NotCompared = ["station", "index", "moleFractions", "converged"];

    /// <summary>The table loads with a derivation for every field.</summary>
    [Fact]
    public void TheTableLoadsWithADerivationForEveryField()
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

    /// <summary>Unknown fields are reported by name.</summary>
    [Fact]
    public void UnknownFieldsAreReportedByName()
    {
        var table = ToleranceTable.Load();
        var e = Assert.Throws<KeyNotFoundException>(() => table.For("noSuchField"));
        Assert.Contains("noSuchField", e.Message, StringComparison.Ordinal);
        _ = Assert.Throws<KeyNotFoundException>(() => table.Matches("noSuchField", 1.0, 1.0));
    }

    /// <summary>Matches adds the absolute and the relative part.</summary>
    [Fact]
    public void MatchesAddsTheAbsoluteAndTheRelativePart()
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
    public void EveryStateFieldOfTheFixturesHasATolerance(string kind)
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
                fields.UnionWith(ComparedFieldsOf(state));
            }
        }

        Assert.NotEmpty(fields);
        var missing = fields.Where(f => !table.Fields.Contains(f)).ToArray();
        Assert.True(missing.Length == 0, $"fields without a tolerance: {string.Join(", ", missing)}");
    }

    /// <summary>The numeric, compared property names of one fixture state (<see cref="NotCompared"/> excluded).</summary>
    private static IEnumerable<string> ComparedFieldsOf(JsonElement state) =>
        state.EnumerateObject()
            .Where(property => property.Value.ValueKind == JsonValueKind.Number && !NotCompared.Contains(property.Name))
            .Select(property => property.Name);

    /// <summary>
    /// The rule that picks which entry compares a reference mole fraction (BOOT.md, the tolerance-table invariant): at the
    /// threshold itself and one ULP above, "moleFraction"; one ULP below, "moleFractionTrace". Seen red once with the
    /// comparison reversed (<c>&lt;</c> for <c>&gt;=</c> in <see cref="ToleranceTable.MoleFractionField"/>), which turned the
    /// threshold and the ULP-above case red (both then answered "moleFractionTrace") while the ULP-below case stayed green
    /// by coincidence of the reversed rule; reverted before this test was committed.
    /// </summary>
    [Fact]
    public void MoleFractionFieldPicksByTheThresholdAndOneUlpOnEachSide()
    {
        var table = ToleranceTable.Load();
        var threshold = table.For("moleFraction").Absolute;
        Assert.Equal("moleFraction", table.MoleFractionField(threshold));
        Assert.Equal("moleFraction", table.MoleFractionField(Math.BitIncrement(threshold)));
        Assert.Equal("moleFractionTrace", table.MoleFractionField(Math.BitDecrement(threshold)));
    }
}
