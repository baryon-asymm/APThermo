using System.Reflection;
using ILGPU;
using ILGPU.Runtime;
using ILGPU.Runtime.CPU;
using IntBuffer = ILGPU.Runtime.MemoryBuffer1D<int, ILGPU.Stride1D.Dense>;

namespace APThermo.Transport.Tests;

/// <summary>Real CPU allocation/copy controls for the production ownership boundary; no GPU or genuine exhaustion.</summary>
public sealed class UploadCopyBoundaryTests
{
    private static readonly FieldInfo ChildBuffers = typeof(Accelerator).GetField("childObjects", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("ILGPU1.5.3 child-buffer observation field missing");

    /// <summary>A failed copy must dispose the current native/wrapper buffers as well as the already owned prefix.</summary>
    [Fact]
    public void ACopyFailureRollsBackTheCurrentAndEarlierRealCpuBuffers()
    {
        using var context = Context.Create(builder => builder.CPU());
        using var accelerator = context.CreateCPUAccelerator(0);
        using var owner = new TransportBufferOwnership();
        using var first = owner.Acquire(() => accelerator.Allocate1D<int>(1));
        var before = Snapshot(accelerator);
        var acquire = Operation<Func<TransportBufferOwnership, Func<IntBuffer>, Action<IntBuffer>, IntBuffer>>("AcquireInitialized", typeof(IntBuffer));
        IntBuffer? current = null;
        MemoryBuffer[] allocated = [];
        Exception? primary = null;
        IntBuffer Allocate()
        {
            current = accelerator.Allocate1D<int>(1);
            allocated = [.. Snapshot(accelerator).Except(before)];
            return current;
        }

        void Initialize(IntBuffer buffer)
        {
            try { buffer.View.CopyFromCPU(null!, [7]); }
            catch (Exception failure) { primary = failure; throw; }
        }

        try
        {
            var actual = Record.Exception(() =>
            {
                _ = owner.Build(tracking => acquire(tracking, Allocate, Initialize));
            });
            _ = Assert.IsType<NullReferenceException>(actual);
            Assert.Same(primary, actual);
            Assert.True(first.IsDisposed);
            Assert.Contains(allocated, buffer => ReferenceEquals(current, buffer));
            Assert.Contains(allocated, buffer => !ReferenceEquals(current, buffer));
            Assert.All(allocated, buffer => Assert.True(buffer.IsDisposed, buffer.GetType().FullName));
            owner.Dispose();
            Assert.All(allocated, buffer => Assert.True(buffer.IsDisposed));
        }
        finally
        {
            current?.Dispose();
            foreach (var buffer in allocated) { buffer.Dispose(); }
        }
    }

    /// <summary>Empty and nonempty int/double columns retain the original values and empty-view semantics.</summary>
    [Fact]
    public void ProductionColumnUploadsPreserveDataAndEmptyViews()
    {
        using var context = Context.Create(builder => builder.CPU());
        using var accelerator = context.CreateCPUAccelerator(0);
        RoundTrip<int>(accelerator, []);
        RoundTrip<double>(accelerator, []);
        RoundTrip<int>(accelerator, [3, 5]);
        RoundTrip<double>(accelerator, [0.25, 2.0]);
    }

    private static void RoundTrip<T>(Accelerator accelerator, T[] values) where T : unmanaged
    {
        using var owner = new TransportBufferOwnership();
        var before = Snapshot(accelerator);
        var upload = Operation<Func<Accelerator, T[], TransportBufferOwnership, ArrayView<T>>>("UploadColumn", typeof(T));
        var view = owner.Build(tracking => upload(accelerator, values, tracking));
        var allocated = Snapshot(accelerator).Except(before).ToArray();
        Assert.Equal(values.LongLength, view.Length);
        for (var index = 0; index < values.Length; index++) { Assert.Equal(values[index], view[index]); }
        if (values.Length == 0)
        {
            Assert.False(view.IsValid);
            Assert.Empty(allocated);
        }

        owner.Dispose();
        Assert.All(allocated, buffer => Assert.True(buffer.IsDisposed));
    }

    private static T Operation<T>(string name, Type argument) where T : Delegate =>
        (typeof(TransportTableBuffers).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Production upload operation {name} missing"))
            .MakeGenericMethod(argument).CreateDelegate<T>();

    private static MemoryBuffer[] Snapshot(Accelerator accelerator) =>
        [.. ((IEnumerable<WeakReference<AcceleratorObject>>)ChildBuffers.GetValue(accelerator)!)
            .Select(reference => reference.TryGetTarget(out var target) ? target : null).OfType<MemoryBuffer>()];
}