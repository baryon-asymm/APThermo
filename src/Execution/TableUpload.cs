namespace APThermo.Execution;

/// <summary>Species upload, optional transport upload and wrapper construction form one ownership transaction.</summary>
internal static class TableUpload
{
    internal static TResult Create<TSpecies, TTransport, TResult>(
        Func<TSpecies> species, Func<TTransport?> transport, Func<TSpecies, TTransport?, TResult> wrap,
        Func<Exception, bool>? ignore = null)
        where TSpecies : class, IDisposable
        where TTransport : class, IDisposable
        where TResult : class, IDisposable
    {
        using var staging = new ExecutionResourceOwner();
        try
        {
            var speciesBuffers = staging.Acquire(species);
            var transportBuffers = staging.AcquireOptional(transport);
            var result = wrap(speciesBuffers, transportBuffers);
            staging.HandOff();
            return result;
        }
        catch (Exception primary)
        {
            staging.PrepareCleanup(primary, ignore);
            throw;
        }
    }
}
