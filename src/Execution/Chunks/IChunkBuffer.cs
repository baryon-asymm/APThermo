using ILGPU.Runtime;

namespace AerospacePropellantThermodynamics.Execution.Chunks;

/// <summary>One device buffer of a chunk, whatever it holds: what it costs per case, and how it moves around a launch.</summary>
internal interface IChunkBuffer : IDisposable
{
    /// <summary>Device bytes one case of a chunk costs in this buffer; zero for a buffer that does not grow with the chunk.</summary>
    long BytesPerCase { get; }

    /// <summary>Allocates the buffer for a chunk of the given size, and uploads it if it is a constant.</summary>
    void Allocate(Accelerator accelerator, int chunk);

    /// <summary>Before the launch: copies the chunk in, or clears it, or neither.</summary>
    void UploadChunk(int offset, int length);

    /// <summary>After the launch: copies the chunk out, or nothing.</summary>
    void DownloadChunk(int offset, int length);
}
