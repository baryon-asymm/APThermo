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

    /// <summary>A passed row requires a completed and internally consistent TRX summary.</summary>
    [Theory]
    [InlineData("Completed", "0", true)]
    [InlineData("Aborted", "0", false)]
    [InlineData("Completed", "1", false)]
    [InlineData("Completed", "invalid", false)]
    public void ReportRequiresCompletedSummaryAndZeroOtherCounters(string outcome, string errors, bool accepted)
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, $"<TestRun><Results><UnitTestResult testName=\"Example\" outcome=\"Passed\" /></Results><ResultSummary outcome=\"{outcome}\"><Counters total=\"1\" executed=\"1\" passed=\"1\" failed=\"0\" notExecuted=\"0\" error=\"{errors}\" timeout=\"0\" aborted=\"0\" inconclusive=\"0\" passedButRunAborted=\"0\" notRunnable=\"0\" disconnected=\"0\" warning=\"0\" completed=\"0\" inProgress=\"0\" pending=\"0\" /></ResultSummary></TestRun>");
            var error = Record.Exception(() => DriverIsolation.VerifyReport(path, "Example"));
            Assert.Equal(accepted, error is null);
        }
        finally { File.Delete(path); }
    }

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
