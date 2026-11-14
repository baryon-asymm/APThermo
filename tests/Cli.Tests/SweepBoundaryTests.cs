using System.Globalization;
using System.Text.Json;
using APThermo.Cli.Documents;

namespace APThermo.Cli.Tests;

/// <summary>Inclusive list/range bounds and the document product guard without materializing a batch.</summary>
[Collection(CliFixture.CollectionName)]
public sealed class SweepBoundaryTests
{
    /// <summary>Both forms admit exactly the number of values named by the limit.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExactlyTheAxisLimitIsAccepted(bool list)
    {
        using var document = Axis(SweepValues.MaxAxisValues, list);
        var values = SweepValues.Read(document.RootElement, "$.sweep.pressure");
        Assert.Equal(SweepValues.MaxAxisValues, values.Count);
        Assert.Equal(0.0, values[0]);
        Assert.Equal(list ? 0.0 : SweepValues.MaxAxisValues - 1.0, values[^1]);
    }

    /// <summary>A refused axis must not allocate its expanded numeric list or array.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OneMoreThanTheAxisLimitIsRefusedBeforeNumericAllocation(bool list)
    {
        using var document = Axis(SweepValues.MaxAxisValues + 1, list);
        var before = GC.GetAllocatedBytesForCurrentThread();
        var exception = Assert.Throws<InputException>(() => SweepValues.Read(document.RootElement, "$.sweep.pressure"));
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Contains("$.sweep.pressure", exception.Message, StringComparison.Ordinal);
        Assert.Contains(SweepValues.MaxAxisValues.ToString(CultureInfo.InvariantCulture), exception.Message, StringComparison.Ordinal);
        // One byte per forbidden value leaves ample room for messages, while an expanded double array needs eight.
        Assert.True(allocated < SweepValues.MaxAxisValues, $"refusal allocated {allocated} bytes");
    }

    /// <summary>The product boundary is checked by the reader, before any call can expand or solve it.</summary>
    [Theory]
    [InlineData(10, true)]
    [InlineData(11, false)]
    public void TheOverallCaseBoundRemainsInclusive(int ratioCount, bool accepted)
    {
        var text = """
            {"propellant":{"reactants":[{"name":"H2","role":"fuel","amount":1}],"mixture":{"oxidizerToFuel":1}},
             "problem":{"type":"equilibrium","kind":"tp","pressure":100000,"temperature":300},
             "sweep":{"oxidizerToFuel":{"from":1,"to":RATIO_COUNT,"step":1},
                      "pressure":{"from":1,"to":1000,"step":1},"temperature":{"from":1,"to":1000,"step":1}}}
            """.Replace("RATIO_COUNT", ratioCount.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);
        if (accepted)
        {
            var input = ProblemDocumentReader.Read(text, "product-boundary");
            Assert.NotNull(input.Sweep);
            Assert.Equal(SweepDocumentReader.MaxCases,
                (long)input.Sweep.OxidizerToFuel!.Count * input.Sweep.Pressure!.Count * input.Sweep.Temperature!.Count);
        }
        else
        {
            var exception = Assert.Throws<InputException>(() => ProblemDocumentReader.Read(text, "product-boundary"));
            Assert.Contains(SweepDocumentReader.MaxCases.ToString(CultureInfo.InvariantCulture), exception.Message, StringComparison.Ordinal);
        }
    }

    private static JsonDocument Axis(int count, bool list) => JsonDocument.Parse(list
        ? "[" + string.Concat(Enumerable.Repeat("0,", count - 1)) + "0]"
        : """{"from":0,"to":COUNT,"step":1}""".Replace("COUNT", (count - 1).ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal));
}