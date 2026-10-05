namespace APThermo.Thermo.Elementary;

/// <summary>
/// The error-free transformations and the one fused multiply-add of the tree. Every operation is one IEEE operation on
/// both accelerators (root <c>BOOT.md</c>, "Math in numerical nodes"): the CPU accelerator inherits RyuJIT's, which never
/// contracts a product with a sum, and the execution node marks every <c>mul</c>, <c>add</c> and <c>sub</c> of a kernel's
/// PTX <c>.rn</c>, which PTX defines as never contracted.
/// </summary>
internal static class ErrorFree
{
    /// <summary><c>a·b + c</c> with one rounding, <see cref="Math.FusedMultiplyAdd(double, double, double)"/>: the only fused operation of the tree.</summary>
    public static double Fma(double a, double b, double c) => Math.FusedMultiplyAdd(a, b, c);

    /// <summary><c>a + b</c> as a double-double, exactly (Knuth).</summary>
    public static Dd TwoSum(double a, double b)
    {
        var s = a + b;
        var bb = s - a;
        return new Dd(s, a - (s - bb) + (b - bb));
    }

    /// <summary><c>a + b</c> as a double-double, exactly, for <c>|a| ≥ |b|</c> or <c>a = 0</c> (Dekker).</summary>
    public static Dd FastTwoSum(double a, double b)
    {
        var s = a + b;
        return new Dd(s, b - (s - a));
    }

    /// <summary><c>a·b</c> as a double-double, exactly, through the fused multiply-add.</summary>
    public static Dd TwoProd(double a, double b)
    {
        var p = a * b;
        return new Dd(p, Fma(a, b, -p));
    }

    /// <summary>2^<paramref name="e"/> for <paramref name="e"/> in −1022 … 1023, built from the exponent field.</summary>
    public static double Pow2(int e) => BitConverter.Int64BitsToDouble((long)(e + 1023) << 52);

    /// <summary><paramref name="v"/>·2^<paramref name="e"/>, in two exact factors so that an exponent of 1024 or −1074 does not overflow the factor.</summary>
    public static double Scale(double v, int e) => v * Pow2(e / 2) * Pow2(e - e / 2);
}
