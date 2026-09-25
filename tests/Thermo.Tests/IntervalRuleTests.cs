namespace APThermo.Thermo.Tests;

/// <summary>L0: interval selection at the exact bounds and outside the range, using the bounds of the records themselves.</summary>
public sealed class IntervalRuleTests
{
    private static readonly CpuFixture Cpu = new();

    /// <summary>A shared bound belongs to the lower interval.</summary>
    [Theory]
    [InlineData("H2O")]
    [InlineData("CO2")]
    [InlineData("AL2O3(a)")]
    [InlineData("W(cr)")]
    public void ASharedBoundBelongsToTheLowerInterval(string species)
    {
        var record = Cpu.Database[species];
        using var buffers = Cpu.Upload(species);
        var view = buffers.View;
        for (var k = 0; k + 1 < record.Intervals.Count; k++)
        {
            var bound = record.Intervals[k].THigh;
            Assert.Equal(k, SpeciesFunctions.IntervalOf(view, 0, bound));
            Assert.Equal(k + 1, SpeciesFunctions.IntervalOf(view, 0, Math.BitIncrement(bound)));
            Assert.True(SpeciesFunctions.IsInRange(view, 0, bound));
        }
    }

    /// <summary>Outside the range the nearest interval is used and flagged.</summary>
    [Theory]
    [InlineData("H2O")]
    [InlineData("AL(cr)")]
    [InlineData("AL2O3(L)")]
    public void OutsideTheRangeTheNearestIntervalIsUsedAndFlagged(string species)
    {
        var record = Cpu.Database[species];
        using var buffers = Cpu.Upload(species);
        var view = buffers.View;
        var first = record.Intervals[0].TLow;
        var last = record.Intervals[^1].THigh;
        var count = record.Intervals.Count;

        Assert.True(SpeciesFunctions.IsInRange(view, 0, first));
        Assert.True(SpeciesFunctions.IsInRange(view, 0, last));
        Assert.False(SpeciesFunctions.IsInRange(view, 0, Math.BitDecrement(first)));
        Assert.False(SpeciesFunctions.IsInRange(view, 0, Math.BitIncrement(last)));
        Assert.Equal(0, SpeciesFunctions.IntervalOf(view, 0, first * 0.5));
        Assert.Equal(count - 1, SpeciesFunctions.IntervalOf(view, 0, last * 2.0));

        // The value outside is the nearest polynomial's: continuous across the bound.
        var below = SpeciesFunctions.CpOverR(view, 0, Math.BitDecrement(first));
        var at = SpeciesFunctions.CpOverR(view, 0, first);
        Assert.True(Math.Abs(below - at) < CpuFixture.RoundingBound * Math.Abs(at), $"{species}: Cp/R jumps at the lower bound");
    }
}
