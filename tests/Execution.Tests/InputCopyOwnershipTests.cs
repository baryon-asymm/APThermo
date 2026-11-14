using System.Reflection;
using ILGPU;
using ILGPU.Runtime;
using ILGPU.Runtime.CPU;

namespace APThermo.Execution.Tests;

/// <summary>A real CPU copy failure cannot hide the latest allocation from rollback.</summary>
public sealed class InputCopyOwnershipTests
{
    /// <summary>The probe input is owned before its host copy can fail.</summary>
    [Fact]
    public void ProbeInputCopyFailureReleasesBothActualCpuBufferLayers()
    {
        using var context = Context.Create(builder => builder.CPU());
        using var accelerator = context.CreateCPUAccelerator(0);
        using var ownership = new ExecutionResourceOwner();
        var before = Buffers(accelerator);
        var method = typeof(Engine).GetMethod("UploadProbeInput", BindingFlags.Static | BindingFlags.NonPublic)!;
        double[] values = [1.0];
        var failure = Assert.Throws<TargetInvocationException>(() => method.Invoke(null, [ownership, accelerator, null, values]));
        _ = Assert.IsType<NullReferenceException>(failure.InnerException);
        var created = Buffers(accelerator).Except(before).Where(buffer => !buffer.IsDisposed).ToArray();
        try
        {
            Assert.Equal(2, created.Length);
            Assert.All(created, buffer => Assert.False(buffer.IsDisposed));
            ownership.PrepareCleanup(failure.InnerException);
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

    private static MemoryBuffer[] Buffers(Accelerator accelerator)
    {
        var field = typeof(Accelerator).GetField("childObjects", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var references = Assert.IsType<IEnumerable<WeakReference<AcceleratorObject>>>(field.GetValue(accelerator), exactMatch: false);
        return [.. references.Select(reference => reference.TryGetTarget(out var target) ? target : null).OfType<MemoryBuffer>()];
    }
}