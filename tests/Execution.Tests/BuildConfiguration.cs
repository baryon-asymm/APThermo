namespace APThermo.Execution.Tests;

/// <summary>
/// The configuration this test assembly was built in. The CPU accelerator executes the kernels from the
/// assemblies' IL, so the host build configuration changes its speed by roughly 2.8x (BOOT.md); the throughput
/// tripwire compares against an approved file measured in one configuration, and must not compare across them.
/// </summary>
internal static class BuildConfiguration
{
#if DEBUG
    public const string Current = "Debug";
#else
    public const string Current = "Release";
#endif
}
