namespace APThermo.Transport.Tests;

/// <summary>Fault controls exercise upload ownership without provoking a device OOM.</summary>
public sealed class BufferOwnershipTests
{
    private sealed class Resource(bool fails = false) : IDisposable
    {
        internal int Attempts { get; private set; }

        public void Dispose()
        {
            Attempts++;
            if (fails && Attempts == 1)
            {
                throw new InvalidOperationException("controlled cleanup failure");
            }
        }
    }

    /// <summary>Rollback attempts all previously owned buffers and keeps the allocating error primary.</summary>
    [Fact]
    public void RollbackKeepsThePrimaryFailureAndAttemptsEveryResource()
    {
        var ownership = new TransportBufferOwnership();
        using var first = new Resource(fails: true);
        _ = ownership.Acquire(() => first);
        using var second = new Resource(fails: true);
        _ = ownership.Acquire(() => second);
        var allocatingFailure = new InvalidOperationException("controlled allocation failure");
        ownership.Rollback(allocatingFailure);
        Assert.Equal(1, first.Attempts);
        Assert.Equal(1, second.Attempts);
        var cleanup = Assert.IsType<AggregateException>(allocatingFailure.Data["TransportBufferCleanup"]);
        Assert.Equal(2, cleanup.InnerExceptions.Count);
        ownership.Dispose();
        Assert.Equal(1, first.Attempts);
        Assert.Equal(1, second.Attempts);
    }

    /// <summary>Successful ownership and repeat disposal are a positive control.</summary>
    [Fact]
    public void SuccessfulResourcesAreDisposedExactlyOnce()
    {
        var ownership = new TransportBufferOwnership();
        using var resource = new Resource();
        _ = ownership.Acquire(() => resource);
        ownership.Dispose();
        ownership.Dispose();
        Assert.Equal(1, resource.Attempts);
    }
    /// <summary>Every upload view contributes one owned buffer, plus the empty prefix.</summary>
    public static TheoryData<int> AcquisitionPrefixes()
    {
        var count = typeof(TransportTableView).GetFields()
            .Count(f => f.FieldType.IsGenericType && f.FieldType.GetGenericTypeDefinition() == typeof(ILGPU.ArrayView<>));
        var data = new TheoryData<int>();
        foreach (var prefix in Enumerable.Range(0, count + 1))
        {
            data.Add(prefix);
        }

        return data;
    }

    /// <summary>The actual production allocation transaction rolls back any completed prefix.</summary>
    [Theory]
    [MemberData(nameof(AcquisitionPrefixes))]
    public void AFailedTransactionRollsBackEveryOwnedPrefix(int count)
    {
        using var resource = new Resource();
        var ownership = new TransportBufferOwnership();
        var expected = new InvalidOperationException("controlled allocating failure");
        var actual = Assert.Throws<InvalidOperationException>(() => ownership.Build<int>(owner =>
        {
            for (var prefix = 0; prefix < count; prefix++)
            {
                _ = owner.Acquire(() => resource);
            }

            _ = owner.Acquire<Resource>(() => throw expected);
            return 0;
        }));
        Assert.Same(expected, actual);
        Assert.Equal(count, resource.Attempts);
        ownership.Dispose();
        Assert.Equal(count, resource.Attempts);
    }
}