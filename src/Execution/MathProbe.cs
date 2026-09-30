namespace APThermo.Execution;

/// <summary>The probe of the root's math list: one value per function per input.</summary>
internal static class MathProbe
{
    /// <summary>
    /// The functions of the root's list, in the order of the probe's outputs. Pow is probed at three exponents, not one
    /// (2026-09-27, the guards audit's F11: a single exponent left the other two unexercised), each a libdevice call of its
    /// own on CUDA; see <see cref="PowExponent1"/>, <see cref="PowExponent2"/> and <see cref="PowExponent3"/>. Min and Max are
    /// each probed with the constant operand on both sides (2026-09-28, the second audit's Execution finding F1): ILGPU moves
    /// a constant left operand of a floating comparison to the right and inverts its NaN ordering while doing so (root
    /// BOOT.md, the third ILGPU defect), so <c>KernelMath.Min(v, 1.0)</c> and <c>KernelMath.Min(1.0, v)</c> compile to
    /// different PTX and must both be probed for a NaN <c>v</c> to be caught. The first <see cref="LibdeviceFunctionCount"/>
    /// entries call into libdevice on CUDA (Exp, Log, Log10, the three Pow exponents, Sqrt, Floor and Ceiling — the post-link
    /// completes seven distinct wrapper names for the probe, <c>pow</c> shared by the three exponents); of the rest, only Abs
    /// is a PTX instruction the compiler emits directly, and Min/Max (in either operand order) compile to comparisons and
    /// selects, no libdevice call and no single instruction either. Every entry from <see cref="LibdeviceFunctionCount"/> on
    /// must still match the CPU accelerator exactly (0 ULP), whichever of the three mechanisms produced it.
    /// </summary>
    public static readonly IReadOnlyList<string> Functions =
        ["Exp", "Log", "Log10", "Pow(1.37)", "Pow(1.4)", "Pow(4.6)", "Sqrt", "Floor", "Ceiling", "Abs", "Min(v,1)", "Max(v,1)", "Min(1,v)", "Max(1,v)"];

    /// <summary>Outputs per input.</summary>
    public static int FunctionCount => Functions.Count;

    /// <summary>
    /// The compile-time count <see cref="Kernels.Probe"/> strides by. The kernel cannot call <see cref="FunctionCount"/> itself:
    /// it would touch the managed string array <see cref="Functions"/>, and kernel-compatible code allows no strings (root
    /// BOOT.md); a <c>const</c> inlines into the kernel, a property getter does not. Internal, not a second public member, so
    /// this file changes no public surface (BOOT.md's Structure: MathProbe's "contract unchanged"); a test ties it to
    /// <see cref="FunctionCount"/> so the two cannot drift silently (F-EX-07).
    /// </summary>
    internal const int StrideCount = 14;

    /// <summary>
    /// The length of the probe's output for <paramref name="inputCount"/> inputs, refusing a count whose output
    /// <see cref="Kernels.Probe"/> could not address (2026-09-30, "No test allocates what it measures"): the kernel strides its
    /// output by <see cref="StrideCount"/> with 32-bit <c>Index1D</c> arithmetic, so more inputs than this would let
    /// <c>index * StrideCount</c> overflow the offset on the device (the second audit's observation 7). The check lives here,
    /// on the count, so a fact can prove the bound without allocating an input array of the size it refuses.
    /// </summary>
    public static int OutputLength(int inputCount)
    {
        var length = (long)inputCount * FunctionCount;
        return length > int.MaxValue
            ? throw new ArgumentException($"{inputCount} inputs times {FunctionCount} functions overflows a 32-bit offset.", nameof(inputCount))
            : (int)length;
    }

    /// <summary>
    /// How many of the leading entries of <see cref="Functions"/> go through a libdevice call on CUDA: Exp, Log, Log10, the
    /// three Pow exponents, Sqrt, Floor and Ceiling (2026-09-28: Floor and Ceiling were documented as "the compiler emits
    /// directly", which held only for Abs; the post-link's own wrapper inventory names <c>__nv_floor</c> and <c>__nv_ceil</c>
    /// among the seven it completes, the second hidden-defect audit's observation 1). The rest (Abs, and Min/Max in both
    /// operand orders) must match the CPU accelerator exactly, not merely within the ULP bound.
    /// </summary>
    internal const int LibdeviceFunctionCount = 9;

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
