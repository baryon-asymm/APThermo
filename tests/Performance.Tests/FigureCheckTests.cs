namespace APThermo.Performance.Tests;

/// <summary>Every generated figure property has both finite and nonfinite controls.</summary>
public sealed class FigureCheckTests
{
    /// <summary>Every double property with each nonfinite value.</summary>
    public static TheoryData<string, double> NonFiniteCases()
    {
        var data = new TheoryData<string, double>();
        foreach (var property in typeof(PerformanceFigures).GetProperties().Where(p => p.PropertyType == typeof(double)))
        {
            foreach (var value in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
            {
                data.Add(property.Name, value);
            }
        }

        return data;
    }

    /// <summary>Setting the generated property nonfinite must fail its guard.</summary>
    [Theory]
    [MemberData(nameof(NonFiniteCases))]
    public void EveryNonfiniteFigureIsRejected(string name, double value)
    {
        var property = typeof(PerformanceFigures).GetProperty(name)!;
        object boxed = default(PerformanceFigures);
        property.SetValue(boxed, value);
        var figures = (PerformanceFigures)boxed;
        Assert.Contains(FigureChecks.NonFinite(in figures, "mutated"), s => s.Contains(name, StringComparison.Ordinal));
    }

    /// <summary>Finite figures remain accepted.</summary>
    [Fact]
    public void FiniteFiguresAreAValidPositiveControl()
    {
        Assert.NotEmpty(FigureChecks.NumericProperties());
        var figures = new PerformanceFigures { AreaRatio = 2.0, SpecificImpulse = 1500.0 };
        Assert.Empty(FigureChecks.NonFinite(in figures, "finite"));
    }
}
