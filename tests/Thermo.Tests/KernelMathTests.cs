using APThermo.Harness;

namespace APThermo.Thermo.Tests;

/// <summary>
/// <see cref="KernelMath.Min"/> and <see cref="KernelMath.Max"/> equal <see cref="Math.Min(double, double)"/> and
/// <see cref="Math.Max(double, double)"/> bit for bit, over every ordered pair of a domain that holds the values the
/// two functions treat specially (BOOT.md, "`KernelMath`"): ±0, ±∞, two NaNs of different payloads, the subnormal
/// bounds, and, since the domain is squared, every value paired with itself (the "equal values" case, +0/−0
/// included). The two NaN payloads prove the first-operand-payload rule for two NaNs (2026-09-28: the reordered form
/// tests <c>val1</c> for NaN before <c>val2</c>, and a pair of two different NaNs is the only case that can tell
/// which operand's bits the result carries). A fixed random sample adds ordinary finite values of mixed sign and
/// magnitude. The execution tests node's <c>ProbeKernelTests</c> proves the same equality on CUDA.
/// </summary>
public sealed class KernelMathTests
{
    /// <summary>±0, ±∞, two NaNs of different payloads, the smallest and largest subnormal, and the smallest normal.</summary>
    private static readonly double[] SpecialValues =
    [
        0.0, -0.0,
        double.PositiveInfinity, double.NegativeInfinity, double.NaN,
        BitConverter.UInt64BitsToDouble(0xFFF8_0000_0000_0001UL), // a NaN of a different payload than double.NaN
        double.Epsilon,                                           // the smallest subnormal
        BitConverter.UInt64BitsToDouble(0x000F_FFFF_FFFF_FFFFUL), // the largest subnormal
        BitConverter.UInt64BitsToDouble(0x0010_0000_0000_0000UL), // the smallest normal
    ];

    /// <summary>
    /// A fixed sample of ordinary finite values, spanning 30 decades on both sides of zero, standing in for the
    /// "random sample" of the criterion: fixed so the test is reproducible, and spread rather than drawn from one
    /// decade so the sample is not accidentally all one sign or one magnitude.
    /// </summary>
    private static double[] Sample()
    {
        var sample = new double[32];
        for (var i = 0; i < sample.Length; i++)
        {
            var magnitude = Math.Pow(10.0, -15.0 + 30.0 * i / (sample.Length - 1));
            sample[i] = i % 2 == 0 ? magnitude : -magnitude;
        }

        return sample;
    }

    /// <summary>Every value the pairs below are drawn from: the special values, a few ordinary ones, and <see cref="Sample"/>.</summary>
    private static double[] Values()
    {
        double[] ordinary = [1.0, -1.0, 2.5, -2.5, 100.0, -100.0, 1e10, -1e10];
        return [.. SpecialValues, .. ordinary, .. Sample()];
    }

    /// <summary><see cref="KernelMath.Min"/> and <see cref="KernelMath.Max"/> equal <see cref="Math.Min(double, double)"/> and
    /// <see cref="Math.Max(double, double)"/> bit for bit, over every ordered pair of <see cref="Values"/>.</summary>
    [Fact]
    public void MinAndMaxEqualSystemMathBitForBitOverEveryOrderedPair()
    {
        var values = Values();
        Assert.NotEmpty(values);
        var compared = 0;
        var mismatches = new List<string>();
        foreach (var a in values)
        {
            foreach (var b in values)
            {
                CompareMin(a, b, mismatches);
                CompareMax(a, b, mismatches);
                compared += 2;
            }
        }

        Assert.True(mismatches.Count == 0, string.Join("\n", mismatches.Take(20)));
        Assert.Equal(values.Length * values.Length * 2, compared);
    }

    private static void CompareMin(double a, double b, List<string> mismatches)
    {
        var expected = Math.Min(a, b);
        var actual = KernelMath.Min(a, b);
        if (!Bits.Same(expected, actual))
        {
            mismatches.Add($"Min({a:R}, {b:R}): Math.Min {expected:R}, KernelMath.Min {actual:R}");
        }
    }

    private static void CompareMax(double a, double b, List<string> mismatches)
    {
        var expected = Math.Max(a, b);
        var actual = KernelMath.Max(a, b);
        if (!Bits.Same(expected, actual))
        {
            mismatches.Add($"Max({a:R}, {b:R}): Math.Max {expected:R}, KernelMath.Max {actual:R}");
        }
    }
}
