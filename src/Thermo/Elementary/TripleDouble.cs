namespace APThermo.Thermo.Elementary;

/// <summary>
/// Triple-double arithmetic for the rare accurate paths (relative error about 2^-140 without cancellation), and the
/// correct rounding of a triple-double to a double. Table-free and branch-light: only <c>+ - * /</c>, the fused
/// multiply-add, <see cref="Math.Floor(double)"/> and <see cref="Math.Abs(double)"/>.
/// </summary>
internal static class TripleDouble
{
    /// <summary>The smallest normal double doubled: below it a scaled result is in the subnormal range.</summary>
    private const double SubnormalLimit = 2.2250738585072014e-308 * 2.0;

    /// <summary>The triple-double of one double.</summary>
    public static Td Of(double a) => new(a, 0.0, 0.0);

    /// <summary>Three Priest passes of <see cref="ErrorFree.TwoSum"/> over five terms, largest first or not; returns the leading three.</summary>
    public static Td Renorm(double x0, double x1, double x2, double x3, double x4)
    {
        var s = ErrorFree.TwoSum(x3, x4);
        x3 = s.Hi;
        x4 = s.Lo;
        s = ErrorFree.TwoSum(x2, x3);
        x2 = s.Hi;
        x3 = s.Lo;
        s = ErrorFree.TwoSum(x1, x2);
        x1 = s.Hi;
        x2 = s.Lo;
        s = ErrorFree.TwoSum(x0, x1);
        x0 = s.Hi;
        x1 = s.Lo;
        s = ErrorFree.TwoSum(x3, x4);
        x3 = s.Hi;
        x4 = s.Lo;
        s = ErrorFree.TwoSum(x2, x3);
        x2 = s.Hi;
        x3 = s.Lo;
        s = ErrorFree.TwoSum(x1, x2);
        x1 = s.Hi;
        x2 = s.Lo;
        s = ErrorFree.TwoSum(x3, x4);
        x3 = s.Hi;
        x4 = s.Lo;
        s = ErrorFree.TwoSum(x2, x3);
        x2 = s.Hi;
        x3 = s.Lo;
        x2 += x3 + x4;
        s = ErrorFree.TwoSum(x0, x1);
        x0 = s.Hi;
        x1 = s.Lo;
        s = ErrorFree.TwoSum(x1, x2);
        x1 = s.Hi;
        x2 = s.Lo;
        s = ErrorFree.TwoSum(x0, x1);
        return new Td(s.Hi, s.Lo, x2);
    }

    /// <summary><c>a + b</c>.</summary>
    public static Td Add(Td a, Td b)
    {
        var h = ErrorFree.TwoSum(a.H, b.H);
        var m = ErrorFree.TwoSum(a.M, b.M);
        var l = ErrorFree.TwoSum(a.L, b.L);
        return Renorm(h.Hi, h.Lo, m.Hi, m.Lo + l.Hi, l.Lo);
    }

    /// <summary><c>a·b</c>.</summary>
    public static Td Mul(Td a, Td b)
    {
        var p0 = ErrorFree.TwoProd(a.H, b.H);
        var p1 = ErrorFree.TwoProd(a.H, b.M);
        var p2 = ErrorFree.TwoProd(a.M, b.H);
        var t = a.H * b.L + a.M * b.M + a.L * b.H + p1.Lo + p2.Lo;
        var u = ErrorFree.TwoSum(p1.Hi, p2.Hi);
        return Renorm(p0.Hi, p0.Lo, u.Hi, u.Lo, t);
    }

    /// <summary><c>a·b</c> for a double <paramref name="b"/>.</summary>
    public static Td MulD(Td a, double b)
    {
        var p0 = ErrorFree.TwoProd(a.H, b);
        var p1 = ErrorFree.TwoProd(a.M, b);
        return Renorm(p0.Hi, p0.Lo, p1.Hi, p1.Lo, a.L * b);
    }

    /// <summary><c>a / n</c> for a small integer-valued <paramref name="n"/> (exact remainders through the fused multiply-add).</summary>
    public static Td DivD(Td a, double n)
    {
        var q0 = a.H / n;
        var r = Add(a, MulD(Of(q0), -n));
        var q1 = r.H / n;
        r = Add(r, MulD(Of(q1), -n));
        var q2 = r.H / n;
        return Renorm(q0, q1, q2, 0.0, 0.0);
    }

    /// <summary><c>a / b</c> for triple-doubles, three long-division steps.</summary>
    public static Td Div(Td a, Td b)
    {
        var q0 = a.H / b.H;
        var r = Add(a, Mul(b, Of(-q0)));
        var q1 = r.H / b.H;
        r = Add(r, Mul(b, Of(-q1)));
        var q2 = r.H / b.H;
        r = Add(r, Mul(b, Of(-q2)));
        var q3 = r.H / b.H;
        return Renorm(q0, q1, q2, q3, 0.0);
    }

    /// <summary>Round to nearest, ties to even, of H + M + L in the normal range (H normal, |M| at most half an ulp of H).</summary>
    /// <remarks>The tie of H + M is detected exactly (the remainder <c>e</c> of the fast sum is half an ulp of the result) and
    /// broken by the sign of L.</remarks>
    public static double Round(Td v)
    {
        var s = ErrorFree.FastTwoSum(v.H, v.M);
        var r = s.Hi;
        var e = s.Lo;
        var tie = e != 0.0 && r + 2.0 * e - r == 2.0 * e && e * v.L > 0.0;
        return tie ? r + 2.0 * e : r;
    }

    /// <summary>Round to nearest of <paramref name="v"/>·2^<paramref name="e"/>, the subnormal range and the overflow included.</summary>
    public static double RoundScaled(Td v, int e)
    {
        var top = ErrorFree.Scale(v.H, e);
        if (!(Math.Abs(top) < SubnormalLimit))
        {
            // Normal range: rounding first and scaling exactly after.
            return ErrorFree.Scale(Round(v), e);
        }

        // Subnormal: round v·2^(e + 1074) to an integer, which is the subnormal's significand.
        var k = e + 1074;
        var w = new Td(ErrorFree.Scale(v.H, k), ErrorFree.Scale(v.M, k), ErrorFree.Scale(v.L, k));
        var n0 = Math.Floor(w.H);
        var f = ErrorFree.TwoSum(w.H - n0, w.M);
        var fl = f.Lo + w.L;
        var up = f.Hi > 0.5 || (f.Hi == 0.5 && (fl > 0.0 || (fl == 0.0 && Math.Floor(n0 * 0.5) * 2.0 != n0)));
        var n = n0 + (up ? 1.0 : 0.0);
        return BitConverter.Int64BitsToDouble((long)n) * (v.H < 0.0 ? -1.0 : 1.0);
    }
}
