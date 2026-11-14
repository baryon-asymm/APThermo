using System.Runtime.ExceptionServices;

namespace APThermo.Execution;

/// <summary>Transactional ownership for managed execution resources, with primary-aware best-effort cleanup.</summary>
internal sealed class ExecutionResourceOwner : IDisposable
{
    internal const string CleanupFailuresKey = "APThermo.Execution.CleanupFailures";

    private sealed class Entry
    {
        internal IDisposable? Resource;
        internal Action? Release;
        internal Exception? Failure;
        internal Entry? Next;
    }

    private Entry? _first;
    private Entry? _last;
    private bool _closed;
    private Exception? _primary;
    private Func<Exception, bool>? _ignore;

    /// <summary>Reserves tracking before the allocation delegate can create a resource.</summary>
    internal T Acquire<T>(Func<T> allocate, bool prepend = false) where T : IDisposable
    {
        var entry = Reserve(prepend);
        var resource = allocate();
        entry.Resource = resource;
        return resource;
    }

    internal T? AcquireOptional<T>(Func<T?> allocate) where T : class, IDisposable
    {
        var entry = Reserve(prepend: false);
        var resource = allocate();
        entry.Resource = resource;
        return resource;
    }

    /// <summary>Accepts an existing resource; failed registration still releases that just-handed resource.</summary>
    internal void Own(IDisposable resource, bool prepend = false)
    {
        try
        {
            Reserve(prepend).Resource = resource;
        }
        catch (Exception primary)
        {
            ReleaseOne(resource, primary);
            throw;
        }
    }

    internal void Own(Action release, bool prepend = false)
    {
        try
        {
            Reserve(prepend).Release = release;
        }
        catch (Exception primary)
        {
            try
            {
                release();
            }
            catch (Exception error) when (Recoverable(error))
            {
                Attach(primary, error);
            }

            throw;
        }
    }

    /// <summary>Closes this staging owner after a completed wrapper has accepted the same resources.</summary>
    internal void HandOff()
    {
        _closed = true;
        _first = null;
        _last = null;
    }

    internal void PrepareCleanup(Exception? primary, Func<Exception, bool>? ignore = null)
    {
        _primary = primary;
        _ignore = ignore;
    }

    public void Dispose()
    {
        var primary = _primary;
        var ignore = _ignore;
        _primary = null;
        _ignore = null;
        Complete(primary, ignore);
    }

    /// <summary>Attempts every registered release before reporting diagnostics, with no allocations in the attempt loop.</summary>
    private void Complete(Exception? primary, Func<Exception, bool>? ignore)
    {
        if (_closed)
        {
            return;
        }

        _closed = true;
        var entries = _first;
        _first = null;
        _last = null;
        Exception? firstFailure = null;
        var failures = 0;
        for (var entry = entries; entry is not null; entry = entry.Next)
        {
            var resource = entry.Resource;
            var release = entry.Release;
            entry.Resource = null;
            entry.Release = null;
            try
            {
                resource?.Dispose();
                release?.Invoke();
            }
            catch (Exception error) when (Recoverable(error) && !Ignored(error, ignore))
            {
                entry.Failure = error;
                firstFailure ??= error;
                failures++;
            }
            catch (Exception error) when (Ignored(error, ignore))
            {
            }
        }

        Report(primary, entries, firstFailure, failures);
    }

    /// <summary>Rollback when construction failed before a resource could enter an owner.</summary>
    internal static void ReleaseOne(IDisposable resource, Exception? primary, Func<Exception, bool>? ignore = null)
    {
        try
        {
            resource.Dispose();
        }
        catch (Exception error) when (Ignored(error, ignore))
        {
        }
        catch (Exception error) when (primary is not null && Recoverable(error))
        {
            Attach(primary, error);
        }
    }

    private Entry Reserve(bool prepend)
    {
        ObjectDisposedException.ThrowIf(_closed, this);
        var entry = new Entry();
        if (prepend)
        {
            entry.Next = _first;
            _first = entry;
        }
        else if (_last is not null)
        {
            _last.Next = entry;
        }
        else
        {
            _first = entry;
        }

        _last = prepend && _last is not null ? _last : entry;
        return entry;
    }

    private static void Report(Exception? primary, Entry? entries, Exception? firstFailure, int failures)
    {
        if (firstFailure is null)
        {
            return;
        }

        if (primary is not null)
        {
            try
            {
                Attach(primary, new AggregateException("execution resource cleanup failed", Failures(entries)));
            }
            catch (OutOfMemoryException)
            {
                // Attempts already finished. Genuine host exhaustion can prevent diagnostic aggregation.
            }

            return;
        }

        if (failures == 1)
        {
            ExceptionDispatchInfo.Capture(firstFailure).Throw();
        }

        throw new AggregateException("execution resource cleanup failed", Failures(entries));
    }

    private static IEnumerable<Exception> Failures(Entry? entries)
    {
        for (var entry = entries; entry is not null; entry = entry.Next)
        {
            if (entry.Failure is { } failure)
            {
                yield return failure;
            }
        }
    }

    private static bool Recoverable(Exception error) => error is not (AccessViolationException or StackOverflowException);

    private static bool Ignored(Exception error, Func<Exception, bool>? ignore)
    {
        if (error is not AggregateException aggregate)
        {
            return ignore?.Invoke(error) == true;
        }

        for (var index = 0; index < aggregate.InnerExceptions.Count; index++)
        {
            if (!Ignored(aggregate.InnerExceptions[index], ignore))
            {
                return false;
            }
        }

        return aggregate.InnerExceptions.Count > 0;
    }

    private static void Attach(Exception primary, Exception cleanup)
    {
        try
        {
            primary.Data[CleanupFailuresKey] = primary.Data[CleanupFailuresKey] is Exception previous
                ? new AggregateException("execution resource cleanup failed", previous, cleanup) : cleanup;
        }
        catch (Exception error) when (Recoverable(error))
        {
            // The original exception takes priority if its diagnostic dictionary cannot be written.
        }
    }
}
