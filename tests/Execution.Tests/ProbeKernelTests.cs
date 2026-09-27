using APThermo.Harness;
using Xunit.Abstractions;

namespace APThermo.Execution.Tests;

/// <summary>L1: the probe kernel of the root's math list loads on CUDA through the post-link and matches the CPU accelerator within the ULP bound.</summary>
[Collection(EngineFixture.CollectionName)]
public sealed class ProbeKernelTests(ITestOutputHelper output)
{
    /// <summary>
    /// The solver's whole input domain, beyond the decade span (2026-09-27, the guards audit's F11): until now the probe's
    /// inputs were positive only, plus a handful of values around 1, so <c>exp</c>'s mostly-negative arguments, <c>Floor</c>
    /// and <c>Ceiling</c>'s negative inputs, and <c>Min</c>/<c>Max</c>'s NaN case were never probed. NaN and the signed zeros
    /// and infinities exercise <see cref="GpuCpuTolerances.UlpDistance"/>'s own special-value handling.
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

    /// <summary>The kernels stride constant matches the function list.</summary>
    [Fact]
    public void TheKernelsStrideConstantMatchesTheFunctionList() =>
        // Kernels.Probe strides by MathProbe.StrideCount, a const so it inlines into the kernel (Functions is a string array, and
        // kernel-compatible code allows no strings); this is what keeps that literal from drifting away from Functions silently
        // if a function is ever added to the root's math list (F-EX-07).
        Assert.Equal(MathProbe.StrideCount, MathProbe.FunctionCount);

    /// <summary>The cpu accelerator reproduces dotnet math exactly.</summary>
    [Fact]
    public void TheCpuAcceleratorReproducesDotnetMathExactly()
    {
        var inputs = Inputs();
        var outputs = EngineFixture.Shared.Cpu.ProbeMath(inputs);
        Assert.Equal(inputs.Length * MathProbe.FunctionCount, outputs.Length);
        for (var i = 0; i < inputs.Length; i++)
        {
            var v = inputs[i];
            var expected = new[]
            {
                Math.Exp(v), Math.Log(v), Math.Log10(v),
                Math.Pow(v, MathProbe.PowExponent1), Math.Pow(v, MathProbe.PowExponent2), Math.Pow(v, MathProbe.PowExponent3),
                Math.Sqrt(v), Math.Abs(v - 1.0), Math.Min(v, 1.0), Math.Max(v, 1.0), Math.Floor(v), Math.Ceiling(v),
            };
            for (var f = 0; f < MathProbe.FunctionCount; f++)
            {
                Assert.True(Bits.Same(expected[f], outputs[i * MathProbe.FunctionCount + f]),
                            $"{MathProbe.Functions[f]}({v:R}): host {expected[f]:R}, cpu accelerator {outputs[i * MathProbe.FunctionCount + f]:R}");
            }
        }
    }

    /// <summary>Cuda matches the cpu accelerator within the ulp bound for every function.</summary>
    [Fact]
    [Trait("Category", "Cuda")]
    public void CudaMatchesTheCpuAcceleratorWithinTheUlpBoundForEveryFunction()
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
        var worst = new long[MathProbe.FunctionCount];
        var mismatches = new List<string>();
        for (var i = 0; i < inputs.Length; i++)
        {
            for (var f = 0; f < MathProbe.FunctionCount; f++)
            {
                var a = cpu[i * MathProbe.FunctionCount + f];
                var b = gpu[i * MathProbe.FunctionCount + f];
                var ulp = GpuCpuTolerances.UlpDistance(a, b);
                worst[f] = Math.Max(worst[f], ulp);
                if (ulp > GpuCpuTolerances.MathUlp)
                {
                    mismatches.Add($"{MathProbe.Functions[f]}({inputs[i]:R}): cpu {a:R}, cuda {b:R}, {ulp} ULP");
                }
            }
        }

        Assert.True(mismatches.Count == 0, string.Join("\n", mismatches.Take(20)));
        var report = string.Join(", ", MathProbe.Functions.Select((name, f) => $"{name} {worst[f]}"));
        Assert.True(worst.Max() <= GpuCpuTolerances.MathUlp, report);
        for (var f = MathProbe.LibdeviceFunctionCount; f < MathProbe.FunctionCount; f++)
        {
            Assert.True(worst[f] == 0, $"{MathProbe.Functions[f]} is a PTX instruction, not a libdevice call, and must be exact: {worst[f]} ULP");
        }
    }

    /// <summary>
    /// Records CUDA against the CPU accelerator for every special input on its own, one line per function (2026-09-27, F11):
    /// evidence for BOOT.md's criterion, read from this fact's own output, not typed by hand. Asserts the same ULP bound as
    /// <see cref="CudaMatchesTheCpuAcceleratorWithinTheUlpBoundForEveryFunction"/>, which already covers these inputs as part
    /// of the whole domain; a divergence here is a finding for the owner (BOOT.md's Constraints), not a reason to loosen it.
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
                var ulp = GpuCpuTolerances.UlpDistance(a, b);
                output.WriteLine($"{MathProbe.Functions[f]}({special[i]:R}): cpu {a:R}, cuda {b:R}, {ulp} ULP");
                Assert.True(ulp <= GpuCpuTolerances.MathUlp, $"{MathProbe.Functions[f]}({special[i]:R}): cpu {a:R}, cuda {b:R}, {ulp} ULP");
            }
        }
    }
}
