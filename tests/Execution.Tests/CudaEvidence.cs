using System.Text.Json;
using Xunit.Abstractions;

namespace APThermo.Execution.Tests;

/// <summary>Reports which CUDA evidence a test actually obtained.</summary>
internal static class CudaEvidence
{
    internal const string Prefix = "CUDA_EVIDENCE:";

    internal static void Bound(ITestOutputHelper output, string device)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentException.ThrowIfNullOrWhiteSpace(device);
    }

    internal static void CompleteHardware(ITestOutputHelper output, string device)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(device);
        Emit(output, "hardware", actualCuda: true, refusalVerified: false, scenarioExecuted: true, device);
    }

    internal static void Refusal(ITestOutputHelper output, Action verify)
    {
        verify();
        Emit(output, "hardware", actualCuda: false, refusalVerified: true, scenarioExecuted: false, device: null);
    }

    internal static void NotRun(ITestOutputHelper output, string scope) =>
        Emit(output, scope, actualCuda: false, refusalVerified: false, scenarioExecuted: false, device: null);

    internal static void DriverExecuted(ITestOutputHelper output) =>
        Emit(output, "driver-only", actualCuda: false, refusalVerified: false, scenarioExecuted: true, device: null);

    private static void Emit(ITestOutputHelper output, string scope, bool actualCuda, bool refusalVerified, bool? scenarioExecuted, string? device) =>
        output.WriteLine(Prefix + JsonSerializer.Serialize(new { scope, actualCuda, refusalVerified, scenarioExecuted, device }));
}
