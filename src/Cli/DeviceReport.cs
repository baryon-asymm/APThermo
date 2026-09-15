using AerospacePropellantThermodynamics.Execution;

namespace AerospacePropellantThermodynamics.Cli;

/// <summary>What the machine offers: the CPU accelerator, always; the CUDA one, or why it could not be bound.</summary>
internal sealed record DeviceReport(AcceleratorInfo Cpu, AcceleratorInfo? Cuda, string? CudaMessage, IReadOnlyList<string> CudaPathsTried);
