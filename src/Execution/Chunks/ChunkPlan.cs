using APThermo.Execution;

namespace APThermo.Execution.Chunks;

/// <summary>
/// The one rule that decides how many cases a launch takes: the option's chunk size, bounded by the device bytes the option lets a
/// chunk hold, never above the case count and never below one case. Results do not depend on the chunking (BOOT.md, determinism).
/// </summary>
internal readonly struct ChunkPlan
{
    private ChunkPlan(int count, int size)
    {
        Count = count;
        Size = size;
    }

    /// <summary>Cases (or stations) in the batch.</summary>
    public int Count { get; }

    /// <summary>Cases (or stations) one launch takes.</summary>
    public int Size { get; }

    /// <summary>The plan for a batch whose chunk costs the given device bytes per case (every buffer of the chunk, <see cref="ChunkBuffers"/>).</summary>
    public static ChunkPlan For(int count, long bytesPerCase, EngineOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        // ChunkSize and ScratchBytes are positive (Engine.Create validates them); the clamps keep a chunk of one case
        // when a single case costs more than the bound, and a program with no per-case buffer from dividing by zero.
        var byMemory = Math.Max(1L, options.ScratchBytes / Math.Max(1L, bytesPerCase));
        return new ChunkPlan(count, (int)Math.Min(count, Math.Min(options.ChunkSize, byMemory)));
    }

    /// <summary>The chunks covering the batch, in order.</summary>
    public IEnumerable<Chunk> Chunks()
    {
        for (var offset = 0; offset < Count; offset += Size)
        {
            yield return new Chunk(offset, Math.Min(Size, Count - offset));
        }
    }
}
