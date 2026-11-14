namespace APThermo.Performance.Tests;

/// <summary>The final area residual is checked separately from the iteration's correction.</summary>
public sealed class AreaRatioResidualTests
{
    /// <summary>Known finite and nonfinite residuals cannot be certified by a small correction.</summary>
    [Theory]
    [InlineData(2.0, 2.0, true)]
    [InlineData(2.0, 2.000001, true)]
    [InlineData(2.0, 2.000004, false)]
    [InlineData(2.0, double.NaN, false)]
    [InlineData(2.0, double.PositiveInfinity, false)]
    [InlineData(double.PositiveInfinity, 2.0, false)]
    public void AFinalAreaRatioMustMeetTheStatedResidual(double requested, double actual, bool expected) =>
        Assert.Equal(expected, AreaRatioIteration.MeetsAreaRatio(requested, actual));
}
