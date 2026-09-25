using System.Globalization;

namespace APThermo.Cli.Tests;

/// <summary>L1: the CSV form against the approved file and its documented layout.</summary>
[Collection("cli")]
public sealed class CsvTests(CliFixture fixture)
{
    /// <summary>Numbers of the approved file are compared as numbers: a cell may differ in its last digits on another CPU, never in its value.</summary>
    public const double Tolerance = 1e-12;

    /// <summary>The csv of the rocket example matches the approved file.</summary>
    [Fact]
    public void TheCsvOfTheRocketExampleMatchesTheApprovedFile()
    {
        var run = CliFixture.Invoke(fixture.Solving("rocket", CliFixture.Document("rocket-lox-lh2.json"), "--format", "csv"));
        Assert.Equal(0, run.Code);
        var actual = run.Output.TrimEnd('\n').Split('\n');
        var approved = File.ReadAllText(CliFixture.Document("rocket-lox-lh2.approved.csv")).Replace("\r", "").TrimEnd('\n').Split('\n');
        Assert.Equal(approved[0], actual[0]);
        Assert.Equal(approved.Length, actual.Length);
        for (var row = 1; row < approved.Length; row++)
        {
            var expectedCells = approved[row].Split(',');
            var actualCells = actual[row].Split(',');
            Assert.Equal(expectedCells.Length, actualCells.Length);
            for (var k = 0; k < expectedCells.Length; k++)
            {
                if (double.TryParse(expectedCells[k], NumberStyles.Float, CultureInfo.InvariantCulture, out var expected)
                    && double.TryParse(actualCells[k], NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                {
                    Assert.True(Math.Abs(value - expected) <= Tolerance * Math.Max(1.0, Math.Abs(expected)), $"row {row}, column {actual[0].Split(',')[k]}: approved {expectedCells[k]}, actual {actualCells[k]}");
                }
                else
                {
                    Assert.Equal(expectedCells[k], actualCells[k]);
                }
            }
        }
    }

    /// <summary>The csv has one row per case and station and no compositions.</summary>
    [Fact]
    public void TheCsvHasOneRowPerCaseAndStationAndNoCompositions()
    {
        var run = CliFixture.Invoke(fixture.Solving("rocket", CliFixture.Document("rocket-sweep.json"), "--format", "csv"));
        Assert.Equal(0, run.Code);
        var lines = run.Output.TrimEnd('\n').Split('\n');
        Assert.Equal(1 + 8 * 4, lines.Length);
        Assert.StartsWith("case,oxidizerToFuel,chamberPressure,station,status,temperature,pressure,", lines[0]);
        Assert.DoesNotContain("H2O", lines[0]);
        Assert.Contains(",specificImpulseSeconds,", lines[0]);
        Assert.Contains(",transportStatus,", lines[0]);
        Assert.StartsWith("0,4,5000000,chamber,ok,", lines[1]);
        Assert.StartsWith("7,7,7000000,exit2,ok,", lines[^1]);
    }

    /// <summary>An equilibrium csv has one row per case with empty performance and transport cells.</summary>
    [Fact]
    public void AnEquilibriumCsvHasOneRowPerCaseWithEmptyPerformanceAndTransportCells()
    {
        var run = CliFixture.Invoke(fixture.Solving("equilibrium", CliFixture.Document("equilibrium-hp.json"), "--format", "csv"));
        Assert.Equal(0, run.Code);
        var lines = run.Output.TrimEnd('\n').Split('\n');
        Assert.Equal(2, lines.Length);
        var header = lines[0].Split(',');
        var cells = lines[1].Split(',');
        Assert.Equal(header.Length, cells.Length);
        Assert.Equal("", cells[Array.IndexOf(header, "specificImpulse")]);
        Assert.Equal("", cells[Array.IndexOf(header, "transportStatus")]);
        Assert.Equal("state", cells[Array.IndexOf(header, "station")]);
        Assert.Equal("hp", cells[Array.IndexOf(header, "kind")]);
    }
}
