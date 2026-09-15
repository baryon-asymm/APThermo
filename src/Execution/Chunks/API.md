# API.md — Execution.Chunks

Namespace `AerospacePropellantThermodynamics.Execution.Chunks`. Every type is
`internal`: the audience is `src/Execution`'s own files (the pipelines, `BatchRun`),
not a neighbour or a caller outside the tree. Everything not listed here is internal
to this node itself and may change without notice even to the parent.

## Chunking ✅

```csharp
namespace AerospacePropellantThermodynamics.Execution.Chunks;

internal readonly record struct Chunk(int Offset, int Length);

internal readonly struct ChunkPlan
{
    public int Count { get; }
    public int Size { get; }

    public static ChunkPlan For(int count, long bytesPerCase, EngineOptions options);
    public IEnumerable<Chunk> Chunks();
}
```

`ChunkPlan.For` takes the batch's case count, the device bytes one case of the
declared buffers costs (`ChunkBuffers.BytesPerCase`, read after every buffer of the
program is declared) and the parent node's `EngineOptions`; `options.ChunkSize` and
`options.ScratchBytes` are assumed positive, validated by the parent's `Engine.Create`
before a plan is ever built. `Chunks()` enumerates the batch's chunks in order,
`Offset + Length` never exceeding `Count`.

## Chunk buffers ✅

```csharp
namespace AerospacePropellantThermodynamics.Execution.Chunks;

internal enum ChunkTransfer
{
    Input,
    Output,
    ClearedOutput,
    Scratch,
    Constant,
}

internal interface IChunkBuffer : IDisposable
{
    long BytesPerCase { get; }
    void Allocate(Accelerator accelerator, int chunk);
    void UploadChunk(int offset, int length);
    void DownloadChunk(int offset, int length);
}

internal sealed class ChunkBuffer<T> : IChunkBuffer where T : unmanaged
{
    internal ChunkBuffer(T[]? host, long perCase, ChunkTransfer transfer);

    public ArrayView<T> View { get; }
    public long BytesPerCase { get; }
    public void Allocate(Accelerator accelerator, int chunk);
    public void UploadChunk(int offset, int length);
    public void DownloadChunk(int offset, int length);
    public void Dispose();
}

internal sealed class ChunkBuffers(Accelerator accelerator) : IDisposable
{
    public long BytesPerCase { get; }

    public ChunkBuffer<T> Input<T>(T[] host, long perCase) where T : unmanaged;
    public ChunkBuffer<T> Output<T>(T[] host, long perCase) where T : unmanaged;
    public ChunkBuffer<T> ClearedOutput<T>(T[] host, long perCase) where T : unmanaged;
    public ChunkBuffer<T> Scratch<T>(long perCase) where T : unmanaged;
    public ChunkBuffer<T> Constant<T>(T[] host) where T : unmanaged;

    public void Allocate(int chunk);
    public void UploadChunk(int offset, int length);
    public void DownloadChunk(int offset, int length);
    public void Dispose();
}
```

A pipeline builds one `ChunkBuffers` per run, declares every buffer its program's
kernel needs through the five typed methods above, reads `BytesPerCase` into
`ChunkPlan.For`, then calls `Allocate(plan.Size)` once; `BatchRun` (the parent node's
own file) drives `UploadChunk`/`DownloadChunk` per chunk and `Dispose` at the end. A
declaration's returned `ChunkBuffer<T>` is where a pipeline reads `.View` to build its
kernel's views struct. `IChunkBuffer` and `ChunkTransfer` exist so `ChunkBuffers` can
hold buffers of different element types in one list and move them uniformly; no code
outside this node names either.

## Side effects

`ChunkBuffer<T>.Allocate` allocates one device buffer through the given `Accelerator`
and, for a `Constant` buffer, uploads its host array immediately. `UploadChunk` and
`DownloadChunk` copy between a chunk's slice of the host array and the device buffer,
or clear the device buffer (`ClearedOutput`, before the launch). `Dispose` frees the
device buffer; disposing a `ChunkBuffers` disposes every buffer it declared. Reading
`.View` or any transfer method before `Allocate` throws
`InvalidOperationException`; nothing here retries or degrades an accelerator failure,
it surfaces it.
