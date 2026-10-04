using ILGPU.Runtime;

namespace APThermo.Execution.Chunks;

/// <summary>
/// The device side of one program's chunk. Every buffer is declared once — its host array, its direction and its per-case stride —
/// and the declarations answer both questions that used to be restated at every call site: what a chunk costs per case, which is
/// what bounds the chunk (<see cref="ChunkPlan"/>), and what moves in and out around a launch.
/// </summary>
internal sealed class ChunkBuffers(Accelerator accelerator) : IDisposable
{
    private readonly List<IChunkBuffer> _buffers = [];

    /// <summary>Device bytes one case of the chunk costs, over every buffer declared.</summary>
    public long BytesPerCase => _buffers.Sum(buffer => buffer.BytesPerCase);

    /// <summary>The largest per-case element count of any buffer declared (BOOT.md, "A chunk's buffers stay within 32-bit
    /// offsets"; the audit's F4): what <see cref="ChunkPlan.For(int, long, long, EngineOptions)"/> caps a chunk against,
    /// beside <see cref="BytesPerCase"/>, so that no buffer's <c>index * perCase</c> kernel offset overflows a 32-bit
    /// <c>Index1D</c>.</summary>
    public long MaxElementsPerCase => _buffers.Count == 0 ? 0 : _buffers.Max(buffer => buffer.ElementsPerCase);

    /// <summary>A buffer filled from its host array before every launch.</summary>
    public ChunkBuffer<T> Input<T>(T[] host, long perCase) where T : unmanaged =>
        Declare(new ChunkBuffer<T>(host, perCase, ChunkTransfer.Input));

    /// <summary>A buffer read back into its host array after every launch.</summary>
    public ChunkBuffer<T> Output<T>(T[] host, long perCase) where T : unmanaged =>
        Declare(new ChunkBuffer<T>(host, perCase, ChunkTransfer.Output));

    /// <summary>An output the kernel writes only for the cases it solved, so it is zeroed before every launch.</summary>
    public ChunkBuffer<T> ClearedOutput<T>(T[] host, long perCase) where T : unmanaged =>
        Declare(new ChunkBuffer<T>(host, perCase, ChunkTransfer.ClearedOutput));

    /// <summary>A buffer the kernel reads and writes: filled from its host array before every launch and read back into it after (2026-10-04).</summary>
    public ChunkBuffer<T> InputOutput<T>(T[] host, long perCase) where T : unmanaged =>
        Declare(new ChunkBuffer<T>(host, perCase, ChunkTransfer.InputOutput));

    /// <summary>Working memory of the kernel: sized per case, never moved.</summary>
    public ChunkBuffer<T> Scratch<T>(long perCase) where T : unmanaged =>
        Declare(new ChunkBuffer<T>(null, perCase, ChunkTransfer.Scratch));

    /// <summary>A buffer that is the same for every case and every chunk, uploaded once at allocation.</summary>
    public ChunkBuffer<T> Constant<T>(T[] host) where T : unmanaged =>
        Declare(new ChunkBuffer<T>(host, 0, ChunkTransfer.Constant));

    /// <summary>Allocates every declared buffer for a chunk of the given size; the views are valid from here on.</summary>
    public void Allocate(int chunk)
    {
        foreach (var buffer in _buffers)
        {
            buffer.Allocate(accelerator, chunk);
        }
    }

    /// <summary>Moves a chunk's inputs in and clears what has to be cleared, in declaration order.</summary>
    public void UploadChunk(int offset, int length)
    {
        foreach (var buffer in _buffers)
        {
            buffer.UploadChunk(offset, length);
        }
    }

    /// <summary>Moves a chunk's outputs out, in declaration order.</summary>
    public void DownloadChunk(int offset, int length)
    {
        foreach (var buffer in _buffers)
        {
            buffer.DownloadChunk(offset, length);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var buffer in _buffers)
        {
            buffer.Dispose();
        }
    }

    private ChunkBuffer<T> Declare<T>(ChunkBuffer<T> buffer) where T : unmanaged
    {
        _buffers.Add(buffer);
        return buffer;
    }
}
