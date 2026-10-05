namespace APThermo.Execution;

/// <summary>The probe of the root's math list: one value per function per input.</summary>
internal static class MathProbe
{
    /// <summary>
    /// The functions of the root's list, in the order of the probe's outputs (2026-10-05: the tree's own <c>Exp</c>, <c>Log</c> and
    /// <c>Pow</c> of <c>KernelMath</c>, which replaced the three <c>System.Math</c> functions and <c>Log10</c>, and the two
    /// entries that make contraction visible). Pow is probed at three exponents, not one (2026-09-27, the guards audit's F11: a
    /// single exponent left the other two unexercised); see <see cref="PowExponent1"/>, <see cref="PowExponent2"/> and
    /// <see cref="PowExponent3"/>. Min and Max are each probed with the constant operand on both sides (2026-09-28, the second
    /// audit's Execution finding F1): ILGPU moves a constant left operand of a floating comparison to the right and inverts its
    /// NaN ordering while doing so (root BOOT.md, the third ILGPU defect), so <c>KernelMath.Min(v, 1.0)</c> and
    /// <c>KernelMath.Min(1.0, v)</c> compile to different PTX and must both be probed for a NaN <c>v</c> to be caught. The last two
    /// entries are the fused multiply-add <c>KernelMath.Fma(v, Factor, Addend)</c> and the same product and sum written
    /// <c>v * Factor + Addend</c>, which differ in the last bit for many <c>v</c> exactly when a compiler contracts the second
    /// into the first: the equality of CUDA with the CPU accelerator on both is the proof that no contraction happens. The probe
    /// must equal the CPU accelerator exactly, every entry on every input (root BOOT.md, "GPU equals CPU, bit for bit").
    /// </summary>
    public static readonly IReadOnlyList<string> Functions =
        ["Exp", "Log", "Pow(1.37)", "Pow(1.4)", "Pow(4.6)", "Sqrt", "Floor", "Ceiling", "Abs", "Min(v,1)", "Max(v,1)", "Min(1,v)", "Max(1,v)", "Fma(v,1.37,4.6)", "v*1.37+4.6"];

    /// <summary>Outputs per input.</summary>
    public static int FunctionCount => Functions.Count;

    /// <summary>
    /// The compile-time count <see cref="Kernels.Probe"/> strides by. The kernel cannot call <see cref="FunctionCount"/> itself:
    /// it would touch the managed string array <see cref="Functions"/>, and kernel-compatible code allows no strings (root
    /// BOOT.md); a <c>const</c> inlines into the kernel, a property getter does not. Internal, not a second public member, so
    /// this file changes no public surface (BOOT.md's Structure: MathProbe's "contract unchanged"); a test ties it to
    /// <see cref="FunctionCount"/> so the two cannot drift silently (F-EX-07).
    /// </summary>
    internal const int StrideCount = 15;

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

    /// <summary>The factor of the two arithmetic entries, <c>Fma(v, Factor, Addend)</c> and <c>v * Factor + Addend</c> (2026-10-05).</summary>
    public const double Factor = 1.37;

    /// <summary>The addend of the two arithmetic entries (2026-10-05).</summary>
    public const double Addend = 4.6;
}
