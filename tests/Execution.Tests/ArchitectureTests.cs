using System.Reflection;
using System.Text.RegularExpressions;
using APThermo.Execution.Ptx;
using APThermo.Harness;
using ILGPU;
using ILGPU.Backends.EntryPoints;
using ILGPU.Backends.PTX;
using ILGPU.Runtime;
using ILGPU.Runtime.Cuda;

namespace APThermo.Execution.Tests;

/// <summary>
/// L1: every architecture ILGPU 1.5.3 declares from SM_75 up passes the post-link and loads on the reference device
/// (BOOT.md, the criterion of 2026-09-26, audit finding F1). The architectures and the entry points both come from
/// reflection, never typed out by hand, so a future ILGPU release or a new kernel is covered automatically. No library of the
/// CUDA Toolkit is involved (2026-10-05): the post-link is a text rewrite of the PTX ILGPU emits for the target.
/// </summary>
[Collection(EngineFixture.CollectionName)]
public sealed partial class ArchitectureTests
{
    [GeneratedRegex(@"_[0-9]{3,}\b")]
    private static partial Regex GeneratedSuffix();

    /// <summary>
    /// The PTX with comment lines, blank lines and the <c>.target</c> line set aside, and ILGPU's generated numeric suffixes
    /// (register and label names) folded to one placeholder, so that two architectures' otherwise identical kernels compare
    /// equal.
    /// </summary>
    private static string Normalize(string ptx) => string.Join('\n', ptx.Split('\n')
        .Select(line => line.TrimEnd('\r'))
        .Where(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal))
        .Where(line => line.Trim().Length > 0)
        .Where(line => !line.TrimStart().StartsWith(".target", StringComparison.Ordinal))
        .Select(line => GeneratedSuffix().Replace(line, "_N")));

    /// <summary>
    /// For every architecture and every entry point: the post-link succeeds and the kernel loads on the reference device; the
    /// post-link marked arithmetic <c>.rn</c> on every architecture and inlined the probe's fused multiply-add; the normalized
    /// PTX equals the device's own; the probe matches the engine's own CUDA probe and the CPU accelerator bit for bit.
    /// </summary>
    [Fact]
    [Trait("Category", "Cuda")]
    [Trait("Category", "LongRunning")]
    public void EveryArchitectureFromSm75UpPassesThePostLinkAndMatchesTheDevice()
    {
        var cuda = EngineFixture.Shared.RequireCuda();
        if (cuda is null)
        {
            return;
        }

        var accelerator = (CudaAccelerator)cuda.IlgpuAccelerator;
        var methods = EntryPoints();
        var baseline = Baseline(accelerator, methods);

        var inputs = ProbeInputs();
        var cpuProbe = EngineFixture.Shared.Cpu.ProbeMath(inputs);
        var deviceProbe = cuda.ProbeMath(inputs);

        var architectures = ArchitecturesFromSm75Up();
        Assert.NotEmpty(architectures);
        foreach (var arch in architectures)
        {
            foreach (var method in methods)
            {
                var link = CompileAndLink(accelerator, arch, method);
                Assert.Equal(baseline[method.Name], Normalize(link.Kernel.PTXAssembly));
                Assert.True(link.RoundedOperations > 0, $"{arch} {method.Name}: no multiplication, addition or subtraction was marked .rn");
                if (method.Name == nameof(Kernels.Probe))
                {
                    Assert.True(link.FusedSites > 0, $"{arch}: the probe's fused multiply-add was not inlined");
                    AssertProbeMatches(accelerator, link.Kernel, inputs, deviceProbe, cpuProbe, arch);
                }
                else
                {
                    accelerator.LoadAutoGroupedKernel(link.Kernel).Dispose();
                }
            }
        }
    }

    /// <summary>The normalized, post-linked PTX of every entry point, compiled for the device's own architecture: the baseline every other architecture's PTX is compared against.</summary>
    private static Dictionary<string, string> Baseline(CudaAccelerator accelerator, IReadOnlyList<MethodInfo> methods)
    {
        var baseline = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var method in methods)
        {
            baseline[method.Name] = Normalize(CompileAndLink(accelerator, accelerator.Architecture, method).Kernel.PTXAssembly);
        }

        return baseline;
    }

    /// <summary>Compiles one entry point for one architecture with a backend of its own, and passes it through the post-link.</summary>
    private static PtxPostLink.LinkResult CompileAndLink(CudaAccelerator accelerator, CudaArchitecture arch, MethodInfo method)
    {
        using var backend = new PTXBackend(accelerator.Context, arch, accelerator.InstructionSet, null!);
        var compiled = (PTXCompiledKernel)backend.Compile(EntryPointDescription.FromImplicitlyGroupedKernel(method), KernelSpecialization.Empty);
        return PtxPostLink.Link(accelerator, compiled);
    }

    /// <summary>Loads and launches the probe kernel of one architecture, and compares it with the engine's own CUDA probe and the CPU accelerator, bit for bit.</summary>
    private static void AssertProbeMatches(
        CudaAccelerator accelerator, PTXCompiledKernel compiled, double[] inputs, double[] deviceProbe, double[] cpuProbe, CudaArchitecture arch)
    {
        using var kernel = accelerator.LoadAutoGroupedKernel(compiled);
        var launch = kernel.CreateLauncherDelegate<Action<AcceleratorStream, Index1D, ArrayView<double>, ArrayView<double>>>();
        using var inputBuffer = accelerator.Allocate1D(inputs);
        using var outputBuffer = accelerator.Allocate1D<double>((long)inputs.Length * MathProbe.FunctionCount);
        launch(accelerator.DefaultStream, inputs.Length, inputBuffer.View, outputBuffer.View);
        accelerator.Synchronize();
        var result = outputBuffer.GetAsArray1D();
        for (var i = 0; i < result.Length; i++)
        {
            Assert.True(Bits.Same(result[i], deviceProbe[i]), $"{arch} probe[{i}]: {result[i]:R} against the engine's own {deviceProbe[i]:R}");
            Assert.True(Bits.Same(result[i], cpuProbe[i]), $"{arch} probe[{i}] against the CPU accelerator: {result[i]:R} vs {cpuProbe[i]:R}");
        }
    }

    /// <summary>Every <see cref="CudaArchitecture"/> ILGPU 1.5.3 declares from SM_75 up, by reflection.</summary>
    private static IReadOnlyList<CudaArchitecture> ArchitecturesFromSm75Up() =>
        [.. typeof(CudaArchitecture).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.FieldType == typeof(CudaArchitecture))
            .Select(f => (CudaArchitecture)f.GetValue(null)!)
            .Where(a => a >= CudaArchitecture.SM_75)
            .OrderBy(a => a)];

    /// <summary>Every entry point of <see cref="Kernels"/>, by reflection: a static method whose first parameter is an <see cref="Index1D"/>.</summary>
    private static IReadOnlyList<MethodInfo> EntryPoints() =>
        [.. typeof(Kernels).GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
            .Where(m => m.GetParameters() is [{ ParameterType.Name: nameof(Index1D) }, ..])];

    /// <summary>Inputs spanning 26 decades (fewer than <c>ProbeKernelTests</c>, since this fact repeats the launch across every architecture).</summary>
    private static double[] ProbeInputs()
    {
        const int count = 256;
        var values = new double[count];
        for (var i = 0; i < count; i++)
        {
            values[i] = Math.Pow(10.0, -13.0 + 26.0 * i / (count - 1));
        }

        return values;
    }
}
