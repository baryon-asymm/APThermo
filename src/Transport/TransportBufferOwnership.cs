namespace APThermo.Transport;

/// <summary>Host-side ownership during transport-table allocation and after publication.</summary>
internal sealed class TransportBufferOwnership : IDisposable
{
    private List<TransportBufferRegistration>? _resources = [];

    /// <summary>Register ownership before allocating; reference publication needs no later allocation.</summary>
    internal T Acquire<T>(Func<T> allocate) where T : class, IDisposable
    {
        ObjectDisposedException.ThrowIf(_resources is null, this);
        var slot = new TransportBufferRegistration();
        _resources.Add(slot);
        var resource = allocate();
        slot.Resource = resource;
        return resource;
    }

    /// <summary>Publish a completed allocation transaction or roll its owned resources back.</summary>
    internal T Build<T>(Func<TransportBufferOwnership, T> acquire)
    {
        try
        {
            return acquire(this);
        }
        catch (Exception allocatingFailure)
        {
            Rollback(allocatingFailure);
            throw;
        }
    }

    /// <summary>Keep the allocating error primary, including if cleanup diagnostic allocation fails.</summary>
    internal void Rollback(Exception allocatingFailure)
    {
        try
        {
            Dispose();
        }
        catch (Exception cleanupFailure) when (cleanupFailure is not (StackOverflowException or AccessViolationException))
        {
            try
            {
                allocatingFailure.Data["TransportBufferCleanup"] = cleanupFailure;
            }
            catch (Exception diagnosticFailure) when (diagnosticFailure is not (StackOverflowException or AccessViolationException))
            {
                // Read-only or failing diagnostic storage must not replace the exact primary error.
            }
        }
    }

    /// <summary>Attempt every resource before allocating aggregate diagnostics; repeated calls are harmless.</summary>
    public void Dispose()
    {
        var resources = _resources;
        _resources = null;
        if (resources is null)
        {
            return;
        }

        for (var index = resources.Count - 1; index >= 0; index--)
        {
            resources[index].Dispose();
        }

        var failures = resources.Select(r => r.Failure).OfType<Exception>().ToArray();
        if (failures.Length > 0)
        {
            throw new AggregateException("Transport-buffer cleanup failed.", failures);
        }
    }
}
