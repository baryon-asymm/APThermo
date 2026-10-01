# API.md — Execution.LibDevice

Namespace `APThermo.Execution.LibDevice`. Every type is `internal`: the audience is
`src/Execution`'s own files (`AcceleratorChoice`, `Engine`, `KernelCache`) and its
tests node, not a neighbour or a caller outside the tree. Everything not listed here is
internal to this node itself and may change without notice even to the parent.

## Discovery ✅

```csharp
namespace APThermo.Execution.LibDevice;

internal enum LocatorPlatform
{
    Windows,
    Linux,
    Other,
}

internal static class LibDeviceLocator
{
    public static string LibraryFileName { get; }      // the platform's libnvvm file name, named in a "not found" message
    public static ValueTuple<string?, string?, IReadOnlyList<string>> Locate(EngineOptions options);   // (Dll, Bitcode, Tried)
    internal static ValueTuple<string?, string?, IReadOnlyList<string>> Locate(EngineOptions options, LocatorPlatform platform, Func<string, string?> environment, string globRoot);   // the seam the tests drive
}
```

The tuples are C# value tuples (`(string? Dll, string? Bitcode, IReadOnlyList<string> Tried)`
for `Locate`), spelled `ValueTuple<…>` in the blocks because the declaration check reads no
tuple syntax.

`Locate` returns the libnvvm and libdevice paths, or nulls, with every path examined, in
the order `BOOT.md`, Constraints, "libdevice discovery order", fixes. It reads the
environment and the file system and nothing else.

## Post-link ✅

```csharp
namespace APThermo.Execution.LibDevice;

internal static class LibDevicePostLink
{
    public const string ExpectedIlgpuVersion = "1.5.3.0";
    public static string IlgpuVersion { get; }          // the loaded ILGPU assembly's version string
    public static void AssertIlgpu();                    // once per process; a mismatch names the ILGPU version
    internal static ValueTuple<FieldInfo, FieldInfo> AssertIlgpu(string expectedVersion);   // the seam the tests drive

    public static IReadOnlyList<string> WrappersCalled(string ptx);
    public static IReadOnlyList<string> WrappersDefined(string ptx);

    public static LinkResult Link(CudaAccelerator accelerator, NvvmAPI nvvm, PTXCompiledKernel compiled);

    internal static void ThrowIfFailed(NvvmResult result, string call, string arch, string? log = null);
    internal static void ThrowIfFailed(CudaError result, string call, string arch, string? log = null);
    internal static void AssertEveryWrapperDefined(string body, IReadOnlyList<string> names);

    internal readonly record struct LinkResult(PTXCompiledKernel Kernel, IReadOnlyList<string> DefinedByIlgpu, IReadOnlyList<string> Compiled);
}
```

`Link` completes a compiled CUDA kernel with the libdevice wrappers it calls and ILGPU
did not define, trial-loads the result on either path, and returns what it did as a
value (`LinkResult`); a kernel that calls no wrapper is returned untouched. A refusal is
an `InvalidOperationException` of the one shape `ThrowIfFailed` builds (the post-link,
the target, the library, the call, the result code, the log). The two `ThrowIfFailed`
overloads and `AssertEveryWrapperDefined` are internal for the tests node.

## WSL workaround ✅

```csharp
namespace APThermo.Execution.LibDevice;

internal static class CudaWslDevices
{
    public static void Register(Context.Builder builder);    // what builder.Cuda() does, for every context of a process under WSL
    internal static bool IsResolverAlreadySet(InvalidOperationException failure);
    internal static ValueTuple<PropertyInfo, MethodInfo> Reflect(string registryPropertyName, string getDevicesMethodName);   // (Registry, GetDevices)
}
```

`Register` tries the public `builder.Cuda()` every time and registers the devices itself
only when it throws the resolver-already-set exception; a missing ILGPU member is an
`AcceleratorUnavailableException` naming it.

## Side effects

`LibDeviceLocator.Locate` reads environment variables and the file system. `Link`
binds the accelerator's context to the calling thread, loads PTX through the CUDA
driver as a trial and calls libnvvm. `Register` calls ILGPU's CUDA device registration.
`AssertIlgpu` reads ILGPU's metadata by reflection. The statics of this node are the
`Lazy` reflected members and constants: nothing records a result of any call.
