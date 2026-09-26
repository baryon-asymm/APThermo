using ILGPU.Runtime.Cuda;

namespace APThermo.Execution.Tests;

/// <summary>
/// L0: the post-link's missing-definition guard and its result-to-exception check, both driven directly without a GPU — the
/// guard reads only the wrapper body libnvvm returned, never the kernel PTX, and the result-to-exception check needs no libnvvm
/// or driver call at all (BOOT.md, "No libnvvm or driver result is ignored").
/// </summary>
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

    /// <summary>The success result of either kind throws nothing.</summary>
    [Fact]
    public void TheSuccessResultOfEitherKindThrowsNothing()
    {
        LibDevicePostLink.ThrowIfFailed(NvvmResult.NVVM_SUCCESS, "CompileProgram", "compute_80");
        LibDevicePostLink.ThrowIfFailed(CudaError.CUDA_SUCCESS, "LoadModule", "compute_80");
    }

    /// <summary>Every non-success <see cref="NvvmResult"/> names the post-link, libnvvm, the call, the result and the target.</summary>
    [Theory]
    [MemberData(nameof(NonSuccessNvvmResults))]
    public void EveryNonSuccessNvvmResultNamesTheLibraryTheCallTheResultAndTheTarget(NvvmResult result)
    {
        var failure = Assert.Throws<InvalidOperationException>(() => LibDevicePostLink.ThrowIfFailed(result, "CompileProgram", "compute_80"));
        Assert.Contains("the libdevice post-link", failure.Message, StringComparison.Ordinal);
        Assert.Contains("libnvvm", failure.Message, StringComparison.Ordinal);
        Assert.Contains("CompileProgram", failure.Message, StringComparison.Ordinal);
        Assert.Contains(result.ToString(), failure.Message, StringComparison.Ordinal);
        Assert.Contains("compute_80", failure.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Every non-success <see cref="CudaError"/> names the post-link, the CUDA driver, the call, the result and the target, the
    /// same shape as an <see cref="NvvmResult"/>.
    /// </summary>
    [Theory]
    [MemberData(nameof(NonSuccessCudaErrors))]
    public void EveryNonSuccessCudaErrorNamesTheLibraryTheCallTheResultAndTheTarget(CudaError error)
    {
        var failure = Assert.Throws<InvalidOperationException>(() => LibDevicePostLink.ThrowIfFailed(error, "LoadModule", "compute_80"));
        Assert.Contains("the libdevice post-link", failure.Message, StringComparison.Ordinal);
        Assert.Contains("the CUDA driver", failure.Message, StringComparison.Ordinal);
        Assert.Contains("LoadModule", failure.Message, StringComparison.Ordinal);
        Assert.Contains(error.ToString(), failure.Message, StringComparison.Ordinal);
        Assert.Contains("compute_80", failure.Message, StringComparison.Ordinal);
    }

    /// <summary>A log, where one exists, is trimmed and carried in the message.</summary>
    [Fact]
    public void ALogWhereOneExistsIsCarriedInTheMessage()
    {
        var failure = Assert.Throws<InvalidOperationException>(
            () => LibDevicePostLink.ThrowIfFailed(NvvmResult.NVVM_ERROR_COMPILATION, "CompileProgram", "compute_80", "  a compiler diagnostic line  "));
        Assert.Contains("a compiler diagnostic line", failure.Message, StringComparison.Ordinal);
    }

    /// <summary>Without a log the message still names the post-link, the library, the call, the result and the target.</summary>
    [Fact]
    public void WithoutALogTheMessageStillNamesTheLibraryTheCallTheResultAndTheTarget()
    {
        var failure = Assert.Throws<InvalidOperationException>(
            () => LibDevicePostLink.ThrowIfFailed(CudaError.CUDA_ERROR_INVALID_PTX, "LoadModule", "compute_80"));
        Assert.Contains("the libdevice post-link", failure.Message, StringComparison.Ordinal);
        Assert.Contains("the CUDA driver", failure.Message, StringComparison.Ordinal);
        Assert.Contains("LoadModule", failure.Message, StringComparison.Ordinal);
        Assert.Contains("CUDA_ERROR_INVALID_PTX", failure.Message, StringComparison.Ordinal);
        Assert.Contains("compute_80", failure.Message, StringComparison.Ordinal);
    }

    /// <summary>Every value of <see cref="NvvmResult"/> but the success one, read from the enum rather than typed out by hand.</summary>
    public static TheoryData<NvvmResult> NonSuccessNvvmResults()
    {
        var data = new TheoryData<NvvmResult>();
        foreach (var result in Enum.GetValues<NvvmResult>().Where(result => result != NvvmResult.NVVM_SUCCESS))
        {
            data.Add(result);
        }

        return data;
    }

    /// <summary>Every value of <see cref="CudaError"/> but the success one, read from the enum rather than typed out by hand.</summary>
    public static TheoryData<CudaError> NonSuccessCudaErrors()
    {
        var data = new TheoryData<CudaError>();
        foreach (var error in Enum.GetValues<CudaError>().Where(error => error != CudaError.CUDA_SUCCESS))
        {
            data.Add(error);
        }

        return data;
    }
}
