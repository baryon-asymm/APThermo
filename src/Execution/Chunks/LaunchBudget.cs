namespace APThermo.Execution.Chunks;

/// <summary>
/// A GPU that drives a display kills a kernel that runs past the driver's run-time limit, on Windows (WDDM) and under
/// WSL2 alike (BOOT.md, "A launch fits a time budget"; the second audit's Execution finding F2). Built once at bind
/// time from the device's own run-time-limit attribute, which the parent node reads (an ILGPU/CUDA concern this node
/// never touches: this type holds no ILGPU type, only a <see cref="TimeSpan"/>). Given none, a launch has no budget at
/// all — the CPU accelerator, and a CUDA device without the limit (TCC mode, headless), never carry one.
/// </summary>
internal sealed class LaunchBudget
{
    /// <summary>
    /// Windows' default kernel run-time limit for a device that drives a display (the <c>TdrDelay</c> default), in
    /// effect under WSL2 as well, since its GPU access goes through the same driver. The CUDA driver reports only
    /// whether the limit is enabled on a device, never its value, so this is the tree's own conservative figure, not a
    /// value read from the device.
    /// </summary>
    public static readonly TimeSpan DefaultRunTimeLimit = TimeSpan.FromSeconds(2);

    private readonly TimeSpan? _limit;

    private LaunchBudget(TimeSpan? limit)
    {
        _limit = limit;
    }

    /// <summary>No budget: every chunk may be as large as the plan's other bounds allow.</summary>
    public static readonly LaunchBudget None = new(null);

    /// <summary>
    /// A budget of a quarter of <paramref name="runTimeLimit"/>, for a device whose run-time limit is enabled. A
    /// quarter, not the whole limit, leaves headroom for a chunk whose per-case time grows between one launch and the
    /// next (a warm-up spike, a heavier station later in a batch) without crossing the driver's own limit.
    /// </summary>
    public static LaunchBudget FromRunTimeLimit(TimeSpan runTimeLimit) => new(runTimeLimit / 4);

    /// <summary>True when this budget actually bounds a launch's duration.</summary>
    public bool IsBounded => _limit.HasValue;

    /// <summary>
    /// How many cases the next launch may take, from how long the previous chunk's launch of <paramref name="previousCases"/>
    /// cases took (<paramref name="previousDuration"/>): the budget divided by the previous chunk's time per case, never
    /// below one case. With no budget this is never called by <see cref="ChunkPlan"/>; calling it anyway answers as if a
    /// launch could take as many cases as the count allows.
    /// </summary>
    public int NextChunkCases(int previousCases, TimeSpan previousDuration)
    {
        if (_limit is not { } limit)
        {
            return int.MaxValue;
        }

        if (previousCases <= 0 || previousDuration <= TimeSpan.Zero)
        {
            return 1;
        }

        var perCase = previousDuration.Ticks / (double)previousCases;
        var cases = limit.Ticks / perCase;
        return cases >= int.MaxValue ? int.MaxValue : Math.Max(1, (int)cases);
    }
}
