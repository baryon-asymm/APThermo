namespace APThermo.Execution.Tests;

/// <summary>Diagnostic compiler metadata uses an already cached kernel and does not confuse PTX declarations with hardware resources.</summary>
[Collection(EngineFixture.CollectionName)]
public sealed class CachedKernelDiagnosticsTests
{
    /// <summary>The public ILGPU retrieval path works on a real cached CPU probe without a second compile or CUDA binding.</summary>
    [Fact]
    public void AnActualCachedKernelCanBeDescribedWithoutRecompiling()
    {
        using var cpu = Engine.Create(new EngineOptions { Accelerator = AcceleratorKind.Cpu });
        _ = cpu.ProbeMath([1.0]);
        var count = cpu.Launchers.Count;
        var launcher = cpu.Launchers.Get<Delegate>(nameof(Kernels.Probe), out var warmUp);
        var lines = CachedKernelDiagnostics.Lines(launcher);
        Assert.Equal(TimeSpan.Zero, warmUp);
        Assert.Equal(count, cpu.Launchers.Count);
        Assert.Contains(lines, line => line.Contains("backend=CPU", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains("not a physical spill", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains("not PTX", StringComparison.Ordinal));
    }

    /// <summary>An ordinary delegate is explicitly unavailable, rather than interpreted as compiler or physical evidence.</summary>
    [Fact]
    public void AnUnrelatedDelegateHasAnExplicitUnavailableResult()
    {
        Action unrelated = () => { };
        var line = Assert.Single(CachedKernelDiagnostics.Lines(unrelated));
        Assert.Contains("unavailable", line, StringComparison.Ordinal);
        Assert.DoesNotContain("localMemoryBytes=", line, StringComparison.Ordinal);
    }

    /// <summary>Only actual .reg declaration lines count; ranges, comments and lookalike directives do not imply physical registers.</summary>
    [Fact]
    public void PtxDeclarationCountIsOnlyADeclarationLineCount()
    {
        const string ptx = ".reg .b32 %r<4096>;\n  .reg .f64 %d<2>;\n// .reg .b64 ignored;\n.register not_a_directive;\n.regular not_a_directive;\n.reg .pred %p;";
        Assert.Equal(3, CachedKernelDiagnostics.RegisterDeclarationLines(ptx));
        var summary = CachedKernelDiagnostics.PtxSummary(ptx);
        Assert.Contains("utf8Bytes=", summary, StringComparison.Ordinal);
        Assert.Contains("sha256=", summary, StringComparison.Ordinal);
        Assert.Contains("virtualRegisterDeclarationLines=3", summary, StringComparison.Ordinal);
        Assert.Contains("not physical registers", summary, StringComparison.Ordinal);
        Assert.DoesNotContain("%r<4096>", summary, StringComparison.Ordinal);
    }
}