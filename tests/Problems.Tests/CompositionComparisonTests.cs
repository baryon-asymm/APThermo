using APThermo.Thermo;

namespace APThermo.Problems.Tests;

/// <summary>Negative controls for the public station comparison's complete composition contract.</summary>
public sealed class CompositionComparisonTests
{
    private static Station Empty() => new("state", default, null,
        new Dictionary<string, double> { ["A"] = 1.0 },
        new Dictionary<string, double> { ["solid"] = 0.25 }, null, null, CaseStatus.Ok);

    /// <summary>A condensed mass change is visible to the relative comparison.</summary>
    [Fact]
    public void CondensedMassDifferencesAreReported()
    {
        var expected = Empty();
        var actual = expected with { CondensedMassFractions = new Dictionary<string, double> { ["solid"] = 0.5 } };
        Assert.NotEmpty(StationEquality.RelativeDifferences(expected, actual, relative: 0.0, moleFractionFloor: 0.0, "mass"));
    }

    /// <summary>Actual-only nonzero species must be detected in both directions and modes.</summary>
    [Fact]
    public void ExtraPositiveSpeciesAreReportedInEitherDirection()
    {
        var expected = Empty();
        var actual = expected with { MoleFractions = new Dictionary<string, double> { ["A"] = 1.0, ["extra"] = 0.25 } };
        Assert.NotEmpty(StationEquality.BitDifferences(expected, actual, "forward"));
        Assert.NotEmpty(StationEquality.BitDifferences(actual, expected, "reverse"));
        Assert.NotEmpty(StationEquality.RelativeDifferences(expected, actual, 0.0, 0.0, "forward"));
        Assert.NotEmpty(StationEquality.RelativeDifferences(actual, expected, 0.0, 0.0, "reverse"));
    }

    /// <summary>Union batching may include extra zero species without a physical difference.</summary>
    [Fact]
    public void ExtraZeroSpeciesAreValidPositiveControls()
    {
        var expected = Empty();
        var actual = expected with
        {
            MoleFractions = new Dictionary<string, double> { ["A"] = 1.0, ["extra"] = 0.0 },
            CondensedMassFractions = new Dictionary<string, double> { ["solid"] = 0.25, ["extra-solid"] = 0.0 },
        };
        Assert.Empty(StationEquality.BitDifferences(expected, actual, "forward"));
        Assert.Empty(StationEquality.BitDifferences(actual, expected, "reverse"));
        Assert.Empty(StationEquality.RelativeDifferences(expected, actual, 0.0, 0.0, "forward"));
        Assert.Empty(StationEquality.RelativeDifferences(actual, expected, 0.0, 0.0, "reverse"));
    }
}
