using System.Globalization;

namespace APThermo.Data.Tests;

/// <summary>Required header fields are physically present, independently of their numeric blank semantics.</summary>
public sealed class FixedWidthHeaderTests
{
    private static readonly string[] Header = ["thermo", "200 1000 6000 20000"];
    private static readonly string[] End = ["END PRODUCTS", "END REACTANTS"];

    /// <summary>Missing exponents or any part of the final field fail at either interval's own line.</summary>
    [Theory]
    [InlineData(2, 48, 5)]
    [InlineData(2, 58, 5)]
    [InlineData(2, 63, 5)]
    [InlineData(2, 79, 5)]
    [InlineData(5, 48, 8)]
    [InlineData(5, 58, 8)]
    [InlineData(5, 63, 8)]
    [InlineData(5, 79, 8)]
    public void TruncatedIntervalHeadersNameTheirSourceLine(int row, int width, int expectedLine)
    {
        var record = Record(2);
        record[row] = record[row][..width];

        var error = Assert.Throws<DatabaseFormatException>(() => Parse(record));

        Assert.Equal(expectedLine, error.LineNumber);
    }

    /// <summary>Formation and assigned enthalpy both require the entire properties field.</summary>
    [Theory]
    [InlineData(0, 65, 5)]
    [InlineData(0, 79, 5)]
    [InlineData(2, 65, 4)]
    [InlineData(2, 79, 4)]
    public void TruncatedPropertiesNameTheirSourceLine(int intervals, int width, int expectedLine)
    {
        var record = Record(intervals);
        record[1] = record[1][..width];

        var error = Assert.Throws<DatabaseFormatException>(() => Parse(record));

        Assert.Equal(expectedLine, error.LineNumber);
    }

    /// <summary>Present blank exponents and enthalpy offsets retain Fortran's zero meaning.</summary>
    [Theory]
    [InlineData(2, 0)]
    [InlineData(5, 1)]
    public void CompleteBlankIntervalHeaderTailMeansZero(int row, int intervalIndex)
    {
        var record = Record(2);
        record[row] = record[row][..23] + new string(' ', 57);

        var interval = Assert.Single(Parse(record).Products).Intervals[intervalIndex];

        Assert.All(interval.Exponents, value => Assert.Equal(0.0, value));
        Assert.Equal(0.0, interval.EnthalpyOffset);
    }

    /// <summary>A complete blank formation or assigned-enthalpy field is still zero.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void CompleteBlankPropertiesTailMeansZero(int intervals)
    {
        var record = Record(intervals);
        record[1] = record[1][..65] + new string(' ', 15);

        var species = Assert.Single(Parse(record).Records("TEST"));

        Assert.Equal(0.0, species.FormationEnthalpy);
    }

    /// <summary>Complete stored fields survive; identity comments and the unused temperature-line tail are optional.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void CompleteRecordsPreserveFieldsWithoutOptionalTails(int intervals)
    {
        var species = Assert.Single(Parse(Record(intervals)).Records("TEST"));

        Assert.Equal("TEST", species.Name);
        Assert.Empty(species.Comment);
        Assert.Equal(7.0, species.FormationEnthalpy);
        Assert.Equal(intervals, species.Intervals.Count);
        Assert.Equal(intervals == 0 ? SpeciesSection.Reactants : SpeciesSection.Products, species.Section);
        Assert.Equal(intervals == 0 ? 300.0 : 0.0, species.AssignedTemperature);
        Assert.All(species.Intervals, interval => Assert.Equal(9.0, interval.EnthalpyOffset));
    }

    private static SpeciesDatabase Parse(string[] record)
    {
        var body = record[1][0] == '0'
            ? End.Take(1).Concat(record).Append(End[1])
            : record.Concat(End);
        return SpeciesDatabase.Parse(new StringReader(string.Join('\n', Header.Concat(body))));
    }

    private static string[] Record(int intervals)
    {
        var properties = Line((1, intervals.ToString(CultureInfo.InvariantCulture)), (11, "H"), (13, "1.0"),
                              (53, "1.0"), (66, "7.0"));
        var header = Line((1, "100"), (12, "1000"), (23, "7"), (24, " -2.0 -1.0  0.0  1.0  2.0  3.0  4.0  0.0"), (66, "9.0"));
        return intervals == 0
            ? ["TEST", properties, "300"]
            : ["TEST", properties, header, Line(), Line(), header, Line(), Line()];
    }

    private static string Line(params (int Column, string Value)[] fields)
    {
        var chars = new string(' ', 80).ToCharArray();
        foreach (var (column, value) in fields)
        {
            value.AsSpan().CopyTo(chars.AsSpan(column - 1));
        }

        return new string(chars);
    }
}
