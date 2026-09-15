namespace APThermo.Execution;

/// <summary>The probe of the root's math list: one value per function per input.</summary>
public static class MathProbe
{
    /// <summary>The functions of the root's list, in the order of the probe's outputs.</summary>
    public static readonly IReadOnlyList<string> Functions =
        ["Exp", "Log", "Log10", "Pow", "Sqrt", "Abs", "Min", "Max", "Floor", "Ceiling"];

    /// <summary>Outputs per input.</summary>
    public static int FunctionCount => Functions.Count;

    /// <summary>
    /// The compile-time count <see cref="Kernels.Probe"/> strides by. The kernel cannot call <see cref="FunctionCount"/> itself:
    /// it would touch the managed string array <see cref="Functions"/>, and kernel-compatible code allows no strings (root
    /// BOOT.md); a <c>const</c> inlines into the kernel, a property getter does not. Internal, not a second public member, so
    /// this file changes no public surface (BOOT.md's Structure: MathProbe's "contract unchanged"); a test ties it to
    /// <see cref="FunctionCount"/> so the two cannot drift silently (F-EX-07).
    /// </summary>
    internal const int StrideCount = 10;

    /// <summary>The exponent the probe passes to Pow.</summary>
    public const double PowExponent = 1.37;
}
