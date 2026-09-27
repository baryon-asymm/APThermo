namespace APThermo.Thermo;

/// <summary>
/// The tree's own minimum and maximum for every numerical node (root <c>BOOT.md</c>, "Math in numerical nodes",
/// 2026-09-27): ILGPU compiles <see cref="Math.Min(double, double)"/> and <see cref="Math.Max(double, double)"/> to
/// the PTX instructions <c>min.f64</c> and <c>max.f64</c>, which return the other operand when one operand is NaN,
/// while <see cref="System.Math"/> propagates NaN back to the caller. <see cref="Min"/> and <see cref="Max"/> equal
/// <see cref="Math.Min(double, double)"/> and <see cref="Math.Max(double, double)"/> bit for bit, on both
/// accelerators, because they are written with comparisons and selections only, following the logic of the .NET 10
/// source of <c>System.Private.CoreLib</c>'s <c>Math.Min(double, double)</c> and <c>Math.Max(double, double)</c>
/// (dotnet/runtime, <c>src/libraries/System.Private.CoreLib/src/System/Math.cs</c>): NaN propagates from either
/// operand, and of two equal values (<c>+0</c> and <c>-0</c> included) <see cref="Min"/> treats <c>-0</c> as smaller
/// and <see cref="Max"/> treats <c>+0</c> as larger. <see cref="double.IsNaN(double)"/> and
/// <see cref="double.IsNegative(double)"/> are used here and nowhere else in the numerical nodes (the root's
/// constraint); the execution node's probe proves ILGPU compiles them on CUDA without libdevice.
/// </summary>
internal static class KernelMath
{
    /// <summary>Equals <see cref="Math.Min(double, double)"/> bit for bit: NaN if either operand is NaN, −0 below +0.</summary>
    public static double Min(double val1, double val2) =>
        val1 != val2
            ? double.IsNaN(val1) ? val1 : val1 < val2 ? val1 : val2
            : double.IsNegative(val1) ? val1 : val2;

    /// <summary>Equals <see cref="Math.Max(double, double)"/> bit for bit: NaN if either operand is NaN, +0 above −0.</summary>
    public static double Max(double val1, double val2) =>
        val1 != val2
            ? double.IsNaN(val1) ? val1 : val2 < val1 ? val1 : val2
            : double.IsNegative(val2) ? val1 : val2;
}
