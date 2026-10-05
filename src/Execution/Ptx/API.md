# API.md — Execution.Ptx

Namespace `APThermo.Execution.Ptx`. Every type is `internal`: the audience is
`src/Execution`'s own files (`AcceleratorChoice`, `Engine`, `KernelCache`) and its
tests node, not a neighbour or a caller outside the tree. Everything not listed here is
internal to this node itself and may change without notice even to the parent.

## Post-link ✅

```csharp
namespace APThermo.Execution.Ptx;

internal static class PtxPostLink
{
    public const string ExpectedIlgpuVersion = "1.5.3.0";
    public static string IlgpuVersion { get; }          // the loaded ILGPU assembly's version string
    public static void AssertIlgpu();                    // once per process; a mismatch names the ILGPU version
    internal static FieldInfo AssertIlgpu(string expectedVersion);   // the seam the tests drive: the reflected backing field of the PTX text

    public static LinkResult Link(CudaAccelerator accelerator, PTXCompiledKernel compiled);
    internal static Rewritten Rewrite(string ptx);       // the text rewrite and the guards, no driver
    internal static void Guard(string ptx, int fusedSites, string arch);
    internal static string TargetArch(string ptx);       // "compute_XX" from the kernel's .target sm_XX line
    internal static void ThrowIfFailed(CudaError result, string call, string arch, string? log = null);

    internal readonly record struct LinkResult(PTXCompiledKernel Kernel, int RoundedOperations, int FusedSites);
    internal readonly record struct Rewritten(string Ptx, int RoundedOperations, int FusedSites);
}
```

`Link` rewrites a compiled CUDA kernel's PTX (`Rewrite`: every call of
`Math.FusedMultiplyAdd` becomes `fma.rn.f64`, every unrounded `mul`, `add` and `sub` of
doubles becomes `.rn`), refuses what the guards forbid, trial-loads the result through the
CUDA driver and replaces the kernel's PTX by the rewritten text. It returns what it did as
a value (`LinkResult`: the operations marked, the calls inlined). A refusal is an
`InvalidOperationException` of the one shape `ThrowIfFailed` and the guards build (the
post-link, the target, what failed, the driver's log). `Rewrite`, `Guard`, `TargetArch`
and `ThrowIfFailed` are internal for the tests node, which drives them on PTX text with no
device.

⚠ 2026-10-05: was `LibDevicePostLink` (namespace `APThermo.Execution.LibDevice`) with
`WrappersCalled`, `WrappersDefined`, `AssertEveryWrapperDefined`, a `ThrowIfFailed` for
libnvvm results, a `Link` taking an `NvvmAPI` and a `LinkResult` of two wrapper lists, and
`LibDeviceLocator` with `LocatorPlatform`; now `PtxPostLink` and none of the others →
HISTORY.md#libdevice-retired-2026-10-05

## WSL workaround ✅

```csharp
namespace APThermo.Execution.Ptx;

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

`Link` binds the accelerator's context to the calling thread and loads PTX through the CUDA
driver as a trial. `Register` calls ILGPU's CUDA device registration. `AssertIlgpu` reads
ILGPU's metadata by reflection. `Rewrite`, `Guard` and `TargetArch` touch nothing. The
statics of this node are the `Lazy` reflected member, the compiled patterns and constants:
nothing records a result of any call.
