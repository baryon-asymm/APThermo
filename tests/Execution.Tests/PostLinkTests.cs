using APThermo.Execution.Ptx;
using ILGPU.Runtime.Cuda;

namespace APThermo.Execution.Tests;

/// <summary>
/// L0: the post-link's text rewrite and its guards, driven directly on PTX text without a GPU (BOOT.md, "The post-link"), and its
/// result-to-exception check, which needs no driver call at all (BOOT.md, "No driver result is ignored"). The text is the shape
/// ILGPU 1.5.3 emits: <c>ArchitectureTests</c> proves on the real kernels, on every architecture, that the rewrite finds what
/// these fragments contain.
/// </summary>
public sealed class PostLinkTests
{
    private const string Header = ".version 8.7\n.target sm_75\n.address_size 64\n\n";

    /// <summary>The declaration ILGPU emits for <c>Math.FusedMultiplyAdd</c>, which has no body.</summary>
    private const string FusedDeclaration =
        ".extern .func (.param .f64 retval0) System_Math_FusedMultiplyAdd_Double(\n" +
        "\t.param .f64 p0,\n\t.param .f64 p1,\n\t.param .f64 p2\n);\n\n";

    /// <summary>One call of it, in the block ILGPU emits: three parameters stored, one call, the result loaded.</summary>
    private const string FusedCall =
        "\t{\n" +
        "\t.param .f64 callParam0;\n" +
        "\tst.param.f64\t[callParam0], %fd1;\n" +
        "\t.param .f64 callParam1;\n" +
        "\tst.param.f64\t[callParam1], %fd2;\n" +
        "\t.param .f64 callParam2;\n" +
        "\tst.param.f64\t[callParam2], %fd3;\n" +
        "\t.param .f64 callRetVal;\n" +
        "\tcall.uni (callRetVal), System_Math_FusedMultiplyAdd_Double, (callParam0, callParam1, callParam2);\n" +
        "\tld.param.f64\t%fd4, [callRetVal];\n" +
        "\t}\n";

    private const string Arithmetic =
        "\tmul.f64\t%fd5, %fd4, %fd1;\n" +
        "\tadd.f64\t%fd6, %fd5, %fd2;\n" +
        "\tsub.f64\t%fd7, %fd6, %fd3;\n" +
        "\tadd.s64\t%rd1, %rd1, 8;\n" +
        "\tmul.lo.s32\t%r1, %r1, %r2;\n";

    private static string Kernel(string body) => Header + FusedDeclaration + ".visible .entry K()\n{\n" + body + "\tret;\n}\n";

    /// <summary>The call of the fused multiply-add becomes the one instruction, with the call's own operands and result.</summary>
    [Fact]
    public void TheFusedMultiplyAddCallBecomesTheInstructionWithItsOwnOperands()
    {
        var rewritten = PtxPostLink.Rewrite(Kernel(FusedCall));
        Assert.Equal(1, rewritten.FusedSites);
        Assert.Contains("fma.rn.f64\t%fd4, %fd1, %fd2, %fd3;", rewritten.Ptx, StringComparison.Ordinal);
        Assert.DoesNotContain("call.uni", rewritten.Ptx, StringComparison.Ordinal);
        Assert.DoesNotContain("callParam", rewritten.Ptx, StringComparison.Ordinal);
    }

    /// <summary>The external declaration of the fused multiply-add goes with its last call.</summary>
    [Fact]
    public void TheExternalDeclarationGoesWithItsLastCall() =>
        Assert.DoesNotContain(".extern", PtxPostLink.Rewrite(Kernel(FusedCall + FusedCall)).Ptx, StringComparison.Ordinal);

    /// <summary>Every multiplication, addition and subtraction of doubles is marked <c>.rn</c>; the integer instructions are left alone.</summary>
    [Fact]
    public void EveryDoubleMultiplyAddAndSubtractIsMarkedRoundToNearest()
    {
        var rewritten = PtxPostLink.Rewrite(Kernel(Arithmetic));
        Assert.Equal(3, rewritten.RoundedOperations);
        Assert.Contains("mul.rn.f64\t%fd5", rewritten.Ptx, StringComparison.Ordinal);
        Assert.Contains("add.rn.f64\t%fd6", rewritten.Ptx, StringComparison.Ordinal);
        Assert.Contains("sub.rn.f64\t%fd7", rewritten.Ptx, StringComparison.Ordinal);
        Assert.Contains("add.s64\t%rd1", rewritten.Ptx, StringComparison.Ordinal);
        Assert.Contains("mul.lo.s32\t%r1", rewritten.Ptx, StringComparison.Ordinal);
        Assert.DoesNotMatch(@"\b(mul|add|sub)\.f64\b", rewritten.Ptx);
    }

    /// <summary>An instruction that already carries a rounding modifier is left as it is and not counted.</summary>
    [Fact]
    public void AnAlreadyMarkedInstructionIsNotMarkedAgain()
    {
        const string marked = "\tmul.rn.f64\t%fd5, %fd4, %fd1;\n\tadd.rz.f64\t%fd6, %fd5, %fd2;\n";
        var rewritten = PtxPostLink.Rewrite(Kernel(marked));
        Assert.Equal(0, rewritten.RoundedOperations);
        Assert.Contains(marked, rewritten.Ptx, StringComparison.Ordinal);
    }

    /// <summary>A fused multiply-add the post-link did not write is refused, whatever its rounding modifier.</summary>
    [Theory]
    [InlineData("fma.rn.f64\t%fd9, %fd1, %fd2, %fd3;")]
    [InlineData("fma.rz.f64\t%fd9, %fd1, %fd2, %fd3;")]
    [InlineData("mad.f64\t%fd9, %fd1, %fd2, %fd3;")]
    [InlineData("mad.rn.f64\t%fd9, %fd1, %fd2, %fd3;")]
    public void AFusedMultiplyAddThePostLinkDidNotWriteIsRefused(string instruction)
    {
        var failure = Assert.Throws<InvalidOperationException>(() => PtxPostLink.Rewrite(Kernel(FusedCall + "\t" + instruction + "\n")));
        Assert.Contains("the PTX post-link for compute_75", failure.Message, StringComparison.Ordinal);
        Assert.Contains("fused multiply-add", failure.Message, StringComparison.Ordinal);
    }

    /// <summary>A fused multiply-add in a kernel that had no call of it is refused: the count of sites inlined is the count allowed.</summary>
    [Fact]
    public void AFusedMultiplyAddInAKernelWithoutAnyCallIsRefused() =>
        Assert.Throws<InvalidOperationException>(() => PtxPostLink.Rewrite(Header + "\tfma.rn.f64\t%fd9, %fd1, %fd2, %fd3;\n"));

    /// <summary>An <c>.approx</c> instruction of doubles is refused: it is not correctly rounded.</summary>
    [Fact]
    public void AnApproximateInstructionOfDoublesIsRefused()
    {
        var failure = Assert.Throws<InvalidOperationException>(() => PtxPostLink.Rewrite(Header + "\trcp.approx.ftz.f64\t%fd9, %fd1;\n"));
        Assert.Contains("approximate", failure.Message, StringComparison.Ordinal);
        Assert.Contains("rcp.approx.ftz.f64", failure.Message, StringComparison.Ordinal);
    }

    /// <summary>An external function is refused: it is declared here and defined elsewhere, so no guard has read it.</summary>
    [Fact]
    public void AnExternalFunctionIsRefused()
    {
        var failure = Assert.Throws<InvalidOperationException>(
            () => PtxPostLink.Rewrite(Header + ".extern .func (.param .f64 r) __nv_exp(.param .f64 a);\n"));
        Assert.Contains("an external function", failure.Message, StringComparison.Ordinal);
        Assert.Contains("compute_75", failure.Message, StringComparison.Ordinal);
    }

    /// <summary>A call of the fused multiply-add the inlining did not recognise keeps its declaration, and the guard refuses the kernel.</summary>
    [Fact]
    public void ACallTheInliningDidNotRecogniseIsRefusedWithItsDeclaration()
    {
        const string unrecognised = "\tcall.uni (%fd8), System_Math_FusedMultiplyAdd_Double, (%fd1, %fd2, %fd3);\n";
        var failure = Assert.Throws<InvalidOperationException>(() => PtxPostLink.Rewrite(Kernel(unrecognised)));
        Assert.Contains("an external function", failure.Message, StringComparison.Ordinal);
    }

    /// <summary>The target of a kernel is read from its own <c>.target</c> line, and a kernel without one is refused.</summary>
    [Fact]
    public void TheTargetIsReadFromTheKernelAndAKernelWithoutOneIsRefused()
    {
        Assert.Equal("compute_120", PtxPostLink.TargetArch(".version 8.7\n.target sm_120\n"));
        _ = Assert.Throws<InvalidOperationException>(() => PtxPostLink.TargetArch(".version 8.7\n"));
    }

    /// <summary>The success result throws nothing.</summary>
    [Fact]
    public void TheSuccessResultThrowsNothing()
    {
        var failure = Record.Exception(() => PtxPostLink.ThrowIfFailed(CudaError.CUDA_SUCCESS, "LoadModule", "compute_80"));
        Assert.Null(failure);
    }

    /// <summary>Every non-success <see cref="CudaError"/> names the post-link, the CUDA driver, the call, the result and the target.</summary>
    [Theory]
    [MemberData(nameof(NonSuccessCudaErrors))]
    public void EveryNonSuccessCudaErrorNamesTheLibraryTheCallTheResultAndTheTarget(CudaError error)
    {
        var failure = Assert.Throws<InvalidOperationException>(() => PtxPostLink.ThrowIfFailed(error, "LoadModule", "compute_80"));
        Assert.Contains("the PTX post-link", failure.Message, StringComparison.Ordinal);
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
            () => PtxPostLink.ThrowIfFailed(CudaError.CUDA_ERROR_INVALID_PTX, "LoadModule", "compute_80", "  a driver diagnostic line  "));
        Assert.Contains("a driver diagnostic line", failure.Message, StringComparison.Ordinal);
    }

    /// <summary>A log padded with NUL characters, as ILGPU's own log buffer returns one (BOOT.md, the audit's observations),
    /// carries no NUL in the message: a plain <see cref="string.Trim()"/> leaves them, since NUL is not whitespace.</summary>
    [Fact]
    public void ALogWithNulPaddingIsTrimmedOfIt()
    {
        var failure = Assert.Throws<InvalidOperationException>(
            () => PtxPostLink.ThrowIfFailed(CudaError.CUDA_ERROR_INVALID_PTX, "LoadModule", "compute_80", "a driver diagnostic\0\0\0\0\0"));
        Assert.Contains("a driver diagnostic", failure.Message, StringComparison.Ordinal);
        Assert.DoesNotContain('\0', failure.Message);
    }

    /// <summary>
    /// A log that trims to nothing (all NUL padding, or whitespace, with no diagnostic text) is treated as no log at all
    /// (2026-09-28, the second audit's observation 7): before the fix, the message still appended ": " and then nothing,
    /// a trailing colon with no diagnostic after it. The message now ends in a plain "." exactly as the no-log case does.
    /// </summary>
    [Fact]
    public void ALogThatTrimsToNothingLeavesNoTrailingColon()
    {
        var withNulOnly = Assert.Throws<InvalidOperationException>(
            () => PtxPostLink.ThrowIfFailed(CudaError.CUDA_ERROR_INVALID_PTX, "LoadModule", "compute_80", "\0\0\0"));
        var withoutLog = Assert.Throws<InvalidOperationException>(
            () => PtxPostLink.ThrowIfFailed(CudaError.CUDA_ERROR_INVALID_PTX, "LoadModule", "compute_80"));
        Assert.Equal(withoutLog.Message, withNulOnly.Message);
        Assert.EndsWith(".", withNulOnly.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(": .", withNulOnly.Message, StringComparison.Ordinal);
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
