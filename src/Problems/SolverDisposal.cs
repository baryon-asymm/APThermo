using System.Runtime.ExceptionServices;

namespace APThermo.Problems;

/// <summary>Completes both resource owners after dropping cached propellant references.</summary>
internal static class SolverDisposal
{
    internal static void Complete(Action releaseReferences, IDisposable systems, IDisposable engine)
    {
        releaseReferences();
        Exception? systemsFailure = null;
        try
        {
            systems.Dispose();
        }
        catch (Exception error) when (error is not AccessViolationException)
        {
            systemsFailure = error;
        }

        try
        {
            engine.Dispose();
        }
        catch (Exception error) when (systemsFailure is not null && error is not AccessViolationException)
        {
            throw new AggregateException("solver resource cleanup failed", systemsFailure, error);
        }

        if (systemsFailure is not null)
        {
            ExceptionDispatchInfo.Capture(systemsFailure).Throw();
        }
    }
}
