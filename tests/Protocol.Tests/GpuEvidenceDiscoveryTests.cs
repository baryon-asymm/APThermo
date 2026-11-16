using System.Reflection;
using System.Runtime.CompilerServices;

namespace APThermo.Protocol.Tests;

/// <summary>Controls VSTest discovery callbacks without executing CUDA tests.</summary>
public sealed class GpuEvidenceDiscoveryTests
{
    /// <summary>An error-free adapter with no cases is complete.</summary>
    [Fact]
    public void EmptyDiscoveryWithoutErrorRemainsComplete()
    {
        var capture = CreateCapture();
        Complete(capture);
        var ensure = capture.GetType().GetMethod("EnsureComplete")!;
        _ = ensure.Invoke(capture, ["empty-project"]);
    }

    /// <summary>An adapter error cannot be disguised as an empty discovery.</summary>
    [Fact]
    public void AdapterErrorMakesEmptyDiscoveryIncomplete()
    {
        var capture = CreateCapture();
        Complete(capture);
        var log = capture.GetType().GetMethod("HandleLogMessage")!;
        var severity = Enum.Parse(log.GetParameters()[0].ParameterType, "Error");
        _ = log.Invoke(capture, [severity, "controlled adapter failure"]);
        var ensure = capture.GetType().GetMethod("EnsureComplete")!;
        var error = Assert.Throws<TargetInvocationException>(() => ensure.Invoke(capture, ["failed-project"]));
        _ = Assert.IsType<InvalidOperationException>(error.InnerException);
    }

    /// <summary>A missing or repeated completion callback fails closed.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void MissingOrRepeatedCompletionIsIncomplete(int callbacks)
    {
        var capture = CreateCapture();
        for (var index = 0; index < callbacks; index++)
        {
            Complete(capture);
        }

        var ensure = capture.GetType().GetMethod("EnsureComplete")!;
        var error = Assert.Throws<TargetInvocationException>(() => ensure.Invoke(capture, ["failed-project"]));
        _ = Assert.IsType<InvalidOperationException>(error.InnerException);
    }

    private static void Complete(object capture)
    {
        var callback = capture.GetType().GetMethod("HandleDiscoveryComplete")!;
        var arguments = RuntimeHelpers.GetUninitializedObject(callback.GetParameters()[0].ParameterType);
        _ = callback.Invoke(capture, [arguments, null]);
    }

    private static object CreateCapture()
    {
        var assembly = Assembly.Load("APThermo.GpuEvidence");
        var type = assembly.GetType("APThermo.GpuEvidence.Capture", throwOnError: true)!;
        return Activator.CreateInstance(type)!;
    }
}
