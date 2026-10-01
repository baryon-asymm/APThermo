using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using APThermo.Execution.Chunks;
using ILGPU.Runtime;

namespace APThermo.Execution.Tests;

/// <summary>
/// L0: the transfers of the <c>Chunks</c> node on the CPU accelerator (<c>src/Execution/Chunks/BOOT.md</c>, 2026-10-01, "Host
/// memory crosses into ILGPU pinned" and "A download that wrote nothing is refused"; this node's criterion of that date).
/// </summary>
[Collection(EngineFixture.CollectionName)]
public sealed class ChunkTransferTests
{
    private const int Attempts = 5;
    private const int Unwritten = -1;
    private static readonly int[] Pattern = [11, 22, 33, 44];
    private static readonly TimeSpan WaitLimit = TimeSpan.FromSeconds(20);

    /// <summary>
    /// A download whose host array a compacting collection moves inside the transfer still arrives. ILGPU 1.5.3's copy takes
    /// the host address, then waits for the accelerator's private <c>syncRoot</c> before it copies; the fact holds that lock
    /// from this thread until the downloading thread is blocked on it, forces a compacting collection there and releases
    /// it. A sibling array of the same age, which nothing pins, proves each attempt compacted: it has another address after
    /// the collection than before it. Every wait carries a timeout, so a broken construction fails in seconds. Red once,
    /// 2026-10-01: with the <c>ref</c> overload restored in <c>DownloadChunk</c> and the guard removed, the host array holds
    /// zeros; with the <c>ref</c> overload restored and the guard kept, the downloading thread throws the guard's
    /// <see cref="InvalidOperationException"/>.
    /// </summary>
    [Fact]
    public void AChunkDownloadSurvivesACompactingCollectionInsideItsTransfer()
    {
        var accelerator = EngineFixture.Shared.Cpu.IlgpuAccelerator;
        var syncRoot = typeof(Accelerator).GetField("syncRoot", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(accelerator);
        Assert.True(syncRoot is not null, "ILGPU's Accelerator has no private field 'syncRoot' any longer: the construction of this fact needs revisiting.");

        // The first download of the element type compiles the transfer, so that the downloading thread below reaches the lock at once.
        Assert.Equal(Pattern, Download(accelerator, [.. Pattern], 0, Pattern.Length));

        var outcome = default(Attempt);
        for (var attempt = 0; attempt < Attempts && !outcome.Compacted; attempt++)
        {
            outcome = Attempt.Run(accelerator, syncRoot);
        }

        Assert.True(outcome.Compacted, $"no attempt of {Attempts} saw the collection move the unpinned sibling array, so the fact would prove nothing.");
        Assert.True(outcome.DownloaderBlocked, "the downloading thread never blocked on the accelerator's lock, so the collection ran outside the transfer.");
        Assert.True(outcome.Finished, "the downloading thread did not finish after the lock was released.");
        Assert.Null(outcome.Refusal);
        Assert.Equal(Pattern, outcome.Host);
    }

    /// <summary>
    /// The guard refuses a download that left its slice as the sentinel and nothing else. A device buffer that holds the
    /// sentinel everywhere stands for a copy that wrote nothing: the refusal names the type, the offset and the length. A
    /// slice with one element written passes, and an empty slice is not refused. Red once, 2026-10-01: with the check after
    /// the copy removed, the first assertion fails.
    /// </summary>
    [Fact]
    public void ADownloadThatLeftItsSliceUnwrittenIsRefused()
    {
        var accelerator = EngineFixture.Shared.Cpu.IlgpuAccelerator;
        var device = new int[Pattern.Length];
        Array.Fill(device, Unwritten);

        var refusal = Assert.Throws<InvalidOperationException>(() => Download(accelerator, device, 2, 2));
        Assert.Contains("a download from the accelerator left its host slice unwritten", refusal.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(Int32), refusal.Message, StringComparison.Ordinal);
        Assert.Contains("offset 2", refusal.Message, StringComparison.Ordinal);
        Assert.Contains("length 2", refusal.Message, StringComparison.Ordinal);

        // The slice [2, 4) of the host array receives the device's first two elements.
        device[0] = 5;
        Assert.Equal([0, 0, 5, Unwritten], Download(accelerator, device, 2, 2));

        Assert.Equal([0, 0, 0, 0], Download(accelerator, device, 2, 0));
    }

    /// <summary>
    /// The sentinel test reads every byte of any unmanaged element type: all bytes set is refused, one byte clear is not,
    /// and an empty slice is not (nothing to download).
    /// </summary>
    [Fact]
    public void OnlyASliceOfSentinelBytesHoldsOnlyTheSentinel()
    {
        Assert.True(ChunkBuffer<int>.HoldsOnlySentinel([Unwritten, Unwritten]));
        Assert.True(ChunkBuffer<double>.HoldsOnlySentinel([BitConverter.Int64BitsToDouble(-1L), BitConverter.Int64BitsToDouble(-1L)]));
        Assert.False(ChunkBuffer<int>.HoldsOnlySentinel([Unwritten, 0]));
        Assert.False(ChunkBuffer<double>.HoldsOnlySentinel([BitConverter.Int64BitsToDouble(-1L), double.NaN]));
        Assert.False(ChunkBuffer<int>.HoldsOnlySentinel([]));
    }

    /// <summary>The host array after one download of a chunk whose device buffer was filled with the given values.</summary>
    private static int[] Download(Accelerator accelerator, int[] device, int offset, int length)
    {
        var host = new int[device.Length];
        using var buffers = new ChunkBuffers(accelerator);
        var output = buffers.Output(host, 1);
        buffers.Allocate(device.Length);
        output.View.CopyFromCPU(device);
        buffers.DownloadChunk(offset, length);
        return host;
    }

    /// <summary>The state one attempt of the collection fact ends in.</summary>
    private readonly record struct Attempt(
        bool Compacted, bool DownloaderBlocked, bool Finished, InvalidOperationException? Refusal, int[] Host)
    {
        /// <summary>Allocates a host array and an unpinned sibling beside it, then downloads under a collection forced inside the transfer.</summary>
        internal static Attempt Run(Accelerator accelerator, object? syncRoot)
        {
            AllocateGarbage();

            // The sibling lies below the host: a collection that finds the host pinned cannot slide an array above it down past it.
            var sibling = new int[Pattern.Length];
            var host = new int[Pattern.Length];
            using var buffers = new ChunkBuffers(accelerator);
            var output = buffers.Output(host, 1);
            buffers.Allocate(Pattern.Length);
            output.View.CopyFromCPU(Pattern);

            InvalidOperationException? refusal = null;
            var downloader = new Thread(() =>
            {
                try
                {
                    buffers.DownloadChunk(0, Pattern.Length);
                }
                catch (InvalidOperationException exception)
                {
                    refusal = exception;
                }
            });
            var before = AddressOf(sibling);
            bool blocked;
            lock (syncRoot!)
            {
                downloader.Start();
                blocked = SpinWait.SpinUntil(() => downloader.ThreadState.HasFlag(System.Threading.ThreadState.WaitSleepJoin), WaitLimit);
                GC.Collect(0, GCCollectionMode.Forced, blocking: true, compacting: true);
            }

            var finished = downloader.Join(WaitLimit);
            return new Attempt(before != AddressOf(sibling), blocked, finished, refusal, host);
        }

        /// <summary>Leaves dead arrays below the arrays allocated after them, so that a compacting collection has a gap to close.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void AllocateGarbage()
        {
            for (var index = 0; index < 8; index++)
            {
                GC.KeepAlive(new byte[4096]);
            }
        }

        private static nint AddressOf(int[] array) =>
            Unsafe.ByteOffset(ref Unsafe.NullRef<byte>(), ref Unsafe.As<int, byte>(ref MemoryMarshal.GetArrayDataReference(array)));
    }
}
