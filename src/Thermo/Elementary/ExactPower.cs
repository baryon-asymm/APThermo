namespace APThermo.Thermo.Elementary;

/// <summary>
/// The exact midpoint cases of pow (Lauter and Lefèvre): x^y that is exactly the midpoint of two adjacent doubles, which no
/// approximation can round correctly (it lies on the boundary), so the rounding is done on the integers.
/// </summary>
/// <remarks>
/// Write <c>x = a·2^e</c> and <c>|y| = p·2^f</c> with <c>a</c> and <c>p</c> odd. For <c>a &gt; 1</c> the power is dyadic only for
/// <c>y &gt; 0</c>, only when <c>a</c> is a perfect (2^k)-th power <c>b^(2^k)</c> (<c>k = −f</c>, <c>f &lt; 0</c>; <c>b = a</c>
/// for an integer y), and only when <c>e·p/2^k</c> is an integer: then <c>x^y = b^p·2^(e·p/2^k)</c> with <c>b^p</c> odd. For
/// <c>a = 1</c> (x a power of two) <c>x^y = 2^(e·y)</c> for any sign of y with <c>e·y</c> an integer. A value <c>b^p·2^t</c>
/// with <c>b^p</c> odd of <c>L</c> bits is the midpoint of two doubles exactly when <c>t = u − 1</c>, <c>u</c> being the
/// exponent of the ulp there, <c>max(t + L − 53, −1074)</c>: in the normal range that is <c>L = 54</c>, in the subnormal range
/// <c>t = −1075</c>. Ties go to even. A result that is representable, or beyond a midpoint, is not a boundary and is left to
/// the approximation, which cannot misround it.
/// </remarks>
internal static class ExactPower
{
    /// <summary>The mantissa field of a double.</summary>
    private const long MantissaMask = 0x000FFFFFFFFFFFFFL;

    /// <summary>The implicit leading bit of a normal double's significand.</summary>
    private const long LeadingBit = 0x0010000000000000L;

    /// <summary>2^54: a midpoint's odd significand is below it.</summary>
    private const ulong Bit54 = 1UL << 54;

    /// <summary>The odd part of y or of the exponent products beyond this cannot make a midpoint (|e·y| = 1075 at most).</summary>
    private const ulong MaxOddPart = 1UL << 20;

    /// <summary>A power-of-two denominator of y beyond this cannot make a midpoint: 2^k divides e·p with |e| ≤ 2^11 and p ≤ 2^20.</summary>
    private const int MaxDenominatorShift = 40;

    /// <summary>An integer y beyond 2^10·odd cannot make a midpoint unless x is 1.</summary>
    private const int MaxIntegerShift = 10;

    /// <summary>The largest exponent a finite double's leading bit can have.</summary>
    private const int MaxTopExponent = 1023;

    /// <summary>The exponent of the ulp of the subnormal range.</summary>
    private const int SubnormalUlpExponent = -1074;

    /// <summary>
    /// Whether x^y, for a finite positive x ≠ 1 and a finite y ≠ 0, is exactly the midpoint of two adjacent doubles, and the
    /// midpoint rounded to nearest even in <paramref name="result"/> when it is.
    /// </summary>
    public static bool TryRoundMidpoint(double x, double y, out double result)
    {
        result = 0.0;
        var a = Odd(x, out var e);
        var p = Odd(Math.Abs(y), out var f);
        var shift = f < 0 ? -f : 0;
        if (f > MaxIntegerShift || shift > MaxDenominatorShift || (a > 1 && y < 0.0))
        {
            return false;
        }

        var power = p << (f > 0 ? f : 0);
        if (!OddRoot(a, shift, out var b) || !OddPower(b, power, out var significand) || power > MaxOddPart)
        {
            return false;
        }

        var scaled = e * (long)power * (y < 0.0 ? -1 : 1);
        return (scaled & ((1L << shift) - 1)) == 0 && RoundMidpoint(significand, (int)(scaled >> shift), out result);
    }

    /// <summary>The odd part of a positive finite double and its binary exponent: <paramref name="v"/> = odd·2^<paramref name="exponent"/>.</summary>
    private static ulong Odd(double v, out int exponent)
    {
        var bits = BitConverter.DoubleToInt64Bits(v);
        var field = (int)(bits >> 52);
        var mantissa = field == 0 ? bits & MantissaMask : bits & MantissaMask | LeadingBit;
        exponent = field == 0 ? SubnormalUlpExponent : field - 1075;
        while ((mantissa & 1) == 0)
        {
            mantissa >>= 1;
            exponent++;
        }

        return (ulong)mantissa;
    }

    /// <summary>The (2^<paramref name="k"/>)-th root of <paramref name="a"/> when it is an integer.</summary>
    private static bool OddRoot(ulong a, int k, out ulong root)
    {
        root = a;
        for (var i = 0; i < k && root > 1; i++)
        {
            var r = (ulong)Math.Sqrt(root);
            if (r * r != root)
            {
                return false;
            }

            root = r;
        }

        return true;
    }

    /// <summary><paramref name="b"/>^<paramref name="n"/> when it is below 2^54.</summary>
    private static bool OddPower(ulong b, ulong n, out ulong power)
    {
        power = 1;
        for (ulong i = 0; i < n && b > 1; i++)
        {
            if (power > Bit54 / b)
            {
                return false;
            }

            power *= b;
        }

        return true;
    }

    /// <summary>The midpoint <paramref name="odd"/>·2^<paramref name="t"/> rounded to nearest even, when it is one.</summary>
    private static bool RoundMidpoint(ulong odd, int t, out double result)
    {
        result = 0.0;
        var length = 0;
        for (var v = odd; v > 0; v >>= 1)
        {
            length++;
        }

        var top = t + length - 1;
        var ulpExponent = top - 52 > SubnormalUlpExponent ? top - 52 : SubnormalUlpExponent;
        if (top > MaxTopExponent || t != ulpExponent - 1)
        {
            return false;
        }

        var below = (odd - 1) >> 1;
        var rounded = (below & 1) == 0 ? below : below + 1;
        result = TripleDouble.RoundScaled(new Td(rounded, 0.0, 0.0), ulpExponent);
        return true;
    }
}
