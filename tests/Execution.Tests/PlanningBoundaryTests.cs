using APThermo.Execution.Chunks;

namespace APThermo.Execution.Tests;

/// <summary>Host-only controls at the integer and accelerator-choice boundaries.</summary>
public sealed class PlanningBoundaryTests
{
    /// <summary>The last partial chunk advances by its actual length without wrapping.</summary>
    [Fact]
    public void TheLargestCountHasAnExactFiniteChunkCovering()
    {
        var options = new EngineOptions { ChunkSize = int.MaxValue - 1, ScratchBytes = long.MaxValue };
        var chunks = ChunkPlan.For(int.MaxValue, bytesPerCase: 1, maxElementsPerCase: 1, options).Chunks().Take(3).ToArray();
        Assert.Equal([new Chunk(0, int.MaxValue - 1), new Chunk(int.MaxValue - 1, 1)], chunks);
        Assert.Equal(int.MaxValue, chunks.Sum(chunk => (long)chunk.Length));
        Assert.Empty(ChunkPlan.For(0, bytesPerCase: 1, maxElementsPerCase: 1, options).Chunks());
    }

    /// <summary>An undefined selection is an argument failure before any backend decision.</summary>
    /// <param name="value">An undefined underlying enum value.</param>
    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    public void UndefinedAcceleratorKindsAreRejectedBeforeBinding(int value)
    {
        var options = new EngineOptions { Accelerator = (AcceleratorKind)value };
        var refused = Assert.Throws<ArgumentOutOfRangeException>(() => AcceleratorChoice.Decide(options, cudaForbidden: true));
        Assert.Equal("options", refused.ParamName);
        Assert.Equal(options.Accelerator, refused.ActualValue);
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => Engine.Create(options));
    }
}