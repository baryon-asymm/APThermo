namespace APThermo.Transport.Tests;

/// <summary>Negative and positive controls for the order-consistency comparator.</summary>
public sealed class AuditGuardTests
{
    /// <summary>Nonfinite numbers cannot certify agreement, in either operand.</summary>
    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void ConsistencyComparisonRejectsNonfiniteOperands(double value)
    {
        var finite = new TransportFigures { Viscosity = 1.0 };
        var nonfinite = new TransportFigures { Viscosity = value };
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
}