using APThermo.Harness;

namespace APThermo.Execution.Tests;

/// <summary>New unsupported public fields cannot silently disappear from exact comparisons.</summary>
public sealed class BitsUnsupportedTests
{
    /// <summary>A changed unsupported Boolean is refused rather than overlooked beside a valid double.</summary>
    [Fact]
    public void UnsupportedMemberCannotBeIgnored()
    {
        var error = Assert.Throws<InvalidOperationException>(() => Bits.Differences(
            new ExtendedState(1.0, false), new ExtendedState(1.0, true), "state").ToArray());
        Assert.Contains(nameof(ExtendedState.NewFlag), error.Message, StringComparison.Ordinal);
    }

    /// <summary>Existing supported members still preserve signed zero and integer differences.</summary>
    [Fact]
    public void SupportedMembersAreStillCompared()
    {
        var differences = Bits.Differences(new SupportedState(0.0, 1), new SupportedState(-0.0, 2), "state").ToArray();
        Assert.Equal(2, differences.Length);
    }

    private readonly record struct ExtendedState(double Value, bool NewFlag);
    private readonly record struct SupportedState(double Value, int Count);
}
