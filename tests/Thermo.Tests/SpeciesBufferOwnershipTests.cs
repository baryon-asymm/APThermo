using System.Reflection;
using ILGPU.Runtime;

namespace APThermo.Thermo.Tests;

/// <summary>Every generated upload boundary rolls back; managed child OOM is a cleanup fault, not host exhaustion.</summary>
public sealed class SpeciesBufferOwnershipTests
{
    private static readonly CpuFixture Cpu = new();

    private static int ColumnCount => typeof(SpeciesTableArrays).GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .Count(property => property.PropertyType.IsArray);

    private sealed class Resource(int index, List<int> attempts, Exception? failure = null) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            attempts.Add(index);
            if (failure is not null)
            {
                throw failure;
            }
        }
    }

    /// <summary>The inventory produces allocation boundaries rather than a hand-maintained number.</summary>
    public static TheoryData<int> FailurePoints()
    {
        var data = new TheoryData<int>();
        foreach (var point in Enumerable.Range(0, ColumnCount + 1))
        {
            data.Add(point);
        }

        return data;
    }

    /// <summary>A failure before any column, after each prefix, or after the final column releases exactly that prefix.</summary>
    [Theory]
    [MemberData(nameof(FailurePoints))]
    public void EveryAllocationBoundaryRollsBack(int failureAt)
    {
        var attempts = new List<int>();
        var primary = new InvalidOperationException("injected allocation failure");
        using var owner = new SpeciesBufferOwnership(ColumnCount);
        var observed = Record.Exception((Action)(() =>
        {
            try
            {
                for (var index = 0; index < ColumnCount; index++)
                {
                    var slot = index;
                    _ = owner.Acquire(() => slot == failureAt ? throw primary : new Resource(slot, attempts));
                }

                throw primary;
            }
            catch (Exception error)
            {
                owner.PrepareCleanup(error);
                owner.Dispose();
                throw;
            }
        }));
        Assert.Same(primary, observed);
        Assert.Equal(Enumerable.Range(0, failureAt), attempts);
    }

    /// <summary>Ordinary and injected OOM cleanup failures remain attached after every owner receives an attempt.</summary>
    [Fact]
    public void CleanupFailuresCannotMaskThePrimaryOrStopTheGroup()
    {
        var attempts = new List<int>();
        var primary = new InvalidOperationException("allocation primary");
        var ordinary = new InvalidOperationException("cleanup child");
        var outOfMemory = Activator.CreateInstance<OutOfMemoryException>();
        using var owner = new SpeciesBufferOwnership(ColumnCount);
        for (var index = 0; index < ColumnCount; index++)
        {
            var slot = index;
            Exception? failure = slot == 0 ? ordinary : slot == 1 ? outOfMemory : null;
            _ = owner.Acquire(() => new Resource(slot, attempts, failure));
        }

        owner.PrepareCleanup(primary);
        var observed = Record.Exception(owner.Dispose);
        Assert.Null(observed);
        Assert.Equal(Enumerable.Range(0, ColumnCount), attempts);
        var diagnostics = Assert.IsType<AggregateException>(primary.Data[SpeciesBufferOwnership.CleanupFailuresKey]);
        Assert.Contains(ordinary, diagnostics.InnerExceptions);
        Assert.Contains(outOfMemory, diagnostics.InnerExceptions);
    }

    /// <summary>Without an active primary, independent cleanup failures are reported after the full attempt loop.</summary>
    [Fact]
    public void CleanupWithoutAPrimaryReportsEveryFailureAfterAllAttempts()
    {
        var attempts = new List<int>();
        var ordinary = new InvalidOperationException("cleanup child");
        var outOfMemory = Activator.CreateInstance<OutOfMemoryException>();
        using var owner = new SpeciesBufferOwnership(ColumnCount);
        for (var index = 0; index < ColumnCount; index++)
        {
            var slot = index;
            Exception? failure = slot == 0 ? ordinary : slot == 1 ? outOfMemory : null;
            _ = owner.Acquire(() => new Resource(slot, attempts, failure));
        }

        var observed = Assert.Throws<AggregateException>(owner.Dispose);
        Assert.Equal(Enumerable.Range(0, ColumnCount), attempts);
        Assert.Contains(ordinary, observed.InnerExceptions);
        Assert.Contains(outOfMemory, observed.InnerExceptions);
    }

    /// <summary>A lower-level cleanup diagnostic is not overwritten by the species group.</summary>
    [Fact]
    public void ExistingSecondaryDiagnosticsAreRetained()
    {
        var primary = new InvalidOperationException("allocation primary");
        var previous = new InvalidOperationException("earlier cleanup");
        var current = Activator.CreateInstance<OutOfMemoryException>();
        primary.Data[SpeciesBufferOwnership.CleanupFailuresKey] = previous;
        using var owner = new SpeciesBufferOwnership(1);
        _ = owner.Acquire(() => new Resource(0, [], current));
        owner.PrepareCleanup(primary);
        owner.Dispose();
        var diagnostics = Assert.IsType<AggregateException>(primary.Data[SpeciesBufferOwnership.CleanupFailuresKey]);
        Assert.Contains(previous, diagnostics.Flatten().InnerExceptions);
        Assert.Contains(current, diagnostics.Flatten().InnerExceptions);
    }

    /// <summary>A full inventory refuses before invoking an untracked allocator.</summary>
    [Fact]
    public void TrackingIsReservedBeforeAllocation()
    {
        var calls = 0;
        using var owner = new SpeciesBufferOwnership(0);
        _ = Assert.Throws<InvalidOperationException>(() => owner.Acquire(() =>
        {
            calls++;
            return new Resource(0, []);
        }));
        Assert.Equal(0, calls);
    }

    /// <summary>The real CPU upload routes every reflected array column through the tested owner.</summary>
    [Fact]
    public void TheActualUploadTracksItsCompleteColumnInventory()
    {
        using var buffers = Cpu.Upload("O2");
        var field = typeof(SpeciesTableBuffers).GetField("_ownership", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var owner = Assert.IsType<SpeciesBufferOwnership>(field.GetValue(buffers));
        Assert.Equal(ColumnCount, owner.Capacity);
        Assert.Equal(ColumnCount, owner.Count);
        var inventory = typeof(SpeciesBufferOwnership).GetField("_resources", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var resources = Assert.IsType<IDisposable?[]>(inventory.GetValue(owner)).OfType<MemoryBuffer>().ToArray();
        Assert.Equal(ColumnCount, resources.Length);
        Assert.All(resources, resource => Assert.False(resource.IsDisposed));
        buffers.Dispose();
        Assert.All(resources, resource => Assert.True(resource.IsDisposed));
    }
}
