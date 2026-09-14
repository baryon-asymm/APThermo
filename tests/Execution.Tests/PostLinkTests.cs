namespace AerospacePropellantThermodynamics.Execution.Tests;

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

    [Fact]
    public void Every_wrapper_with_a_definition_passes()
    {
        LibDevicePostLink.AssertEveryWrapperDefined(TwoDefinitions, ["__nv_exp", "__nv_log"]);
    }

    [Fact]
    public void A_wrapper_body_with_one_definition_removed_names_that_wrapper()
    {
        var oneDefinition = TwoDefinitions[..TwoDefinitions.IndexOf(OneDefinitionCut, StringComparison.Ordinal)];
        var failure = Assert.Throws<InvalidOperationException>(() => LibDevicePostLink.AssertEveryWrapperDefined(oneDefinition, ["__nv_exp", "__nv_log"]));
        Assert.Contains("__nv_log", failure.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("__nv_exp", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_wrapper_body_with_every_definition_removed_names_every_wrapper()
    {
        var failure = Assert.Throws<InvalidOperationException>(() => LibDevicePostLink.AssertEveryWrapperDefined("no definitions in this text", ["__nv_exp", "__nv_log"]));
        Assert.Contains("__nv_exp", failure.Message, StringComparison.Ordinal);
        Assert.Contains("__nv_log", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_call_site_is_not_mistaken_for_a_definition()
    {
        // The call site spells the name followed by a comma, never a parenthesis (see Wrapper_names_are_read_from_the_ptx_without_the_prefix);
        // a guard that searched the whole linked text rather than the wrapper body alone could be fooled by this line into believing a
        // definition exists (F-EX-05).
        const string callSiteOnly = "call.uni (r), __ilgpu__nv_exp, (a);";
        var failure = Assert.Throws<InvalidOperationException>(() => LibDevicePostLink.AssertEveryWrapperDefined(callSiteOnly, ["__nv_exp"]));
        Assert.Contains("__nv_exp", failure.Message, StringComparison.Ordinal);
    }
}
