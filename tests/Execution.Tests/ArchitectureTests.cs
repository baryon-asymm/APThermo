using System.Reflection;
using System.Text.RegularExpressions;
using ILGPU;
using ILGPU.Backends.EntryPoints;
using ILGPU.Backends.PTX;
using ILGPU.Runtime;
using ILGPU.Runtime.Cuda;

namespace APThermo.Execution.Tests;

/// <summary>
/// L1: every architecture ILGPU 1.5.3 declares from SM_75 up passes the post-link and loads on the reference device
/// (BOOT.md, the criterion of 2026-09-26, audit finding F1). The architectures and the entry points both come from
/// reflection, never typed out by hand, so a future ILGPU release or a new kernel is covered automatically.
/// </summary>
[Collection(EngineFixture.CollectionName)]
public sealed partial class ArchitectureTests
{
    [GeneratedRegex(@"_[0-9]{3,}\b")]
    private static partial Regex GeneratedSuffix();

    /// <summary>
    /// The PTX with comment lines, blank lines and the <c>.target</c> line set aside, and ILGPU's generated numeric suffixes
    /// (register and label names) folded to one placeholder, so that two architectures' otherwise identical kernels compare
    /// equal (the spike behind BOOT.md's design measured this normalization against libnvvm 12.9, 13.3 and 13.4).
    /// </summary>
    private static string Normalize(string ptx) => string.Join('\n', ptx.Split('\n')
        .Select(line => line.TrimEnd('\r'))
        .Where(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal))
        .Where(line => line.Trim().Length > 0)
        .Where(line => !line.TrimStart().StartsWith(".target", StringComparison.Ordinal))
        .Select(line => GeneratedSuffix().Replace(line, "_N")));

    /// <summary>
    /// For every architecture and every entry point: the post-link succeeds and the kernel loads on the reference device; both
    /// paths of the post-link occur across the range; the normalized PTX equals the device's own; the probe matches the
    /// engine's own CUDA probe bit for bit and stays within the GPU/CPU tolerance of the CPU accelerator.
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
        var (dll, bitcode, _) = LibDeviceLocator.Locate(new EngineOptions());
        var methods = EntryPoints();
        var baseline = Baseline(accelerator, dll!, bitcode!, methods);

        var inputs = ProbeInputs();
        var cpuProbe = EngineFixture.Shared.Cpu.ProbeMath(inputs);
        var deviceProbe = cuda.ProbeMath(inputs);

        var sawIlgpuComplete = false;
        var sawPostLinkComplete = false;
        foreach (var arch in ArchitecturesFromSm75Up())
        {
            foreach (var method in methods)
            {
                var (ptx, link) = CompileAndLink(accelerator, dll!, bitcode!, arch, method);
                Assert.Equal(baseline[method.Name], Normalize(ptx));
                sawIlgpuComplete |= link.DefinedByIlgpu.Count > 0 && link.Compiled.Count == 0;
                sawPostLinkComplete |= link.Compiled.Count > 0 && link.DefinedByIlgpu.Count == 0;
                if (method.Name == nameof(Kernels.Probe))
                {
                    AssertProbeMatches(accelerator, link.Kernel, inputs, deviceProbe, cpuProbe, arch);
                }
                else
                {
                    accelerator.LoadAutoGroupedKernel(link.Kernel).Dispose();
                }
            }
        }

        Assert.True(sawIlgpuComplete, "no architecture from SM_75 up had ILGPU define every wrapper the kernels call");
        Assert.True(sawPostLinkComplete, "no architecture from SM_75 up needed the post-link to compile every wrapper");
    }

    /// <summary>The normalized, post-linked PTX of every entry point, compiled for the device's own architecture: the baseline every other architecture's PTX is compared against.</summary>
    private static Dictionary<string, string> Baseline(CudaAccelerator accelerator, string dll, string bitcode, IReadOnlyList<MethodInfo> methods)
    {
        var baseline = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var method in methods)
        {
            var (ptx, _) = CompileAndLink(accelerator, dll, bitcode, accelerator.Architecture, method);
            baseline[method.Name] = Normalize(ptx);
        }

        return baseline;
    }

    /// <summary>
    /// Compiles one entry point for one architecture with a backend of its own, and passes it through the post-link. Creates
    /// its own <see cref="NvvmAPI"/> rather than sharing one across backends (2026-09-28, the second audit's observation 4):
    /// <c>PTXBackend.Dispose</c> frees the <c>NvvmAPI</c> it was given, so a shared instance is freed once per backend, which
    /// only appeared to work in this fixture because the CUDA engine created earlier keeps the same libnvvm library loaded; a
    /// libnvvm held by nothing else crashed in <c>NvvmAPI.GetIRVersion</c> once the audit tried it.
    /// </summary>
    private static (string Ptx, LibDevicePostLink.LinkResult Link) CompileAndLink(
        CudaAccelerator accelerator, string dll, string bitcode, CudaArchitecture arch, MethodInfo method)
    {
        using var nvvm = NvvmAPI.Create(dll, bitcode);
        using var backend = new PTXBackend(accelerator.Context, arch, accelerator.InstructionSet, nvvm);
        var compiled = (PTXCompiledKernel)backend.Compile(EntryPointDescription.FromImplicitlyGroupedKernel(method), KernelSpecialization.Empty);
        var linked = LibDevicePostLink.Link(accelerator, nvvm, compiled);
        return (linked.Kernel.PTXAssembly, linked);
    }

    /// <summary>Loads and launches the probe kernel of one architecture, and compares it with the engine's own CUDA probe and the CPU accelerator.</summary>
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
            Assert.True(BitConverter.DoubleToInt64Bits(result[i]) == BitConverter.DoubleToInt64Bits(deviceProbe[i]),
                        $"{arch} probe[{i}]: {result[i]:R} against the engine's own {deviceProbe[i]:R}");
            Assert.True(GpuCpuTolerances.UlpDistance(cpuProbe[i], result[i]) <= GpuCpuTolerances.MathUlp,
                        $"{arch} probe[{i}] against the CPU accelerator: {result[i]:R} vs {cpuProbe[i]:R}");
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

    /// <summary>Inputs spanning 26 decades, as the probe's own tolerance is measured over (fewer than <c>ProbeKernelTests</c>, since this fact repeats the launch across every architecture).</summary>
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
