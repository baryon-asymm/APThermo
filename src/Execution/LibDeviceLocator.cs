namespace APThermo.Execution;

/// <summary>The host platform for libdevice discovery. Any value but <see cref="Windows"/> and <see cref="Linux"/> does no discovery.</summary>
internal enum LocatorPlatform
{
    Windows,
    Linux,
    Other,
}

/// <summary>Finds libnvvm and libdevice in the order BOOT.md fixes.</summary>
internal static class LibDeviceLocator
{
    private const string WindowsDllName = "nvvm64_40_0.dll";
    private const string LinuxDllName = "libnvvm.so";
    private const string BitcodeName = "libdevice.10.bc";

    /// <summary>The platform's libnvvm file name, named in a message when it was not found.</summary>
    public static string LibraryFileName => DllName(CurrentPlatform());

    /// <summary>The dll and bitcode paths, or nulls, with every path examined.</summary>
    public static (string? Dll, string? Bitcode, IReadOnlyList<string> Tried) Locate(EngineOptions options)
    {
        var platform = CurrentPlatform();
        return Locate(options, platform, Environment.GetEnvironmentVariable, DefaultGlobRoot(platform, Environment.GetEnvironmentVariable));
    }

    /// <summary>
    /// The seam the tests drive: <paramref name="platform"/> in place of <see cref="OperatingSystem"/>, <paramref name="environment"/>
    /// in place of <see cref="Environment.GetEnvironmentVariable(string)"/>, and <paramref name="globRoot"/> in place of the
    /// platform's own fixed base directory (<c>%ProgramFiles%\NVIDIA GPU Computing Toolkit\CUDA</c> on Windows, <c>/usr/local</c> on
    /// Linux) under which the versioned toolkit directories are found.
    /// </summary>
    internal static (string? Dll, string? Bitcode, IReadOnlyList<string> Tried) Locate(
        EngineOptions options, LocatorPlatform platform, Func<string, string?> environment, string globRoot)
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

        if (!options.LibDeviceDiscovery || platform == LocatorPlatform.Other)
        {
            return (null, null, tried);
        }

        var dllName = DllName(platform);
        foreach (var root in ToolkitRoots(platform, environment, globRoot))
        {
            var bitcode = Path.Combine(root, "nvvm", "libdevice", BitcodeName);
            foreach (var dll in DllCandidates(platform, root, dllName))
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

    /// <summary>The platform's own default glob root: the base directory under which versioned toolkit directories are found.</summary>
    private static string DefaultGlobRoot(LocatorPlatform platform, Func<string, string?> environment) => platform switch
    {
        LocatorPlatform.Windows => Path.Combine(environment("ProgramFiles") ?? @"C:\Program Files", "NVIDIA GPU Computing Toolkit", "CUDA"),
        LocatorPlatform.Linux => "/usr/local",
        LocatorPlatform.Other => "",
        _ => "",
    };

    private static LocatorPlatform CurrentPlatform() =>
        OperatingSystem.IsWindows() ? LocatorPlatform.Windows :
        OperatingSystem.IsLinux() ? LocatorPlatform.Linux : LocatorPlatform.Other;

    private static string DllName(LocatorPlatform platform) => platform switch
    {
        LocatorPlatform.Windows => WindowsDllName,
        LocatorPlatform.Linux => LinuxDllName,
        LocatorPlatform.Other => WindowsDllName,
        _ => WindowsDllName,
    };

    /// <summary>The library file(s) tried under one root, in order: two layouts on Windows, one on Linux.</summary>
    private static IEnumerable<string> DllCandidates(LocatorPlatform platform, string root, string dllName) =>
        platform == LocatorPlatform.Windows
            ? [Path.Combine(root, "nvvm", "bin", dllName), Path.Combine(root, "nvvm", "bin", "x64", dllName)]
            : [Path.Combine(root, "nvvm", "lib64", dllName)];

    /// <summary>
    /// Windows: <c>CUDA_PATH</c>, then the toolkit directories under <paramref name="globRoot"/> from the newest version down.
    /// Linux: <c>CUDA_PATH</c>, then <c>CUDA_HOME</c>, then <c>&lt;globRoot&gt;/cuda</c>, then the <c>cuda-*</c> directories under
    /// <paramref name="globRoot"/> from the newest version down. A root already yielded is skipped.
    /// </summary>
    private static IEnumerable<string> ToolkitRoots(LocatorPlatform platform, Func<string, string?> environment, string globRoot)
    {
        var comparer = platform == LocatorPlatform.Windows ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var seen = new HashSet<string>(comparer);

        var cudaPath = environment("CUDA_PATH");
        if (!string.IsNullOrWhiteSpace(cudaPath) && seen.Add(cudaPath))
        {
            yield return cudaPath;
        }

        if (platform == LocatorPlatform.Linux)
        {
            var cudaHome = environment("CUDA_HOME");
            if (!string.IsNullOrWhiteSpace(cudaHome) && seen.Add(cudaHome))
            {
                yield return cudaHome;
            }

            var fixedRoot = Path.Combine(globRoot, "cuda");
            if (seen.Add(fixedRoot))
            {
                yield return fixedRoot;
            }

            foreach (var dir in VersionedDirectories(globRoot, "cuda-*", "cuda-"))
            {
                if (seen.Add(dir))
                {
                    yield return dir;
                }
            }

            yield break;
        }

        foreach (var dir in VersionedDirectories(globRoot, "v*", "v"))
        {
            if (seen.Add(dir))
            {
                yield return dir;
            }
        }
    }

    /// <summary>The subdirectories of <paramref name="baseDir"/> matching <paramref name="pattern"/>, newest version first.</summary>
    private static IEnumerable<string> VersionedDirectories(string baseDir, string pattern, string prefix)
    {
        if (!Directory.Exists(baseDir))
        {
            yield break;
        }

        var versioned = Directory.GetDirectories(baseDir, pattern)
            .Select(dir => (Dir: dir, Version: ParseVersion(Path.GetFileName(dir), prefix)))
            .Where(entry => entry.Version is not null)
            .OrderByDescending(entry => entry.Version)
            .Select(entry => entry.Dir);
        foreach (var dir in versioned)
        {
            yield return dir;
        }
    }

    private static Version? ParseVersion(string name, string prefix) =>
        name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && Version.TryParse(name[prefix.Length..], out var version) ? version : null;
}
