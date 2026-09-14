using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using ILGPU;
using ILGPU.Backends.PTX;
using ILGPU.Runtime.Cuda;

namespace AerospacePropellantThermodynamics.Execution;

/// <summary>
/// Links ILGPU's libdevice wrappers into a compiled CUDA kernel, the one place in the tree that knows ILGPU's internals: ILGPU
/// 1.5.3 emits the NVVM version metadata before the target lines, libnvvm rejects the module and the wrappers silently go missing.
/// <see cref="Link"/> reads as the sequence of its stages: find the target, compile the wrapper body, check every call has a
/// definition, splice it after the kernel's header, trial-load the result.
/// </summary>
internal static class LibDevicePostLink
{
    /// <summary>The ILGPU version whose internals this post-link was written against.</summary>
    public const string ExpectedIlgpuVersion = "1.5.3.0";

    private const string FragmentsType = "ILGPU.Backends.PTX.PTXLibDeviceNvvm";
    private const string FragmentsField = "fragments";
    private const string AssemblyField = "<PTXAssembly>k__BackingField";
    private const string TargetTriple = "target triple = \"nvptx64-unknown-cuda\"";
    private const string TargetDataLayout =
        "target datalayout = \"e-p:64:64:64-i1:8:8-i8:8:8-i16:16:16-i32:32:32-i64:64:64-f32:32:32-f64:64:64-v16:16:16-v32:32:32-v64:64:64-v128:128:128-n16:32:64\"";

    private static readonly Regex WrapperCall = new(@"__ilgpu__nv_[A-Za-z0-9_]+", RegexOptions.Compiled);

    /// <summary>A wrapper's own <c>.func</c> definition line, over the wrapper body only — never the kernel PTX that calls it.</summary>
    private static readonly Regex WrapperDefinition =
        new(@"^\s*\.(visible|weak)?\s*\.func\b[^;]*?(__ilgpu__nv_[A-Za-z0-9_]+)\s*\(", RegexOptions.Compiled | RegexOptions.Multiline);

    private static readonly Regex Target = new(@"^\.target\s+sm_(\d+)", RegexOptions.Compiled | RegexOptions.Multiline);
    private static readonly Lazy<(FieldInfo Fragments, FieldInfo Assembly)> Members = new(() => AssertIlgpu(ExpectedIlgpuVersion));

    /// <summary>The ILGPU version string of the loaded assembly.</summary>
    public static string IlgpuVersion => typeof(Context).Assembly.GetName().Version?.ToString() ?? "unknown";

    /// <summary>Asserts the ILGPU version and the reflected members once; an exception names the version.</summary>
    public static void AssertIlgpu() => _ = Members.Value;

    /// <summary>The assertion against a given version, for the tests to prove it fails loudly.</summary>
    internal static (FieldInfo Fragments, FieldInfo Assembly) AssertIlgpu(string expectedVersion)
    {
        var version = IlgpuVersion;
        if (version != expectedVersion)
        {
            throw new InvalidOperationException(
                $"ILGPU {version} is loaded, but the libdevice post-link was written for ILGPU {expectedVersion}; its internals must be re-verified.");
        }

        var assembly = typeof(PTXBackend).Assembly;
        var fragments = assembly.GetType(FragmentsType)?.GetField(FragmentsField, BindingFlags.NonPublic | BindingFlags.Static);
        if (fragments is null || fragments.FieldType != typeof(Dictionary<string, string>))
        {
            throw new InvalidOperationException($"ILGPU {version}: {FragmentsType}.{FragmentsField} is not the dictionary of wrapper fragments the post-link expects.");
        }

        var backing = typeof(PTXCompiledKernel).GetField(AssemblyField, BindingFlags.NonPublic | BindingFlags.Instance);
        if (backing is null || backing.FieldType != typeof(string))
        {
            throw new InvalidOperationException($"ILGPU {version}: {nameof(PTXCompiledKernel)}.{AssemblyField} is not the string field the post-link expects.");
        }

        return (fragments, backing);
    }

    /// <summary>The wrapper names a PTX text calls, without the <c>__ilgpu</c> prefix (as the fragment keys are), in order of appearance.</summary>
    public static IReadOnlyList<string> WrappersCalled(string ptx) =>
        WrapperCall.Matches(ptx).Select(m => m.Value["__ilgpu".Length..]).Distinct().ToList();

    /// <summary>Replaces the kernel's PTX by the PTX with the wrappers it calls defined, compiled by libnvvm for the kernel's target.</summary>
    public static PTXCompiledKernel Link(CudaAccelerator accelerator, NvvmAPI nvvm, PTXCompiledKernel compiled)
    {
        var ptx = compiled.PTXAssembly;
        var names = WrappersCalled(ptx);
        if (names.Count == 0)
        {
            return compiled;
        }

        var arch = TargetArch(ptx);
        var body = WrapperBody(nvvm, names, arch);
        AssertEveryWrapperDefined(body, names);
        var linked = InsertAfterHeader(ptx, body);

        // The driver checks the PTX against a context bound to the calling thread; bind the accelerator's before the trial load.
        accelerator.Bind();
        TrialLoad(linked);
        Members.Value.Assembly.SetValue(compiled, linked);
        return compiled;
    }

    /// <summary>The kernel's own target, from its <c>.target sm_XX</c> line: ILGPU's choice per device, not a fixed value.</summary>
    private static string TargetArch(string ptx)
    {
        var match = Target.Match(ptx);
        if (!match.Success)
        {
            throw new InvalidOperationException("the kernel PTX has no .target line.");
        }

        return "compute_" + match.Groups[1].Value;
    }

    /// <summary>The wrapper PTX libnvvm compiles from ILGPU's own fragments, its module-header lines stripped (the kernel supplies its own).</summary>
    private static string WrapperBody(NvvmAPI nvvm, IReadOnlyList<string> names, string arch)
    {
        var fragments = (Dictionary<string, string>)Members.Value.Fragments.GetValue(null)!;
        foreach (var name in names)
        {
            if (!fragments.ContainsKey(name))
            {
                throw new InvalidOperationException($"the kernel calls the libdevice wrapper {name}, for which ILGPU {IlgpuVersion} has no fragment.");
            }
        }

        nvvm.GetIRVersion(out var irMajor, out _, out _, out _);
        var module = new StringBuilder();
        module.Append(TargetTriple).Append('\n').Append(TargetDataLayout).Append('\n');
        module.Append("!nvvmir.version = !{!0}\n!0 = !{i32 ").Append(irMajor).Append(", i32 0}\n");
        foreach (var name in names)
        {
            module.Append(fragments[name]).Append('\n');
        }

        var wrapperPtx = CompileWrappers(nvvm, module.ToString(), arch);
        return string.Join("\n", wrapperPtx.Split('\n')
            .Where(line => !(line.StartsWith(".version", StringComparison.Ordinal) || line.StartsWith(".target", StringComparison.Ordinal)
                             || line.StartsWith(".address_size", StringComparison.Ordinal))));
    }

    private static string CompileWrappers(NvvmAPI nvvm, string module, string arch)
    {
        var moduleBytes = Encoding.ASCII.GetBytes(module);
        var libdevice = nvvm.LibDeviceBytes.ToArray();
        nvvm.CreateProgram(out var program);
        try
        {
            using var options = new NvvmOptions(arch);
            CompileAgainstLibdevice(nvvm, program, moduleBytes, libdevice, arch, options);
            nvvm.GetCompiledResult(program, out var wrapperPtx);
            return wrapperPtx ?? throw new InvalidOperationException("libnvvm returned no PTX for the libdevice wrappers.");
        }
        finally
        {
            nvvm.DestroyProgram(ref program);
        }
    }

    private static void CompileAgainstLibdevice(NvvmAPI nvvm, IntPtr program, byte[] moduleBytes, byte[] libdevice, string arch, NvvmOptions options)
    {
        unsafe
        {
            fixed (byte* modulePointer = moduleBytes)
            fixed (byte* libdevicePointer = libdevice)
            {
                nvvm.AddModuleToProgram(program, (IntPtr)modulePointer, (IntPtr)moduleBytes.Length, "apthermo-wrappers");
                nvvm.LazyAddModuleToProgram(program, (IntPtr)libdevicePointer, (IntPtr)libdevice.Length, "libdevice");
                var result = nvvm.CompileProgram(program, options.Count, options.Pointer);
                nvvm.GetProgramLog(program, out var log);
                if (result != NvvmResult.NVVM_SUCCESS)
                {
                    throw new InvalidOperationException($"libnvvm could not compile the libdevice wrappers for {arch} ({result}): {(log ?? "").Trim()}");
                }
            }
        }
    }

    /// <summary>The wrapper body spliced right after the kernel's module header: the callee must precede the call site.</summary>
    private static string InsertAfterHeader(string ptx, string body)
    {
        var headerEnd = ptx.IndexOf(".address_size", StringComparison.Ordinal);
        if (headerEnd < 0)
        {
            throw new InvalidOperationException("the kernel PTX has no .address_size line.");
        }

        headerEnd = ptx.IndexOf('\n', headerEnd) + 1;
        return string.Concat(ptx.AsSpan(0, headerEnd), body, "\n", ptx.AsSpan(headerEnd));
    }

    /// <summary>
    /// Every wrapper the kernel calls has a <c>.func</c> definition in the body libnvvm produced (not the kernel PTX, which also
    /// contains a call to the same name and would make a substring search pass regardless of whether the definition exists). A set
    /// comparison so the message names exactly the wrappers that are missing, not merely the first name in the call list.
    /// </summary>
    internal static void AssertEveryWrapperDefined(string body, IReadOnlyList<string> names)
    {
        var defined = WrapperDefinition.Matches(body).Select(m => m.Groups[2].Value["__ilgpu".Length..]).ToHashSet(StringComparer.Ordinal);
        var missing = names.Except(defined).ToList();
        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                $"the post-link produced no definition of the libdevice wrapper{(missing.Count > 1 ? "s" : "")} {string.Join(", ", missing)}.");
        }
    }

    private static void TrialLoad(string linked)
    {
        var error = CudaAPI.CurrentAPI.LoadModule(out var handle, linked, out var log);
        if (error != CudaError.CUDA_SUCCESS)
        {
            throw new InvalidOperationException($"the linked PTX was refused by the CUDA driver ({error}): {(log ?? "").Trim()}");
        }

        CudaAPI.CurrentAPI.DestroyModule(handle);
    }

    /// <summary>The one-element libnvvm compiler-options array (<c>-arch=...</c>), owning its two unmanaged allocations.</summary>
    private readonly struct NvvmOptions : IDisposable
    {
        private readonly IntPtr _option;

        public NvvmOptions(string arch)
        {
            _option = Marshal.StringToHGlobalAnsi("-arch=" + arch);
            Pointer = Marshal.AllocHGlobal(IntPtr.Size);
            Marshal.WriteIntPtr(Pointer, _option);
        }

        public IntPtr Pointer { get; }

        public int Count => 1;

        public void Dispose()
        {
            Marshal.FreeHGlobal(Pointer);
            Marshal.FreeHGlobal(_option);
        }
    }
}
