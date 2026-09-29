using System.Diagnostics;
using APThermo.Execution.Chunks;
using ILGPU.Runtime.Cuda;

namespace APThermo.Execution;

/// <summary>The loop of a batch, and nothing else: per chunk, upload, launch and synchronise, download, each in its timer scope.</summary>
internal static class BatchRun
{
    /// <summary>
    /// Runs the launch over every chunk of the plan, sizing each chunk from the plan's launch budget when it has one
    /// (BOOT.md, "A launch fits a time budget"; <see cref="ChunkPlan.FirstChunkCases"/>, <see cref="ChunkPlan.NextChunkCases"/>).
    /// The launch takes the number of cases of the chunk. A launch the driver kills for running past a display GPU's
    /// run-time limit (<see cref="CudaError.CUDA_ERROR_LAUNCH_TIMEOUT"/>) becomes <see cref="AcceleratorUnavailableException"/>
    /// naming the limit, the chunk's own case count and the CPU accelerator as the remedy; no ILGPU or CUDA type reaches
    /// the caller. The session records the loss (BOOT.md, the third audit pass's finding 2): its context is sticky from
    /// here on, so every later call on this session, directly or through its engine, refuses instead of touching it again.
    /// </summary>
    public static void Execute(AcceleratorSession session, ChunkPlan plan, ChunkBuffers buffers, RunTimer timer, Action<int> launch)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(buffers);
        ArgumentNullException.ThrowIfNull(timer);
        ArgumentNullException.ThrowIfNull(launch);

        try
        {
            var offset = 0;
            var length = plan.FirstChunkCases(session.Accelerator.MaxNumThreads);
            while (offset < plan.Count)
            {
                length = Math.Min(length, plan.Count - offset);
                using (timer.Uploading())
                {
                    buffers.UploadChunk(offset, length);
                }

                var elapsed = Launch(launch, length, session, timer);

                using (timer.Downloading())
                {
                    buffers.DownloadChunk(offset, length);
                }

                offset += length;
                if (offset < plan.Count)
                {
                    length = plan.NextChunkCases(length, elapsed, offset);
                }
            }
        }
        finally
        {
            DisposeChunkBuffers(session, buffers);
        }
    }

    /// <summary>
    /// Disposes the chunk buffers this loop was given, in the one place of this node that drops a
    /// <see cref="CudaException"/> carrying a lost session's own sticky error (BOOT.md, the third audit pass's finding
    /// 2): ILGPU's own cleanup of a device buffer on a context a launch timeout already killed rethrows that same
    /// error, which would otherwise replace whatever is already propagating (the translated
    /// <see cref="AcceleratorUnavailableException"/>, or nothing at all on an ordinary run). Every other pipeline
    /// disposal this node performs on a possibly lost session — the uploaded tables, and the accelerator and context
    /// below them — reads the same <see cref="AcceleratorSession.DropsAfterLoss"/> decision through its own literal
    /// <c>Dispose()</c> call, since a diagnostic (CA2000) refuses a disposal routed through a shared method instead of
    /// a call visible in the disposing method itself; nowhere in this node drops any other exception.
    /// </summary>
    private static void DisposeChunkBuffers(AcceleratorSession session, ChunkBuffers buffers)
    {
        try
        {
            buffers.Dispose();
        }
        catch (CudaException failure) when (session.DropsAfterLoss(failure))
        {
        }
    }

    /// <summary>The kernel phase of one chunk: launches, synchronises, and reports how long it took. A launch timeout
    /// is translated here, the one place a CUDA-specific exception is ever caught in this loop.</summary>
    private static TimeSpan Launch(Action<int> launch, int length, AcceleratorSession session, RunTimer timer)
    {
        var start = Stopwatch.GetTimestamp();
        try
        {
            launch(length);
            session.Accelerator.Synchronize();
        }
        catch (CudaException failure) when (failure.Error == nameof(CudaError.CUDA_ERROR_LAUNCH_TIMEOUT))
        {
            var timeout = TimeoutFailure(length, failure);
            session.MarkLost(timeout);
            throw timeout;
        }

        var elapsed = Stopwatch.GetElapsedTime(start);
        timer.Add(RunPhase.Kernel, elapsed);
        return elapsed;
    }

    /// <summary>The documented message of a launch the driver killed for its run time (BOOT.md, "A launch fits a time
    /// budget"; <c>API.md</c>'s errors table): names the run-time limit, the chunk's case count and the CPU
    /// accelerator as the remedy.</summary>
    private static AcceleratorUnavailableException TimeoutFailure(int cases, CudaException inner) =>
        new($"the launch of {cases} case(s) exceeded the device's kernel run-time limit " +
            $"({LaunchBudget.DefaultRunTimeLimit.TotalSeconds:0.###} s by default on a display GPU); " +
            "retry on the CPU accelerator, or on a device without a run-time limit (TCC mode, headless).", [], inner);
}
