using System.Runtime.CompilerServices;

namespace APThermo.Transport;

/// <summary>Host-side ownership during transport-table allocation and after publication.</summary>
internal sealed class TransportBufferOwnership : IDisposable
{
    private readonly List<StrongBox<IDisposable?>> _resources = [];

    /// <summary>Register the ownership slot before allocating; publication needs no later allocation.</summary>
    internal T Acquire<T>(Func<T> allocate) where T : class, IDisposable
    {
        var slot = new StrongBox<IDisposable?>();
        _resources.Add(slot);
        var resource = allocate();
        slot.Value = resource;
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

    /// <summary>Preserve the allocating failure while retaining secondary cleanup errors.</summary>
    internal void Rollback(Exception allocatingFailure)
    {
        try
        {
            Dispose();
        }
        catch (AggregateException cleanupFailure)
        {
            allocatingFailure.Data["TransportBufferCleanup"] = cleanupFailure;
        }
    }

    /// <summary>Attempts every resource after ordinary cleanup failures; fatal CLR failures propagate.</summary>
    public void Dispose()
    {
        List<Exception>? failures = null;
        for (var index = _resources.Count - 1; index >= 0; index--)
        {
            try
            {
                _resources[index].Value?.Dispose();
            }
            catch (Exception failure) when (failure is not (OutOfMemoryException or StackOverflowException or AccessViolationException))
            {
                (failures ??= []).Add(failure);
            }
        }

        _resources.Clear();
        if (failures is not null)
        {
            throw new AggregateException("Transport-buffer cleanup failed.", failures);
        }
    }
}