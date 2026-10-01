using System.Reflection;
using System.Runtime.InteropServices;
using APThermo.Execution.LibDevice;

namespace APThermo.Execution.Tests;

/// <summary>
/// L0/L1: every CUDA context of a process binds, including the second and later ones under WSL (BOOT.md, 2026-09-27; the
/// orchestrator's WSL run of 2026-09-27 found the second context of a process failed there). On Windows this is a repeat of
/// what every other CUDA test already exercises once: <c>CudaWslDevices.Register</c> never takes its reflection branch there,
/// since ILGPU's own resolver install never throws outside WSL.
/// </summary>
[Collection(EngineFixture.CollectionName)]
public sealed class CudaWslDevicesTests
{
    /// <summary>
    /// Three CUDA engines, created one after another in this one process, each bind and each run the probe. Before the fix,
    /// under WSL, the second (or a later) one throws: ILGPU's <c>builder.Cuda()</c> tries to install a second
    /// <c>DllImportResolver</c> on its own assembly and .NET refuses. On Windows this was already green, since the resolver
    /// is a WSL-only detour; it stays green after the fix, which only adds a fallback the public call's own success never
    /// reaches.
    /// </summary>
    [Fact]
    [Trait("Category", "Cuda")]
    public void EveryCudaEngineOfTheProcessBindsAndProbes()
    {
        if (Engine.CudaForbidden)
        {
            return;
        }

        for (var i = 0; i < 3; i++)
        {
            using var engine = Engine.Create(new EngineOptions { Accelerator = AcceleratorKind.Cuda });
            Assert.Equal(AcceleratorKind.Cuda, engine.Accelerator.Kind);
            var probe = engine.ProbeMath([1.0]);
            Assert.Equal(MathProbe.FunctionCount, probe.Length);
        }
    }

    /// <summary>
    /// The reflection seam <c>CudaWslDevices.Reflect</c> takes its two ILGPU member names as parameters exactly so this fact
    /// can hand it a wrong one without needing WSL to reach the code at all: a renamed or removed
    /// <c>Context.Builder.DeviceRegistry</c> or <c>CudaDevice.GetDevices</c> is an <see cref="AcceleratorUnavailableException"/>
    /// naming it, the same shape every other bind failure of this node takes, so a future ILGPU release that moves either
    /// member fails loudly at the first WSL bind instead of silently reporting no device.
    /// </summary>
    [Fact]
    public void ARenamedIlgpuMemberNamesItself()
    {
        var registry = Assert.Throws<AcceleratorUnavailableException>(() => CudaWslDevices.Reflect("NoSuchRegistryProperty", "GetDevices"));
        Assert.Contains("NoSuchRegistryProperty", registry.Message, StringComparison.Ordinal);

        var getDevices = Assert.Throws<AcceleratorUnavailableException>(() => CudaWslDevices.Reflect("DeviceRegistry", "NoSuchGetDevicesMethod"));
        Assert.Contains("NoSuchGetDevicesMethod", getDevices.Message, StringComparison.Ordinal);

        // The real names still resolve, on this pinned ILGPU version: proves the fact above tests a wrong name, not a broken
        // reflection call.
        var (found, _) = CudaWslDevices.Reflect("DeviceRegistry", "GetDevices");
        Assert.NotNull(found);
    }

    /// <summary>
    /// The resolver-already-set failure is recognised by <c>TargetSite</c>, not by message text (2026-09-28, the second
    /// audit's observation 5): a real second <see cref="NativeLibrary.SetDllImportResolver(Assembly, DllImportResolver)"/>
    /// on the same assembly throws exactly the shape ILGPU's own resolver install does, and is recognised; a different
    /// <see cref="InvalidOperationException"/> that happens to carry the identical English text, thrown from ordinary code
    /// rather than from <see cref="NativeLibrary.SetDllImportResolver(Assembly, DllImportResolver)"/> itself, is not — the
    /// mutation this fact guards against (matching by message alone) would accept both.
    /// </summary>
    [Fact]
    public void TheResolverAlreadySetFailureIsRecognisedByTargetSiteNotByMessage()
    {
        // A fresh, on-disk copy of an already-built assembly, loaded into its own load context so it is a distinct
        // runtime Assembly object from the one every other test uses: NativeLibrary.SetDllImportResolver refuses a
        // dynamic (in-memory) assembly, and there is no public way to clear a resolver once set, so setting one on
        // the real, shared copy would leak it for the rest of the process.
        var source = Path.Combine(AppContext.BaseDirectory, "APThermo.Harness.dll");
        var copy = Path.Combine(Path.GetTempPath(), $"APThermo.Harness.{Guid.NewGuid():N}.dll");
        File.Copy(source, copy);
        var assembly = Assembly.LoadFile(copy);
        NativeLibrary.SetDllImportResolver(assembly, (_, _, _) => IntPtr.Zero);
        var real = Assert.Throws<InvalidOperationException>(
            () => NativeLibrary.SetDllImportResolver(assembly, (_, _, _) => IntPtr.Zero));
        Assert.True(CudaWslDevices.IsResolverAlreadySet(real));

        var lookAlike = new InvalidOperationException("A resolver is already set for the assembly.");
        Assert.False(CudaWslDevices.IsResolverAlreadySet(lookAlike));
    }
}
