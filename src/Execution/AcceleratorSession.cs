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

    private AcceleratorSession(Context context)
    {
        Context = context;
    }

    /// <summary>The accelerator; available once the build has attached it.</summary>
    public Accelerator Accelerator => _accelerator ?? throw new InvalidOperationException("the session has no accelerator: its build did not finish.");

    /// <summary>The libnvvm binding of the CUDA path, null on the CPU accelerator.</summary>
    public NvvmAPI? Nvvm { get; private set; }

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

    /// <summary>The context the accelerator was created on.</summary>
    public Context Context { get; }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Nvvm?.Dispose();
        _accelerator?.Dispose();
        Context.Dispose();
    }
}
