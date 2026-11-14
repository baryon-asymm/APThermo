using System.Diagnostics;
using APThermo.Execution.Chunks;
using ILGPU.Runtime.Cuda;

namespace APThermo.Execution;

/// <summary>The batch loop: upload, launch/synchronise and download each chunk, preserving a primary failure during cleanup.</summary>
internal static class BatchRun
{
    public static void Execute(AcceleratorSession session, ChunkPlan plan, ChunkBuffers buffers, RunTimer timer, Action<int> launch)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(buffers);
        ArgumentNullException.ThrowIfNull(timer);
        ArgumentNullException.ThrowIfNull(launch);

        Exception? primary = null;
        try
        {
            var offset = 0;
            var length = plan.FirstChunkCases();
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
        catch (Exception failure)
        {
            primary = failure;
            throw;
        }
        finally
        {
            buffers.DisposePreserving(primary, session.CleanupFilter);
        }
    }

    /// <summary>Normalizes only a CUDA launch timeout; the lost session refuses subsequent work.</summary>
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

    private static AcceleratorUnavailableException TimeoutFailure(int cases, CudaException inner) =>
        new($"the launch of {cases} case(s) exceeded the device's kernel run-time limit " +
            $"({LaunchBudget.DefaultRunTimeLimit.TotalSeconds:0.###} s by default on a display GPU); " +
            "retry on the CPU accelerator, or on a device without a run-time limit (TCC mode, headless).", inner);
}
