using System.Globalization;

namespace APThermo.Execution.Tests;

/// <summary>
/// The per-iteration rule of the throughput tripwire without a GPU (Execution.Tests BOOT.md, 2026-10-04): the limit, the message of a
/// breach and the failure of a record that carries no figure, driven over synthetic records.
/// </summary>
public sealed class ThroughputRecordTests
{
    private const string ApprovedFile = "Throughput.approved.txt";

    private static Dictionary<string, string> Record(double perIteration) =>
        ThroughputRecord.Parse(["ratio: 23.58", ThroughputRecord.Line(ThroughputRecord.PerIterationKey, perIteration)]);

    /// <summary>A kernel time per step inside the limit, above or below the approved one, passes.</summary>
    [Fact]
    public void AKernelTimePerStepInsideTheLimitPasses()
    {
        var approved = Record(8.0e-8);
        Assert.Null(ThroughputRecord.Violation(approved, ApprovedFile, 8.0e-8));
        Assert.Null(ThroughputRecord.Violation(approved, ApprovedFile, 4.0e-8));
        Assert.Null(ThroughputRecord.Violation(approved, ApprovedFile, 8.0e-8 * 1.04));
        Assert.Null(ThroughputRecord.Violation(approved, ApprovedFile, 8.0e-8 * (ThroughputRecord.PerIterationLimit - 0.001)));
    }

    /// <summary>A kernel time per step 25 % above the approved one, the regression of the loop-live local passed by reference, fails and names the file and both figures.</summary>
    [Fact]
    public void AKernelTimePerStepAboveTheLimitFails()
    {
        var message = ThroughputRecord.Violation(Record(8.0e-8), ApprovedFile, 8.0e-8 * 1.25);
        Assert.NotNull(message);
        Assert.Contains(ApprovedFile, message, StringComparison.Ordinal);
        Assert.Contains("125.0", message, StringComparison.Ordinal);
        Assert.Contains("1.000e-07", message, StringComparison.Ordinal);
        Assert.Contains("8.000e-08", message, StringComparison.Ordinal);
    }

    /// <summary>A record without the figure, or with one that is not a positive number, fails with its own message and never skips.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("not a number")]
    [InlineData("0")]
    [InlineData("-1e-8")]
    public void ARecordWithoutAValidFigureFails(string? text)
    {
        var lines = new List<string> { "ratio: 23.58" };
        if (text is not null)
        {
            lines.Add($"{ThroughputRecord.PerIterationKey}: {text}");
        }

        var message = ThroughputRecord.Violation(ThroughputRecord.Parse(lines), ApprovedFile, 1.0e-9);
        Assert.NotNull(message);
        Assert.Contains(ThroughputRecord.PerIterationKey, message, StringComparison.Ordinal);
        Assert.Contains("re-approve", message, StringComparison.Ordinal);
    }

    /// <summary>The line of the figure parses back to the printed digits, in both of the record's formats.</summary>
    [Fact]
    public void ALineParsesBackToItsFigure()
    {
        var parsed = ThroughputRecord.Parse([
            ThroughputRecord.Line(ThroughputRecord.PerIterationKey, 7.857e-8),
            ThroughputRecord.Line(ThroughputRecord.IterationsPerCaseKey, 22.4031)]);
        Assert.Equal(7.857e-8, double.Parse(parsed[ThroughputRecord.PerIterationKey], CultureInfo.InvariantCulture), 12);
        Assert.Equal("22.403", parsed[ThroughputRecord.IterationsPerCaseKey]);
    }
}
