using System.Reflection;
using ILGPU;
using ILGPU.Runtime;
using ILGPU.Runtime.CPU;

namespace APThermo.Thermo.Tests;

/// <summary>The production column upload records ownership before copying into a real CPU allocation.</summary>
public sealed class ColumnCopyOwnershipTests
{
    /// <summary>A null explicit stream injects a deterministic copy fault, not an allocation fault.</summary>
    [Fact]
    public void ColumnCopyFailureReleasesBothActualCpuBufferLayers()
    {
        using var context = Context.Create(builder => builder.CPU());
        using var accelerator = context.CreateCPUAccelerator(0);
        using var ownership = new SpeciesBufferOwnership(1);
        var before = Buffers(accelerator);
        var method = typeof(SpeciesTableBuffers).GetMethod("UploadColumn", BindingFlags.Static | BindingFlags.NonPublic)!.MakeGenericMethod(typeof(double));
        double[] values = [1.0];
        var failure = Assert.Throws<TargetInvocationException>(() => method.Invoke(null, [ownership, accelerator, null, values]));
        _ = Assert.IsType<NullReferenceException>(failure.InnerException);
        var created = Buffers(accelerator).Except(before).Where(buffer => !buffer.IsDisposed).ToArray();
        try
        {
            Assert.Equal(2, created.Length);
            Assert.All(created, buffer => Assert.False(buffer.IsDisposed));
            ownership.PrepareCleanup(failure.InnerException!);
            ownership.Dispose();
            Assert.All(created, buffer => Assert.True(buffer.IsDisposed, $"rollback missed {buffer.GetType().Name}"));
        }
        finally
        {
            foreach (var buffer in created)
            {
                buffer.Dispose();
            }
        }
    }

    /// <summary>An empty column retains its zero-length allocation and needs no stream or host copy.</summary>
    [Fact]
    public void EmptyColumnDoesNotAttemptACopy()
    {
        using var context = Context.Create(builder => builder.CPU());
        using var accelerator = context.CreateCPUAccelerator(0);
        using var ownership = new SpeciesBufferOwnership(1);
        var method = typeof(SpeciesTableBuffers).GetMethod("UploadColumn", BindingFlags.Static | BindingFlags.NonPublic)!.MakeGenericMethod(typeof(double));
        using var buffer = Assert.IsType<MemoryBuffer>(method.Invoke(null, [ownership, accelerator, null, Array.Empty<double>()]), exactMatch: false);
        Assert.Equal(0, buffer.Length);
    }

    private static MemoryBuffer[] Buffers(Accelerator accelerator)
    {
        var field = typeof(Accelerator).GetField("childObjects", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var references = Assert.IsType<IEnumerable<WeakReference<AcceleratorObject>>>(field.GetValue(accelerator), exactMatch: false);
        return [.. references.Select(reference => reference.TryGetTarget(out var target) ? target : null).OfType<MemoryBuffer>()];
    }
}