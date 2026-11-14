using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using APThermo.Fixtures;
using APThermo.Thermo;

namespace APThermo.Performance.Tests;

/// <summary>A bounded independent raw-NASA sonic known answer; it does not reuse the solver's reported values as expectations.</summary>
[Collection(CpuFixture.CollectionName)]
public sealed class IndependentBeOThroatTests
{
    /// <summary>The independently solved T/p/c* coexistence throat is retained within the existing polish budget.</summary>
    [Fact]
    public void TheBeOThroatMatchesTheIndependentSonicKnownAnswer()
    {
        using var oracle = LoadOracle();
        var solution = Solve(oracle.RootElement);
        Assert.Empty(Violations(oracle.RootElement, solution));
    }

    /// <summary>Actual measured values are corrupted one at a time; the independent guard must reject each.</summary>
    [Theory]
    [InlineData("temperature")]
    [InlineData("pressure")]
    [InlineData("characteristicVelocity")]
    public void ACorruptedMeasuredFieldIsRejected(string field)
    {
        using var oracle = LoadOracle();
        var solution = Solve(oracle.RootElement);
        Assert.Empty(Violations(oracle.RootElement, solution));
        var state = solution.Outcome.Stations[1];
        var figures = solution.Outcome.Figures[1];
        switch (field)
        {
            case "temperature": state.Temperature *= 1.0001; break;
            case "pressure": state.Pressure *= 1.0001; break;
            case "characteristicVelocity": figures.CharacteristicVelocity *= 1.0001; break;
            default: throw new ArgumentOutOfRangeException(nameof(field));
        }

        solution.Outcome.Stations[1] = state;
        solution.Outcome.Figures[1] = figures;
        Assert.Contains(Violations(oracle.RootElement, solution), message => message.StartsWith(field, StringComparison.Ordinal));
    }

    private static RocketSolution Solve(JsonElement oracle)
    {
        var fixture = RocketHost.Load(oracle.GetProperty("case").GetString()!);
        Assert.Equal(oracle.GetProperty("inputSha256").GetString(), HashOfFile(fixture.Path));
        var solution = RocketHost.Solve(CpuFixture.Shared, fixture);
        Assert.Equal(CaseStatus.Ok, solution.Status);
        Assert.Equal(CaseStatus.Ok, solution.Outcome.StationStatus[1]);
        return solution;
    }

    private static JsonDocument LoadOracle()
    {
        var oracle = JsonDocument.Parse(File.ReadAllText(RepositoryPaths.Resolve("tests", "Performance.Tests", "IndependentBeOThroat.json")));
        Assert.Equal(oracle.RootElement.GetProperty("dataSha256").GetString(), HashOfFile(Path.Combine(RepositoryPaths.Data, "thermo.inp")));
        return oracle;
    }

    private static IReadOnlyList<string> Violations(JsonElement oracle, RocketSolution solution)
    {
        var actual = new Dictionary<string, double>(StringComparer.Ordinal)
        {
            ["temperature"] = solution.Outcome.Stations[1].Temperature,
            ["pressure"] = solution.Outcome.Stations[1].Pressure,
            ["characteristicVelocity"] = solution.Outcome.Figures[1].CharacteristicVelocity,
        };
        var expected = oracle.GetProperty("knownAnswer").EnumerateObject().ToArray();
        Assert.Equal(expected.Select(property => property.Name).Order(StringComparer.Ordinal), actual.Keys.Order(StringComparer.Ordinal));
        return [.. expected.Where(property => !CpuFixture.Shared.Tolerances.Matches("polishThresholdRelative",
                double.Parse(property.Value.GetString()!, CultureInfo.InvariantCulture), actual[property.Name]))
            .Select(property => $"{property.Name}: independent {property.Value.GetString()}, measured {actual[property.Name]:R}")];
    }

    private static string HashOfFile(string path) => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
}