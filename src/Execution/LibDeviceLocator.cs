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
                if (!File.Exists(dll))
                {
                    continue;
                }

                tried.Add(bitcode);
                if (File.Exists(bitcode))
                {
                    return (dll, bitcode, tried);
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
