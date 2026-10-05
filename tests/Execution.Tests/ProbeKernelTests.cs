using APThermo.Harness;
using APThermo.Thermo;
using Xunit.Abstractions;

namespace APThermo.Execution.Tests;

/// <summary>L1: the probe kernel of the root's math list loads on CUDA through the post-link and equals the CPU accelerator bit for bit, and the CPU accelerator equals the host.</summary>
[Collection(EngineFixture.CollectionName)]
public sealed class ProbeKernelTests(ITestOutputHelper output)
{
    /// <summary>
    /// The solver's whole input domain, beyond the decade span (2026-09-27, the guards audit's F11): until now the probe's
    /// inputs were positive only, plus a handful of values around 1, so <c>exp</c>'s mostly-negative arguments, <c>Floor</c>
    /// and <c>Ceiling</c>'s negative inputs, and <c>Min</c>/<c>Max</c>'s NaN case were never probed. NaN and the signed zeros
    /// and infinities exercise the special-value handling of every function, compared by bits.
    /// </summary>
    private static double[] SpecialInputs() =>
    [
        1.0, 0.5, 1.5, 2.0, 2.5, 1e-300,
        -0.5, -1.5, -2.5,
        0.0, -0.0,
        double.PositiveInfinity, double.NegativeInfinity, double.NaN,
        double.Epsilon,                                            // the smallest subnormal
        BitConverter.UInt64BitsToDouble(0x000F_FFFF_FFFF_FFFFUL),  // the largest subnormal
        BitConverter.UInt64BitsToDouble(0x0010_0000_0000_0000UL),  // the smallest normal
    ];

    /// <summary>Inputs spanning 26 decades on both sides of zero, plus <see cref="SpecialInputs"/>.</summary>
    private static double[] Inputs()
    {
        const int count = 4096;
        var decade = new double[count];
        for (var i = 0; i < count; i++)
        {
            decade[i] = Math.Pow(10.0, -13.0 + 26.0 * i / (count - 1));
        }

        var special = SpecialInputs();
        var values = new double[count * 2 + special.Length];
        for (var i = 0; i < count; i++)
        {
            values[i] = decade[i];
            values[count + i] = -decade[i];
        }

        special.CopyTo(values, count * 2);
        return values;
    }

    /// <summary>
    /// Whether two probe values are the same: the same bits, or both NaN. A NaN's payload and sign are the hardware's, not the
    /// program's (the x86 unit hands on the payload of its operand, PTX arithmetic returns its own canonical NaN, and
    /// <c>Abs(NaN - 1)</c> shows the difference), and the tree never reads them: a NaN is a failed case, reported by its status.
    /// Everything that is not a NaN is compared by its bits.
    /// </summary>
    private static bool SameValue(double expected, double actual) =>
        Bits.Same(expected, actual) || (double.IsNaN(expected) && double.IsNaN(actual));

    /// <summary>The kernels stride constant matches the function list.</summary>
    [Fact]
    public void TheKernelsStrideConstantMatchesTheFunctionList() =>
        // Kernels.Probe strides by MathProbe.StrideCount, a const so it inlines into the kernel (Functions is a string array, and
        // kernel-compatible code allows no strings); this is what keeps that literal from drifting away from Functions silently
        // if a function is ever added to the root's math list (F-EX-07).
        Assert.Equal(MathProbe.StrideCount, MathProbe.FunctionCount);

    /// <summary>The cpu accelerator reproduces the host's own functions exactly: the tree's <c>KernelMath</c> and the IEEE operations of .NET.</summary>
    [Fact]
    public void TheCpuAcceleratorReproducesTheHostFunctionsExactly()
    {
        var inputs = Inputs();
        var outputs = EngineFixture.Shared.Cpu.ProbeMath(inputs);
        Assert.Equal(inputs.Length * MathProbe.FunctionCount, outputs.Length);
        for (var i = 0; i < inputs.Length; i++)
        {
            var v = inputs[i];
            var expected = new[]
            {
                KernelMath.Exp(v), KernelMath.Log(v),
                KernelMath.Pow(v, MathProbe.PowExponent1), KernelMath.Pow(v, MathProbe.PowExponent2), KernelMath.Pow(v, MathProbe.PowExponent3),
                Math.Sqrt(v), Math.Floor(v), Math.Ceiling(v), Math.Abs(v - 1.0),
                KernelMath.Min(v, 1.0), KernelMath.Max(v, 1.0), KernelMath.Min(1.0, v), KernelMath.Max(1.0, v),
                KernelMath.Fma(v, MathProbe.Factor, MathProbe.Addend), v * MathProbe.Factor + MathProbe.Addend,
            };
            for (var f = 0; f < MathProbe.FunctionCount; f++)
            {
                Assert.True(SameValue(expected[f], outputs[i * MathProbe.FunctionCount + f]),
                            $"{MathProbe.Functions[f]}({v:R}): host {expected[f]:R}, cpu accelerator {outputs[i * MathProbe.FunctionCount + f]:R}");
            }
        }
    }

    /// <summary>Cuda equals the cpu accelerator bit for bit for every function, on the whole input domain.</summary>
    [Fact]
    [Trait("Category", "Cuda")]
    public void CudaEqualsTheCpuAcceleratorBitForBitForEveryFunction()
    {
        var cuda = EngineFixture.Shared.RequireCuda();
        if (cuda is null)
        {
            return;
        }

        var inputs = Inputs();
        var cpu = EngineFixture.Shared.Cpu.ProbeMath(inputs);
        var gpu = cuda.ProbeMath(inputs);
        Assert.Equal(cpu.Length, gpu.Length);
        var mismatches = new List<string>();
        for (var i = 0; i < inputs.Length; i++)
        {
            for (var f = 0; f < MathProbe.FunctionCount; f++)
            {
                var a = cpu[i * MathProbe.FunctionCount + f];
                var b = gpu[i * MathProbe.FunctionCount + f];
                if (!SameValue(a, b))
                {
                    mismatches.Add($"{MathProbe.Functions[f]}({inputs[i]:R}): cpu {a:R}, cuda {b:R}");
                }
            }
        }

        Assert.True(mismatches.Count == 0, $"{mismatches.Count} of {cpu.Length} values differ:\n" + string.Join("\n", mismatches.Take(20)));
    }

    /// <summary>
    /// Records CUDA against the CPU accelerator for every special input on its own, one line per function (2026-09-27, F11):
    /// evidence for BOOT.md's criterion, read from this fact's own output, not typed by hand. Asserts the same bit equality as
    /// <see cref="CudaEqualsTheCpuAcceleratorBitForBitForEveryFunction"/>, which already covers these inputs as part of the
    /// whole domain.
    /// </summary>
    [Fact]
    [Trait("Category", "Cuda")]
    public void TheSpecialInputsAreRecordedAgainstCuda()
    {
        var cuda = EngineFixture.Shared.RequireCuda();
        if (cuda is null)
        {
            return;
        }

        var special = SpecialInputs();
        var cpu = EngineFixture.Shared.Cpu.ProbeMath(special);
        var gpu = cuda.ProbeMath(special);
        for (var i = 0; i < special.Length; i++)
        {
            for (var f = 0; f < MathProbe.FunctionCount; f++)
            {
                var a = cpu[i * MathProbe.FunctionCount + f];
                var b = gpu[i * MathProbe.FunctionCount + f];
                output.WriteLine($"{MathProbe.Functions[f]}({special[i]:R}): cpu {a:R}, cuda {b:R}");
                Assert.True(SameValue(a, b), $"{MathProbe.Functions[f]}({special[i]:R}): cpu {a:R}, cuda {b:R}");
            }
        }
    }
}
