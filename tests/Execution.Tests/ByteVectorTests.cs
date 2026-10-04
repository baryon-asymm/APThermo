using System.Reflection;
using System.Text.RegularExpressions;
using APThermo.Execution.LibDevice;
using ILGPU;
using ILGPU.Backends.EntryPoints;
using ILGPU.Backends.PTX;
using ILGPU.Runtime;
using ILGPU.Runtime.Cuda;

namespace APThermo.Execution.Tests;

/// <summary>
/// L1: the PTX ILGPU 1.5.3 compiles for every entry point holds no vector load or store of bytes or predicates (Recovery
/// BOOT.md, "No whole-struct copies"). Two adjacent <c>bool</c> fields of a struct that the compiler carries as one value are loaded as
/// <c>ld.local.v2.b8 {%p, %p}</c>, which ptxas refuses with "Arguments mismatch for instruction 'ld'", so that no kernel loads on
/// CUDA; the CPU accelerator never sees it. The entry points come from reflection, and the check fails when it finds none.
/// </summary>
[Collection(EngineFixture.CollectionName)]
public sealed partial class ByteVectorTests
{
    [GeneratedRegex(@"^\s*(ld|st)(\.[a-z]+)*\.v[0-9]\.(b8|pred)\b", RegexOptions.Multiline)]
    private static partial Regex ByteVector();

    /// <summary>No entry point's PTX loads or stores a vector of bytes or predicates.</summary>
    [Fact]
    [Trait("Category", "Cuda")]
    public void NoEntryPointLoadsOrStoresAVectorOfBytesOrPredicates()
    {
        var cuda = EngineFixture.Shared.RequireCuda();
        if (cuda is null)
        {
            return;
        }

        var accelerator = (CudaAccelerator)cuda.IlgpuAccelerator;
        var (dll, bitcode, _) = LibDeviceLocator.Locate(new EngineOptions());
        var methods = typeof(Kernels).GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
            .Where(m => m.GetParameters().Length > 0 && m.GetParameters()[0].ParameterType == typeof(Index1D))
            .ToList();
        Assert.NotEmpty(methods);
        var offenders = new List<string>();
        foreach (var method in methods)
        {
            using var nvvm = NvvmAPI.Create(dll!, bitcode!);
            using var backend = new PTXBackend(accelerator.Context, accelerator.Architecture, accelerator.InstructionSet, nvvm);
            var compiled = (PTXCompiledKernel)backend.Compile(EntryPointDescription.FromImplicitlyGroupedKernel(method), KernelSpecialization.Empty);
            Assert.NotEmpty(compiled.PTXAssembly);
            var found = ByteVector().Count(compiled.PTXAssembly);
            if (found > 0)
            {
                offenders.Add($"{method.Name}: {found}");
            }
        }

        Assert.True(offenders.Count == 0, $"vector loads or stores of bytes or predicates: {string.Join(", ", offenders)}");
    }
}
