using System.Diagnostics.CodeAnalysis;

namespace APThermo.Problems.Tests;

/// <summary>Fault-injection controls for cache release ordering and secondary cleanup.</summary>
public sealed class SolverDisposalTests
{
    private sealed class Resource(Exception? failure = null) : IDisposable
    {
        internal bool Attempted { get; private set; }

        public void Dispose()
        {
            if (Attempted)
            {
                return;
            }

            Attempted = true;
            if (failure is not null)
            {
                throw failure;
            }
        }
    }

    [SuppressMessage("Usage", "CA2201:Do not raise reserved exception types", Justification = "A fake resource injects a child cleanup OOM; this does not exhaust the host.")]
    private static OutOfMemoryException SimulatedOutOfMemory() => new("injected child cleanup");

    /// <summary>A recoverable child failure, including an injected OOM, cannot retain managed caches or skip the engine.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CacheReferencesAreReleasedBeforeAnyCleanupFailure(bool outOfMemory)
    {
        var released = false;
        Exception failure = outOfMemory ? SimulatedOutOfMemory() : new InvalidOperationException("injected child cleanup");
        using var systems = new Resource(failure);
        using var engine = new Resource();
        var observed = Assert.ThrowsAny<Exception>(() => SolverDisposal.Complete(() => released = true, systems, engine));
        Assert.Same(failure, observed);
        Assert.True(released);
        Assert.True(systems.Attempted);
        Assert.True(engine.Attempted);
    }

    /// <summary>Both diagnostic failures survive when both owners refuse disposal.</summary>
    [Fact]
    public void BothOwnerFailuresRemainObservable()
    {
        var primary = new InvalidOperationException("systems");
        var secondary = SimulatedOutOfMemory();
        using var systems = new Resource(primary);
        using var engine = new Resource(secondary);
        var observed = Assert.Throws<AggregateException>(() => SolverDisposal.Complete(() => { }, systems, engine));
        Assert.Equal(new Exception[] { primary, secondary }, observed.InnerExceptions);
        Assert.True(engine.Attempted);
    }
}
