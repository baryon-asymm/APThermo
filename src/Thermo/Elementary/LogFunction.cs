using System.Runtime.CompilerServices;

namespace APThermo.Thermo.Elementary;

/// <summary>
/// Correctly rounded log: a table-reduced double-double fast path with a rounding test, and a table-free triple-double
/// accurate path for the argument the test cannot certify. Written once, for both accelerators.
/// </summary>
/// <remarks>
/// Fast path. <c>x = 2^e·m</c>, <c>m</c> in [1, 2), <c>i = round((m − 1)·32)</c> from the mantissa bits; the table
/// (<see cref="LogTable"/>) holds <c>c_i = RN(1/(1 + i/32))</c> and −log <c>c_i</c> as a double-double, the entry for
/// <c>i = 32</c> being the ln 2 double-double itself, so that <c>e·ln 2 − log c</c> is exactly 0 for
/// <c>x</c> in [1 − 2^-7, 1 + 2^-6) and there is no cancellation. <c>r = m·c_i − 1</c> is an exact double-double
/// (<see cref="ErrorFree.TwoProd"/>, then Sterbenz), <c>|r| ≤ 2^-6</c>; log1p(r) is <c>r − r²/2 + r³/3 − r⁴/4</c> in
/// double-double plus <c>r⁵·Q(r)</c> (degree 9, terms to <c>r^14</c>) in double, the low part carrying
/// <c>rl·(1 − r + r² − r³)</c>, <c>−s2.Lo/2</c>, <c>−s2.Hi·s2.Lo/2</c> and the low part of <c>r³/3</c>. The error budget of
/// the returned double-double, relative, is about 2^-77; the worst measured is 2^-77.3 (the region of i = 1 to 2
/// where <c>A</c> is not 0 and the cancellation is at most 2×), and the fixture's margin fact holds it at least four
/// times below <see cref="FastError"/>, 2^-74. The rounding test is the one of <see cref="ExpFunction"/>.
/// </remarks>
internal static class LogFunction
{
    /// <summary>The fast path's relative error bound, 2^-74.</summary>
    public const double FastError = 5.293955920339377e-23;

    /// <summary>The mantissa field of a double.</summary>
    private const long MantissaMask = 0x000FFFFFFFFFFFFFL;

    /// <summary>The bits of 1.0: the exponent field of a mantissa in [1, 2).</summary>
    private const long OneBits = 0x3FF0000000000000L;

    /// <summary>The bits of the smallest normal double: below them (as a positive double) the argument is subnormal.</summary>
    private const long SmallestNormalBits = 0x0010000000000000L;

    /// <summary>2^54, the scale that lifts a subnormal into the normal range.</summary>
    private const double SubnormalScale = 18014398509481984.0;

    /// <summary>The largest mantissa of the accurate path that stays unhalved, the square root of 2.</summary>
    private const double Sqrt2 = 1.4142135623730951;

    /// <summary>The number of odd terms of the accurate path's atanh series is this plus one (|s| ≤ 0.1716, truncation below 2^-140).</summary>
    private const int AccurateLastTerm = 26;

    /// <summary>log(<paramref name="x"/>) correctly rounded to nearest, ties to even; NaN gives <paramref name="x"/>, a negative argument NaN, ±0 gives −∞.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static double Log(double x) =>
        double.IsNaN(x)
            ? x
            : x < 0.0
                ? double.NaN
                : x == 0.0
                    ? double.NegativeInfinity
                    : x == double.PositiveInfinity
                        ? x
                        : RoundingTest.Certifies(Fast(x), FastError, out var rounded) ? rounded : TripleDouble.Round(AccurateTd(x));

    /// <summary>log(<paramref name="x"/>) as a double-double for a positive finite x.</summary>
    public static Dd Fast(double x)
    {
        var bits = BitConverter.DoubleToInt64Bits(x);
        var e = 0;
        if (bits < SmallestNormalBits)
        {
            bits = BitConverter.DoubleToInt64Bits(x * SubnormalScale);
            e = -54;
        }

        e += (int)(bits >> 52) - 1023;
        var mant = bits & MantissaMask;
        var m = BitConverter.Int64BitsToDouble(mant | OneBits);
        var i = (int)((mant + (1L << 46)) >> 47);
        var t = LogTable.At(i);
        var p = ErrorFree.TwoProd(m, t.C);
        var r = ErrorFree.TwoSum(p.Hi - 1.0, p.Lo);
        var lp = Log1p(r.Hi, r.Lo);
        var ea = ErrorFree.TwoProd(e, ElementaryConstants.Ln2High);
        var a = ErrorFree.TwoSum(ea.Hi, t.Hi);
        var alow = a.Lo + ea.Lo + e * ElementaryConstants.Ln2Low + t.Lo;
        var z = ErrorFree.TwoSum(a.Hi, lp.Hi);
        return ErrorFree.FastTwoSum(z.Hi, z.Lo + alow + lp.Lo);
    }

    /// <summary>log(<paramref name="x"/>) as a triple-double: e ln 2 + 2 atanh((m − 1)/(m + 1)), m in [√½, √2), 27 odd terms.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static Td AccurateTd(double x)
    {
        var bits = BitConverter.DoubleToInt64Bits(x);
        var e = 0;
        if (bits < SmallestNormalBits)
        {
            bits = BitConverter.DoubleToInt64Bits(x * SubnormalScale);
            e = -54;
        }

        e += (int)(bits >> 52) - 1023;
        var m = BitConverter.Int64BitsToDouble((bits & MantissaMask) | OneBits);
        if (m > Sqrt2)
        {
            m *= 0.5;
            e += 1;
        }

        var den = ErrorFree.TwoSum(m, 1.0);
        var s = TripleDouble.Div(TripleDouble.Of(m - 1.0), new Td(den.Hi, den.Lo, 0.0));
        var u = TripleDouble.Mul(s, s);
        var sum = TripleDouble.DivD(TripleDouble.Of(1.0), 2 * (AccurateLastTerm + 1) + 1);
        for (var k = AccurateLastTerm; k >= 0; k--)
        {
            sum = TripleDouble.Add(TripleDouble.DivD(TripleDouble.Of(1.0), 2 * k + 1), TripleDouble.Mul(u, sum));
        }

        var atanh2 = TripleDouble.MulD(TripleDouble.Mul(s, sum), 2.0);
        var ln2 = new Td(ElementaryConstants.Ln2T0, ElementaryConstants.Ln2T1, ElementaryConstants.Ln2T2);
        return TripleDouble.Add(TripleDouble.MulD(ln2, e), atanh2);
    }

    /// <summary>log1p(rh + rl) for <c>|rh|</c> at most about 2^-6 and <c>|rl|</c> at most 2^-53, as a double-double: r − r²/2 + r³/3 − r⁴/4 in double-double, the rest (r⁵/5 to r^14/14) in double.</summary>
    private static Dd Log1p(double rh, double rl)
    {
        var s2 = ErrorFree.TwoProd(rh, rh);
        var c3 = ErrorFree.TwoProd(rh, s2.Hi);
        var t3h = c3.Hi * ElementaryConstants.ThirdHigh;
        var t3l = ErrorFree.Fma(c3.Hi, ElementaryConstants.ThirdHigh, -t3h) + c3.Hi * ElementaryConstants.ThirdLow + (c3.Lo + rh * s2.Lo) * ElementaryConstants.ThirdHigh;
        var t4 = ErrorFree.TwoProd(s2.Hi, s2.Hi);
        var q = TailPolynomial(rh);
        var tail = t4.Hi * rh * q;
        var a = ErrorFree.FastTwoSum(rh, -0.5 * s2.Hi);
        var b = ErrorFree.FastTwoSum(a.Hi, t3h);
        var c = ErrorFree.FastTwoSum(b.Hi, -0.25 * t4.Hi);
        var low = a.Lo + b.Lo + c.Lo + rl * (1.0 - rh + s2.Hi - rh * s2.Hi) - 0.5 * s2.Lo + t3l - 0.25 * t4.Lo - 0.5 * s2.Hi * s2.Lo + tail;
        return ErrorFree.FastTwoSum(c.Hi, low);
    }

    /// <summary>1/5 − r/6 + r²/7 − … − r^9/14, the polynomial of log1p's tail by the fused multiply-add Horner scheme.</summary>
    private static double TailPolynomial(double r)
    {
        var q = ErrorFree.Fma(r, -1.0 / 14, 1.0 / 13);
        q = ErrorFree.Fma(r, q, -1.0 / 12);
        q = ErrorFree.Fma(r, q, 1.0 / 11);
        q = ErrorFree.Fma(r, q, -1.0 / 10);
        q = ErrorFree.Fma(r, q, 1.0 / 9);
        q = ErrorFree.Fma(r, q, -1.0 / 8);
        q = ErrorFree.Fma(r, q, 1.0 / 7);
        q = ErrorFree.Fma(r, q, -1.0 / 6);
        return ErrorFree.Fma(r, q, 1.0 / 5);
    }
}
