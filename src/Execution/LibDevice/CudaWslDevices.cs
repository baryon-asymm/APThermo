using System.Reflection;
using System.Runtime.InteropServices;
using ILGPU;
using ILGPU.Backends.PTX;
using ILGPU.Runtime;
using ILGPU.Runtime.Cuda;

namespace APThermo.Execution.LibDevice;

/// <summary>
/// Works around ILGPU 1.5.3's WSL defect (BOOT.md, "Every CUDA context of a process binds under WSL", 2026-09-27):
/// <c>CudaContextExtensions.CudaInternal</c> calls <c>NativeLibrary.SetDllImportResolver</c> on its own assembly every
/// time it runs under WSL, and .NET allows only one resolver per assembly, so the second CUDA context of a process throws
/// <see cref="InvalidOperationException"/> ("A resolver is already set for the assembly") before any device is registered.
/// </summary>
internal static class CudaWslDevices
{
    private const string SetDllImportResolverName = "SetDllImportResolver";

    /// <summary>
    /// Registers CUDA devices on <paramref name="builder"/> exactly as the no-argument <c>builder.Cuda()</c> would. The public
    /// call is tried every time — no static state records whether a resolver was ever set — so outside WSL, and for the first
    /// CUDA context of a process under WSL, this is the only path taken. When it throws the resolver exception (every CUDA
    /// context after the first, under WSL), the resolver ILGPU needs is already in place, and the devices are registered
    /// directly through ILGPU's own internal <c>CudaDevice.GetDevices(configure, predicate, registry)</c>, the call
    /// <c>CudaInternal</c> makes right after setting the resolver, with the same two delegates the no-argument overload
    /// passes to it (ILGPU source, <c>CudaContextExtensions.cs</c>, tag v1.5.3). A member <c>CudaInternal</c> no longer has is
    /// an <see cref="AcceleratorUnavailableException"/> naming it, so a changed ILGPU fails loudly at the first bind rather
    /// than silently reporting no device.
    /// </summary>
    public static void Register(Context.Builder builder)
    {
        try
        {
            _ = builder.Cuda();
        }
        catch (InvalidOperationException failure) when (IsResolverAlreadySet(failure))
        {
            RegisterByReflection(builder);
        }
    }

    /// <summary>
    /// Recognises the resolver-already-set failure by where it was thrown, not by its message text (2026-09-28, the second
    /// audit's observation 5): <see cref="NativeLibrary.SetDllImportResolver(System.Reflection.Assembly, DllImportResolver)"/>
    /// throws <see cref="InvalidOperationException"/> with the English text "A resolver is already set for the assembly",
    /// but an application trimmed with <c>UseSystemResourceKeys</c> gets the resource key in its place, and a message check
    /// would then miss the failure — every later engine of the process would fall back to the CPU accelerator instead of
    /// binding CUDA under WSL, with no failure of its own to explain why. Internal, not private, so a test can hand it any
    /// <see cref="InvalidOperationException"/> — including one whose message happens to match but whose <c>TargetSite</c>
    /// does not — without needing WSL to reach this code the way <see cref="Register"/> does.
    /// </summary>
    internal static bool IsResolverAlreadySet(InvalidOperationException failure) =>
        failure.TargetSite?.Name == SetDllImportResolverName && failure.TargetSite.DeclaringType == typeof(NativeLibrary);

    /// <summary>The predicate the no-argument <c>Cuda()</c> overload passes to <c>CudaInternal</c>: a device with a known
    /// architecture and an instruction set the PTX backend supports. Both members are public on the pinned ILGPU version, so
    /// this predicate needs no reflection of its own, unlike the configure delegate's registration call below.</summary>
    private static bool AcceptsEveryDevice(CudaDevice device) =>
        device.Architecture.HasValue && device.InstructionSet.HasValue
        && PTXCodeGenerator.SupportedInstructionSets.Contains(device.InstructionSet.Value);

    private static void RegisterByReflection(Context.Builder builder)
    {
        var (registryProperty, getDevices) = Members.Value;
        var registry = registryProperty.GetValue(builder);
        _ = getDevices.Invoke(null, [new Action<CudaDeviceOverride>(_ => { }), new Predicate<CudaDevice>(AcceptsEveryDevice), registry]);
    }

    private const string RegistryPropertyName = "DeviceRegistry";
    private const string GetDevicesMethodName = "GetDevices";

    private static readonly Lazy<(PropertyInfo Registry, MethodInfo GetDevices)> Members = new(() => Reflect(RegistryPropertyName, GetDevicesMethodName));

    /// <summary>
    /// Reflects the two ILGPU internals this workaround needs; a missing one names itself in the exception. Takes the member
    /// names as parameters, rather than reading the two consts directly, so a test can hand it a wrong name and see the
    /// exception name it, without needing WSL to reach this code the way <see cref="Register"/> does.
    /// </summary>
    internal static (PropertyInfo Registry, MethodInfo GetDevices) Reflect(string registryPropertyName, string getDevicesMethodName)
    {
        // Neither name can be given with nameof: both members are internal to ILGPU, which grants this assembly no
        // InternalsVisibleTo, so the compiler cannot see them; only reflection reaches across that boundary (the same reason
        // LibDevicePostLink reads its own ILGPU internals by name rather than nameof).
        var registryProperty = typeof(Context.Builder).GetProperty(registryPropertyName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        if (registryProperty is null || registryProperty.PropertyType != typeof(DeviceRegistry))
        {
            throw new AcceleratorUnavailableException($"ILGPU {LibDevicePostLink.IlgpuVersion}: Context.Builder.{registryPropertyName} is not the property the WSL workaround expects.");
        }

        var getDevices = typeof(CudaDevice).GetMethod(getDevicesMethodName, BindingFlags.Static | BindingFlags.NonPublic, binder: null,
            types: [typeof(Action<CudaDeviceOverride>), typeof(Predicate<CudaDevice>), typeof(DeviceRegistry)], modifiers: null);
        return getDevices is null
            ? throw new AcceleratorUnavailableException(
                $"ILGPU {LibDevicePostLink.IlgpuVersion}: CudaDevice.{getDevicesMethodName}(Action<CudaDeviceOverride>, Predicate<CudaDevice>, DeviceRegistry) is not the method the WSL workaround expects.")
            : (registryProperty, getDevices);
    }
}
