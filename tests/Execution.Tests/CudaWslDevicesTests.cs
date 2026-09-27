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
}
