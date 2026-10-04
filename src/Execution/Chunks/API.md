# API.md — Execution.Chunks

Namespace `APThermo.Execution.Chunks`. Every type is
`internal`: the audience is `src/Execution`'s own files (the pipelines, `BatchRun`),
not a neighbour or a caller outside the tree. Everything not listed here is internal
to this node itself and may change without notice even to the parent.

## Chunking ✅

```csharp
namespace APThermo.Execution.Chunks;

internal readonly record struct Chunk(int Offset, int Length);

internal sealed class LaunchBudget
{
    public static readonly TimeSpan DefaultRunTimeLimit;   // 2 s: the default Windows WDDM/WSL2 kernel run-time limit

    public static readonly LaunchBudget None;
    public static LaunchBudget FromRunTimeLimit(TimeSpan runTimeLimit);

    public bool IsBounded { get; }
    public int NextChunkCases(int previousCases, TimeSpan previousDuration);
}

internal readonly struct ChunkPlan
{
    public int Count { get; }
    public int Size { get; }
    public LaunchBudget Budget { get; }

    public static ChunkPlan For(int count, long bytesPerCase, long maxElementsPerCase, EngineOptions options);
    public static ChunkPlan For(int count, long bytesPerCase, long maxElementsPerCase, EngineOptions options, LaunchBudget budget);
    public IEnumerable<Chunk> Chunks();
    public int FirstChunkCases(int wave);
    public int NextChunkCases(int previousCases, TimeSpan previousDuration, int coveredSoFar);
}
```

`ChunkPlan.For` takes the batch's case count, the device bytes one case of the
declared buffers costs (`ChunkBuffers.BytesPerCase`, read after every buffer of the
program is declared), the largest per-case element count of any declared buffer
(`ChunkBuffers.MaxElementsPerCase`, 2026-09-26: the audit's F4) and the parent node's
`EngineOptions`; `options.ChunkSize` and `options.ScratchBytes` are assumed positive,
validated by the parent's `Engine.Create` before a plan is ever built. The size is also
capped so that `chunk × maxElementsPerCase` never exceeds `int.MaxValue`, since the
kernels slice a buffer with 32-bit `Index1D` arithmetic. `Chunks()` enumerates the
batch's chunks in order, `Offset + Length` never exceeding `Count`, ignoring `Budget`
(unchanged since 2026-09-15; no caller of `Chunks()` needs a time-bounded chunk today).

**`LaunchBudget` and the two step methods** (2026-09-28, the second audit's Execution
finding F2). The four-argument `For` overload defaults to `LaunchBudget.None`
(unbounded, the pre-existing behaviour); the parent's `AcceleratorChoice` passes the
five-argument overload with the session's own budget, built at bind time from the
device's run-time-limit attribute — `None` for the CPU accelerator or a device
without the limit, `FromRunTimeLimit(LaunchBudget.DefaultRunTimeLimit)` otherwise.
`LaunchBudget` holds no ILGPU type: it is one nullable `TimeSpan` and arithmetic on
`TimeSpan.Ticks`. `FirstChunkCases(wave)` returns `Size` when the plan is unbounded,
or a chunk clamped to one device wave (`Accelerator.MaxNumThreads`, the caller's own
measure of "one launch that surely fits") otherwise, so the very first chunk — with no
measured duration yet to scale from — starts small on a bounded device. `NextChunkCases`
on the plan scales the previous chunk's case count by `LaunchBudget.NextChunkCases`
(the previous chunk's measured time per case against a quarter of the run-time limit,
so three retries stay inside it even if the estimate is off), clamped to `Size` and to
what remains of the batch, and never below one case. `BatchRun` (the parent node's own
file) drives this loop; `Chunks()` is unaffected and stays the choice for a caller with
no timing loop of its own.

## Chunk buffers ✅

```csharp
namespace APThermo.Execution.Chunks;

internal enum ChunkTransfer
{
    Input,
    Output,
    ClearedOutput,
    InputOutput,                                    // 2026-10-04: uploaded before the launch and downloaded after it
    Scratch,
    Constant,
}

internal interface IChunkBuffer : IDisposable
{
    long BytesPerCase { get; }
    long ElementsPerCase { get; }
    void Allocate(Accelerator accelerator, int chunk);
    void UploadChunk(int offset, int length);
    void DownloadChunk(int offset, int length);
}

internal sealed class ChunkBuffer<T> : IChunkBuffer where T : unmanaged
{
    internal ChunkBuffer(T[]? host, long perCase, ChunkTransfer transfer);

    public ArrayView<T> View { get; }
    public long BytesPerCase { get; }
    public long ElementsPerCase { get; }
    public void Allocate(Accelerator accelerator, int chunk);
    public void UploadChunk(int offset, int length);
    public void DownloadChunk(int offset, int length);
    public void Dispose();
}

internal sealed class ChunkBuffers(Accelerator accelerator) : IDisposable
{
    public long BytesPerCase { get; }
    public long MaxElementsPerCase { get; }         // 2026-09-26: the audit's F4, what ChunkPlan.For's offset cap clamps against

    public ChunkBuffer<T> Input<T>(T[] host, long perCase) where T : unmanaged;
    public ChunkBuffer<T> Output<T>(T[] host, long perCase) where T : unmanaged;
    public ChunkBuffer<T> ClearedOutput<T>(T[] host, long perCase) where T : unmanaged;
    public ChunkBuffer<T> InputOutput<T>(T[] host, long perCase) where T : unmanaged;   // 2026-10-04: uploaded before the launch, downloaded after
    public ChunkBuffer<T> Scratch<T>(long perCase) where T : unmanaged;
    public ChunkBuffer<T> Constant<T>(T[] host) where T : unmanaged;

    public void Allocate(int chunk);
    public void UploadChunk(int offset, int length);
    public void DownloadChunk(int offset, int length);
    public void Dispose();
}
```

A pipeline builds one `ChunkBuffers` per run, declares every buffer its program's
kernel needs through the six typed methods above, reads `BytesPerCase` and
`MaxElementsPerCase` into `ChunkPlan.For`, then calls `Allocate(plan.Size)` once; `BatchRun` (the parent node's
own file) drives `UploadChunk`/`DownloadChunk` per chunk and `Dispose` at the end. A
declaration's returned `ChunkBuffer<T>` is where a pipeline reads `.View` to build its
kernel's views struct. `IChunkBuffer` and `ChunkTransfer` exist so `ChunkBuffers` can
hold buffers of different element types in one list and move them uniformly; no code
outside this node names either.

## Unwritten downloads ✅

```csharp
namespace APThermo.Execution.Chunks;

internal sealed class ChunkBuffer<T> : IChunkBuffer where T : unmanaged
{
    // 2026-10-01: true when every element of the slice still holds the sentinel (every byte 0xFF);
    // false for an empty slice. DownloadChunk fills the slice with the sentinel, copies, and throws
    // when this is true (BOOT.md, "A download that wrote nothing is refused").
    internal static bool HoldsOnlySentinel(ReadOnlySpan<T> slice);
}
```

## Side effects

`ChunkBuffer<T>.Allocate` allocates one device buffer through the given `Accelerator`
and, for a `Constant` buffer, uploads its host array immediately. `UploadChunk` and
`DownloadChunk` copy between a chunk's slice of the host array and the device buffer, in both directions for an `InputOutput` buffer (2026-10-04),
or clear the device buffer (`ClearedOutput`, before the launch). Every copy pins the
host array for its duration (2026-10-01). `DownloadChunk` first fills the chunk's host
slice with the sentinel and throws `InvalidOperationException` when the copy left the
whole slice holding it ("a download from the accelerator left its host slice
unwritten", naming the element type, the chunk's offset and length). `Dispose` frees the
device buffer; disposing a `ChunkBuffers` disposes every buffer it declared. Reading
`.View` or any transfer method before `Allocate` throws
`InvalidOperationException`; nothing here retries or degrades an accelerator failure,
it surfaces it.
