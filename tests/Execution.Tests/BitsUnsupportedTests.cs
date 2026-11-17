using APThermo.Harness;

namespace APThermo.Execution.Tests;

/// <summary>Unsupported public members cannot silently disappear from exact comparisons.</summary>
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

    /// <summary>A public unsupported field is refused independently from properties.</summary>
    [Fact]
    public void UnsupportedPublicFieldCannotBeIgnored()
    {
        var error = Assert.Throws<InvalidOperationException>(() => Bits.Differences(
            new UnsupportedFieldState(1.0, false), new UnsupportedFieldState(1.0, true), "state").ToArray());
        Assert.Contains(nameof(UnsupportedFieldState.NewFlag), error.Message, StringComparison.Ordinal);
    }

    /// <summary>Supported public fields preserve signed zero and integer differences.</summary>
    [Fact]
    public void SupportedPublicFieldsAreStillCompared()
    {
        var differences = Bits.Differences(new SupportedFieldState(0.0, 1), new SupportedFieldState(-0.0, 2), "state").ToArray();
        Assert.Equal(2, differences.Length);
    }

    private readonly record struct ExtendedState(double Value, bool NewFlag);
    private readonly record struct SupportedState(double Value, int Count);

    private struct UnsupportedFieldState(double value, bool newFlag)
    {
        public double Value = value;
        public bool NewFlag = newFlag;
    }

    private struct SupportedFieldState(double value, int count)
    {
        public double Value = value;
        public int Count = count;
    }
}
