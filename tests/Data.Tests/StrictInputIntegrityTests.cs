using System.Text;

namespace APThermo.Data.Tests;

/// <summary>Real fixed-column and transport corruption controls; no generated output is an oracle.</summary>
public sealed class StrictInputIntegrityTests
{
    private static readonly LoadedDatabase Loaded = new();
    private static readonly string[] Header = ["thermo", "    200.00   1000.00   6000.00  20000.   9/8/2021"];
    private static readonly string[] End = ["END PRODUCTS", "END REACTANTS", ""];

    private static string[] WaterRecord()
    {
        var lines = File.ReadAllLines(Loaded.ThermoPath, Encoding.Latin1);
        var start = Array.FindIndex(lines, line => line.StartsWith("H2O ", StringComparison.Ordinal));
        Assert.True(start >= 0);
        return [.. lines.Skip(start).Take(8)];
    }

    private static string Thermo(string[] record) => string.Join('\n', Header.Concat(record).Concat(End));

    private static string[] TransportLines() => File.ReadAllLines(Loaded.TransPath, Encoding.Latin1);

    /// <summary>Both physical polynomial lines, truncated inside fixed numeric columns, fail at the exact line.</summary>
    [Theory]
    [InlineData(3, 48, 6)]
    [InlineData(4, 48, 7)]
    public void TruncatedThermoCoefficientsAreRejected(int index, int length, int expectedLine)
    {
        var record = WaterRecord();
        record[index] = record[index][..length];
        var error = Assert.Throws<DatabaseFormatException>(() => SpeciesDatabase.Parse(new StringReader(Thermo(record))));
        Assert.Equal(expectedLine, error.LineNumber);
    }

    /// <summary>A physically present blank numeric field still has Fortran's zero value.</summary>
    [Fact]
    public void CompleteBlankCoefficientStillMeansZero()
    {
        var record = WaterRecord();
        record[3] = new string(' ', 16) + record[3][16..];
        var database = SpeciesDatabase.Parse(new StringReader(Thermo(record)));
        Assert.Equal(0.0, Assert.Single(database.Products).Intervals[0].Coefficients[0]);
    }

    /// <summary>A truncated real transport fit may not silently become zero coefficients.</summary>
    [Fact]
    public void TruncatedTransportFitIsRejected()
    {
        var lines = TransportLines();
        lines[2] = lines[2][..50];
        var error = Assert.Throws<DatabaseFormatException>(() => SpeciesDatabase.Parse(
            new StringReader(Thermo(WaterRecord())), new StringReader(string.Join('\n', lines))));
        Assert.Equal(3, error.LineNumber);
    }

    /// <summary>A complete NASA record and legal trailing comments still load.</summary>
    [Fact]
    public void CompleteTransportWithCommentsIsAccepted()
    {
        var text = string.Join('\n', TransportLines()) + "\n ! retained comment\n\n";
        var database = SpeciesDatabase.Parse(new StringReader(Thermo(WaterRecord())), new StringReader(text));
        Assert.NotNull(database.Transport!.Find("Ar"));
    }

    /// <summary>The end marker is mandatory and exact and its trailer cannot contain another record.</summary>
    [Theory]
    [InlineData("", "missing")]
    [InlineData("endless\n", "prefix")]
    [InlineData("end\ngarbage\n", "tail")]
    public void InvalidTransportEndIsRejected(string trailer, string description)
    {
        var prefix = TransportLines().SkipLast(1);
        var text = string.Join('\n', prefix) + "\n" + trailer;
        var error = Assert.Throws<DatabaseFormatException>(() => SpeciesDatabase.Parse(
            new StringReader(Thermo(WaterRecord())), new StringReader(text)));
        Assert.True(error.LineNumber > 0, description);
    }

    /// <summary>A duplicate single-species record is not silently indexed by first occurrence.</summary>
    [Fact]
    public void DuplicateSpeciesIsRejectedAtItsSecondHeader()
    {
        var lines = TransportLines();
        var first = lines.Skip(1).Take(7);
        var text = string.Join('\n', lines.Take(8).Concat(first).Concat(["end"]));
        var error = Assert.Throws<DatabaseFormatException>(() => SpeciesDatabase.Parse(
            new StringReader(Thermo(WaterRecord())), new StringReader(text)));
        Assert.Equal(9, error.LineNumber);
    }

    /// <summary>Pair order does not create a second key for the same interaction.</summary>
    [Fact]
    public void DuplicateUnorderedPairIsRejectedAtItsSecondHeader()
    {
        var fit = TransportLines()[2];
        var first = $"{"Ar",-16}{"H2",-16}  V1C0 example";
        var second = $"{"H2",-16}{"Ar",-16}  V1C0 example";
        var text = string.Join('\n', "title", first, fit, second, fit, "end");
        var error = Assert.Throws<DatabaseFormatException>(() => SpeciesDatabase.Parse(
            new StringReader(Thermo(WaterRecord())), new StringReader(text)));
        Assert.Equal(4, error.LineNumber);
    }
}
