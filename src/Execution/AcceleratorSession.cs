using APThermo.Execution.Chunks;
using ILGPU;
using ILGPU.Runtime;
using ILGPU.Runtime.Cuda;

namespace APThermo.Execution;

/// <summary>One context and accelerator, with transactional construction and complete primary-aware cleanup.</summary>
internal sealed class AcceleratorSession : IDisposable
{
    private readonly ExecutionResourceOwner _ownership;
    private bool _disposed;
    private AcceleratorUnavailableException? _lostBy;

    private AcceleratorSession(Context context)
    {
        Context = context;
        CleanupFilter = IgnoreCleanupFailure;
        _ownership = new ExecutionResourceOwner();
    }

    public Accelerator Accelerator
    {
        get => field ?? throw new InvalidOperationException("the session has no accelerator: its build did not finish.");
        private set;
    }

    public LaunchBudget Budget { get; private set; } = LaunchBudget.None;

    public AcceleratorInfo Info
    {
        get => field ?? throw new InvalidOperationException("the session has no description: its build did not finish.");
        private set;
    }

    /// <summary>Allocated during construction, so cleanup never creates this delegate while a primary is propagating.</summary>
    internal Func<Exception, bool> CleanupFilter { get; }

    public static AcceleratorSession Build(Context context, Func<AcceleratorSession, AcceleratorInfo> build)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(build);
        AcceleratorSession? session = null;
        try
        {
            session = new AcceleratorSession(context);
            session._ownership.Own(context);
            session.Info = build(session);
            return session;
        }
        catch (Exception primary)
        {
            if (session is null)
            {
                ExecutionResourceOwner.ReleaseOne(context, primary);
            }
            else
            {
                session.DisposePreserving(primary);
            }

            throw;
        }
    }

    public T Attach<T>(T accelerator) where T : Accelerator
    {
        _ownership.Own(accelerator, prepend: true);
        Accelerator = accelerator;
        return accelerator;
    }

    public LaunchBudget Attach(LaunchBudget budget) => Budget = budget;

    public Context Context { get; }

    internal void MarkLost(AcceleratorUnavailableException timeout) => _lostBy ??= timeout;

    internal void ThrowIfLost()
    {
        if (_lostBy is { } timeout)
        {
            throw new AcceleratorUnavailableException(
                $"this engine's CUDA context was lost by an earlier launch timeout ({timeout.Message}); " +
                "create a new engine, or use the CPU accelerator.", timeout);
        }
    }

    internal bool DropsAfterLoss(CudaError error) =>
        _lostBy is not null && error == CudaError.CUDA_ERROR_LAUNCH_TIMEOUT;

    internal bool DropsAfterLoss(CudaException failure) =>
        Enum.TryParse<CudaError>(failure.Error, out var error) && DropsAfterLoss(error);

    private bool IgnoreCleanupFailure(Exception failure) => failure is CudaException cuda && DropsAfterLoss(cuda);

    public void Dispose() => DisposePreserving(null);

    internal void DisposePreserving(Exception? primary)
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _ownership.PrepareCleanup(primary, CleanupFilter);
        _ownership.Dispose();
    }
}
