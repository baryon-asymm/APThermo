using System.Reflection;
using ILGPU;
using ILGPU.Runtime;
using ILGPU.Runtime.CPU;

namespace APThermo.Execution.Tests;

/// <summary>Acquisition and wrapping faults retain their primary and release every acquired owner.</summary>
[Collection(EngineFixture.CollectionName)]
public sealed class TransactionOwnershipTests
{
    private sealed class Resource(int index, List<int> attempts, Exception? failure = null) : IDisposable
    {
        internal bool IsDisposed { get; private set; }

        public void Dispose()
        {
            if (IsDisposed)
            {
                return;
            }

            IsDisposed = true;
            attempts.Add(index);
            if (failure is not null)
            {
                throw failure;
            }
        }
    }

    private static ExecutionResourceOwner Ownership(object wrapper) => Assert.IsType<ExecutionResourceOwner>(
        wrapper.GetType().GetField("_ownership", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(wrapper));

    /// <summary>Both allocation boundaries and the final wrapper boundary are tested with ordinary and simulated OOM primaries.</summary>
    [Theory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(2, true)]
    public void EveryUploadTransactionBoundaryPreservesThePrimary(int failureAt, bool outOfMemory)
    {
        var attempts = new List<int>();
        Exception primary = outOfMemory ? Activator.CreateInstance<OutOfMemoryException>() : new InvalidOperationException("allocation primary");
        var ordinary = new InvalidOperationException("species cleanup");
        var childOutOfMemory = Activator.CreateInstance<OutOfMemoryException>();
        var observed = Record.Exception((Action)(() =>
            _ = TableUpload.Create<Resource, Resource, ExecutionResourceOwner>(
                () => failureAt == 0 ? throw primary : new Resource(0, attempts, ordinary),
                () => failureAt == 1 ? throw primary : new Resource(1, attempts, childOutOfMemory),
                (_, _) => throw primary)));
        Assert.Same(primary, observed);
        Assert.Equal(Enumerable.Range(0, failureAt), attempts);
        if (failureAt > 0)
        {
            var diagnostics = Assert.IsType<AggregateException>(primary.Data[ExecutionResourceOwner.CleanupFailuresKey]);
            Assert.Contains(ordinary, diagnostics.Flatten().InnerExceptions);
            if (failureAt == 2)
            {
                Assert.Contains(childOutOfMemory, diagnostics.Flatten().InnerExceptions);
            }
        }
    }

    /// <summary>A completed wrapper keeps both resources alive until its own disposal.</summary>
    [Fact]
    public void SuccessfulWrappingTransfersOwnershipWithoutEarlyDisposal()
    {
        var attempts = new List<int>();
        using var species = new Resource(0, attempts);
        using var transport = new Resource(1, attempts);
        using var wrapper = TableUpload.Create(
            () => species, () => transport, (first, second) =>
            {
                var owner = new ExecutionResourceOwner();
                owner.Own(first);
                owner.Own(second!);
                return owner;
            });
        Assert.False(species.IsDisposed);
        Assert.False(transport.IsDisposed);
        wrapper.Dispose();
        Assert.Equal([0, 1], attempts);
    }

    /// <summary>The optional-transport branch transfers only the real species resource.</summary>
    [Fact]
    public void AnUploadWithoutTransportTransfersOnlyItsSpeciesResource()
    {
        var attempts = new List<int>();
        using var species = new Resource(0, attempts);
        using var wrapper = TableUpload.Create<Resource, Resource, ExecutionResourceOwner>(
            () => species, () => null, (first, second) =>
            {
                Assert.Null(second);
                var owner = new ExecutionResourceOwner();
                owner.Own(first);
                return owner;
            });
        Assert.False(species.IsDisposed);
        wrapper.Dispose();
        Assert.Equal([0], attempts);
    }

    /// <summary>A rejected registration releases a resource that had already been handed over.</summary>
    [Fact]
    public void ARegistrationFailureCannotLoseAnExistingResource()
    {
        var attempts = new List<int>();
        var cleanup = Activator.CreateInstance<OutOfMemoryException>();
        using var owner = new ExecutionResourceOwner();
        owner.Dispose();
        using var resource = new Resource(0, attempts, cleanup);
        var primary = Assert.Throws<ObjectDisposedException>(() => owner.Own(resource));
        Assert.True(resource.IsDisposed);
        Assert.Same(cleanup, primary.Data[ExecutionResourceOwner.CleanupFailuresKey]);
    }

    /// <summary>Construction rollback releases the actual CPU accelerator/context after an earlier independent cleanup fault.</summary>
    [Fact]
    public void SessionConstructionFailureStillReleasesItsContext()
    {
        using var context = Context.Create(builder => builder.CPU());
        Accelerator? accelerator = null;
        var primary = new InvalidOperationException("session build primary");
        var cleanup = Activator.CreateInstance<OutOfMemoryException>();
        var observed = Record.Exception((Action)(() => _ = AcceleratorSession.Build(context, session =>
        {
            accelerator = session.Attach(context.CreateCPUAccelerator(0));
            Ownership(session).Own(() => throw cleanup, prepend: true);
            throw primary;
        })));
        Assert.Same(primary, observed);
        Assert.True(context.IsDisposed);
        Assert.True(accelerator!.IsDisposed);
        var diagnostics = Assert.IsType<AggregateException>(primary.Data[ExecutionResourceOwner.CleanupFailuresKey]);
        Assert.Contains(cleanup, diagnostics.InnerExceptions);
    }

    /// <summary>A lost session ignores only its own sticky CUDA error; unrelated ordinary disposal faults still report.</summary>
    [Fact]
    public void ALostEngineStillReportsUnrelatedCleanupFailures()
    {
        using var engine = Engine.Create(new EngineOptions { Accelerator = AcceleratorKind.Cpu });
        var accelerator = engine.IlgpuAccelerator;
        engine.MarkLost(new AcceleratorUnavailableException("earlier timeout"));
        var failure = new InvalidOperationException("unrelated cleanup");
        Ownership(engine).Own(() => throw failure, prepend: true);
        Assert.Same(failure, Record.Exception(engine.Dispose));
        Assert.True(accelerator.IsDisposed);
        Assert.True(accelerator.Context.IsDisposed);
    }

    /// <summary>Engine cleanup attempts its actual session and remaining owners after an earlier cache-group failure.</summary>
    [Fact]
    public void EngineDisposalStillReleasesItsSessionAfterAnotherOwnerFails()
    {
        using var engine = Engine.Create(new EngineOptions { Accelerator = AcceleratorKind.Cpu });
        var accelerator = engine.IlgpuAccelerator;
        var failure = new InvalidOperationException("injected cache-group failure");
        var reachedTail = false;
        Ownership(engine).Own(() => throw failure, prepend: true);
        Ownership(engine).Own(() => reachedTail = true);
        var observed = Record.Exception(engine.Dispose);
        Assert.Same(failure, observed);
        Assert.True(reachedTail);
        Assert.True(accelerator.IsDisposed);
        Assert.True(accelerator.Context.IsDisposed);
        Assert.Equal(0, engine.Launchers.Count);
    }
}
