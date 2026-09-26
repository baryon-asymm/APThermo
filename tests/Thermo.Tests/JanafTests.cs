using System.Text.Json;
using APThermo.Fixtures;

namespace APThermo.Thermo.Tests;

/// <summary>
/// L0: the fits against a second source, the NIST-JANAF tables typed into janaf.json with their citation. A plausibility
/// check of formulas and units: the tolerance per species, recorded in the fixture with the reason, reflects how far the
/// NASA records' own sources differ from JANAF.
/// </summary>
public sealed class JanafTests
{
    private static readonly CpuFixture Cpu = new();

    private static string FixturePath => RepositoryPaths.Resolve("tests", "Thermo.Tests", "janaf.json");

    /// <summary>Theory data: one row per JANAF table entry, with the species' recorded relative tolerance.</summary>
    public static TheoryData<string, double, double, double, double, double> Rows()
    {
        var data = new TheoryData<string, double, double, double, double, double>();
        using var document = JsonDocument.Parse(File.ReadAllText(FixturePath));
        foreach (var species in document.RootElement.GetProperty("species").EnumerateArray())
        {
            var name = species.GetProperty("name").GetString()!;
            var tolerance = species.GetProperty("relativeTolerance").GetDouble();
            foreach (var row in species.GetProperty("rows").EnumerateArray())
            {
                data.Add(name, row.GetProperty("temperature").GetDouble(), row.GetProperty("cp").GetDouble(),
                         row.GetProperty("entropy").GetDouble(), row.GetProperty("enthalpyIncrement").GetDouble(), tolerance);
            }
        }

        return data;
    }

    /// <summary>Fits reproduce the JANAF rows within the recorded tolerance.</summary>
    [Theory]
    [MemberData(nameof(Rows))]
    public void FitsReproduceTheJANAFRowsWithinTheRecordedTolerance(string species, double temperature, double cp, double entropy, double enthalpyIncrement, double tolerance)
    {
        using var buffers = Cpu.Upload(species);
        var view = buffers.View;
        var rMol = PhysicalConstants.R / 1000.0; // J/(mol K)
        var cpFit = SpeciesFunctions.CpOverR(view, 0, temperature) * rMol;
        var sFit = SpeciesFunctions.SOverR(view, 0, temperature) * rMol;
        var hFit = (SpeciesFunctions.HOverRT(view, 0, temperature) * temperature - SpeciesFunctions.HOverRT(view, 0, 298.15) * 298.15) * rMol / 1000.0; // kJ/mol

        AssertRelative(cp, cpFit, tolerance, $"{species} Cp at {temperature} K");
        AssertRelative(entropy, sFit, tolerance, $"{species} S at {temperature} K");
        if (enthalpyIncrement != 0.0)
        {
            AssertRelative(enthalpyIncrement, hFit, tolerance, $"{species} H - H298 at {temperature} K");
        }
        else
        {
            Assert.True(Math.Abs(hFit) < CpuFixture.RoundingBound, $"{species}: the increment at 298.15 K is {hFit}");
        }
    }

    /// <summary>The fixture cites its source and bounds every tolerance.</summary>
    [Fact]
    public void TheFixtureCitesItsSourceAndBoundsEveryTolerance()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(FixturePath));
        var source = document.RootElement.GetProperty("source").GetString()!;
        Assert.Contains("NIST-JANAF", source, StringComparison.Ordinal);
        Assert.Contains("janaf.nist.gov", source, StringComparison.Ordinal);
        foreach (var species in document.RootElement.GetProperty("species").EnumerateArray())
        {
            var tolerance = species.GetProperty("relativeTolerance").GetDouble();
            Assert.True(tolerance is > 0.0 and <= 0.025, $"{species.GetProperty("name").GetString()}: tolerance {tolerance}");
            Assert.False(string.IsNullOrWhiteSpace(species.GetProperty("note").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(species.GetProperty("nasaSource").GetString()));
        }
    }

    private static void AssertRelative(double expected, double actual, double tolerance, string what)
    {
        var relative = Math.Abs(actual - expected) / Math.Abs(expected);
        Assert.True(relative <= tolerance, $"{what}: table {expected}, fit {actual:F4}, relative difference {relative:E2}, tolerance {tolerance}");
    }
}
