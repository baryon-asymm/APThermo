using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using ILGPU;
using ILGPU.Backends.PTX;
using ILGPU.Runtime.Cuda;

namespace AerospacePropellantThermodynamics.Execution;

/// <summary>Finds libnvvm and libdevice in the order BOOT.md fixes.</summary>
internal static class LibDeviceLocator
{
    private const string DllName = "nvvm64_40_0.dll";
    private const string BitcodeName = "libdevice.10.bc";

    /// <summary>The dll and bitcode paths, or nulls, with every path examined.</summary>
    public static (string? Dll, string? Bitcode, IReadOnlyList<string> Tried) Locate(EngineOptions options)
    {
        var tried = new List<string>();
        if (options.LibNvvmPath is not null || options.LibDevicePath is not null)
        {
            var dll = options.LibNvvmPath ?? "";
            var bitcode = options.LibDevicePath ?? "";
            tried.Add(dll);
            tried.Add(bitcode);
            if (File.Exists(dll) && File.Exists(bitcode))
            {
                return (dll, bitcode, tried);
            }
        }

        if (!options.LibDeviceDiscovery)
        {
            return (null, null, tried);
        }

        foreach (var root in ToolkitRoots())
        {
            var bitcode = Path.Combine(root, "nvvm", "libdevice", BitcodeName);
            foreach (var dll in new[] { Path.Combine(root, "nvvm", "bin", DllName), Path.Combine(root, "nvvm", "bin", "x64", DllName) })
            {
                tried.Add(dll);
                if (File.Exists(dll))
                {
                    tried.Add(bitcode);
                    if (File.Exists(bitcode))
                    {
                        return (dll, bitcode, tried);
                    }
                }
            }
        }

        return (null, null, tried);
    }

    /// <summary>CUDA_PATH, then the toolkit directories under Program Files from the newest version down.</summary>
    private static IEnumerable<string> ToolkitRoots()
    {
        var cudaPath = Environment.GetEnvironmentVariable("CUDA_PATH");
        if (!string.IsNullOrWhiteSpace(cudaPath))
        {
            yield return cudaPath;
        }

        var programFiles = Environment.GetEnvironmentVariable("ProgramFiles") ?? @"C:\Program Files";
        var toolkits = Path.Combine(programFiles, "NVIDIA GPU Computing Toolkit", "CUDA");
        if (!Directory.Exists(toolkits))
        {
            yield break;
        }

        var versions = Directory.GetDirectories(toolkits, "v*")
            .Select(dir => (Dir: dir, Version: ParseVersion(Path.GetFileName(dir))))
            .Where(entry => entry.Version is not null)
            .OrderByDescending(entry => entry.Version)
            .Select(entry => entry.Dir);
        foreach (var dir in versions)
        {
            if (!string.Equals(dir, cudaPath, StringComparison.OrdinalIgnoreCase))
            {
                yield return dir;
            }
        }
    }

    private static Version? ParseVersion(string name) => Version.TryParse(name.TrimStart('v', 'V'), out var version) ? version : null;
}

/// <summary>
/// Links ILGPU's libdevice wrappers into a compiled CUDA kernel, the one place in the tree that knows ILGPU's internals: ILGPU
/// 1.5.3 emits the NVVM version metadata before the target lines, libnvvm rejects the module and the wrappers silently go missing.
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
        var (fragmentsField, assemblyField) = Members.Value;
        var ptx = compiled.PTXAssembly;
        var names = WrappersCalled(ptx);
        if (names.Count == 0)
        {
            return compiled;
        }

        var fragments = (Dictionary<string, string>)fragmentsField.GetValue(null)!;
        foreach (var name in names)
        {
            if (!fragments.ContainsKey(name))
            {
                throw new InvalidOperationException($"the kernel calls the libdevice wrapper {name}, for which ILGPU {IlgpuVersion} has no fragment.");
            }
        }

        var targetMatch = Target.Match(ptx);
        if (!targetMatch.Success)
        {
            throw new InvalidOperationException("the kernel PTX has no .target line.");
        }

        var arch = "compute_" + targetMatch.Groups[1].Value;
        nvvm.GetIRVersion(out var irMajor, out _, out _, out _);
        var module = new StringBuilder();
        module.Append(TargetTriple).Append('\n').Append(TargetDataLayout).Append('\n');
        module.Append("!nvvmir.version = !{!0}\n!0 = !{i32 ").Append(irMajor).Append(", i32 0}\n");
        foreach (var name in names)
        {
            module.Append(fragments[name]).Append('\n');
        }

        var wrapperPtx = CompileWrappers(nvvm, module.ToString(), arch);
        var body = string.Join("\n", wrapperPtx.Split('\n')
            .Where(line => !(line.StartsWith(".version", StringComparison.Ordinal) || line.StartsWith(".target", StringComparison.Ordinal)
                             || line.StartsWith(".address_size", StringComparison.Ordinal))));

        // The callee must precede the call site: the wrappers go right after the module header.
        var headerEnd = ptx.IndexOf(".address_size", StringComparison.Ordinal);
        if (headerEnd < 0)
        {
            throw new InvalidOperationException("the kernel PTX has no .address_size line.");
        }

        headerEnd = ptx.IndexOf('\n', headerEnd) + 1;
        var linked = string.Concat(ptx.AsSpan(0, headerEnd), body, "\n", ptx.AsSpan(headerEnd));
        foreach (var name in names)
        {
            if (!linked.Contains(".func" + " ", StringComparison.Ordinal) || !linked.Contains("__ilgpu" + name + "(", StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"the post-link produced no definition of the libdevice wrapper {name}.");
            }
        }

        // The driver checks the PTX against a context bound to the calling thread; bind the accelerator's before the trial load.
        accelerator.Bind();
        var error = CudaAPI.CurrentAPI.LoadModule(out var handle, linked, out var log);
        if (error != CudaError.CUDA_SUCCESS)
        {
            throw new InvalidOperationException($"the linked PTX was refused by the CUDA driver ({error}): {(log ?? "").Trim()}");
        }

        CudaAPI.CurrentAPI.DestroyModule(handle);
        assemblyField.SetValue(compiled, linked);
        return compiled;
    }

    private static string CompileWrappers(NvvmAPI nvvm, string module, string arch)
    {
        var moduleBytes = Encoding.ASCII.GetBytes(module);
        var libdevice = nvvm.LibDeviceBytes.ToArray();
        nvvm.CreateProgram(out var program);
        try
        {
            var option = Marshal.StringToHGlobalAnsi("-arch=" + arch);
            var options = Marshal.AllocHGlobal(IntPtr.Size);
            try
            {
                Marshal.WriteIntPtr(options, option);
                unsafe
                {
                    fixed (byte* modulePointer = moduleBytes)
                    fixed (byte* libdevicePointer = libdevice)
                    {
                        nvvm.AddModuleToProgram(program, (IntPtr)modulePointer, (IntPtr)moduleBytes.Length, "apthermo-wrappers");
                        nvvm.LazyAddModuleToProgram(program, (IntPtr)libdevicePointer, (IntPtr)libdevice.Length, "libdevice");
                        var result = nvvm.CompileProgram(program, 1, options);
                        nvvm.GetProgramLog(program, out var log);
                        if (result != NvvmResult.NVVM_SUCCESS)
                        {
                            throw new InvalidOperationException($"libnvvm could not compile the libdevice wrappers for {arch} ({result}): {(log ?? "").Trim()}");
                        }
                    }
                }

                nvvm.GetCompiledResult(program, out var wrapperPtx);
                return wrapperPtx ?? throw new InvalidOperationException("libnvvm returned no PTX for the libdevice wrappers.");
            }
            finally
            {
                Marshal.FreeHGlobal(options);
                Marshal.FreeHGlobal(option);
            }
        }
        finally
        {
            nvvm.DestroyProgram(ref program);
        }
    }
}
