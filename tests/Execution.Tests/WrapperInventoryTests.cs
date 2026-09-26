using APThermo.Fixtures;

namespace APThermo.Execution.Tests;

/// <summary>
/// L0: the post-link's wrapper inventory over ILGPU 1.5.3's own PTX of the probe kernel, driven without a GPU through the two
/// committed fixtures (BOOT.md, Constraints: their provenance). <c>probe.sm_89.ptx</c> is ILGPU's own compile for an
/// architecture it defines every wrapper on; <c>probe.sm_120.ptx</c> is the same kernel for an architecture it defines none on.
/// Neither fixture is an expected value typed into a test: every fact below asserts a relationship the root's math list and the
/// two regimes imply, not a literal wrapper name.
/// </summary>
public sealed class WrapperInventoryTests
{
    private static string Sm89() => ReadFixture("probe.sm_89.ptx");

    private static string Sm120() => ReadFixture("probe.sm_120.ptx");

    private static string ReadFixture(string name) =>
        File.ReadAllText(RepositoryPaths.Resolve("tests", "Execution.Tests", "Ptx", name));

    /// <summary>On SM_89 ILGPU defines every wrapper the kernel calls: nothing is missing.</summary>
    [Fact]
    public void OnSm89EveryCalledWrapperIsAlreadyDefined()
    {
        var ptx = Sm89();
        var called = LibDevicePostLink.WrappersCalled(ptx);
        var defined = LibDevicePostLink.WrappersDefined(ptx);
        Assert.NotEmpty(called);
        Assert.Equal(called.ToHashSet(StringComparer.Ordinal), defined.ToHashSet(StringComparer.Ordinal));
    }

    /// <summary>On SM_120 ILGPU defines none of the wrappers the kernel calls: every one of them is missing.</summary>
    [Fact]
    public void OnSm120NoWrapperIsDefined()
    {
        var ptx = Sm120();
        var called = LibDevicePostLink.WrappersCalled(ptx);
        var defined = LibDevicePostLink.WrappersDefined(ptx);
        Assert.NotEmpty(called);
        Assert.Empty(defined);
    }

    /// <summary>The same kernel calls the same wrappers regardless of the architecture it was compiled for.</summary>
    [Fact]
    public void BothArchitecturesCallTheSameWrappers()
    {
        var called89 = LibDevicePostLink.WrappersCalled(Sm89());
        var called120 = LibDevicePostLink.WrappersCalled(Sm120());
        Assert.Equal(called89.ToHashSet(StringComparer.Ordinal), called120.ToHashSet(StringComparer.Ordinal));
    }

    /// <summary>
    /// No wrapper's own parameter name (<c>__ilgpu__nv_exp_param_0</c>, and for <c>__nv_pow</c> a second parameter declared on a
    /// line of its own ending in a comma, exactly the shape a naive scan could mistake for a call) is read as a call, on the
    /// architecture that actually defines the wrappers.
    /// </summary>
    [Fact]
    public void NoParameterNameIsReadAsACall()
    {
        var called = LibDevicePostLink.WrappersCalled(Sm89());
        Assert.DoesNotContain(called, name => name.Contains("param", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>The inventory reads the same called and defined sets whether the PTX text uses LF or CRLF line ends.</summary>
    [Fact]
    public void TheInventoryIsTheSameWithLfAndCrlfLineEnds()
    {
        var lf = Sm89().ReplaceLineEndings("\n");
        var crlf = lf.ReplaceLineEndings("\r\n");
        Assert.Equal(LibDevicePostLink.WrappersCalled(lf), LibDevicePostLink.WrappersCalled(crlf));
        Assert.Equal(LibDevicePostLink.WrappersDefined(lf), LibDevicePostLink.WrappersDefined(crlf));

        var lf120 = Sm120().ReplaceLineEndings("\n");
        var crlf120 = lf120.ReplaceLineEndings("\r\n");
        Assert.Equal(LibDevicePostLink.WrappersCalled(lf120), LibDevicePostLink.WrappersCalled(crlf120));
        Assert.Equal(LibDevicePostLink.WrappersDefined(lf120), LibDevicePostLink.WrappersDefined(crlf120));
    }
}
