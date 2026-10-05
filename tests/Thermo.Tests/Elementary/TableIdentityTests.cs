using APThermo.Thermo.Elementary;

namespace APThermo.Thermo.Tests.Elementary;

/// <summary>
/// The generated tables and constants hold their identities (BOOT.md, "No state"): nothing in them is typed, and a changed
/// digit breaks a relation the generator's exact arithmetic guarantees. The relations are computed in double-double with the
/// tree's own error-free transformations, each against a bound far below the table's own 2^-106.
/// </summary>
public sealed class TableIdentityTests
{
    /// <summary>2^-104: the bound of the exp table's square identity, an order above the table's own rounding.</summary>
    private const double SquareBound = 4.930380657631324e-32;

    /// <summary>2^-53: half an ulp of 1, the bound of a correctly rounded reciprocal's product with its operand.</summary>
    private const double ReductionBound = 1.1102230246251565e-16;

    /// <summary>2^-70: the bound of the cross-table identity, which goes through the exp fast path's own error.</summary>
    private const double CrossBound = 8.470329472543003e-22;

    /// <summary>T(j)² = T(2j) for j in 0 … 31: the exp table is a chain of squares of 2^(1/64).</summary>
    [Fact]
    public void TheExpTableSquaresToItself()
    {
        for (var j = 0; j < 32; j++)
        {
            var t = ExpTable.At(j);
            var square = ErrorFree.TwoProd(t.Hi, t.Hi);
            var squareLo = square.Lo + 2.0 * t.Hi * t.Lo;
            var target = ExpTable.At(2 * j);
            var difference = square.Hi - target.Hi + (squareLo - target.Lo);
            Assert.True(Math.Abs(difference) <= SquareBound * target.Hi, $"T({j})² differs from T({2 * j}) by {difference:E3}");
        }
    }

    /// <summary>The exp table is increasing, starts at exactly 1 and ends below 2, and every entry's low part is below half an ulp of its high part.</summary>
    [Fact]
    public void TheExpTableIsOrderedAndNormalized()
    {
        Assert.Equal(1.0, ExpTable.At(0).Hi);
        Assert.Equal(0.0, ExpTable.At(0).Lo);
        for (var j = 1; j < 64; j++)
        {
            var t = ExpTable.At(j);
            Assert.True(t.Hi > ExpTable.At(j - 1).Hi && t.Hi < 2.0, $"T({j}) is out of order");
            Assert.True(Math.Abs(t.Lo) <= 0.5 * (Math.BitIncrement(t.Hi) - t.Hi), $"T({j}) is not normalized");
        }
    }

    /// <summary>c_i·(1 + i/32) = 1 within 2^-53 for every row, c_0 = 1 and c_32 = 1/2, and the entry of 32 is the ln 2 double-double.</summary>
    [Fact]
    public void TheLogTableReducesEveryBoundaryToOne()
    {
        Assert.Equal(1.0, LogTable.At(0).C);
        Assert.Equal(0.5, LogTable.At(32).C);
        Assert.Equal(ElementaryConstants.Ln2High, LogTable.At(32).Hi);
        Assert.Equal(ElementaryConstants.Ln2Low, LogTable.At(32).Lo);
        for (var i = 0; i <= 32; i++)
        {
            var product = ErrorFree.TwoProd(LogTable.At(i).C, 1.0 + i / 32.0);
            Assert.True(Math.Abs(product.Hi - 1.0 + product.Lo) <= ReductionBound, $"c_{i}·(1 + {i}/32) is {product.Hi - 1.0 + product.Lo:E3} from 1");
        }
    }

    /// <summary>exp(−(−log c_i)) = c_i: the two tables agree through the exp fast path, to 2^-70.</summary>
    [Fact]
    public void TheTwoTablesAgreeWithEachOther()
    {
        for (var i = 1; i < 32; i++)
        {
            var entry = LogTable.At(i);
            var f = ExpFunction.Fast(-entry.Hi, -entry.Lo, out var k);
            var value = ErrorFree.Scale(f.Hi, k >> 6) + ErrorFree.Scale(f.Lo, k >> 6);
            Assert.True(Math.Abs(value - entry.C) <= CrossBound * entry.C, $"exp(log c_{i}) = {value:R}, c_{i} = {entry.C:R}");
        }
    }

    /// <summary>The ln 2 constants are one number: 64 times the step is the double-double, the triple-double extends it, and 1/ln 2 times it is 1.</summary>
    [Fact]
    public void TheLn2ConstantsAreOneNumber()
    {
        Assert.Equal(ElementaryConstants.Ln2High, 64.0 * ElementaryConstants.ExpStepHigh);
        Assert.Equal(ElementaryConstants.Ln2Low, 64.0 * ElementaryConstants.ExpStepLow);
        Assert.Equal(ElementaryConstants.Ln2High, ElementaryConstants.Ln2T0);
        Assert.Equal(ElementaryConstants.Ln2Low, ElementaryConstants.Ln2T1);
        Assert.True(Math.Abs(ElementaryConstants.Ln2T2) <= 0.5 * (Math.BitIncrement(Math.Abs(ElementaryConstants.Ln2T1)) - Math.Abs(ElementaryConstants.Ln2T1)));
        var product = ErrorFree.TwoProd(ElementaryConstants.InverseLn2, ElementaryConstants.Ln2High);
        Assert.True(Math.Abs(product.Hi - 1.0 + (product.Lo + ElementaryConstants.InverseLn2 * ElementaryConstants.Ln2Low)) <= ReductionBound);
        Assert.Equal(ElementaryConstants.ExpInverseStep, 64.0 * ElementaryConstants.InverseLn2);
        Assert.Equal(1.0 / 3.0, ElementaryConstants.ThirdHigh);
        Assert.True(Math.Abs(ElementaryConstants.ThirdLow) <= 0.5 * (Math.BitIncrement(ElementaryConstants.ThirdHigh) - ElementaryConstants.ThirdHigh));
    }
}
