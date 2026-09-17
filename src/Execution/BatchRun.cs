using APThermo.Execution.Chunks;

namespace APThermo.Execution;

/// <summary>The loop of a batch, and nothing else: per chunk, upload, launch and synchronise, download, each in its timer scope.</summary>
internal static class BatchRun
{
    /// <summary>Runs the launch over every chunk of the plan. The launch takes the number of cases of the chunk.</summary>
    public static void Execute(AcceleratorSession session, ChunkPlan plan, ChunkBuffers buffers, RunTimer timer, Action<int> launch)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(buffers);
        ArgumentNullException.ThrowIfNull(timer);
        ArgumentNullException.ThrowIfNull(launch);
        foreach (var chunk in plan.Chunks())
        {
            using (timer.Uploading())
            {
                buffers.UploadChunk(chunk.Offset, chunk.Length);
            }

            using (timer.Launching())
            {
                launch(chunk.Length);
                session.Accelerator.Synchronize();
            }

            using (timer.Downloading())
            {
                buffers.DownloadChunk(chunk.Offset, chunk.Length);
            }
        }
    }
}
