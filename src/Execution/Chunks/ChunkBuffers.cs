using System.Security.Cryptography;
using ILGPU.Runtime;

namespace APThermo.Execution.Chunks;

/// <summary>Declarations and transactional device ownership of one program's chunk.</summary>
internal sealed class ChunkBuffers(Accelerator accelerator) : IDisposable
{
    private readonly List<IChunkBuffer> _buffers = [];
    private ExecutionResourceOwner? _ownership;
    private bool _disposed;

    public long BytesPerCase => _buffers.Sum(buffer => buffer.BytesPerCase);

    public long MaxElementsPerCase => _buffers.Count == 0 ? 0 : _buffers.Max(buffer => buffer.ElementsPerCase);

    public ChunkBuffer<T> Input<T>(T[] host, long perCase) where T : unmanaged =>
        Declare(new ChunkBuffer<T>(host, perCase, ChunkTransfer.Input));

    public ChunkBuffer<T> Output<T>(T[] host, long perCase) where T : unmanaged =>
        Declare(new ChunkBuffer<T>(host, perCase, ChunkTransfer.Output));

    public ChunkBuffer<T> ClearedOutput<T>(T[] host, long perCase) where T : unmanaged =>
        Declare(new ChunkBuffer<T>(host, perCase, ChunkTransfer.ClearedOutput));

    public ChunkBuffer<T> InputOutput<T>(T[] host, long perCase) where T : unmanaged =>
        Declare(new ChunkBuffer<T>(host, perCase, ChunkTransfer.InputOutput));

    public ChunkBuffer<T> Scratch<T>(long perCase) where T : unmanaged =>
        Declare(new ChunkBuffer<T>(null, perCase, ChunkTransfer.Scratch));

    public ChunkBuffer<T> Constant<T>(T[] host) where T : unmanaged =>
        Declare(new ChunkBuffer<T>(host, 0, ChunkTransfer.Constant));

    /// <summary>Hash the complete initial declaration inventory before any allocation or upload; unsupported declarations disable reuse.</summary>
    public string? InputIdentity()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_ownership is not null) { throw new InvalidOperationException("buffer identity must be captured before allocation"); }
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        BufferIdentityFrame.Text(hash, "APThermo.ChunkBuffers.InputIdentity.v1");
        BufferIdentityFrame.Integer(hash, _buffers.Count);
        foreach (var buffer in _buffers)
        {
            if (!buffer.AppendIdentity(hash)) { return null; }
        }

        return Convert.ToHexString(hash.GetHashAndReset());
    }

    /// <summary>Registers the declared owners before allocation; partial allocation rolls the whole group back.</summary>
    public void Allocate(int chunk)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_ownership is not null)
        {
            throw new InvalidOperationException("the chunk buffers have already been allocated");
        }

        var ownership = new ExecutionResourceOwner();
        _ownership = ownership;
        try
        {
            foreach (var buffer in _buffers)
            {
                ownership.Own(buffer);
            }

            foreach (var buffer in _buffers)
            {
                buffer.Allocate(accelerator, chunk);
            }
        }
        catch (Exception primary)
        {
            DisposePreserving(primary);
            throw;
        }
    }

    public void UploadChunk(int offset, int length)
    {
        foreach (var buffer in _buffers)
        {
            buffer.UploadChunk(offset, length);
        }
    }

    public void DownloadChunk(int offset, int length)
    {
        foreach (var buffer in _buffers)
        {
            buffer.DownloadChunk(offset, length);
        }
    }

    public void Dispose() => DisposePreserving(null);

    /// <summary>The batch loop supplies a propagating primary and the session's preallocated sticky-error filter.</summary>
    internal void DisposePreserving(Exception? primary, Func<Exception, bool>? ignore = null)
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _ownership?.PrepareCleanup(primary, ignore);
        _ownership?.Dispose();
    }

    private ChunkBuffer<T> Declare<T>(ChunkBuffer<T> buffer) where T : unmanaged
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_ownership is not null)
        {
            throw new InvalidOperationException("cannot declare a buffer after allocation");
        }

        _buffers.Add(buffer);
        return buffer;
    }
}
