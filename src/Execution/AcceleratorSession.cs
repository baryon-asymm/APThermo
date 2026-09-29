using APThermo.Execution.Chunks;
using ILGPU;
using ILGPU.Runtime;
using ILGPU.Runtime.Cuda;

namespace APThermo.Execution;

/// <summary>
/// One ILGPU context, the accelerator built on it, the libnvvm binding the CUDA path needs, and the description of all three.
/// Disposes them in order, once, and disposes whatever the build created when the build fails: the rule lives here instead of
/// once per creation path.
/// </summary>
internal sealed class AcceleratorSession : IDisposable
{
    private Accelerator? _accelerator;
    private bool _disposed;
    private AcceleratorUnavailableException? _lostBy;

    private AcceleratorSession(Context context)
    {
        Context = context;
    }

    /// <summary>The accelerator; available once the build has attached it.</summary>
    public Accelerator Accelerator => _accelerator ?? throw new InvalidOperationException("the session has no accelerator: its build did not finish.");

    /// <summary>The libnvvm binding of the CUDA path, null on the CPU accelerator.</summary>
    public NvvmAPI? Nvvm { get; private set; }

    /// <summary>The launch budget of this session's accelerator (2026-09-28, "A launch fits a time budget"):
    /// <see cref="LaunchBudget.None"/> until the build delegate calls <see cref="Attach(LaunchBudget)"/>, which every
    /// build path does before <see cref="Build"/> returns — the CPU accelerator and a CUDA device with no run-time
    /// limit attach <see cref="LaunchBudget.None"/> explicitly, a CUDA device that has one attaches a bounded budget.</summary>
    public LaunchBudget Budget { get; private set; } = LaunchBudget.None;

    /// <summary>What the session is bound to, as the results report it.</summary>
    public AcceleratorInfo Info
    {
        get => field ?? throw new InvalidOperationException("the session has no description: its build did not finish.");
        private set;
    }

    /// <summary>Builds a session around a context; whatever the build attached, the context included, is disposed when it throws.</summary>
    public static AcceleratorSession Build(Context context, Func<AcceleratorSession, AcceleratorInfo> build)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(build);
        var session = new AcceleratorSession(context);
        try
        {
            session.Info = build(session);
            return session;
        }
        catch
        {
            session.Dispose();
            throw;
        }
    }

    /// <summary>Hands the accelerator to the session, which owns it from that moment; the caller keeps its own type.</summary>
    public T Attach<T>(T accelerator) where T : Accelerator
    {
        _accelerator = accelerator;
        return accelerator;
    }

    /// <summary>Hands the libnvvm binding to the session, which owns it from that moment.</summary>
    public NvvmAPI Attach(NvvmAPI nvvm) => Nvvm = nvvm;

    /// <summary>Records the launch budget the build delegate decided for this session's accelerator.</summary>
    public LaunchBudget Attach(LaunchBudget budget) => Budget = budget;

    /// <summary>The context the accelerator was created on.</summary>
    public Context Context { get; }

    /// <summary>
    /// Records that a launch timeout left this session's CUDA context sticky (BOOT.md, the third audit pass's finding
    /// 2): every later CUDA call on it returns the same error, so a fresh run must not be attempted and the engine's own
    /// cleanup must not let that error replace whatever is already propagating. Only the first timeout is kept.
    /// </summary>
    internal void MarkLost(AcceleratorUnavailableException timeout) => _lostBy ??= timeout;

    /// <summary>Refuses a call on a session an earlier launch timeout left unusable, naming that timeout.</summary>
    internal void ThrowIfLost()
    {
        if (_lostBy is { } timeout)
        {
            throw new AcceleratorUnavailableException(
                $"this engine's CUDA context was lost by an earlier launch timeout ({timeout.Message}); " +
                "create a new engine, or use the CPU accelerator.", [], timeout);
        }
    }

    /// <summary>
    /// The one decision behind every drop this node performs on disposal (BOOT.md, the third audit pass's finding 2):
    /// whether a just-caught <see cref="CudaException"/> is this session's own sticky error from the launch timeout
    /// already recorded by <see cref="MarkLost"/>. Every disposal that might run on a lost session — this session's own
    /// <see cref="Dispose"/> below, a pipeline's chunk buffers (<see cref="BatchRun"/>) and the uploaded tables
    /// (<c>Engine</c>) — reads this method in its catch filter; a session that was never marked lost, or any exception
    /// but that one sticky error, answers false, and the disposal's exception propagates as it always did.
    /// </summary>
    internal bool DropsAfterLoss(CudaException failure) =>
        _lostBy is not null && failure.Error == nameof(CudaError.CUDA_ERROR_LAUNCH_TIMEOUT);

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        try
        {
            Nvvm?.Dispose();
        }
        catch (CudaException failure) when (DropsAfterLoss(failure))
        {
        }

        try
        {
            _accelerator?.Dispose();
        }
        catch (CudaException failure) when (DropsAfterLoss(failure))
        {
        }

        try
        {
            Context.Dispose();
        }
        catch (CudaException failure) when (DropsAfterLoss(failure))
        {
        }
    }
}
