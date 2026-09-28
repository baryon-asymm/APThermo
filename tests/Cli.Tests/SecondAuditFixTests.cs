using System.Globalization;
using System.Text.Json;
using APThermo.Cli.Documents;

namespace APThermo.Cli.Tests;

/// <summary>
/// L0: the second hidden-defect audit of 2026-09-28 (Data, Problems and Cli, findings F4 to F7 and observation 8),
/// the findings and observations this node's own code answers: a sweep range whose step count is not finite or
/// exceeds the axis limit is refused before it is rounded to an <c>int</c>, so a genuinely false "not an integer"
/// reason never fires (F4); a sweep's Cartesian product is bounded before any solve (F4); a blank
/// <c>--output</c>/<c>--database</c> value is refused whether empty or white space only (F5); a lone UTF-16
/// surrogate in a member name or a string value is refused by path instead of crashing (F6); a reactant
/// propellant's mass refusal carries the document's path like every other refusal (F7); and the <c>species</c>
/// listing's <c>run</c> section records no limit it does not take (observation 8).
/// </summary>
[Collection("cli")]
public sealed class SecondAuditFixTests
{
    /// <summary>
    /// A range whose step count is not finite is refused at the axis, naming the path and the limit, instead of
    /// overflowing the array allocation (finding F4): <c>(to - from) / step</c> here is +Infinity, which used to
    /// pass the old "ends on a step" test (Infinity is not &gt; 1e-9 x Infinity) and then overflow
    /// <c>(int)Math.Round</c> and <c>count + 1</c>, an unhandled <see cref="OverflowException"/>.
    /// </summary>
    [Fact]
    public void ARangeWithANonFiniteStepCountIsRefusedNamingThePathAndTheLimit()
    {
        using var document = JsonDocument.Parse("""{"from": 1.0, "to": 1e308, "step": 1e-300}""");
        var e = Assert.Throws<InputException>(() => SweepValues.Read(document.RootElement, "$.sweep.pressure"));
        Assert.Contains("$.sweep.pressure", e.Message, StringComparison.Ordinal);
        Assert.Contains(SweepValues.MaxAxisValues.ToString(CultureInfo.InvariantCulture), e.Message, StringComparison.Ordinal);
    }

    /// <summary>A range of 2^31-1 steps is refused at the axis limit instead of allocating that many values (finding F4).</summary>
    [Fact]
    public void ARangeOfTwoToTheThirtyOneStepsIsRefusedNamingThePathAndTheLimit()
    {
        using var document = JsonDocument.Parse("""{"from": 1.0, "to": 2147483648.0, "step": 1.0}""");
        var e = Assert.Throws<InputException>(() => SweepValues.Read(document.RootElement, "$.sweep.pressure"));
        Assert.Contains("$.sweep.pressure", e.Message, StringComparison.Ordinal);
        Assert.Contains(SweepValues.MaxAxisValues.ToString(CultureInfo.InvariantCulture), e.Message, StringComparison.Ordinal);
    }

    /// <summary>A range spanning the whole double range with a huge step is refused at the axis limit (finding F4).</summary>
    [Fact]
    public void ARangeSpanningTheWholeDoubleRangeIsRefusedNamingThePathAndTheLimit()
    {
        using var document = JsonDocument.Parse("""{"from": -1e308, "to": 1e308, "step": 1e300}""");
        var e = Assert.Throws<InputException>(() => SweepValues.Read(document.RootElement, "$.sweep.pressure"));
        Assert.Contains("$.sweep.pressure", e.Message, StringComparison.Ordinal);
        Assert.Contains(SweepValues.MaxAxisValues.ToString(CultureInfo.InvariantCulture), e.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 3e9 steps of 1 is a real integer count, but it used to saturate <c>(int)Math.Round</c> to
    /// <see cref="int.MaxValue"/> first and fail the "ends on a step" test with a false reason (finding F4): the
    /// axis limit, checked before that rounding, now refuses it for the true reason, never the false one.
    /// </summary>
    [Fact]
    public void ARangeOfThreeBillionStepsIsRefusedByTheAxisLimitNotAFalseStepReason()
    {
        using var document = JsonDocument.Parse("""{"from": 0.0, "to": 3e9, "step": 1.0}""");
        var e = Assert.Throws<InputException>(() => SweepValues.Read(document.RootElement, "$.sweep.pressure"));
        Assert.Contains("$.sweep.pressure", e.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("is not an integer", e.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// A sweep whose three axes are each within the per-axis limit, but whose Cartesian product (300 x 300 x 300 =
    /// 27 000 000) exceeds the document's case limit, is refused before <c>Sweeps.Expand</c> would materialize it
    /// and before any solve runs (finding F4: "three axes of 2 000 values each would have been materialized as
    /// 8 x 10^9 cases").
    /// </summary>
    [Fact]
    public void ASweepWhoseCartesianProductExceedsTheCaseLimitIsRefusedBeforeAnySolve()
    {
        const string document = """
            {
              "propellant": {
                "reactants": [
                  { "name": "O2(L)", "role": "oxidizer", "amount": 1.0, "temperature": 90.17 },
                  { "name": "H2(L)", "role": "fuel", "amount": 1.0, "temperature": 20.27 }
                ],
                "mixture": { "oxidizerToFuel": 6.0 }
              },
              "problem": { "type": "equilibrium", "kind": "tp", "pressure": 1000000.0, "temperature": 3000.0 },
              "sweep": {
                "oxidizerToFuel": { "from": 1.0, "to": 300.0, "step": 1.0 },
                "pressure": { "from": 100000.0, "to": 3090000.0, "step": 10000.0 },
                "temperature": { "from": 2000.0, "to": 4990.0, "step": 10.0 }
              }
            }
            """;
        var path = CliFixture.Shared.TempFile("sweep-cartesian-too-large.json");
        File.WriteAllText(path, document);
        var run = CliFixture.Invoke(CliFixture.Shared.Solving("equilibrium", path));
        Assert.Equal(2, run.Code);
        Assert.Contains("$.sweep", run.Error, StringComparison.Ordinal);
        Assert.Contains(SweepDocumentReader.MaxCases.ToString(CultureInfo.InvariantCulture), run.Error, StringComparison.Ordinal);
        Assert.Empty(run.Output);
    }

    /// <summary>
    /// A whitespace-only <c>--output</c> value is refused naming the option (finding F5, the fix of 2026-09-26
    /// incomplete): on Windows, <c>File.WriteAllText(" ", …)</c> trims the trailing blanks, finds an empty path and
    /// throws an unhandled <see cref="ArgumentException"/>, exit 3.
    /// </summary>
    [Theory]
    [InlineData(" ")]
    [InlineData("  ")]
    public void AWhitespaceOnlyOutputValueIsExit2NamingTheOption(string value)
    {
        var run = CliFixture.Invoke(["schema", "devices", "--output", value]);
        Assert.Equal(2, run.Code);
        Assert.Contains("--output", run.Error, StringComparison.Ordinal);
        Assert.Empty(run.Output);
    }

    /// <summary>
    /// A lone UTF-16 surrogate in a reactant name is refused naming the string's path, instead of crashing when the
    /// escape is unescaped to a .NET string (finding F6).
    /// </summary>
    [Fact]
    public void ALoneSurrogateInAReactantNameIsExit2NamingThePath()
    {
        const string document = """
            {
              "propellant": {
                "reactants": [
                  { "name": "O2(L)\ud800", "role": "oxidizer", "amount": 1.0, "temperature": 90.17 },
                  { "name": "H2(L)", "role": "fuel", "amount": 1.0, "temperature": 20.27 }
                ],
                "mixture": { "oxidizerToFuel": 6.0 }
              },
              "problem": { "type": "rocket", "chamberPressure": 7000000.0 }
            }
            """;
        var path = CliFixture.Shared.TempFile("surrogate-name.json");
        File.WriteAllText(path, document);
        var run = CliFixture.Invoke(CliFixture.Shared.Solving("rocket", path));
        Assert.Equal(2, run.Code);
        Assert.Contains("not valid UTF-16", run.Error, StringComparison.Ordinal);
        Assert.Contains("$.propellant.reactants[0].name", run.Error, StringComparison.Ordinal);
        Assert.Empty(run.Output);
    }

    /// <summary>A lone UTF-16 surrogate in a states record's composition key is refused naming the map's path (finding F6).</summary>
    [Fact]
    public void ALoneSurrogateInACompositionKeyIsExit2NamingThePath()
    {
        const string record = """{"pressure": 1000000.0, "temperature": 3000.0, "composition": {"H": 200.0, "\udc00": 5.0}}""";
        var path = CliFixture.Shared.TempFile("surrogate-key.jsonl");
        File.WriteAllText(path, record);
        var run = CliFixture.Invoke(CliFixture.Shared.Solving("states", path));
        Assert.Equal(2, run.Code);
        Assert.Contains($"{path}: record 0: a member name at $.composition is not valid UTF-16 text", run.Error, StringComparison.Ordinal);
        Assert.Empty(run.Output);
    }

    /// <summary>
    /// A lone UTF-16 surrogate in an unknown record member's own name is refused naming the record's path, reached
    /// at every object's construction since the duplicate-field check of 2026-09-26 (finding F6, present since the
    /// first version).
    /// </summary>
    [Fact]
    public void ALoneSurrogateInAnUnknownRecordMemberIsExit2NamingThePath()
    {
        const string record = """{"pressure": 1000000.0, "temperature": 3000.0, "composition": {"H": 200.0}, "\udc00x": 1.0}""";
        var path = CliFixture.Shared.TempFile("surrogate-field.jsonl");
        File.WriteAllText(path, record);
        var run = CliFixture.Invoke(CliFixture.Shared.Solving("states", path));
        Assert.Equal(2, run.Code);
        Assert.Contains($"{path}: record 0: a member name at $ is not valid UTF-16 text", run.Error, StringComparison.Ordinal);
        Assert.Empty(run.Output);
    }

    /// <summary>A lone UTF-16 surrogate in an unknown root member's own name is refused naming the root (finding F6).</summary>
    [Fact]
    public void ALoneSurrogateInAnUnknownRootMemberIsExit2NamingThePath()
    {
        const string document = """
            {
              "\ud800": true,
              "propellant": {
                "reactants": [
                  { "name": "O2(L)", "role": "oxidizer", "amount": 1.0, "temperature": 90.17 },
                  { "name": "H2(L)", "role": "fuel", "amount": 1.0, "temperature": 20.27 }
                ],
                "mixture": { "oxidizerToFuel": 6.0 }
              },
              "problem": { "type": "rocket", "chamberPressure": 7000000.0 }
            }
            """;
        var path = CliFixture.Shared.TempFile("surrogate-root.json");
        File.WriteAllText(path, document);
        var run = CliFixture.Invoke(CliFixture.Shared.Solving("rocket", path));
        Assert.Equal(2, run.Code);
        Assert.Contains("not valid UTF-16", run.Error, StringComparison.Ordinal);
        Assert.Contains("$", run.Error, StringComparison.Ordinal);
        Assert.Empty(run.Output);
    }

    /// <summary>
    /// <c>--mass-tolerance</c> against a reactant document is exit 2, as <c>API.md</c> now says instead of "keeps
    /// the default" (finding F7).
    /// </summary>
    [Fact]
    public void MassToleranceAgainstAReactantDocumentIsExit2AsApiNowSays()
    {
        var run = CliFixture.Invoke(CliFixture.Shared.Solving("equilibrium", CliFixture.Document("equilibrium-hp.json"), "--mass-tolerance", "0.01"));
        Assert.Equal(2, run.Code);
        Assert.Contains("--mass-tolerance", run.Error, StringComparison.Ordinal);
        Assert.Empty(run.Output);
    }

    /// <summary>
    /// The <c>species</c> listing's <c>run</c> section records no <c>threshold</c> or <c>massTolerance</c>, options
    /// the command does not take (observation 8): it used to record both options' unused defaults as if the caller
    /// had asked for them.
    /// </summary>
    [Fact]
    public void TheSpeciesListingsRunSectionRecordsNoThresholdOrMassTolerance()
    {
        var run = CliFixture.Invoke(["species", "--database", CliFixture.Shared.DatabasePath]);
        Assert.True(run.Code == 0, $"exit code {run.Code}: {run.Error}");
        using var document = run.Json();
        var section = document.RootElement.GetProperty("run");
        Assert.False(section.TryGetProperty("threshold", out _));
        Assert.False(section.TryGetProperty("massTolerance", out _));
    }
}
