using System.Reflection;
using System.Security.Cryptography;
using APThermo.Execution.Chunks;
using ILGPU.Runtime;

namespace APThermo.Execution.Tests;

/// <summary>Multiple managed cleanup faults cannot mask a run's primary exception or stop remaining owners.</summary>
[Collection(EngineFixture.CollectionName)]
public sealed class BatchCleanupAuditTests
{
    private const string DiagnosticsKey = "APThermo.Execution.CleanupFailures";

    private sealed class Buffer(int index, List<int> attempts, Exception? cleanupFailure = null) : IChunkBuffer
    {
        private bool _disposed;
        private Exception? _failure = cleanupFailure;

        public long BytesPerCase => 0;

        public long ElementsPerCase => 0;

        public bool AppendIdentity(IncrementalHash hash) => false;

        public void Allocate(Accelerator accelerator, int chunk)
        {
        }

        public void UploadChunk(int offset, int length)
        {
        }

        public void DownloadChunk(int offset, int length)
        {
        }

        internal void Disarm() => _failure = null;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            attempts.Add(index);
            if (_failure is { } failure)
            {
                throw failure;
            }
        }
    }

    private static void Add(ChunkBuffers buffers, params IChunkBuffer[] owned)
    {
        var field = typeof(ChunkBuffers).GetField("_buffers", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var declarations = Assert.IsType<List<IChunkBuffer>>(field.GetValue(buffers));
        declarations.AddRange(owned);
        buffers.Allocate(1);
    }

    /// <summary>The already-normalized timeout remains the primary value even when cleanup has two unrelated faults.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void APrimaryTimeoutSurvivesMultipleCleanupFailures(bool outOfMemoryFirst)
    {
        var attempts = new List<int>();
        var ordinary = new InvalidOperationException("injected ordinary child cleanup");
        var outOfMemory = Activator.CreateInstance<OutOfMemoryException>();
        using var first = new Buffer(1, attempts, outOfMemoryFirst ? outOfMemory : ordinary);
        using var second = new Buffer(2, attempts, outOfMemoryFirst ? ordinary : outOfMemory);
        using var third = new Buffer(3, attempts);
        using var cpu = Engine.Create(new EngineOptions { Accelerator = AcceleratorKind.Cpu });
        using var buffers = new ChunkBuffers(cpu.IlgpuAccelerator);
        Add(buffers, first, second, third);
        var primary = new AcceleratorUnavailableException("normalized launch timeout");
        cpu.MarkLost(primary);
        var plan = ChunkPlan.For(1, 0, 0, new EngineOptions { Accelerator = AcceleratorKind.Cpu });
        Exception? observed;
        try
        {
            observed = Record.Exception(() => cpu.RunBatchLoop(plan, buffers, new RunTimer(), _ => throw primary));
        }
        finally
        {
            first.Disarm();
            second.Disarm();
        }

        Assert.Same(primary, observed);
        Assert.Equal([1, 2, 3], attempts);
        var diagnostics = Assert.IsType<AggregateException>(primary.Data[DiagnosticsKey]);
        Assert.Contains(ordinary, diagnostics.Flatten().InnerExceptions);
        Assert.Contains(outOfMemory, diagnostics.Flatten().InnerExceptions);
    }

    /// <summary>Without a primary failure, all independent cleanup diagnostics remain visible.</summary>
    [Fact]
    public void ACompletedRunAttemptsEveryOwnerBeforeReportingCleanupFailures()
    {
        var attempts = new List<int>();
        var ordinary = new InvalidOperationException("injected ordinary child cleanup");
        var outOfMemory = Activator.CreateInstance<OutOfMemoryException>();
        using var first = new Buffer(1, attempts, ordinary);
        using var second = new Buffer(2, attempts, outOfMemory);
        using var third = new Buffer(3, attempts);
        using var cpu = Engine.Create(new EngineOptions { Accelerator = AcceleratorKind.Cpu });
        using var buffers = new ChunkBuffers(cpu.IlgpuAccelerator);
        Add(buffers, first, second, third);
        var plan = ChunkPlan.For(1, 0, 0, new EngineOptions { Accelerator = AcceleratorKind.Cpu });
        Exception? observed;
        try
        {
            observed = Record.Exception(() => cpu.RunBatchLoop(plan, buffers, new RunTimer(), _ => { }));
        }
        finally
        {
            first.Disarm();
            second.Disarm();
        }

        Assert.Equal([1, 2, 3], attempts);
        var diagnostics = Assert.IsType<AggregateException>(observed);
        Assert.Contains(ordinary, diagnostics.Flatten().InnerExceptions);
        Assert.Contains(outOfMemory, diagnostics.Flatten().InnerExceptions);
    }
}
