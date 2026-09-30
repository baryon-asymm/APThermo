using APThermo.Harness;

namespace APThermo.Thermo.Tests;

/// <summary>
/// <see cref="KernelMath.Min"/> and <see cref="KernelMath.Max"/> against <see cref="Math.Min(double, double)"/> and
/// <see cref="Math.Max(double, double)"/>, over every ordered pair of a domain that holds the values the two functions
/// treat specially (BOOT.md, "`KernelMath`"): ±0, ±∞, two NaNs of different payloads, the subnormal bounds, and, since
/// the domain is squared, every value paired with itself (the "equal values" case, +0/−0 included). A fixed sample
/// adds ordinary finite values of mixed sign and magnitude. Two facts, split on 2026-09-30 because
/// <see cref="System.Math"/> gives no payload guarantee for two NaNs of different payloads (RyuJIT's optimized
/// expansion of <see cref="Math.Min(double, double)"/> and <see cref="Math.Max(double, double)"/> returns the other
/// one than the managed body does): the equality of every non-NaN result and of NaN-ness with
/// <see cref="System.Math"/>, and the payload rule of <see cref="KernelMath"/> itself, asserted against its documented
/// formula. The execution tests node's <c>ProbeKernelTests</c> proves the same equality on CUDA.
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

    /// <summary>
    /// Every ordered pair of <see cref="Values"/>, both functions: whenever <see cref="System.Math"/> returns a number,
    /// <see cref="KernelMath"/> returns the same bits (±0 included), and whenever <see cref="System.Math"/> returns a
    /// NaN, <see cref="KernelMath"/> returns a NaN (its payload is not compared here, see
    /// <see cref="TwoNaNsGiveTheFirstNaNOperandExactly"/>).
    /// </summary>
    [Fact]
    public void MinAndMaxEqualSystemMathOnEveryNonNaNResultAndInNaNNessOverEveryOrderedPair()
    {
        var values = Values();
        Assert.NotEmpty(values);
        var compared = 0;
        var mismatches = new List<string>();
        foreach (var a in values)
        {
            foreach (var b in values)
            {
                CompareWithSystemMath("Min", a, b, Math.Min(a, b), KernelMath.Min(a, b), mismatches);
                CompareWithSystemMath("Max", a, b, Math.Max(a, b), KernelMath.Max(a, b), mismatches);
                compared += 2;
            }
        }

        Assert.True(mismatches.Count == 0, string.Join(Environment.NewLine, mismatches.Take(20)));
        Assert.Equal(values.Length * values.Length * 2, compared);
    }

    /// <summary>
    /// Every ordered pair of <see cref="Values"/> that holds a NaN, both functions: the result is the first NaN
    /// operand, exactly its bits, the documented formula <c>IsNaN(a) ? a : b</c> (BOOT.md, "<c>KernelMath</c>"), which
    /// is the rule both accelerators share. The domain must hold NaNs of at least two different payloads, or the fact
    /// could not tell the first operand from the second.
    /// </summary>
    [Fact]
    public void TwoNaNsGiveTheFirstNaNOperandExactly()
    {
        var values = Values();
        var payloads = values.Where(double.IsNaN).Select(BitConverter.DoubleToInt64Bits).Distinct().Count();
        Assert.True(payloads >= 2, $"the domain holds {payloads} distinct NaN bit patterns, at least 2 are needed");
        var compared = 0;
        var mismatches = new List<string>();
        foreach (var a in values)
        {
            foreach (var b in values.Where(b => double.IsNaN(a) || double.IsNaN(b)))
            {
                var expected = double.IsNaN(a) ? a : b;
                CheckPayload("Min", a, b, expected, KernelMath.Min(a, b), mismatches);
                CheckPayload("Max", a, b, expected, KernelMath.Max(a, b), mismatches);
                compared += 2;
            }
        }

        Assert.True(mismatches.Count == 0, string.Join(Environment.NewLine, mismatches.Take(20)));
        Assert.True(compared > 0, "the domain holds no pair with a NaN operand");
    }

    private static void CompareWithSystemMath(string name, double a, double b, double system, double actual, List<string> mismatches)
    {
        var agrees = double.IsNaN(system) ? double.IsNaN(actual) : Bits.Same(system, actual);
        if (!agrees)
        {
            mismatches.Add($"{name}({a:R}, {b:R}): Math.{name} {system:R}, KernelMath.{name} {actual:R}");
        }
    }

    private static void CheckPayload(string name, double a, double b, double expected, double actual, List<string> mismatches)
    {
        if (!Bits.Same(expected, actual))
        {
            mismatches.Add($"{name}({BitConverter.DoubleToInt64Bits(a):X16}, {BitConverter.DoubleToInt64Bits(b):X16}): expected bits {BitConverter.DoubleToInt64Bits(expected):X16}, KernelMath.{name} bits {BitConverter.DoubleToInt64Bits(actual):X16}");
        }
    }
}
