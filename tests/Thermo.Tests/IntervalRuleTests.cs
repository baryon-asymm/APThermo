namespace AerospacePropellantThermodynamics.Thermo.Tests;

/// <summary>L0: interval selection at the exact bounds and outside the range, using the bounds of the records themselves.</summary>
public sealed class IntervalRuleTests : IClassFixture<CpuFixture>
{
    private readonly CpuFixture _cpu;

    public IntervalRuleTests(CpuFixture cpu) => _cpu = cpu;

    [Theory]
    [InlineData("H2O")]
    [InlineData("CO2")]
    [InlineData("AL2O3(a)")]
    [InlineData("W(cr)")]
    public void A_shared_bound_belongs_to_the_lower_interval(string species)
    {
        var record = _cpu.Database[species];
        using var buffers = _cpu.Upload(species);
        var view = buffers.View;
        for (var k = 0; k + 1 < record.Intervals.Count; k++)
        {
            var bound = record.Intervals[k].THigh;
            Assert.Equal(k, SpeciesFunctions.IntervalOf(view, 0, bound));
            Assert.Equal(k + 1, SpeciesFunctions.IntervalOf(view, 0, Math.BitIncrement(bound)));
            Assert.True(SpeciesFunctions.IsInRange(view, 0, bound));
        }
    }

    [Theory]
    [InlineData("H2O")]
    [InlineData("AL(cr)")]
    [InlineData("AL2O3(L)")]
    public void Outside_the_range_the_nearest_interval_is_used_and_flagged(string species)
    {
        var record = _cpu.Database[species];
        using var buffers = _cpu.Upload(species);
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
        Assert.True(Math.Abs(below - at) < 1e-9 * Math.Abs(at), $"{species}: Cp/R jumps at the lower bound");
    }
}
