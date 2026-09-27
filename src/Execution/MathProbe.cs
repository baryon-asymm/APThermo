namespace APThermo.Execution;

/// <summary>The probe of the root's math list: one value per function per input.</summary>
internal static class MathProbe
{
    /// <summary>
    /// The functions of the root's list, in the order of the probe's outputs. Pow is probed at three exponents, not one
    /// (2026-09-27, the guards audit's F11: a single exponent left the other two unexercised), each a libdevice call of its
    /// own on CUDA; see <see cref="PowExponent1"/>, <see cref="PowExponent2"/> and <see cref="PowExponent3"/>. The first
    /// <see cref="LibdeviceFunctionCount"/> entries call into libdevice on CUDA; the rest (Abs, Min, Max, Floor, Ceiling) are
    /// PTX instructions the compiler emits directly.
    /// </summary>
    public static readonly IReadOnlyList<string> Functions =
        ["Exp", "Log", "Log10", "Pow(1.37)", "Pow(1.4)", "Pow(4.6)", "Sqrt", "Abs", "Min", "Max", "Floor", "Ceiling"];

    /// <summary>Outputs per input.</summary>
    public static int FunctionCount => Functions.Count;

    /// <summary>
    /// The compile-time count <see cref="Kernels.Probe"/> strides by. The kernel cannot call <see cref="FunctionCount"/> itself:
    /// it would touch the managed string array <see cref="Functions"/>, and kernel-compatible code allows no strings (root
    /// BOOT.md); a <c>const</c> inlines into the kernel, a property getter does not. Internal, not a second public member, so
    /// this file changes no public surface (BOOT.md's Structure: MathProbe's "contract unchanged"); a test ties it to
    /// <see cref="FunctionCount"/> so the two cannot drift silently (F-EX-07).
    /// </summary>
    internal const int StrideCount = 12;

    /// <summary>
    /// How many of the leading entries of <see cref="Functions"/> call into libdevice on CUDA (Exp, Log, Log10, the three Pow
    /// exponents, Sqrt): the rest (Abs, Min, Max, Floor, Ceiling) are PTX instructions the compiler emits directly and must
    /// match the CPU accelerator exactly, not merely within the ULP bound (2026-09-27, F11; the test boundary used to be the
    /// literal 5, before Pow gained two more slots).
    /// </summary>
    internal const int LibdeviceFunctionCount = 7;

    /// <summary>
    /// The exponents <see cref="Kernels.Probe"/> passes to Pow (2026-09-27, F11), host-readable for the tests that build their
    /// own expected values. The kernel cannot index this array itself, for the reason <see cref="StrideCount"/> already gives
    /// for <see cref="Functions"/>; it uses the three consts below, which inline.
    /// </summary>
    public static readonly IReadOnlyList<double> PowExponents = [PowExponent1, PowExponent2, PowExponent3];

    /// <summary>The first exponent the probe passes to Pow.</summary>
    public const double PowExponent1 = 1.37;

    /// <summary>The second exponent the probe passes to Pow (2026-09-27, F11).</summary>
    public const double PowExponent2 = 1.4;

    /// <summary>The third exponent the probe passes to Pow (2026-09-27, F11).</summary>
    public const double PowExponent3 = 4.6;
}
