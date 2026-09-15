using System.Runtime.CompilerServices;
using ILGPU;
using ILGPU.Runtime;

namespace AerospacePropellantThermodynamics.Execution.Chunks;

/// <summary>
/// One device buffer of a chunk, declared once with its host array, its direction and its per-case stride, so that the stride is
/// written beside the array it belongs to instead of being restated at every transfer.
/// </summary>
internal sealed class ChunkBuffer<T> : IChunkBuffer where T : unmanaged
{
    private readonly T[]? _host;
    private readonly long _perCase;
    private readonly ChunkTransfer _transfer;
    private MemoryBuffer1D<T, Stride1D.Dense>? _buffer;

    internal ChunkBuffer(T[]? host, long perCase, ChunkTransfer transfer)
    {
        _host = host;
        _perCase = perCase;
        _transfer = transfer;
    }

    /// <summary>The device view, for the views struct of the kernel; available once the buffers are allocated.</summary>
    public ArrayView<T> View => Allocated.View;

    /// <inheritdoc />
    public long BytesPerCase => _transfer == ChunkTransfer.Constant ? 0 : _perCase * Unsafe.SizeOf<T>();

    private MemoryBuffer1D<T, Stride1D.Dense> Allocated =>
        _buffer ?? throw new InvalidOperationException("the chunk buffers have not been allocated yet.");

    /// <inheritdoc />
    public void Allocate(Accelerator accelerator, int chunk)
    {
        var length = _transfer == ChunkTransfer.Constant ? _host!.LongLength : (long)chunk * _perCase;

        // ILGPU refuses an empty allocation, and a batch may legitimately have nothing here (a rocket batch without exits).
        _buffer = accelerator.Allocate1D<T>(Math.Max(1L, length));
        if (_transfer == ChunkTransfer.Constant && length > 0)
        {
            _buffer.View.SubView(0, length).CopyFromCPU(_host!);
        }
    }

    /// <inheritdoc />
    public void UploadChunk(int offset, int length)
    {
        if (_transfer == ChunkTransfer.ClearedOutput)
        {
            Allocated.MemSetToZero();
            return;
        }

        var span = Span(length);
        if (_transfer == ChunkTransfer.Input && span > 0)
        {
            Allocated.View.SubView(0, span).CopyFromCPU(ref _host![Start(offset)], span);
        }
    }

    /// <inheritdoc />
    public void DownloadChunk(int offset, int length)
    {
        var span = Span(length);
        if (_transfer is (ChunkTransfer.Output or ChunkTransfer.ClearedOutput) && span > 0)
        {
            Allocated.View.SubView(0, span).CopyToCPU(ref _host![Start(offset)], span);
        }
    }

    /// <inheritdoc />
    public void Dispose() => _buffer?.Dispose();

    private long Span(int length) => length * _perCase;

    private long Start(int offset) => offset * _perCase;
}
