using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ILGPU;
using ILGPU.Runtime;

namespace APThermo.Execution.Chunks;

/// <summary>
/// One device buffer of a chunk, declared once with its host array, its direction and its per-case stride, so that the stride is
/// written beside the array it belongs to instead of being restated at every transfer.
/// </summary>
internal sealed class ChunkBuffer<T> : IChunkBuffer where T : unmanaged
{
    private const byte Sentinel = 0xFF;

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

    /// <inheritdoc />
    public long ElementsPerCase => _transfer == ChunkTransfer.Constant ? 0 : _perCase;

    private MemoryBuffer1D<T, Stride1D.Dense> Allocated =>
        _buffer ?? throw new InvalidOperationException("the chunk buffers have not been allocated yet.");

    /// <inheritdoc />
    public void Allocate(Accelerator accelerator, int chunk)
    {
        var length = _transfer == ChunkTransfer.Constant ? _host!.LongLength : chunk * _perCase;

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
            var start = Start(offset);
            CheckHostLength(start, span);

            // The span overload pins the slice for the whole transfer; the ref overload does not (BOOT.md, "Host memory crosses into ILGPU pinned").
            Allocated.View.BaseView.SubView(0, span).CopyFromCPU(new ReadOnlySpan<T>(_host!, (int)start, (int)span));
        }
    }

    /// <inheritdoc />
    public void DownloadChunk(int offset, int length)
    {
        var span = Span(length);
        if (_transfer is ChunkTransfer.Output or ChunkTransfer.ClearedOutput && span > 0)
        {
            var start = Start(offset);
            CheckHostLength(start, span);
            var slice = new Span<T>(_host!, (int)start, (int)span);

            // A copy that ran overwrites every byte of the slice with the device's; a slice left as the sentinel was never written (BOOT.md, "A download that wrote nothing is refused").
            MemoryMarshal.AsBytes(slice).Fill(Sentinel);
            Allocated.View.BaseView.SubView(0, span).CopyToCPU(slice);
            if (HoldsOnlySentinel(slice))
            {
                throw new InvalidOperationException(
                    $"a download from the accelerator left its host slice unwritten: {typeof(T).Name}, offset {start}, length {span}.");
            }
        }
    }

    /// <summary>
    /// True when every byte of the slice is the sentinel a download fills it with, so the copy wrote nothing; false for an empty slice.
    /// </summary>
    internal static bool HoldsOnlySentinel(ReadOnlySpan<T> slice) =>
        !slice.IsEmpty && MemoryMarshal.AsBytes(slice).IndexOfAnyExcept(Sentinel) < 0;

    /// <inheritdoc />
    public void Dispose() => _buffer?.Dispose();

    private long Span(int length) => length * _perCase;

    private long Start(int offset) => offset * _perCase;

    /// <summary>
    /// Refuses a chunk slice the host array is too short for (BOOT.md, the second audit's observation 6) and names the
    /// chunk. The span constructors of the copies refuse such a slice too, with an exception that names no chunk. The
    /// <c>ref</c> overloads this check once guarded were unsafe for their address as well as for their bounds, and no
    /// <c>ref</c> into the host array reaches ILGPU any longer (BOOT.md, "Host memory crosses into ILGPU pinned").
    /// </summary>
    private void CheckHostLength(long start, long span)
    {
        if (start + span > _host!.LongLength)
        {
            throw new ArgumentException(
                $"the host array has {_host.LongLength} elements, but the chunk needs [{start}, {start + span}).");
        }
    }
}
