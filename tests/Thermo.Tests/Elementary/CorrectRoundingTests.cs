using APThermo.Thermo.Elementary;

namespace APThermo.Thermo.Tests.Elementary;

/// <summary>
/// The tree's own exp, log and pow are correctly rounded (BOOT.md, "Correctly rounded"): every input of every oracle
/// fixture gives exactly the bits of the exact result rounded to nearest even, through the public entries and, forced,
/// through the accurate paths alone. The fixtures are the constructed families (the hard-to-round neighbourhoods of 1, the
/// thresholds, every table boundary, the midpoint cases of pow) and a selection of CORE-MATH's worst-case lists
/// (<c>fixtures/PROVENANCE.txt</c>).
/// </summary>
public sealed class CorrectRoundingTests
{
    /// <summary>Every fixture file, by function: the families and the worst-case selection.</summary>
    public static TheoryData<string, string> Fixtures
    {
        get
        {
            var data = new TheoryData<string, string>();
            foreach (var function in ElementaryFixtures.Functions)
            {
                data.Add(function, function + ".txt");
                data.Add(function, function + ".wc.txt");
            }

            return data;
        }
    }

    /// <summary>Every row's result equals the oracle's bit for bit; an empty fixture fails.</summary>
    /// <param name="function">exp, log or pow.</param>
    /// <param name="file">The fixture file.</param>
    [Theory]
    [MemberData(nameof(Fixtures))]
    public void EveryFixtureInputIsCorrectlyRounded(string function, string file)
    {
        var rows = ElementaryFixtures.Read(file);
        Assert.True(rows.Count >= 1000, $"{file}: {rows.Count} rows, the fixture is missing or truncated");
        var arity = ElementaryFixtures.Arity(function);
        var wrong = rows
            .Where(row => BitConverter.DoubleToInt64Bits(ElementaryFixtures.Evaluate(function, row)) != BitConverter.DoubleToInt64Bits(row[arity]))
            .Select(row => Describe(function, row, ElementaryFixtures.Evaluate(function, row)))
            .ToList();
        Assert.True(wrong.Count == 0, $"{file}: {wrong.Count} of {rows.Count} not correctly rounded, first:\n{string.Join('\n', wrong.Take(8))}");
    }

    /// <summary>The accurate path alone, forced on every row it can take, gives the oracle's bits too: it is what rounds the cases the fast path cannot certify.</summary>
    /// <param name="function">exp, log or pow.</param>
    /// <param name="file">The fixture file.</param>
    [Theory]
    [MemberData(nameof(Fixtures))]
    public void TheAccuratePathAgreesWithTheOracleOnEveryFixture(string function, string file)
    {
        var arity = ElementaryFixtures.Arity(function);
        var rows = ElementaryFixtures.Read(file).Where(row => AccurateDomain(function, row)).ToList();
        Assert.True(rows.Count >= 500, $"{file}: {rows.Count} rows in the accurate path's domain");
        var wrong = rows
            .Where(row => BitConverter.DoubleToInt64Bits(Accurate(function, row)) != BitConverter.DoubleToInt64Bits(row[arity]))
            .Select(row => Describe(function, row, Accurate(function, row)))
            .ToList();
        Assert.True(wrong.Count == 0, $"{file}: {wrong.Count} of {rows.Count} not correctly rounded by the accurate path, first:\n{string.Join('\n', wrong.Take(8))}");
    }

    /// <summary>Whether the accurate path is reached for the row: exp between the thresholds, log of a positive finite number, pow of a positive finite number other than 1 with a finite non-zero exponent whose product with the log is within the thresholds (the entry returns the overflow and the underflow itself before it calls the accurate path).</summary>
    private static bool AccurateDomain(string function, double[] row) =>
        function switch
        {
            "exp" => row[0] is >= ExpFunction.XZero and <= ExpFunction.XMax,
            "log" => row[0] > 0.0 && double.IsFinite(row[0]),
            _ => row[0] > 0.0 && double.IsFinite(row[0]) && row[0] != 1.0 && row[1] != 0.0 && double.IsFinite(row[1])
                 && PowFunction.LogProduct(row[0], row[1]).Hi is >= ExpFunction.XZero and <= ExpFunction.XMax,
        };

    private static double Accurate(string function, double[] row) =>
        function switch
        {
            "exp" => ExpFunction.Accurate(row[0], 0.0),
            "log" => TripleDouble.Round(LogFunction.AccurateTd(row[0])),
            _ => PowFunction.Accurate(row[0], row[1]),
        };

    private static string Describe(string function, double[] row, double actual)
    {
        var arity = ElementaryFixtures.Arity(function);
        var inputs = string.Join(' ', row.Take(arity).Select(ElementaryFixtures.Encode));
        return $"{function}({inputs}) = {ElementaryFixtures.Encode(actual)}, oracle {ElementaryFixtures.Encode(row[arity])}";
    }
}
