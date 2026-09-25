using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using APThermo.Cli.Documents;
using APThermo.Fixtures;
using APThermo.Harness;

namespace APThermo.Cli.Tests;

/// <summary>L0: the strict readers of the input documents, their messages, and the examples of the API document.</summary>
[Collection("cli")]
public sealed class InputDocumentTests
{
    /// <summary>
    /// Relative slack on a mass read back from a message: the message rounds it to about 7 significant figures
    /// (observed: 2000.03, 1.000015, 1), so a full-precision, independently derived mass matches it only up to
    /// around 1e-7 relative; this is looser by a decade, far below any deviation worth catching.
    /// </summary>
    private const double GramsTolerance = 1e-6;

    /// <summary>The fragment every invalid document of documents/invalid must produce, save the four that report a mass (below); a document in neither dictionary fails the theory.</summary>
    private static readonly Dictionary<string, string> Expected = new(StringComparer.Ordinal)
    {
        ["malformed.json"] = "malformed JSON",
        ["states-malformed.json"] = "malformed JSON",
        ["unknown-field.json"] = "unknown field 'expansionRatio' at $.problem",
        ["missing-field.json"] = "missing field 'chamberPressure' at $.problem",
        ["wrong-unit.json"] = "unknown field 'chamberPressureBar' at $.problem",
        ["two-targets.json"] = "'enthalpy' at $.problem does not belong to a tp problem",
        ["empty-only.json"] = "'only' at $.propellant is empty",
        ["bad-range.json"] = "does not end on a step",
        ["unknown-reactant.json"] = "'H2(LIQUID)' is not in the database",
        ["temperature-out-of-range.json"] = "'O2(L)': temperature 150 K is outside",
        ["ratio-sweep-without-ratio.json"] = "sweep over oxidizerToFuel",
        ["states-two-targets.json"] = "exactly one of enthalpy, temperature and entropy",
        ["states-unknown-field.json"] = "unknown field 'pressureBar'",
        ["states-rocket-without-enthalpy.json"] = "a record with exits needs an enthalpy",
    };

    /// <summary>
    /// The four documents whose message reports a mass: the fixed text before the number, and the composition (read
    /// from the document itself) the grams are derived from (<see cref="CliFixture.GramsOf"/>), never typed (the
    /// review's F-TF-12 found "2000.03 g" typed in two files). The number itself is compared numerically
    /// (<see cref="AssertMassReported"/>), since summing element moles in another order than the library's own can
    /// move its last digit without moving its value.
    /// </summary>
    private static readonly Dictionary<string, string> MassMessagePrefix = new(StringComparer.Ordinal)
    {
        ["states-two-kilograms.json"] = "record 0: the composition weighs ",
        ["states-mol-per-gram.json"] = "record 0: the composition weighs ",
        ["states-kmol-per-kg.json"] = "record 0: the composition weighs ",
        ["elemental-two-kilograms.json"] = "$.propellant.elementMoles: the composition weighs ",
    };

    /// <summary>Invalid.</summary>
    public static TheoryData<string> Invalid() => [.. CliFixture.InvalidDocumentNames()];

    /// <summary>Problems.</summary>
    public static TheoryData<string> Problems() => [.. CliFixture.ProblemDocumentNames()];

    /// <summary>The `composition` of the first record of an invalid states document, or the `propellant.elementMoles` of an invalid problem document.</summary>
    private static IReadOnlyDictionary<string, double> CompositionOf(string name)
    {
        var document = JsonNode.Parse(File.ReadAllText(CliFixture.Document(Path.Combine("invalid", name))))!;
        return CliFixture.CompositionOf(name.StartsWith("states", StringComparison.Ordinal) ? document[0]!["composition"]! : document["propellant"]!["elementMoles"]!);
    }

    /// <summary>The message names the composition by <paramref name="prefix"/> and reports its mass, the library's, with the database's atomic weights.</summary>
    private static void AssertMassReported(string prefix, string name, string error)
    {
        Assert.Contains(prefix, error);
        var start = error.IndexOf(prefix, StringComparison.Ordinal) + prefix.Length;
        var end = error.IndexOf(" g", start, StringComparison.Ordinal);
        Assert.True(end > start, $"no ' g' after '{prefix}' in: {error}");
        var reported = double.Parse(error[start..end], CultureInfo.InvariantCulture);
        var expected = CliFixture.Shared.GramsOf(CompositionOf(name));
        Assert.True(Math.Abs(reported - expected) <= GramsTolerance * Math.Max(1.0, Math.Abs(expected)), $"reported {reported:R} g, derived {expected:R} g");
    }

    /// <summary>An invalid document is exit 2 with the documented message and no output.</summary>
    [Theory]
    [MemberData(nameof(Invalid))]
    public void AnInvalidDocumentIsExit2WithTheDocumentedMessageAndNoOutput(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        var path = CliFixture.Document(Path.Combine("invalid", name));
        var command = name.StartsWith("states", StringComparison.Ordinal) ? "states" : "rocket";
        var output = CliFixture.Shared.TempFile(name + ".out.json");
        var run = CliFixture.Invoke(CliFixture.Shared.Solving(command, path, "--output", output));
        Assert.Equal(2, run.Code);
        if (Expected.TryGetValue(name, out var fragment))
        {
            Assert.Contains(fragment, run.Error);
        }
        else if (MassMessagePrefix.TryGetValue(name, out var prefix))
        {
            AssertMassReported(prefix, name, run.Error);
        }
        else
        {
            Assert.Fail($"no expected message recorded for {name}");
        }

        Assert.Empty(run.Output);
        Assert.False(File.Exists(output), "a document was written for an invalid input");
    }

    /// <summary>Every example document validates against the input schema.</summary>
    [Theory]
    [MemberData(nameof(Problems))]
    public void EveryExampleDocumentValidatesAgainstTheInputSchema(string name)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(CliFixture.Document(name)));
        var errors = JsonSchema.Parse(CliFixture.SchemaText("input")).Validate(document.RootElement);
        Assert.True(errors.Count == 0, string.Join("; ", errors));
    }

    /// <summary>Every states document validates against the states schema.</summary>
    [Fact]
    public void EveryStatesDocumentValidatesAgainstTheStatesSchema()
    {
        var schema = JsonSchema.Parse(CliFixture.SchemaText("states"));
        foreach (var name in new[] { "states.json", "states-part1.json", "states-part2.json", "states-ap-al-record.json" })
        {
            using var document = JsonDocument.Parse(File.ReadAllText(CliFixture.Document(name)));
            var errors = schema.Validate(document.RootElement);
            Assert.True(errors.Count == 0, $"{name}: {string.Join("; ", errors)}");
        }

        foreach (var line in File.ReadAllLines(CliFixture.Document("states.jsonl")).Where(l => l.Trim().Length > 0))
        {
            using var record = JsonDocument.Parse(line);
            Assert.Empty(schema.Validate(record.RootElement));
        }
    }

    /// <summary>Every example of the api document is read or validates and its records solve.</summary>
    [Fact]
    public void EveryExampleOfTheApiDocumentIsReadOrValidatesAndItsRecordsSolve()
    {
        var api = File.ReadAllText(RepositoryPaths.Resolve("src", "Cli", "API.md"));
        var inputs = CliFixture.JsonFencesOf(api, "## Input document ✅");
        Assert.NotEmpty(inputs);
        var inputSchema = JsonSchema.Parse(CliFixture.SchemaText("input"));
        for (var i = 0; i < inputs.Count; i++)
        {
            _ = ProblemDocumentReader.Read(inputs[i], $"API.md input example {i}");
            using var document = JsonDocument.Parse(inputs[i]);
            Assert.Empty(inputSchema.Validate(document.RootElement));
        }

        var records = CliFixture.JsonFencesOf(api, "## Command line ✅");
        Assert.NotEmpty(records);
        for (var i = 0; i < records.Count; i++)
        {
            Assert.NotEmpty(StateRecordReader.Read([($"API.md record example {i}", records[i])]));

            // An example record is a real record: it solves, and in particular it weighs one kilogram (2026-09-13: the earlier example did not).
            var path = CliFixture.Shared.TempFile($"api-record-{i}.json");
            File.WriteAllText(path, records[i]);
            var run = CliFixture.Invoke(CliFixture.Shared.Solving("states", path));
            Assert.True(run.Code == 0, $"API.md record example {i}: exit code {run.Code}: {run.Error}");
        }

        var outputs = CliFixture.JsonFencesOf(api, "## Output document ✅");
        var output = Assert.Single(outputs);
        using var example = JsonDocument.Parse(output);
        var errors = JsonSchema.Parse(CliFixture.SchemaText("output")).Validate(example.RootElement);
        Assert.True(errors.Count == 0, string.Join("; ", errors));
    }

    /// <summary>State records may come as an array a single object or lines.</summary>
    [Fact]
    public void StateRecordsMayComeAsAnArrayASingleObjectOrLines()
    {
        var fromArray = StateRecordReader.Read([("states.json", File.ReadAllText(CliFixture.Document("states.json")))]);
        var fromLines = StateRecordReader.Read([("states.jsonl", File.ReadAllText(CliFixture.Document("states.jsonl")))]);
        var fromFiles = StateRecordReader.Read([
            ("states-part1.json", File.ReadAllText(CliFixture.Document("states-part1.json"))),
            ("states-part2.json", File.ReadAllText(CliFixture.Document("states-part2.json"))),
        ]);
        Assert.Equal(3, fromArray.Count);
        foreach (var other in new[] { fromLines, fromFiles })
        {
            Assert.Equal(fromArray.Select(r => r.Record.Pressure), other.Select(r => r.Record.Pressure));
            Assert.Equal(fromArray.Select(r => r.Record.Enthalpy), other.Select(r => r.Record.Enthalpy));
            Assert.Equal(fromArray.Select(r => r.Source.Index), other.Select(r => r.Source.Index));
        }

        Assert.True(fromArray[2].Record.HasExits);
        Assert.False(fromArray[0].Record.HasExits);
    }

    /// <summary>A range expands inclusively and lands on its end.</summary>
    [Fact]
    public void ARangeExpandsInclusivelyAndLandsOnItsEnd()
    {
        using var whole = JsonDocument.Parse("""{ "from": 4.0, "to": 7.0, "step": 1.0 }""");
        Assert.Equal([4.0, 5.0, 6.0, 7.0], SweepValues.Read(whole.RootElement, "$.sweep.oxidizerToFuel"));
        using var fine = JsonDocument.Parse("""{ "from": 4.0, "to": 8.0, "step": 0.25 }""");
        var values = SweepValues.Read(fine.RootElement, "$.sweep.oxidizerToFuel");
        Assert.Equal(17, values.Count);
        Assert.Equal(8.0, values[^1]);
        using var list = JsonDocument.Parse("[5.0e6, 7.0e6]");
        Assert.Equal([5.0e6, 7.0e6], SweepValues.Read(list.RootElement, "$.sweep.chamberPressure"));
        using var empty = JsonDocument.Parse("[]");
        _ = Assert.Throws<InputException>(() => SweepValues.Read(empty.RootElement, "$.sweep.chamberPressure"));
    }
}
