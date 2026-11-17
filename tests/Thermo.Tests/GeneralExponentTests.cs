using System.Text;
using APThermo.Data;
using APThermo.Fixtures;

namespace APThermo.Thermo.Tests;

/// <summary>Analytic synthetic polynomial controls for nonstandard and ordinary exponents.</summary>
public sealed class GeneralExponentTests
{
    private static readonly CpuFixture Cpu = new();
    private static readonly string[] Header = ["thermo", "    200.00   1000.00   6000.00  20000.   9/8/2021"];

    /// <summary>One synthetic term has closed-form Cp/R, H/RT and S/R at 400 K.</summary>
    [Theory]
    [InlineData(0.5, "cp", 20.0)]
    [InlineData(0.5, "enthalpy", 40.0 / 3.0)]
    [InlineData(0.5, "entropy", 40.0)]
    [InlineData(1.0, "cp", 400.0)]
    [InlineData(1.0, "enthalpy", 200.0)]
    [InlineData(1.0, "entropy", 400.0)]
    public void SyntheticSingleTermMatchesAnalyticIntegrals(double exponent, string property, double expected)
    {
        var lines = File.ReadAllLines(Path.Combine(RepositoryPaths.Data, "thermo.inp"), Encoding.Latin1);
        var start = Array.FindIndex(lines, line => line.StartsWith("H2 ", StringComparison.Ordinal));
        Assert.True(start >= 0, "the H2 format template is required");
        var record = lines.Skip(start).Take(11).ToArray();
        Assert.Equal(11, record.Length);
        record[2] = record[2][..23] + exponent.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture).PadLeft(5) + record[2][28..];
        record[3] = " 1.000000000D+00" + string.Concat(Enumerable.Repeat(" 0.000000000D+00", 4));
        record[4] = new string(' ', 80);
        var document = string.Join('\n', Header.Concat(record).Concat(["END PRODUCTS", "END REACTANTS", ""]));
        var database = SpeciesDatabase.Parse(new StringReader(document));
        var table = SpeciesTable.Build(database, ["H"], ["H2"]);
        using var buffers = SpeciesTableBuffers.Upload(Cpu.Accelerator, table);
        var view = buffers.View;
        const double temperature = 400.0;
        Assert.True(SpeciesFunctions.IsInRange(view, 0, temperature));
        var actual = property switch
        {
            "cp" => SpeciesFunctions.CpOverR(view, 0, temperature),
            "enthalpy" => SpeciesFunctions.HOverRT(view, 0, temperature),
            "entropy" => SpeciesFunctions.SOverR(view, 0, temperature),
            _ => throw new ArgumentException("unknown synthetic property", nameof(property)),
        };
        Assert.True(Cpu.Tolerances.Matches("thermoFunction", expected, actual), $"{property} at exponent {exponent}: {actual:R}");
    }
}
