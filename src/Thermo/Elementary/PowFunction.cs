using System.Runtime.CompilerServices;

namespace APThermo.Thermo.Elementary;

/// <summary>
/// Correctly rounded pow, built on the exp and log cores, with the special values of IEEE 754 and C99 Annex F. Written
/// once, for both accelerators.
/// </summary>
/// <remarks>
/// Fast path for a positive finite x ≠ 1 and a finite y ≠ 0: <c>z = y·log x</c> as a double-double (the log fast path,
/// <see cref="ErrorFree.TwoProd"/>), then the exp fast path at the double-double argument, rounded by the same test with
/// ε = <see cref="ExpFunction.FastError"/> + <c>|z|</c>·<see cref="LogFunction.FastError"/>. A result beyond
/// <see cref="ExpFunction.XMax"/> is +∞, below <see cref="ExpFunction.XZero"/> is 0, and a subnormal result goes to the
/// accurate path. The accurate path first resolves the exact midpoints (<see cref="ExactPower"/>), which no approximation
/// can round, then rounds the triple-double log × y → triple-double exp.
/// </remarks>
internal static class PowFunction
{
    /// <summary>2^53: from here on every double is an even integer.</summary>
    private const double TwoPow53 = 9007199254740992.0;

    /// <summary>Above this z = y·log x the result overflows.</summary>
    private const double ZOverflow = 709.8;

    /// <summary>x^y correctly rounded to nearest, ties to even, with the C99 special values; a NaN operand gives the first NaN operand.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static double Pow(double x, double y)
    {
        if (y == 0.0 || x == 1.0)
        {
            return 1.0;
        }

        if (double.IsNaN(x) || double.IsNaN(y))
        {
            return double.IsNaN(x) ? x : y;
        }

        var yInt = Math.Floor(y) == y;
        var yOdd = yInt && Math.Abs(y) < TwoPow53 && Math.Floor(y * 0.5) * 2.0 != y;
        var xInfinite = Math.Abs(x) == double.PositiveInfinity;
        var yInfinite = Math.Abs(y) == double.PositiveInfinity;
        if (x < 0.0 && !yInt && !yInfinite && !xInfinite)
        {
            return double.NaN;
        }

        var negative = x < 0.0 && yOdd;
        if (yInfinite)
        {
            return InfiniteExponent(Math.Abs(x), y);
        }

        if (x == 0.0 || xInfinite)
        {
            var big = x == 0.0 ? y < 0.0 : y > 0.0;
            var magnitude = big ? double.PositiveInfinity : 0.0;
            return negative || (yOdd && BitConverter.DoubleToInt64Bits(x) < 0) ? -magnitude : magnitude;
        }

        var value = Positive(Math.Abs(x), y);
        return negative ? -value : value;
    }

    /// <summary>|x|^y for an infinite y and |x| ≠ 1.</summary>
    private static double InfiniteExponent(double magnitude, double y)
    {
        var below = magnitude < 1.0;
        var towardZero = y < 0.0;
        var big = below == towardZero;
        return magnitude == 1.0 ? 1.0 : big ? double.PositiveInfinity : 0.0;
    }

    /// <summary>x^y for a positive finite x ≠ 1 and a finite y ≠ 0.</summary>
    private static double Positive(double x, double y)
    {
        var z = LogProduct(x, y);
        if (z.Hi > ZOverflow)
        {
            return double.PositiveInfinity;
        }

        if (z.Hi < ExpFunction.XZero)
        {
            return 0.0;
        }

        if (!(z.Hi < ExpFunction.XSubnormal + 1.0))
        {
            var f = ExpFunction.Fast(z.Hi, z.Lo, out var k);
            if (RoundingTest.Certifies(f, FastErrorBound(z.Hi), out var rounded))
            {
                return ErrorFree.Scale(rounded, k >> 6);
            }
        }

        return Accurate(x, y);
    }

    /// <summary><c>z = y·log x</c> as a double-double, the log fast path times y, for a positive finite x.</summary>
    public static Dd LogProduct(double x, double y)
    {
        var l = LogFunction.Fast(x);
        var z = ErrorFree.TwoProd(y, l.Hi);
        return new Dd(z.Hi, z.Lo + y * l.Lo);
    }

    /// <summary>The relative error bound of the fast path for an exponent argument <c>z</c>: the exp fast path's plus <c>|z|</c> times the log fast path's.</summary>
    public static double FastErrorBound(double z) => ExpFunction.FastError + Math.Abs(z) * LogFunction.FastError;

    /// <summary>The accurate path: the exact midpoint when there is one, else the triple-double log × y → triple-double exp.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static double Accurate(double x, double y) =>
        ExactPower.TryRoundMidpoint(x, y, out var mid) ? mid : Exponentiated(TripleDouble.MulD(LogFunction.AccurateTd(x), y));

    /// <summary>exp(<paramref name="z"/>) correctly rounded, the overflow and the underflow included.</summary>
    private static double Exponentiated(Td z) =>
        z.H > ZOverflow
            ? double.PositiveInfinity
            : z.H < ExpFunction.XZero ? 0.0 : TripleDouble.RoundScaled(ExpFunction.AccurateTd(z, out var e), e);
}
