namespace APThermo.Execution.Tests;

/// <summary>L0: the post-link's missing-definition guard, driven directly without a GPU — it reads only the wrapper body libnvvm returned, never the kernel PTX.</summary>
public sealed class PostLinkTests
{
    /// <summary>Two definitions, in the shape libnvvm actually returns: the wrapper's name immediately before its parameter list's parenthesis.</summary>
    private const string TwoDefinitions =
        ".visible .func  (.param .b64 func_retval0) __ilgpu__nv_exp(\n" +
        "\t.param .b64 __ilgpu__nv_exp_param_0\n" +
        ")\n" +
        "{\n" +
        "\tret;\n" +
        "}\n" +
        "\n" +
        ".visible .func  (.param .b64 func_retval0) __ilgpu__nv_log(\n" +
        "\t.param .b64 __ilgpu__nv_log_param_0\n" +
        ")\n" +
        "{\n" +
        "\tret;\n" +
        "}\n";

    private const string OneDefinitionCut = ".visible .func  (.param .b64 func_retval0) __ilgpu__nv_log(";

    /// <summary>Every wrapper with a definition passes.</summary>
    [Fact]
    public void EveryWrapperWithADefinitionPasses() =>
        LibDevicePostLink.AssertEveryWrapperDefined(TwoDefinitions, ["__nv_exp", "__nv_log"]);

    /// <summary>A wrapper body with one definition removed names that wrapper.</summary>
    [Fact]
    public void AWrapperBodyWithOneDefinitionRemovedNamesThatWrapper()
    {
        var oneDefinition = TwoDefinitions[..TwoDefinitions.IndexOf(OneDefinitionCut, StringComparison.Ordinal)];
        var failure = Assert.Throws<InvalidOperationException>(() => LibDevicePostLink.AssertEveryWrapperDefined(oneDefinition, ["__nv_exp", "__nv_log"]));
        Assert.Contains("__nv_log", failure.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("__nv_exp", failure.Message, StringComparison.Ordinal);
    }

    /// <summary>A wrapper body with every definition removed names every wrapper.</summary>
    [Fact]
    public void AWrapperBodyWithEveryDefinitionRemovedNamesEveryWrapper()
    {
        var failure = Assert.Throws<InvalidOperationException>(() => LibDevicePostLink.AssertEveryWrapperDefined("no definitions in this text", ["__nv_exp", "__nv_log"]));
        Assert.Contains("__nv_exp", failure.Message, StringComparison.Ordinal);
        Assert.Contains("__nv_log", failure.Message, StringComparison.Ordinal);
    }

    /// <summary>A call site is not mistaken for a definition.</summary>
    [Fact]
    public void ACallSiteIsNotMistakenForADefinition()
    {
        // The call site spells the name followed by a comma, never a parenthesis (see WrapperNamesAreReadFromThePtxWithoutThePrefix);
        // a guard that searched the whole linked text rather than the wrapper body alone could be fooled by this line into believing a
        // definition exists (F-EX-05).
        const string callSiteOnly = "call.uni (r), __ilgpu__nv_exp, (a);";
        var failure = Assert.Throws<InvalidOperationException>(() => LibDevicePostLink.AssertEveryWrapperDefined(callSiteOnly, ["__nv_exp"]));
        Assert.Contains("__nv_exp", failure.Message, StringComparison.Ordinal);
    }
}
