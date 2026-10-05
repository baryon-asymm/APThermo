using APThermo.Thermo.Elementary;
using Xunit.Abstractions;

namespace APThermo.Thermo.Tests.Elementary;

/// <summary>
/// The fast paths' error bounds (BOOT.md, "Two phases, one decision"): the rounding test is only as sound as the bound it
/// uses, and a bound is an analytic budget in the source, not a number that happened to pass. The margin files carry the
/// exact value of each input as a double-double; the fact computes the error of the fast path's double-double against it,
/// relative, and requires it to stay at least four times below the bound the rounding test uses. The inputs are the
/// worst places of each reduction (the largest remainder of exp's, every table boundary of log's) beside random ones.
/// </summary>
public sealed class FastPathMarginTests(ITestOutputHelper output)
{
    /// <summary>The factor by which the worst measured error must stay below the bound the rounding test uses.</summary>
    public const double RequiredMargin = 4.0;

    /// <summary>For each function the worst relative error of the fast path over its margin file is at most a quarter of the bound.</summary>
    /// <param name="function">exp, log or pow.</param>
    [Theory]
    [InlineData("exp")]
    [InlineData("log")]
    [InlineData("pow")]
    public void TheFastPathErrorStaysFourTimesBelowItsBound(string function)
    {
        var rows = ElementaryFixtures.Read(function + ".margin.txt");
        Assert.True(rows.Count >= 1000, $"{function}.margin.txt: {rows.Count} rows, the fixture is missing or truncated");
        var worst = 0.0;
        var bound = 0.0;
        foreach (var row in rows)
        {
            var (error, allowed) = Measure(function, row);
            if (error / allowed > worst / bound || bound == 0.0)
            {
                worst = error;
                bound = allowed;
            }
        }

        var share = worst / bound;
        output.WriteLine($"{function}: worst relative error {worst:E3} against the bound {bound:E3}: {share:P1} of it, margin {1.0 / share:F2}x over {rows.Count} inputs");
        Assert.True(share <= 1.0 / RequiredMargin, $"{function}: the fast path's error is {share:P1} of its bound ({1.0 / share:F2}x margin), less than the required {RequiredMargin}x");
    }

    /// <summary>The relative error of the fast path at one margin row and the bound the rounding test uses there.</summary>
    private static (double Error, double Bound) Measure(string function, double[] row)
    {
        switch (function)
        {
            case "exp":
                return Relative(ExpFunction.Fast(row[0], 0.0, out var k), row[1], row[2], k, ExpFunction.FastError);
            case "log":
                return Relative(LogFunction.Fast(row[0]), row[1], row[2], 0, LogFunction.FastError);
            default:
                var z = PowFunction.LogProduct(row[0], row[1]);
                var f = ExpFunction.Fast(z.Hi, z.Lo, out var j);
                return Relative(f, row[2], row[3], j, PowFunction.FastErrorBound(z.Hi));
        }
    }

    /// <summary>|f − t|/|t| for the fast path's value f, which is exp(x)/2^(k >> 6), and the exact t = hi + lo of the fixture, scaled down by the same power of two (exactly: the fixtures keep |x| within 600).</summary>
    private static (double Error, double Bound) Relative(Dd f, double hi, double lo, int k, double bound)
    {
        var scaledHi = ErrorFree.Scale(hi, -(k >> 6));
        var scaledLo = ErrorFree.Scale(lo, -(k >> 6));
        return (Math.Abs(f.Hi - scaledHi + (f.Lo - scaledLo)) / Math.Abs(scaledHi), bound);
    }
}
