namespace APThermo.Thermo.Tests.Elementary;

/// <summary>
/// The special values of exp, log and pow follow IEEE 754 and C99 Annex F (BOOT.md, "Correctly rounded"): the results the
/// standard fixes by the operands alone, and the thresholds beyond which the result is infinity or zero, equal the
/// .NET runtime's bit for bit (a NaN equals a NaN of any payload). The finite results in between are the oracle fixtures'.
/// </summary>
public sealed class SpecialValueTests
{
    private static readonly double[] ExpArguments =
    [
        double.NaN, double.PositiveInfinity, double.NegativeInfinity, 0.0, -0.0, 710.0, 1e300, -1e300, -746.0, -750.0, double.MaxValue, double.MinValue,
    ];

    private static readonly double[] LogArguments =
    [
        double.NaN, double.PositiveInfinity, double.NegativeInfinity, 0.0, -0.0, -1.0, -double.Epsilon, 1.0, -1e300, double.MaxValue,
    ];

    private static readonly double[] PowBases =
    [
        double.NaN, double.PositiveInfinity, double.NegativeInfinity, 0.0, -0.0, 1.0, -1.0, 2.0, -2.0, 0.5, -0.5, 3.0, 1e300, -1e300, double.Epsilon,
    ];

    private static readonly double[] PowExponents =
    [
        double.NaN, double.PositiveInfinity, double.NegativeInfinity, 0.0, -0.0, 1.0, -1.0, 2.0, -2.0, 3.0, -3.0, 0.5, -0.5, 1e300, -1e300, 1075.0, -1075.0,
    ];

    /// <summary>exp of every special argument equals the runtime's.</summary>
    [Fact]
    public void ExpOfTheSpecialArgumentsEqualsTheRuntimes()
    {
        Assert.NotEmpty(ExpArguments);
        var wrong = ExpArguments.Where(x => !Same(KernelMath.Exp(x), Math.Exp(x))).Select(x => $"exp({x:R}) = {KernelMath.Exp(x):R}, runtime {Math.Exp(x):R}").ToList();
        Assert.True(wrong.Count == 0, string.Join('\n', wrong));
    }

    /// <summary>log of every special argument equals the runtime's.</summary>
    [Fact]
    public void LogOfTheSpecialArgumentsEqualsTheRuntimes()
    {
        Assert.NotEmpty(LogArguments);
        var wrong = LogArguments.Where(x => !Same(KernelMath.Log(x), Math.Log(x))).Select(x => $"log({x:R}) = {KernelMath.Log(x):R}, runtime {Math.Log(x):R}").ToList();
        Assert.True(wrong.Count == 0, string.Join('\n', wrong));
    }

    /// <summary>pow of every pair of special operands whose result the standard fixes equals the runtime's.</summary>
    [Fact]
    public void PowOfTheSpecialOperandsEqualsTheRuntimes()
    {
        var compared = 0;
        var wrong = new List<string>();
        foreach (var x in PowBases)
        {
            foreach (var y in PowExponents)
            {
                var expected = Math.Pow(x, y);
                if (!IsFixedByTheOperands(x, y, expected))
                {
                    continue;
                }

                compared++;
                if (!Same(KernelMath.Pow(x, y), expected))
                {
                    wrong.Add($"pow({x:R}, {y:R}) = {KernelMath.Pow(x, y):R}, runtime {expected:R}");
                }
            }
        }

        Assert.True(compared >= 150, $"{compared} pairs compared: the operand lists are too short for the rule");
        Assert.True(wrong.Count == 0, string.Join('\n', wrong));
    }

    /// <summary>Whether the standard fixes the result by the operands alone: an operand that is not a finite non-zero number, a base of magnitude one, a negative base with a non-integer exponent, or a result that is NaN, infinite or zero.</summary>
    private static bool IsFixedByTheOperands(double x, double y, double result) =>
        !double.IsFinite(x) || !double.IsFinite(y) || x == 0.0 || y == 0.0 || Math.Abs(x) == 1.0
        || (x < 0.0 && Math.Floor(y) != y) || !double.IsFinite(result) || result == 0.0;

    private static bool Same(double actual, double expected) =>
        double.IsNaN(expected) ? double.IsNaN(actual) : BitConverter.DoubleToInt64Bits(actual) == BitConverter.DoubleToInt64Bits(expected);
}
