using System.Reflection;
using ILGPU;
using ILGPU.Runtime;
using ILGPU.Runtime.CPU;

namespace APThermo.Thermo.Tests;

/// <summary>Exercises the actual upload transaction on a real CPU accelerator.</summary>
public sealed class SpeciesConstructorRollbackTests
{
    private static readonly CpuFixture Cpu = new();

    /// <summary>A second-column copy failure rolls back the first actual allocation without test-owned cleanup.</summary>
    [Fact]
    public void ConstructorRollsBackActualBuffersWhenAColumnCopyFails()
    {
        using var context = Context.Create(builder => builder.CPU());
        using var accelerator = context.CreateCPUAccelerator(0);
        var table = SpeciesTable.Build(Cpu.Database, ["O"], ["O2"]);
        var before = Buffers(accelerator);
        MemoryBuffer[] captured = [];
        AcceleratorStream StreamForColumn(int index)
        {
            if (index == 1)
            {
                captured = [.. Buffers(accelerator).Except(before).Where(buffer => !buffer.IsDisposed)];
                return null!;
            }

            return accelerator.DefaultStream;
        }

        var constructor = typeof(SpeciesTableBuffers).GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance)
            .Single(candidate => candidate.GetParameters().Length == 3);
        try
        {
            var thrown = Assert.Throws<TargetInvocationException>(() => constructor.Invoke([accelerator, table, (Func<int, AcceleratorStream>)StreamForColumn]));
            _ = Assert.IsType<NullReferenceException>(thrown.InnerException);
            Assert.Equal(2, captured.Length);
            Assert.All(captured, buffer => Assert.True(buffer.IsDisposed, $"constructor rollback missed {buffer.GetType().Name}"));
        }
        finally
        {
            foreach (var buffer in captured)
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
