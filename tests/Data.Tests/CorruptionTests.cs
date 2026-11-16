using System.Text;

namespace APThermo.Data.Tests;

/// <summary>L1: a corrupted record fails the load with the line number, on copies in memory.</summary>
public sealed class CorruptionTests
{
    private static readonly LoadedDatabase Loaded = new();

    private readonly string[] _lines = File.ReadAllLines(Loaded.ThermoPath, Encoding.Latin1);

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

    /// <summary>The minimal file itself loads.</summary>
    [Fact]
    public void TheMinimalFileItselfLoads()
    {
        var database = SpeciesDatabase.Parse(new StringReader(MinimalFile(r => r)));
        _ = Assert.Single(database.Products);
        Assert.Equal("H2O", database.Products[0].Name);
        Assert.Equal(2, database.Products[0].Intervals.Count);
        Assert.Null(database.Transport);
    }

    /// <summary>A truncated coefficient line names its line.</summary>
    [Fact]
    public void ATruncatedCoefficientLineNamesItsLine()
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

    /// <summary>A missing interval fails before the end marker.</summary>
    [Fact]
    public void AMissingIntervalFailsBeforeTheEndMarker()
    {
        var text = MinimalFile(r => [.. r.Take(5)]);
        var e = Assert.Throws<DatabaseFormatException>(() => SpeciesDatabase.Parse(new StringReader(text)));
        Assert.True(e.LineNumber >= 6, $"line {e.LineNumber}");
    }

    /// <summary>A file without the end marker fails.</summary>
    [Fact]
    public void AFileWithoutTheEndMarkerFails()
    {
        var text = MinimalFile(r => r).Replace("END REACTANTS\n", string.Empty, StringComparison.Ordinal);
        _ = Assert.Throws<DatabaseFormatException>(() => SpeciesDatabase.Parse(new StringReader(text)));
    }

    /// <summary>A bad coefficient count is rejected.</summary>
    [Fact]
    public void ABadCoefficientCountIsRejected()
    {
        var text = MinimalFile(r =>
        {
            r[2] = r[2][..22] + "6" + r[2][23..];
            return r;
        });
        var e = Assert.Throws<DatabaseFormatException>(() => SpeciesDatabase.Parse(new StringReader(text)));
        Assert.Contains("6 coefficients", e.Message, StringComparison.Ordinal);
    }

    /// <summary>A missing file is reported before parsing.</summary>
    [Fact]
    public void AMissingFileIsReportedBeforeParsing() =>
        _ = Assert.Throws<FileNotFoundException>(() => SpeciesDatabase.Load(Path.Combine(Path.GetTempPath(), "no-such-thermo.inp")));

    /// <summary>
    /// F-TD-08: a negative interval count is a format error stamped with its line, like every other bad field,
    /// instead of the ArgumentOutOfRangeException that List&lt;T&gt;'s constructor would raise further down, without
    /// a file or a line.
    /// </summary>
    [Fact]
    public void ANegativeIntervalCountNamesItsLine()
    {
        var text = MinimalFile(r =>
        {
            r[1] = "-1" + r[1][2..]; // the interval count occupies columns 1-2 of the properties line
            return r;
        });
        var e = Assert.Throws<DatabaseFormatException>(() => SpeciesDatabase.Parse(new StringReader(text)));
        Assert.Equal(4, e.LineNumber);
        Assert.Contains("record starting at line 3", e.Message, StringComparison.Ordinal);
        Assert.Contains("-1", e.Message, StringComparison.Ordinal);
    }
    /// <summary>The section transition cannot be omitted or repeated.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("END PRODUCTS\nEND PRODUCTS\n")]
    public void AProductSectionNeedsExactlyOneTerminator(string replacement)
    {
        var text = MinimalFile(r => r).Replace("END PRODUCTS\n", replacement, StringComparison.Ordinal);
        _ = Assert.Throws<DatabaseFormatException>(() => SpeciesDatabase.Parse(new StringReader(text)));
    }
    /// <summary>Nonfinite header bounds fail at the header's source line.</summary>
    [Theory]
    [InlineData("NaN")]
    [InlineData("1e999")]
    public void NonfiniteHeaderBoundsAreMalformed(string token)
    {
        var text = MinimalFile(r => r).Replace("200.00", token, StringComparison.Ordinal);
        var error = Assert.Throws<DatabaseFormatException>(() => SpeciesDatabase.Parse(new StringReader(text)));
        Assert.Equal(2, error.LineNumber);
    }

    /// <summary>A nonfinite coefficient is a located format error, not a loaded polynomial.</summary>
    [Theory]
    [InlineData("NaN")]
    [InlineData("1e999")]
    public void NonfiniteCoefficientsAreMalformed(string token)
    {
        var text = MinimalFile(record =>
        {
            record[3] = token.PadLeft(16) + record[3][16..];
            return record;
        });
        var error = Assert.Throws<DatabaseFormatException>(() => SpeciesDatabase.Parse(new StringReader(text)));
        Assert.Equal(6, error.LineNumber);
    }
    /// <summary>Empty lines and whitespace-prefixed comments remain legal after the final section.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("\n  \t\n")]
    [InlineData(" ! comment\n\t! another comment\n")]
    public void ABlankOrCommentTrailerIsAccepted(string trailer)
    {
        var text = MinimalFile(record => record) + trailer;
        var database = SpeciesDatabase.Parse(new StringReader(text));
        Assert.Equal("H2O", Assert.Single(database.Products).Name);
    }

    /// <summary>Unexpected trailer lines are rejected at their precise one-based source location.</summary>
    [Theory]
    [InlineData("garbage\n", 0)]
    [InlineData("  ! comment\n\n  extra record\n", 2)]
    [InlineData("\t\nEND PRODUCTS\n", 1)]
    public void UnexpectedTrailerContentIsRejectedAtItsSourceLine(string trailer, int precedingLines)
    {
        var original = MinimalFile(record => record);
        var error = Assert.Throws<DatabaseFormatException>(() => SpeciesDatabase.Parse(new StringReader(original + trailer)));
        Assert.Null(error.FileName);
        Assert.Equal(original.Count(character => character == '\n') + precedingLines + 1, error.LineNumber);
        Assert.Contains("unexpected content after 'END REACTANTS'", error.Message, StringComparison.Ordinal);
    }

}
