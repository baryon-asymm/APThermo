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
    /// the caller.
    /// </summary>
    public static void Execute(AcceleratorSession session, ChunkPlan plan, ChunkBuffers buffers, RunTimer timer, Action<int> launch)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(buffers);
        ArgumentNullException.ThrowIfNull(timer);
        ArgumentNullException.ThrowIfNull(launch);

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
            throw TimeoutFailure(length, failure);
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
