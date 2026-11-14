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
    public int FirstChunkCases();
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
`TimeSpan.Ticks`. `FirstChunkCases()` returns `Size` when unbounded and one case
when bounded (zero for an empty plan). `NextChunkCases` scales the previous measured
time through the budget estimate, caps growth at twice the previous case count,
then respects `Size` and the remainder, with a one-case floor. CPU/headless plans
retain their static size. `BatchRun` drives the timing loop; `Chunks()` remains the
static covering. One indivisible case may still exceed the runtime limit.

⚠ 2026-11-14: the old wave argument and its claim of a launch that surely fits
were unsupported by measured case duration. A bounded run now samples one case
and grows at most twice per observation; this is a heuristic, not a TDR guarantee.
The unused wave parameter was removed from this internal contract.

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
    internal void DisposePreserving(Exception? primary, Func<Exception, bool>? ignore = null);
}
```

⚠ 2026-11-14: the cleanup signature was also duplicated under `ChunkBuffer<T>`.
The full-signature check identified `ChunkBuffers` as its sole actual owner;
the unsupported duplicate is removed without changing the cleanup contract.

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

⚠ 2026-11-14: disposal formerly stopped at the first failed buffer and could replace
a run's primary failure. Tracking is reserved before allocation, partial allocation
rolls back, and DisposePreserving keeps that primary while recording secondary
errors. Allocation and declaration form one build phase: Allocate is called once,
and declarations after it are refused. Repeated disposal is safe.
