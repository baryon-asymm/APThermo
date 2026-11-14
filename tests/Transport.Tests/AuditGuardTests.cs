namespace APThermo.Transport.Tests;

/// <summary>Negative and positive controls for the order-consistency comparator.</summary>
public sealed class AuditGuardTests
{
    /// <summary>Nonfinite numbers cannot certify agreement, in either operand.</summary>
    [Theory]
    [MemberData(nameof(NonfiniteFigureCases))]
    public void ConsistencyComparisonRejectsNonfiniteOperands(string name, double value)
    {
        var finite = new TransportFigures { Viscosity = 1.0 };
        object boxed = finite;
        typeof(TransportFigures).GetProperty(name)!.SetValue(boxed, value);
        var nonfinite = (TransportFigures)boxed;
        Assert.NotEmpty(EquilibriumConsistencyTests.CompareFigures(finite, nonfinite, "right"));
        Assert.NotEmpty(EquilibriumConsistencyTests.CompareFigures(nonfinite, finite, "left"));
    }

    /// <summary>Equal finite values, including zero, remain valid positive controls.</summary>
    [Fact]
    public void ConsistencyComparisonAcceptsEqualFiniteOperands()
    {
        Assert.Empty(EquilibriumConsistencyTests.CompareFigures(default, default, "zero"));
        var finite = new TransportFigures { Viscosity = 1.0 };
        Assert.Empty(EquilibriumConsistencyTests.CompareFigures(finite, finite, "finite"));
        Assert.NotEmpty(EquilibriumConsistencyTests.CompareFigures(finite, new TransportFigures { Viscosity = 2.0 }, "different"));
    }
    /// <summary>An independent reflection inventory covers every numeric output guard.</summary>
    public static TheoryData<string, double> NonfiniteFigureCases()
    {
        var data = new TheoryData<string, double>();
        foreach (var property in typeof(TransportFigures).GetProperties().Where(p => p.PropertyType == typeof(double)))
        {
            foreach (var value in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
            {
                data.Add(property.Name, value);
            }
        }

        return data;
    }

    /// <summary>Every nonfinite output property makes the finalization guard refuse it.</summary>
    [Theory]
    [MemberData(nameof(NonfiniteFigureCases))]
    public void EveryNonfiniteOutputPropertyIsRejected(string name, double value)
    {
        object boxed = default(TransportFigures);
        typeof(TransportFigures).GetProperty(name)!.SetValue(boxed, value);
        var figures = (TransportFigures)boxed;
        Assert.False(StationEvaluation.Finite(in figures), name);
    }

    /// <summary>A finite result remains a positive control for finalization.</summary>
    [Fact]
    public void FiniteOutputPropertiesAreAccepted()
    {
        var figures = new TransportFigures { Viscosity = 1.0, FrozenHeatCapacity = 2.0 };
        Assert.True(StationEvaluation.Finite(in figures));
    }
}
