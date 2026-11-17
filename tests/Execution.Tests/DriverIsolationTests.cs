namespace APThermo.Execution.Tests;

/// <summary>Malformed child identity cannot accidentally turn a parent fact into an empty pass.</summary>
public sealed class DriverIsolationTests
{
    /// <summary>Malformed and wrong-mode sentinels fail without starting a process.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("auto|bad|relative")]
    [InlineData("explicit|0123456789abcdef0123456789abcdef|relative")]
    public void InvalidSentinelIsRejected(string sentinel) =>
        _ = Assert.Throws<InvalidOperationException>(() => DriverIsolation.Parse(sentinel, "auto"));

    /// <summary>A generated child sentinel preserves its unique marker identity.</summary>
    [Fact]
    public void ExactSentinelIsParsed()
    {
        var nonce = Guid.NewGuid().ToString("N");
        var marker = Path.Combine(Path.GetTempPath(), "apthermo-cuda-probe-" + nonce, "completed-" + nonce);
        var (actualMarker, actualNonce) = DriverIsolation.Parse("auto|" + nonce + "|" + marker, "auto");
        Assert.Equal(marker, actualMarker);
        Assert.Equal(nonce, actualNonce);
        _ = Assert.Throws<InvalidOperationException>(() => DriverIsolation.Parse("auto|" + nonce + "|" + marker, "explicit"));
        var wrongDirectory = Path.Combine(Path.GetTempPath(), "apthermo-cuda-probe-other", "completed-" + nonce);
        _ = Assert.Throws<InvalidOperationException>(() => DriverIsolation.Parse("auto|" + nonce + "|" + wrongDirectory, "auto"));
    }
}
