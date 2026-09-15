using APThermo.Harness;

namespace APThermo.Execution.Tests;

/// <summary>L1: the probe kernel of the root's math list loads on CUDA through the post-link and matches the CPU accelerator within the ULP bound.</summary>
[Collection(EngineCollection.Name)]
public sealed class ProbeKernelTests(EngineFixture fixture)
{
    /// <summary>Inputs spanning 26 decades, plus the values around one where Floor and Ceiling differ.</summary>
    private static double[] Inputs()
    {
        const int count = 4096;
        var values = new double[count + 6];
        for (var i = 0; i < count; i++)
        {
            values[i] = Math.Pow(10.0, -13.0 + 26.0 * i / (count - 1));
        }

        values[count] = 1.0;
        values[count + 1] = 0.5;
        values[count + 2] = 1.5;
        values[count + 3] = 2.0;
        values[count + 4] = 2.5;
        values[count + 5] = 1e-300;
        return values;
    }

    [Fact]
    public void The_kernels_stride_constant_matches_the_function_list()
    {
        // Kernels.Probe strides by MathProbe.StrideCount, a const so it inlines into the kernel (Functions is a string array, and
        // kernel-compatible code allows no strings); this is what keeps that literal from drifting away from Functions silently
        // if a function is ever added to the root's math list (F-EX-07).
        Assert.Equal(MathProbe.StrideCount, MathProbe.FunctionCount);
    }

    [Fact]
    public void The_cpu_accelerator_reproduces_dotnet_math_exactly()
    {
        var inputs = Inputs();
        var outputs = fixture.Cpu.ProbeMath(inputs);
        Assert.Equal(inputs.Length * MathProbe.FunctionCount, outputs.Length);
        for (var i = 0; i < inputs.Length; i++)
        {
            var v = inputs[i];
            var expected = new[]
            {
                Math.Exp(v), Math.Log(v), Math.Log10(v), Math.Pow(v, MathProbe.PowExponent), Math.Sqrt(v),
                Math.Abs(v - 1.0), Math.Min(v, 1.0), Math.Max(v, 1.0), Math.Floor(v), Math.Ceiling(v),
            };
            for (var f = 0; f < MathProbe.FunctionCount; f++)
            {
                Assert.True(Bits.Same(expected[f], outputs[i * MathProbe.FunctionCount + f]),
                            $"{MathProbe.Functions[f]}({v:R}): host {expected[f]:R}, cpu accelerator {outputs[i * MathProbe.FunctionCount + f]:R}");
            }
        }
    }

    [Fact]
    [Trait("Category", "Cuda")]
    public void Cuda_matches_the_cpu_accelerator_within_the_ulp_bound_for_every_function()
    {
        var cuda = fixture.RequireCuda();
        if (cuda is null)
        {
            return;
        }

        var inputs = Inputs();
        var cpu = fixture.Cpu.ProbeMath(inputs);
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
        for (var f = 5; f < MathProbe.FunctionCount; f++)
        {
            Assert.True(worst[f] == 0, $"{MathProbe.Functions[f]} is a PTX instruction, not a libdevice call, and must be exact: {worst[f]} ULP");
        }
    }
}
