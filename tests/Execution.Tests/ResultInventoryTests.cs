namespace APThermo.Execution.Tests;

/// <summary>Controls generated result-member coverage and explicit backend metadata policy.</summary>
public sealed class ResultInventoryTests
{
    /// <summary>A new numerical array must be explicitly handled before any result can compare.</summary>
    [Fact]
    public void ANewResultArrayCannotBeOmitted()
    {
        var error = Assert.Throws<InvalidOperationException>(() => ResultInventory.Verify(typeof(FutureResult), nameof(FutureResult.Count)));
        Assert.Contains(nameof(FutureResult.Future), error.Message, StringComparison.Ordinal);
    }

    /// <summary>A listed member of an unsupported type is still refused.</summary>
    [Fact]
    public void AnUnsupportedResultTypeCannotBeListedAway()
    {
        var error = Assert.Throws<InvalidOperationException>(() => ResultInventory.Verify(typeof(UnsupportedResult), nameof(UnsupportedResult.Count), nameof(UnsupportedResult.Future)));
        Assert.Contains(nameof(UnsupportedResult.Future), error.Message, StringComparison.Ordinal);
    }

    /// <summary>Known metadata is validated separately and is not required to match across backends.</summary>
    [Fact]
    public void MetadataIsValidatedWithoutRequiringBackendEquality()
    {
        var cpu = new AcceleratorInfo(AcceleratorKind.Cpu, "cpu", "1.5.3", 2);
        var gpu = new AcceleratorInfo(AcceleratorKind.Cuda, "gpu", "1.5.3", 1);
        var timings = new RunTimings(TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero);
        ResultInventory.VerifyMetadata(cpu, timings);
        ResultInventory.VerifyMetadata(gpu, timings);
        var invalid = new AcceleratorInfo(AcceleratorKind.Cpu, "", "1.5.3", 2);
        _ = Assert.Throws<InvalidOperationException>(() => ResultInventory.VerifyMetadata(invalid, timings));
    }

    /// <summary>An added Engine.Run result must have an exact comparison, discovered from signatures.</summary>
    [Fact]
    public void EveryEngineRunResultHasAnExactComparison()
    {
        ResultInventory.VerifyEngine(typeof(Engine), typeof(ExactComparison));
        _ = Assert.Throws<InvalidOperationException>(() => ResultInventory.VerifyEngine(typeof(FutureEngine), typeof(ExactComparison)));
    }

    /// <summary>New metadata fields require a reviewed policy even though their values need not match.</summary>
    [Fact]
    public void ANewMetadataMemberCannotDisappear()
    {
        var error = Assert.Throws<InvalidOperationException>(() =>
            ResultInventory.VerifyMetadataShape(typeof(FutureMetadata), (nameof(FutureMetadata.Known), typeof(int))));
        Assert.Contains(nameof(FutureMetadata.Future), error.Message, StringComparison.Ordinal);
    }

    private sealed class FutureResult
    {
        public int Count { get; } = 1;
        public double[] Future { get; } = [1.0];
        public AcceleratorInfo Accelerator { get; } = null!;
        public RunTimings Timings { get; } = null!;
    }

    private sealed class UnsupportedResult
    {
        public int Count { get; } = 1;
        public string Future { get; } = "unsupported";
        public AcceleratorInfo Accelerator { get; } = null!;
        public RunTimings Timings { get; } = null!;
    }

    private sealed class FutureEngine
    {
        public int Run() => GetHashCode();
    }

    private sealed class FutureMetadata
    {
        public int Known { get; } = 1;
        public string Future { get; } = "unknown";
    }
}
