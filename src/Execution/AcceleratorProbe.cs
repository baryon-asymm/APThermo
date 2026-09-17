namespace APThermo.Execution;

/// <summary>
/// The two questions a consumer asks about an accelerator without running a batch: what a set of
/// <see cref="EngineOptions"/> binds to, and whether the environment forbids CUDA
/// (root <c>BOOT.md</c>, Delivery: Tree contracts; the API review of 2026-09-15, finding F1,
/// fixed in <c>9036c6a</c>). Replaces the public surface <see cref="Engine"/> held
/// for the command line's device listing before that review: <see cref="Engine"/> is internal, and
/// <c>Solver.Create</c> keeps creating one of its own for every batch.
/// </summary>
public static class AcceleratorProbe
{
    /// <summary>
    /// Binds an accelerator exactly as <c>Engine.Create</c> would, releases it immediately, and returns its
    /// description. Throws <see cref="AcceleratorUnavailableException"/> exactly as <c>Engine.Create</c> does.
    /// Creating a CUDA context takes time; call this once per accelerator kind, not per case.
    /// </summary>
    /// <param name="options">The engine options to bind with, or <see langword="null"/> for the default
    /// options.</param>
    /// <returns>A description of the accelerator the given options would bind to.</returns>
    /// <exception cref="AcceleratorUnavailableException">No accelerator of the requested kind could be bound.</exception>
    public static AcceleratorInfo Describe(EngineOptions? options = null)
    {
        using var engine = Engine.Create(options);
        return engine.Accelerator;
    }

    /// <value><see langword="true"/> when the environment forbids CUDA (the <c>APTHERMO_NO_CUDA</c> rule).</value>
    public static bool CudaForbidden => Engine.CudaForbidden;
}
