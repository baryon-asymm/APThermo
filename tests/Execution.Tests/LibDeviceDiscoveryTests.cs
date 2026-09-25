namespace APThermo.Execution.Tests;

/// <summary>
/// L0: libdevice discovery over fake toolkit trees built under a temp directory, driven through the internal seam
/// (<see cref="LocatorPlatform"/>, an injected environment lookup, an injected glob root) so both platforms and both Windows dll
/// layouts are covered from one host OS.
/// </summary>
public sealed class LibDeviceDiscoveryTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("apthermo-libdevice-").FullName;

    /// <summary>Removes the fake toolkit tree built under the temporary root.</summary>
    public void Dispose() => Directory.Delete(_root, recursive: true);

    /// <summary>Explicit paths win over discovery and are tried first.</summary>
    [Fact]
    public void ExplicitPathsWinOverDiscoveryAndAreTriedFirst()
    {
        var explicitDll = WriteFile(Path.Combine(_root, "explicit", "my.dll"));
        var explicitBitcode = WriteFile(Path.Combine(_root, "explicit", "my.bc"));
        var toolkits = Path.Combine(_root, "toolkits");
        _ = WriteWindowsToolkit(Path.Combine(toolkits, "v99.0"), legacyLayout: true);
        var options = new EngineOptions { LibNvvmPath = explicitDll, LibDevicePath = explicitBitcode };

        var (dll, bitcode, tried) = LibDeviceLocator.Locate(options, LocatorPlatform.Windows, Env(), toolkits);

        Assert.Equal(explicitDll, dll);
        Assert.Equal(explicitBitcode, bitcode);
        Assert.Equal([explicitDll, explicitBitcode], tried);
    }

    /// <summary>An unsupported platform does no discovery.</summary>
    [Fact]
    public void AnUnsupportedPlatformDoesNoDiscovery()
    {
        var toolkits = Path.Combine(_root, "toolkits");
        _ = WriteWindowsToolkit(Path.Combine(toolkits, "v1.0"), legacyLayout: true);

        var (dll, bitcode, tried) = LibDeviceLocator.Locate(new EngineOptions(), LocatorPlatform.Other, Env(), toolkits);

        Assert.Null(dll);
        Assert.Null(bitcode);
        Assert.Empty(tried);
    }

    /// <summary>Windows with no cuda path and no toolkit base directory examines nothing.</summary>
    [Fact]
    public void WindowsWithNoCudaPathAndNoToolkitBaseDirectoryExaminesNothing()
    {
        // The hosted Windows CI runner: no CUDA_PATH, and the default toolkit base itself does not exist,
        // so VersionedDirectories yields no root to look under and the honest answer is an empty list.
        var toolkits = Path.Combine(_root, "toolkits");   // never created

        var (dll, bitcode, tried) = LibDeviceLocator.Locate(new EngineOptions(), LocatorPlatform.Windows, Env(), toolkits);

        Assert.Null(dll);
        Assert.Null(bitcode);
        Assert.Empty(tried);
    }

    /// <summary>Windows tries cuda path before the toolkit directories.</summary>
    [Fact]
    public void WindowsTriesCudaPathBeforeTheToolkitDirectories()
    {
        var cudaPath = Path.Combine(_root, "cuda-path");
        var (cudaPathDll, _) = WriteWindowsToolkit(cudaPath, legacyLayout: false);
        var toolkits = Path.Combine(_root, "toolkits");
        _ = WriteWindowsToolkit(Path.Combine(toolkits, "v99.0"), legacyLayout: false);

        var (dll, _, tried) = LibDeviceLocator.Locate(new EngineOptions(), LocatorPlatform.Windows, Env(cudaPath: cudaPath), toolkits);

        Assert.Equal(cudaPathDll, dll);
        Assert.All(tried, path => Assert.StartsWith(cudaPath, path));   // the toolkit directory root was never tried: CUDA_PATH already matched
    }

    /// <summary>Windows orders the toolkit directories newest version first.</summary>
    [Fact]
    public void WindowsOrdersTheToolkitDirectoriesNewestVersionFirst()
    {
        // v13.3 sorts before v9.0 numerically but after it alphabetically: proves the order is by parsed Version, not by string.
        var toolkits = Path.Combine(_root, "toolkits");
        _ = WriteWindowsToolkit(Path.Combine(toolkits, "v9.0"), legacyLayout: false);
        var (v13Dll, _) = WriteWindowsToolkit(Path.Combine(toolkits, "v13.3"), legacyLayout: false);

        var (dll, _, _) = LibDeviceLocator.Locate(new EngineOptions(), LocatorPlatform.Windows, Env(), toolkits);

        Assert.Equal(v13Dll, dll);
    }

    /// <summary>Windows tries both dll layouts.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void WindowsTriesBothDllLayouts(bool legacyLayout)
    {
        var toolkits = Path.Combine(_root, "toolkits");
        var (dllPath, bitcodePath) = WriteWindowsToolkit(Path.Combine(toolkits, "v1.0"), legacyLayout);

        var (dll, bitcode, _) = LibDeviceLocator.Locate(new EngineOptions(), LocatorPlatform.Windows, Env(), toolkits);

        Assert.Equal(dllPath, dll);
        Assert.Equal(bitcodePath, bitcode);
    }

    /// <summary>A library present without bitcode is skipped for the next root.</summary>
    [Fact]
    public void ALibraryPresentWithoutBitcodeIsSkippedForTheNextRoot()
    {
        var toolkits = Path.Combine(_root, "toolkits");
        _ = WriteFile(Path.Combine(toolkits, "v13.0", "nvvm", "bin", "x64", "nvvm64_40_0.dll"));   // no bitcode next to it
        var (goodDll, goodBitcode) = WriteWindowsToolkit(Path.Combine(toolkits, "v12.0"), legacyLayout: false);

        var (dll, bitcode, tried) = LibDeviceLocator.Locate(new EngineOptions(), LocatorPlatform.Windows, Env(), toolkits);

        Assert.Equal(goodDll, dll);
        Assert.Equal(goodBitcode, bitcode);
        Assert.Contains(Path.Combine(toolkits, "v13.0", "nvvm", "libdevice", "libdevice.10.bc"), tried);
    }

    /// <summary>A root named twice is tried once.</summary>
    [Fact]
    public void ARootNamedTwiceIsTriedOnce()
    {
        var toolkits = Path.Combine(_root, "toolkits");
        var v130 = Path.Combine(toolkits, "v13.0");
        // No bitcode next to the dll: the root never succeeds, so discovery exhausts every root and the dedup is exercised for real.
        var dllPath = WriteFile(Path.Combine(v130, "nvvm", "bin", "x64", "nvvm64_40_0.dll"));

        var (dll, bitcode, tried) = LibDeviceLocator.Locate(new EngineOptions(), LocatorPlatform.Windows, Env(cudaPath: v130), toolkits);

        Assert.Null(dll);
        Assert.Null(bitcode);
        Assert.Equal(1, tried.Count(path => path == dllPath));   // CUDA_PATH named the same directory as a toolkit version: tried once
    }

    /// <summary>Linux tries cuda path before the toolkit directories.</summary>
    [Fact]
    public void LinuxTriesCudaPathBeforeTheToolkitDirectories()
    {
        var cudaPath = Path.Combine(_root, "cuda-path");
        var (cudaPathDll, _) = WriteLinuxToolkit(cudaPath);
        var glob = Path.Combine(_root, "usr-local");
        _ = WriteLinuxToolkit(Path.Combine(glob, "cuda"));
        _ = WriteLinuxToolkit(Path.Combine(glob, "cuda-13.3"));

        var (dll, _, tried) = LibDeviceLocator.Locate(new EngineOptions(), LocatorPlatform.Linux, Env(cudaPath), glob);

        Assert.Equal(cudaPathDll, dll);
        Assert.All(tried, path => Assert.StartsWith(cudaPath, path));   // neither the fixed root nor a version was ever tried
    }

    /// <summary>Linux tries cuda home before the fixed root and the fixed root before versions.</summary>
    [Fact]
    public void LinuxTriesCudaHomeBeforeTheFixedRootAndTheFixedRootBeforeVersions()
    {
        var cudaHome = Path.Combine(_root, "cuda-home");
        var (cudaHomeDll, _) = WriteLinuxToolkit(cudaHome);
        var glob = Path.Combine(_root, "usr-local");
        var (fixedDll, _) = WriteLinuxToolkit(Path.Combine(glob, "cuda"));
        _ = WriteLinuxToolkit(Path.Combine(glob, "cuda-13.3"));

        var (dllViaHome, _, _) = LibDeviceLocator.Locate(new EngineOptions(), LocatorPlatform.Linux, Env(cudaHome: cudaHome), glob);
        Assert.Equal(cudaHomeDll, dllViaHome);   // CUDA_HOME beats a fixed root and a version that also match

        var (dllViaFixed, _, _) = LibDeviceLocator.Locate(new EngineOptions(), LocatorPlatform.Linux, Env(), glob);
        Assert.Equal(fixedDll, dllViaFixed);     // with no CUDA_PATH or CUDA_HOME, the fixed root beats a version that also matches
    }

    /// <summary>Linux orders the versioned directories newest first.</summary>
    [Fact]
    public void LinuxOrdersTheVersionedDirectoriesNewestFirst()
    {
        // cuda-13.3 sorts before cuda-9.0 numerically but after it alphabetically: proves the order is by parsed Version, not by string.
        var glob = Path.Combine(_root, "usr-local");
        _ = WriteLinuxToolkit(Path.Combine(glob, "cuda-9.0"));
        var (v13Dll, _) = WriteLinuxToolkit(Path.Combine(glob, "cuda-13.3"));

        var (dll, _, _) = LibDeviceLocator.Locate(new EngineOptions(), LocatorPlatform.Linux, Env(), glob);

        Assert.Equal(v13Dll, dll);
    }

    /// <summary>Linux skips a root that equals an earlier one.</summary>
    [Fact]
    public void LinuxSkipsARootThatEqualsAnEarlierOne()
    {
        var cudaPath = Path.Combine(_root, "cuda-path");
        _ = WriteFile(Path.Combine(cudaPath, "nvvm", "lib64", "libnvvm.so"));   // no bitcode: never succeeds, so discovery exhausts every root
        var dllPath = Path.Combine(cudaPath, "nvvm", "lib64", "libnvvm.so");
        var glob = Path.Combine(_root, "usr-local");

        var (dll, bitcode, tried) = LibDeviceLocator.Locate(new EngineOptions(), LocatorPlatform.Linux, Env(cudaPath, cudaHome: cudaPath), glob);

        Assert.Null(dll);
        Assert.Null(bitcode);
        Assert.Equal(1, tried.Count(path => path == dllPath));   // CUDA_HOME named the same directory as CUDA_PATH: tried once, not twice
    }

    private static Func<string, string?> Env(string? cudaPath = null, string? cudaHome = null) => name => name switch
    {
        "CUDA_PATH" => cudaPath,
        "CUDA_HOME" => cudaHome,
        _ => null,
    };

    private static (string Dll, string Bitcode) WriteWindowsToolkit(string root, bool legacyLayout)
    {
        var dllDir = legacyLayout ? Path.Combine(root, "nvvm", "bin") : Path.Combine(root, "nvvm", "bin", "x64");
        var dll = WriteFile(Path.Combine(dllDir, "nvvm64_40_0.dll"));
        var bitcode = WriteFile(Path.Combine(root, "nvvm", "libdevice", "libdevice.10.bc"));
        return (dll, bitcode);
    }

    private static (string Dll, string Bitcode) WriteLinuxToolkit(string root)
    {
        var dll = WriteFile(Path.Combine(root, "nvvm", "lib64", "libnvvm.so"));
        var bitcode = WriteFile(Path.Combine(root, "nvvm", "libdevice", "libdevice.10.bc"));
        return (dll, bitcode);
    }

    private static string WriteFile(string path)
    {
        _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "stub");
        return path;
    }
}
