namespace APThermo.Transport;

/// <summary>Preallocated ownership and failure slots for one transport-buffer acquisition.</summary>
internal sealed class TransportBufferRegistration : IDisposable
{
    internal IDisposable? Resource { get; set; }

    internal Exception? Failure { get; private set; }

    /// <summary>Record ordinary failures without allocating, so later resources are still attempted.</summary>
    public void Dispose()
    {
        var resource = Resource;
        Resource = null;
        try
        {
            resource?.Dispose();
        }
        catch (Exception failure) when (failure is not (StackOverflowException or AccessViolationException))
        {
            Failure = failure;
        }
    }
}
