using System.Runtime.CompilerServices;

namespace APThermo.Thermo.Elementary;

/// <summary>
/// Correctly rounded exp: a double-double fast path with a rounding test, and a table-free triple-double accurate path for
/// the argument the test cannot certify and for a subnormal result. Written once, for both accelerators.
/// </summary>
/// <remarks>
/// Fast path. <c>k = round(64 x/ln 2)</c> from the fused multiply-add with the shifter 1.5·2^52; <c>rh = x − k·L1</c> is
/// exact (<c>L1 = RN(ln 2/64)</c>: the difference has at most 53 significant bits for every <c>|k| ≤ 2^17</c>, and the
/// fused multiply-add rounds once), <c>rl = −k·L2</c> carries the rest of the step, <c>|rl| ≤ 2^-43</c>, and
/// <c>|rh| ≤ ln 2/128</c>. Then <c>E = exp(rh) − 1 = rh + rh²/2 + rh³·P(rh)</c> with the first two terms in
/// double-double (<see cref="ErrorFree.TwoProd"/>) and <c>P</c> of degree 4 (truncation <c>rh^8/8! ≈ 2^-75.6</c>,
/// <c>|q| ≤ 2^-25</c>); <c>exp(rh + rl) = (1 + E)(1 + rl)</c>; the result is <c>T·(1 + E)</c> with <c>T = 2^(j/64)</c>
/// from <see cref="ExpTable"/>, <c>j = k mod 64</c>. The error budget of the returned double-double, relative:
/// the rounding of <c>q</c> about 2^-77.7, the truncation 2^-75.6, the cross terms below 2^-85, the table 2^-106; the
/// worst measured is 2^-74.6 (the fixture's margin fact holds it at least four times below
/// <see cref="FastError"/>, 2^-72). The rounding test returns <c>h + l</c> only when <c>h + (l − ε h)</c> and
/// <c>h + (l + ε h)</c> agree, ε = <see cref="FastError"/>.
/// </remarks>
internal static class ExpFunction
{
    /// <summary>The largest x whose exp rounds to a finite double.</summary>
    public const double XMax = 709.782712893384;

    /// <summary>Below this exp(x) is under a quarter of the smallest subnormal and rounds to +0.</summary>
    public const double XZero = -746.0;

    /// <summary>Below this the result is subnormal and only the accurate path rounds it.</summary>
    public const double XSubnormal = -708.3964185322641;

    /// <summary>The fast path's relative error bound, 2^-72.</summary>
    public const double FastError = 2.117582368135751e-22;

    /// <summary>Below this <c>|x|</c>, 2^-54, exp(x) rounds to 1 + x.</summary>
    private const double Tiny = 5.551115123125783e-17;

    /// <summary>1.5·2^52: adding it rounds a double of magnitude below 2^51 to an integer.</summary>
    private const double Shifter = 6755399441055744.0;

    /// <summary>Degree of the Taylor polynomial of the accurate path; the truncation is below 2^-140 for <c>|y| ≤ 0.347</c>.</summary>
    private const int AccurateDegree = 27;

    /// <summary>exp(<paramref name="x"/>) correctly rounded to nearest, ties to even; NaN gives <paramref name="x"/>.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static double Exp(double x)
    {
        if (double.IsNaN(x))
        {
            return x;
        }

        if (x > XMax)
        {
            return double.PositiveInfinity;
        }

        if (x < XZero)
        {
            return 0.0;
        }

        if (Math.Abs(x) < Tiny)
        {
            return 1.0 + x;
        }

        if (x < XSubnormal)
        {
            return Accurate(x, 0.0);
        }

        var f = Fast(x, 0.0, out var k);
        return RoundingTest.Certifies(f, FastError, out var rounded) ? ErrorFree.Scale(rounded, k >> 6) : Accurate(x, 0.0);
    }

    /// <summary>exp(<paramref name="x"/> + <paramref name="xl"/>) / 2^(k >> 6) as a double-double, for <c>|xl|</c> at most about 2^-43; <paramref name="k"/> is the reduction index.</summary>
    public static Dd Fast(double x, double xl, out int k)
    {
        var t = ErrorFree.Fma(x, ElementaryConstants.ExpInverseStep, Shifter);
        var kd = t - Shifter;
        k = (int)kd;
        var rh = ErrorFree.Fma(-kd, ElementaryConstants.ExpStepHigh, x);
        var rl = ErrorFree.Fma(-kd, ElementaryConstants.ExpStepLow, xl);
        var sq = ErrorFree.TwoProd(rh, rh);
        var p = ErrorFree.Fma(rh, ErrorFree.Fma(rh, ErrorFree.Fma(rh, ErrorFree.Fma(rh, 1.0 / 5040, 1.0 / 720), 1.0 / 120), 1.0 / 24), 1.0 / 6);
        var q = sq.Hi * rh * p;
        var e = ErrorFree.FastTwoSum(rh, 0.5 * sq.Hi);
        var el = e.Lo + ErrorFree.Fma(0.5, sq.Lo, q);
        el = ErrorFree.Fma(rl, e.Hi + q, el + rl);
        var tab = ExpTable.At(k & 63);
        var ph = ErrorFree.TwoProd(tab.Hi, e.Hi);
        var h = ErrorFree.FastTwoSum(tab.Hi, ph.Hi);
        var lo = h.Lo + ph.Lo + ErrorFree.Fma(tab.Hi, el, ErrorFree.Fma(tab.Lo, e.Hi, tab.Lo));
        return ErrorFree.FastTwoSum(h.Hi, lo);
    }

    /// <summary>The accurate path: exp(x + xl) = 2^e exp(y), y = x + xl − e ln 2 in triple-double, Taylor to degree 27, then the correct rounding of the scaled value.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static double Accurate(double x, double xl) => TripleDouble.RoundScaled(AccurateTd(new Td(x, xl, 0.0), out var e), e);

    /// <summary>exp(<paramref name="x"/>) / 2^<paramref name="e"/> as a triple-double, for a triple-double x with <c>|x|</c> below about 746.</summary>
    public static Td AccurateTd(Td x, out int e)
    {
        var ed = Math.Floor(x.H * ElementaryConstants.InverseLn2 + 0.5);
        e = (int)ed;
        var y = TripleDouble.Add(x, TripleDouble.MulD(new Td(ElementaryConstants.Ln2T0, ElementaryConstants.Ln2T1, ElementaryConstants.Ln2T2), -ed));
        var s = TripleDouble.Of(1.0);
        for (var n = AccurateDegree; n >= 1; n--)
        {
            s = TripleDouble.Add(TripleDouble.Of(1.0), TripleDouble.DivD(TripleDouble.Mul(y, s), n));
        }

        return s;
    }
}
