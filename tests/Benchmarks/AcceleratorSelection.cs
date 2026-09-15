using AerospacePropellantThermodynamics.Execution;

namespace AerospacePropellantThermodynamics.Benchmarks;

/// Maps a benchmark's accelerator axis to `EngineOptions` (BOOT.md, Invariants: CUDA
/// is optional; a benchmark asking for it falls back to the CPU accelerator and
/// records why through `AcceleratorInfo.CudaSkippedBecause`, and never fails). `Cpu`
/// pins the CPU accelerator so that a CPU-only group is never silently routed to CUDA;
/// `Cuda` asks for CUDA through `Auto`, which is the one `AcceleratorKind` that falls
/// back instead of throwing `AcceleratorUnavailableException`.
internal static class AcceleratorSelection
{
    public static EngineOptions OptionsFor(AcceleratorKind requested) => requested switch
    {
        AcceleratorKind.Cpu => new EngineOptions { Accelerator = AcceleratorKind.Cpu },
        AcceleratorKind.Cuda => new EngineOptions { Accelerator = AcceleratorKind.Auto },
        _ => throw new ArgumentOutOfRangeException(nameof(requested), requested, "expected Cpu or Cuda"),
    };
}
