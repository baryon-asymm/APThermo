namespace APThermo.Thermo;

/// <summary>
/// The tree's own minimum and maximum for every numerical node (root <c>BOOT.md</c>, "Math in numerical nodes",
/// 2026-09-27): ILGPU compiles <see cref="Math.Min(double, double)"/> and <see cref="Math.Max(double, double)"/> to
/// the PTX instructions <c>min.f64</c> and <c>max.f64</c>, which return the other operand when one operand is NaN,
/// while <see cref="System.Math"/> propagates NaN back to the caller. <see cref="Min"/> and <see cref="Max"/> equal
/// <see cref="Math.Min(double, double)"/> and <see cref="Math.Max(double, double)"/> on every value that is not NaN, bit for bit,
/// and return NaN whenever either operand is NaN (the payload of that NaN is the first NaN operand's, which <see cref="System.Math"/>
/// does not promise), on both accelerators, because they are written with comparisons and selections only: NaN propagates from either operand,
/// and of two equal values (<c>+0</c> and <c>-0</c> included) <see cref="Min"/> treats <c>-0</c> as smaller and
/// <see cref="Max"/> treats <c>+0</c> as larger. <see cref="double.IsNaN(double)"/> and
/// <see cref="double.IsNegative(double)"/> are used here and nowhere else in the numerical nodes (the root's
/// constraint); the execution node's probe proves ILGPU compiles them on CUDA without libdevice.
/// </summary>
/// <remarks>
/// Both operands are tested for NaN before either takes part in an ordered comparison (2026-09-28, the root's third
/// ILGPU defect): ILGPU moves a constant left operand of a floating-point comparison to the right and inverts its NaN
/// ordering while doing so, so once inlining makes one operand a compile-time constant, an ordered comparison against
/// a NaN second operand can answer differently on CUDA than on the CPU. The form below tests <c>val1</c>, then
/// <c>val2</c>, before any comparison touches either, so no comparison this method performs ever sees a NaN operand,
/// whichever side ILGPU moves a constant to. This returns the same value as before for every pair, two NaNs included
/// (the first operand's payload): the change reorders the checks, it does not change what they decide.
/// </remarks>
internal static class KernelMath
{
    /// <summary>Equals <see cref="Math.Min(double, double)"/> bit for bit off NaN: NaN if either operand is NaN, −0 below +0.</summary>
    public static double Min(double val1, double val2) =>
        double.IsNaN(val1)
            ? val1
            : double.IsNaN(val2)
                ? val2
                : val1 != val2
                    ? val1 < val2 ? val1 : val2
                    : double.IsNegative(val1) ? val1 : val2;

    /// <summary>Equals <see cref="Math.Max(double, double)"/> bit for bit off NaN: NaN if either operand is NaN, +0 above −0.</summary>
    public static double Max(double val1, double val2) =>
        double.IsNaN(val1)
            ? val1
            : double.IsNaN(val2)
                ? val2
                : val1 != val2
                    ? val2 < val1 ? val1 : val2
                    : double.IsNegative(val2) ? val1 : val2;
}
