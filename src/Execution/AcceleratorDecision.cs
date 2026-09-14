namespace AerospacePropellantThermodynamics.Execution;

/// <summary>
/// What <see cref="AcceleratorChoice"/> decided: the session to run on, and — when <see cref="AcceleratorKind.Auto"/> fell back to
/// the CPU accelerator — the failure that turned the choice and the libnvvm and libdevice paths that were examined. The reason is
/// also on <c>Session.Info.CudaSkippedBecause</c>, where every batch result carries it; here it survives as a value of its own
/// instead of dying in a discarded exception.
/// </summary>
internal sealed record AcceleratorDecision(AcceleratorSession Session, string? CudaSkippedBecause, IReadOnlyList<string> PathsTried);
