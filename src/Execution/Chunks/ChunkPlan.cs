namespace APThermo.Execution.Chunks;

/// <summary>
/// The one rule that decides how many cases a launch takes: the option's chunk size, bounded by the device bytes the option lets a
/// chunk hold, never above the case count and never below one case. Results do not depend on the chunking (BOOT.md, determinism).
/// </summary>
internal readonly struct ChunkPlan
{
    private ChunkPlan(int count, int size, LaunchBudget budget)
    {
        Count = count;
        Size = size;
        Budget = budget;
    }

    /// <summary>Cases (or stations) in the batch.</summary>
    public int Count { get; }

    /// <summary>Cases (or stations) one launch takes, ignoring any launch budget (<see cref="Chunks"/> uses only this;
    /// a budget-bound run instead steps with <see cref="FirstChunkCases"/> and <see cref="NextChunkCases"/>, never above
    /// this bound).</summary>
    public int Size { get; }

    /// <summary>The launch budget this plan was built with (2026-09-28, the second audit's Execution finding F2):
    /// <see cref="LaunchBudget.None"/> unless a budget is given to <see cref="For(int, long, long, EngineOptions, LaunchBudget)"/>.</summary>
    public LaunchBudget Budget { get; }

    /// <summary>The plan for a batch with no launch budget (<see cref="LaunchBudget.None"/>): see the five-argument overload.</summary>
    public static ChunkPlan For(int count, long bytesPerCase, long maxElementsPerCase, EngineOptions options) =>
        For(count, bytesPerCase, maxElementsPerCase, options, LaunchBudget.None);

    /// <summary>
    /// The plan for a batch whose chunk costs the given device bytes per case (every buffer of the chunk,
    /// <see cref="ChunkBuffers.BytesPerCase"/>), capped so that no buffer's largest per-case element count
    /// (<see cref="ChunkBuffers.MaxElementsPerCase"/>) ever multiplies by the chunk size past <see cref="int.MaxValue"/>
    /// (BOOT.md, "A chunk's buffers stay within 32-bit offsets"; the audit's F4): the kernels slice a buffer with
    /// <c>Index1D</c> arithmetic, 32-bit, and a chunk bounded by bytes alone could still overflow it on a huge
    /// <see cref="EngineOptions.ScratchBytes"/> against a table with few, wide buffers. <paramref name="budget"/> is a
    /// fourth bound (2026-09-28, "A launch fits a time budget"), read by <see cref="FirstChunkCases"/> and
    /// <see cref="NextChunkCases"/>, never by <see cref="Size"/> or <see cref="Chunks"/> themselves.
    /// </summary>
    public static ChunkPlan For(int count, long bytesPerCase, long maxElementsPerCase, EngineOptions options, LaunchBudget budget)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(budget);

        // ChunkSize and ScratchBytes are positive (Engine.Create validates them); the clamps keep a chunk of one case
        // when a single case costs more than a bound, and a program with no per-case buffer from dividing by zero.
        var byMemory = Math.Max(1L, options.ScratchBytes / Math.Max(1L, bytesPerCase));
        var byOffset = maxElementsPerCase <= 0 ? long.MaxValue : Math.Max(1L, int.MaxValue / maxElementsPerCase);
        return new ChunkPlan(count, (int)Math.Min(count, Math.Min(options.ChunkSize, Math.Min(byMemory, byOffset))), budget);
    }

    /// <summary>The chunks covering the batch, in order, every one <see cref="Size"/> cases but the last: the static
    /// covering, unaffected by <see cref="Budget"/>. A budget-bound run does not use this; <see cref="BatchRun"/> steps
    /// with <see cref="FirstChunkCases"/> and <see cref="NextChunkCases"/> instead.</summary>
    public IEnumerable<Chunk> Chunks()
    {
        for (var offset = 0; offset < Count; offset += Size)
        {
            yield return new Chunk(offset, Math.Min(Size, Count - offset));
        }
    }

    /// <summary>
    /// How many cases the very first chunk of a run should take: with no budget, <see cref="Size"/> (the case Windows
    /// or Linux runs on Windows-forever, and the pre-existing behaviour); with a budget, one wave of the device
    /// (<paramref name="wave"/> cases — its multiprocessors times the threads each holds at once), never above
    /// <see cref="Size"/> or <see cref="Count"/>.
    /// </summary>
    public int FirstChunkCases(int wave) =>
        Budget.IsBounded ? Math.Clamp(wave, 1, Math.Min(Size, Count)) : Math.Min(Size, Count);

    /// <summary>
    /// How many cases the next chunk should take, given the previous chunk's case count and measured launch duration, and
    /// how many cases of the batch are already covered. With no budget this is <see cref="Size"/> again (or the batch's
    /// remainder, whichever is smaller); with a budget it is the budget's own answer, still never above <see cref="Size"/>
    /// and never above the remainder.
    /// </summary>
    public int NextChunkCases(int previousCases, TimeSpan previousDuration, int coveredSoFar)
    {
        var remainder = Count - coveredSoFar;
        var length = Budget.IsBounded ? Budget.NextChunkCases(previousCases, previousDuration) : Size;
        return Math.Clamp(Math.Min(length, Size), 1, remainder);
    }
}
