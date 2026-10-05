using System.Reflection;
using System.Text.RegularExpressions;
using ILGPU;
using ILGPU.Backends.PTX;
using ILGPU.Runtime.Cuda;

namespace APThermo.Execution.Ptx;

/// <summary>
/// Fixes the arithmetic of a compiled CUDA kernel and checks it before it is loaded: the one place in the tree that knows
/// ILGPU's internals (root <c>BOOT.md</c>, "Math in numerical nodes" and the ILGPU constraint). <see cref="Link"/> turns
/// <c>Math.FusedMultiplyAdd</c>, which ILGPU emits as a call of an undefined external function, into the single instruction
/// <c>fma.rn.f64</c>; marks every <c>mul</c>, <c>add</c> and <c>sub</c> of <c>.f64</c> with <c>.rn</c>, which PTX defines as
/// never contracted into a fused multiply-add (the CPU never contracts either: the two accelerators then run the same
/// operations and return the same bits); refuses a kernel whose PTX then holds a fused multiply-add it did not write, an
/// <c>.approx</c> f64 instruction or an external function; and trial-loads the result through the CUDA driver, so that a refusal
/// carries the driver's log. No driver result is ignored (the parent's <c>BOOT.md</c>, Invariants): every call into the driver is
/// checked through <see cref="ThrowIfFailed"/>, the one place that turns a non-success result into an exception.
/// </summary>
internal static partial class PtxPostLink
{
    /// <summary>The ILGPU version whose internals this post-link was written against.</summary>
    public const string ExpectedIlgpuVersion = "1.5.3.0";

    private const string AssemblyField = "<PTXAssembly>k__BackingField";

    /// <summary>
    /// The call sequence ILGPU 1.5.3 emits for <c>Math.FusedMultiplyAdd</c>: three parameters stored, one call, the result
    /// loaded, all in one block. The inlined form is the one instruction the call stands for.
    /// </summary>
    [GeneratedRegex(
        @"\{\s*\.param \.f64 callParam0;\s*st\.param\.f64\s*\[callParam0\], (\S+);\s*\.param \.f64 callParam1;\s*st\.param\.f64\s*\[callParam1\], (\S+);\s*\.param \.f64 callParam2;\s*st\.param\.f64\s*\[callParam2\], (\S+);\s*\.param \.f64 callRetVal;\s*call\.uni \(callRetVal\), \w*FusedMultiplyAdd\w*, \(\s*callParam0,\s*callParam1,\s*callParam2\s*\);\s*ld\.param\.f64\s*(\S+), \[callRetVal\];\s*\}")]
    private static partial Regex FusedCall();

    /// <summary>The declaration of the external function ILGPU emits for <c>Math.FusedMultiplyAdd</c>, which has no body.</summary>
    [GeneratedRegex(@"\.extern \.func \(\.param \.f64 \w+\) \w*FusedMultiplyAdd\w*\(\s*\.param \.f64 \w+,\s*\.param \.f64 \w+,\s*\.param \.f64 \w+\s*\)\s*;\s*")]
    private static partial Regex FusedDeclaration();

    /// <summary>A call of the fused multiply-add's external function that the inlining did not recognise.</summary>
    [GeneratedRegex(@"\bcall(?:\.uni)?\b[^;]*FusedMultiplyAdd")]
    private static partial Regex SurvivingFusedCall();

    /// <summary>A multiplication, addition or subtraction of doubles without a rounding modifier: the ones a PTX compiler may contract.</summary>
    [GeneratedRegex(@"\b(mul|add|sub)\.f64\b")]
    private static partial Regex UnroundedArithmetic();

    /// <summary>Any fused multiply-add of doubles, whatever its rounding modifier, and the older <c>mad</c> forms.</summary>
    [GeneratedRegex(@"\b(?:fma|mad)(?:\.\w+)*\.f64\b")]
    private static partial Regex FusedInstruction();

    /// <summary>An instruction of doubles with the <c>.approx</c> modifier: not correctly rounded, so not the same on every device.</summary>
    [GeneratedRegex(@"\b\w+(?:\.\w+)*\.approx(?:\.\w+)*\.f64\b")]
    private static partial Regex ApproximateInstruction();

    /// <summary>An external function: declared here and defined elsewhere, so not part of the kernel the guards have read.</summary>
    [GeneratedRegex(@"\.extern\b[^;{]*")]
    private static partial Regex ExternalFunction();

    [GeneratedRegex(@"^\.target\s+sm_(\d+)", RegexOptions.Multiline)]
    private static partial Regex Target();

    private static readonly Lazy<FieldInfo> Backing = new(() => AssertIlgpu(ExpectedIlgpuVersion));

    /// <summary>The ILGPU version string of the loaded assembly.</summary>
    public static string IlgpuVersion => typeof(Context).Assembly.GetName().Version?.ToString() ?? "unknown";

    /// <summary>Asserts the ILGPU version and the reflected member once; an exception names the version.</summary>
    public static void AssertIlgpu() => _ = Backing.Value;

    /// <summary>The assertion against a given version, for the tests to prove it fails loudly.</summary>
    internal static FieldInfo AssertIlgpu(string expectedVersion)
    {
        var version = IlgpuVersion;
        if (version != expectedVersion)
        {
            throw new InvalidOperationException($"ILGPU {version} is loaded, but the PTX post-link was written for ILGPU {expectedVersion}; its internals must be re-verified.");
        }

        var backing = typeof(PTXCompiledKernel).GetField(AssemblyField, BindingFlags.NonPublic | BindingFlags.Instance);
        return backing is null || backing.FieldType != typeof(string)
            ? throw new InvalidOperationException($"ILGPU {version}: {nameof(PTXCompiledKernel)}.{AssemblyField} is not the string field the post-link expects.")
            : backing;
    }

    /// <summary>What <see cref="Link"/> did to a kernel: how many multiply, add and subtract instructions it marked <c>.rn</c> and how many calls of the fused multiply-add it turned into the instruction.</summary>
    internal readonly record struct LinkResult(PTXCompiledKernel Kernel, int RoundedOperations, int FusedSites);

    /// <summary>The PTX after the post-link's rewrite, and what the rewrite did.</summary>
    internal readonly record struct Rewritten(string Ptx, int RoundedOperations, int FusedSites);

    /// <summary>
    /// Rewrites, checks and trial-loads the kernel (BOOT.md, "The post-link"): the fused multiply-add calls become the instruction,
    /// every unrounded multiply, add and subtract becomes <c>.rn</c>, the guards read the result, the driver loads it once and
    /// destroys the module again, and the kernel's PTX is replaced by the rewritten text.
    /// </summary>
    public static LinkResult Link(CudaAccelerator accelerator, PTXCompiledKernel compiled)
    {
        var rewritten = Rewrite(compiled.PTXAssembly);
        var arch = TargetArch(rewritten.Ptx);

        // The driver checks the PTX against a context bound to the calling thread; bind the accelerator's before the trial load.
        accelerator.Bind();
        TrialLoad(rewritten.Ptx, arch);
        Backing.Value.SetValue(compiled, rewritten.Ptx);
        return new LinkResult(compiled, rewritten.RoundedOperations, rewritten.FusedSites);
    }

    /// <summary>The text rewrite and the guards, without a driver: what the facts that read PTX drive.</summary>
    internal static Rewritten Rewrite(string ptx)
    {
        var sites = FusedCall().Count(ptx);
        var inlined = FusedCall().Replace(ptx, "fma.rn.f64\t$4, $1, $2, $3;");
        var arch = TargetArch(inlined);

        // The declaration goes only when no call of it is left: a surviving call keeps its external declaration, which the guard refuses.
        if (!SurvivingFusedCall().IsMatch(inlined))
        {
            inlined = FusedDeclaration().Replace(inlined, string.Empty);
        }

        var rounded = UnroundedArithmetic().Count(inlined);
        var linked = UnroundedArithmetic().Replace(inlined, "$1.rn.f64");
        Guard(linked, sites, arch);
        return new Rewritten(linked, rounded, sites);
    }

    /// <summary>
    /// The guards on the rewritten PTX: a fused multiply-add it did not write (the count of <c>fma</c> and <c>mad</c> of doubles is
    /// not the number of sites it inlined), an <c>.approx</c> instruction of doubles, an external function (the fused
    /// multiply-add's own among them, when a call survived the inlining). Each refusal
    /// names the kernel's target and the offending text.
    /// </summary>
    internal static void Guard(string ptx, int fusedSites, string arch)
    {
        var fused = FusedInstruction().Matches(ptx).Select(match => match.Value).ToList();
        var written = fused.Count(text => text == "fma.rn.f64");
        var foreign = fused.Where(text => text != "fma.rn.f64").ToList();
        if (foreign.Count > 0 || written != fusedSites)
        {
            throw new InvalidOperationException(FailureMessage(
                $"the PTX holds {fused.Count} fused multiply-add instruction(s) of doubles ({(foreign.Count > 0 ? foreign[0] : "fma.rn.f64")}) and the post-link wrote {fusedSites}", arch, null));
        }

        RefuseFirst(ApproximateInstruction(), ptx, "an approximate instruction of doubles", arch);
        RefuseFirst(ExternalFunction(), ptx, "an external function", arch);
    }

    private static void RefuseFirst(Regex pattern, string ptx, string what, string arch)
    {
        var match = pattern.Match(ptx);
        if (match.Success)
        {
            throw new InvalidOperationException(FailureMessage($"the PTX holds {what}: {match.Value.Trim()}", arch, null));
        }
    }

    /// <summary>The one shape every checked failure throws in: it names the post-link, the target, the CUDA driver, the call and the result code, with the log where one exists. Throws nothing for <see cref="CudaError.CUDA_SUCCESS"/>.</summary>
    internal static void ThrowIfFailed(CudaError result, string call, string arch, string? log = null)
    {
        if (result != CudaError.CUDA_SUCCESS)
        {
            throw new InvalidOperationException(FailureMessage($"the CUDA driver's {call} returned {result}", arch, log));
        }
    }

    /// <summary>The one message shape: the post-link, the target, what failed, and, where one exists, a non-empty log. An empty log (after trimming) is treated as no log at all, so the message carries no trailing ": " with nothing after it (2026-09-28, the second audit's observation 7).</summary>
    private static string FailureMessage(string outcome, string arch, string? log)
    {
        var trimmed = log is null ? null : TrimLog(log);
        return string.IsNullOrEmpty(trimmed)
            ? $"the PTX post-link for {arch}: {outcome}."
            : $"the PTX post-link for {arch}: {outcome}: {trimmed}";
    }

    /// <summary>Trims a driver log of both whitespace and the NUL padding of ILGPU's own log buffer (BOOT.md, the audit's observations): a plain <see cref="string.Trim()"/> leaves the padding, which a caller sees as trailing squares or nothing at all depending on the terminal.</summary>
    private static string TrimLog(string log) => log.Trim(['\0', ' ', '\t', '\r', '\n']);

    /// <summary>The kernel's own target, from its <c>.target sm_XX</c> line: ILGPU's choice per device, not a fixed value.</summary>
    internal static string TargetArch(string ptx)
    {
        var match = Target().Match(ptx);
        return !match.Success
            ? throw new InvalidOperationException("the kernel PTX has no .target line.")
            : "compute_" + match.Groups[1].Value;
    }

    /// <summary>
    /// Loads the linked PTX once through the CUDA driver as a trial and destroys the module again. The driver's
    /// <c>DestroyModule</c> is reached only when its <c>LoadModule</c> already succeeded, so it is always checked in the
    /// ordinary (not best-effort) way.
    /// </summary>
    private static void TrialLoad(string linked, string arch)
    {
        var loadResult = CudaAPI.CurrentAPI.LoadModule(out var handle, linked, out var log);
        ThrowIfFailed(loadResult, nameof(CudaAPI.LoadModule), arch, log);
        ThrowIfFailed(CudaAPI.CurrentAPI.DestroyModule(handle), nameof(CudaAPI.DestroyModule), arch);
    }
}
