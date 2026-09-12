using System.Text;

namespace AerospacePropellantThermodynamics.Data.Tests;

/// <summary>L1: a corrupted record fails the load with the line number, on copies in memory.</summary>
public sealed class CorruptionTests : IClassFixture<LoadedDatabase>
{
    private readonly string[] _lines;

    public CorruptionTests(LoadedDatabase loaded)
    {
        _lines = File.ReadAllLines(loaded.ThermoPath, Encoding.Latin1);
    }

    private string MinimalFile(Func<string[], string[]> mutate)
    {
        var start = Array.FindIndex(_lines, l => l.StartsWith("H2O ", StringComparison.Ordinal));
        var record = _lines.Skip(start).Take(2 + 3 * 2).ToArray(); // H2O: two intervals of three lines each
        var lines = new List<string> { "thermo", "    200.00   1000.00   6000.00  20000.   9/8/2021" };
        lines.AddRange(mutate(record));
        lines.Add("END PRODUCTS");
        lines.Add("END REACTANTS");
        return string.Join('\n', lines) + "\n";
    }

    [Fact]
    public void The_minimal_file_itself_loads()
    {
        var database = SpeciesDatabase.Parse(new StringReader(MinimalFile(r => r)));
        Assert.Single(database.Products);
        Assert.Equal("H2O", database.Products[0].Name);
        Assert.Equal(2, database.Products[0].Intervals.Count);
        Assert.Null(database.Transport);
    }

    [Fact]
    public void A_truncated_coefficient_line_names_its_line()
    {
        var text = MinimalFile(r =>
        {
            r[3] = r[3][..20] + "X" + r[3][21..];
            return r;
        });
        var e = Assert.Throws<DatabaseFormatException>(() => SpeciesDatabase.Parse(new StringReader(text)));
        Assert.Equal(6, e.LineNumber);
        Assert.Contains("record starting at line 3", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_missing_interval_fails_before_the_end_marker()
    {
        var text = MinimalFile(r => r.Take(5).ToArray());
        var e = Assert.Throws<DatabaseFormatException>(() => SpeciesDatabase.Parse(new StringReader(text)));
        Assert.True(e.LineNumber >= 6, $"line {e.LineNumber}");
    }

    [Fact]
    public void A_file_without_the_end_marker_fails()
    {
        var text = MinimalFile(r => r).Replace("END REACTANTS\n", string.Empty, StringComparison.Ordinal);
        Assert.Throws<DatabaseFormatException>(() => SpeciesDatabase.Parse(new StringReader(text)));
    }

    [Fact]
    public void A_bad_coefficient_count_is_rejected()
    {
        var text = MinimalFile(r =>
        {
            r[2] = r[2][..22] + "6" + r[2][23..];
            return r;
        });
        var e = Assert.Throws<DatabaseFormatException>(() => SpeciesDatabase.Parse(new StringReader(text)));
        Assert.Contains("6 coefficients", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_missing_file_is_reported_before_parsing()
    {
        Assert.Throws<FileNotFoundException>(() => SpeciesDatabase.Load(Path.Combine(Path.GetTempPath(), "no-such-thermo.inp")));
    }
}
