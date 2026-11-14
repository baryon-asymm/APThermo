using System.Runtime.ExceptionServices;

namespace APThermo.Thermo;

/// <summary>Fixed, preallocated ownership of the flat species-table upload columns.</summary>
internal sealed class SpeciesBufferOwnership(int capacity) : IDisposable
{
    internal const string CleanupFailuresKey = "APThermo.Thermo.CleanupFailures";

    private readonly IDisposable?[] _resources = new IDisposable?[capacity];
    private readonly Exception?[] _failures = new Exception?[capacity];
    private bool _disposed;
    private Exception? _primary;

    internal int Capacity => _resources.Length;

    /// <summary>Reserved acquisition slots, including a slot whose factory failed.</summary>
    internal int Count { get; private set; }

    internal T Acquire<T>(Func<T> allocate) where T : class, IDisposable
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (Count >= Capacity)
        {
            throw new InvalidOperationException("the species upload exceeds its reserved buffer inventory");
        }

        var slot = Count++;
        var resource = allocate();
        _resources[slot] = resource;
        return resource;
    }

    internal void PrepareCleanup(Exception primary) => _primary = primary;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        var primary = _primary;
        _primary = null;
        Exception? first = null;
        var count = 0;
        for (var index = 0; index < Count; index++)
        {
            var resource = _resources[index];
            _resources[index] = null;
            try
            {
                resource?.Dispose();
            }
            catch (Exception error) when (error is not (AccessViolationException or StackOverflowException))
            {
                _failures[index] = error;
                first ??= error;
                count++;
            }
        }

        Report(primary, first, count);
    }

    private void Report(Exception? primary, Exception? first, int count)
    {
        if (first is null)
        {
            return;
        }

        if (primary is not null)
        {
            try
            {
                var cleanup = new AggregateException("species upload cleanup failed", Failures());
                primary.Data[CleanupFailuresKey] = primary.Data[CleanupFailuresKey] is Exception previous
                    ? new AggregateException("species upload cleanup failed", previous, cleanup) : cleanup;
            }
            catch (Exception error) when (error is not (AccessViolationException or StackOverflowException))
            {
                // All attempts already ran; an unwritable dictionary or genuine host OOM cannot replace the primary.
            }

            return;
        }

        if (count == 1)
        {
            ExceptionDispatchInfo.Capture(first).Throw();
        }

        throw new AggregateException("species upload cleanup failed", Failures());
    }

    private IEnumerable<Exception> Failures()
    {
        foreach (var failure in _failures)
        {
            if (failure is not null)
            {
                yield return failure;
            }
        }
    }
}
