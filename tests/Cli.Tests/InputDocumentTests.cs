using System.Text.Json;
using AerospacePropellantThermodynamics.Fixtures;

namespace AerospacePropellantThermodynamics.Cli.Tests;

/// <summary>L0: the strict readers of the input documents, their messages, and the examples of the API document.</summary>
[Collection(CliCollection.Name)]
public sealed class InputDocumentTests(CliFixture fixture)
{
    /// <summary>The fragment every invalid document of documents/invalid must produce; a document without an entry fails the theory.</summary>
    private static readonly IReadOnlyDictionary<string, string> Expected = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["malformed.json"] = "malformed JSON",
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
        ["states-rocket-without-enthalpy.json"] = "needs 'enthalpy'",
        ["states-two-kilograms.json"] = "record 0: the composition weighs 2000.03 g with the database's atomic weights",
        ["states-mol-per-gram.json"] = "record 0: the composition weighs 1.000015 g",
        ["states-kmol-per-kg.json"] = "record 0: the composition weighs 1 g",
        ["elemental-two-kilograms.json"] = "$.propellant.elementMoles: the composition weighs 2000.03 g",
    };

    public static IEnumerable<object[]> Invalid() => CliFixture.InvalidDocumentNames().Select(n => new object[] { n });

    public static IEnumerable<object[]> Problems() => CliFixture.ProblemDocumentNames().Select(n => new object[] { n });

    [Theory]
    [MemberData(nameof(Invalid))]
    public void An_invalid_document_is_exit_2_with_the_documented_message_and_no_output(string name)
    {
        Assert.True(Expected.TryGetValue(name, out var fragment), $"no expected message recorded for {name}");
        var path = fixture.Document(Path.Combine("invalid", name));
        var command = name.StartsWith("states", StringComparison.Ordinal) ? "states" : "rocket";
        var output = fixture.TempFile(name + ".out.json");
        var run = fixture.Invoke(fixture.Solving(command, path, "--output", output));
        Assert.Equal(2, run.Code);
        Assert.Contains(fragment, run.Error);
        Assert.Empty(run.Output);
        Assert.False(File.Exists(output), "a document was written for an invalid input");
    }

    [Theory]
    [MemberData(nameof(Problems))]
    public void Every_example_document_validates_against_the_input_schema(string name)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(fixture.Document(name)));
        var errors = JsonSchema.Load(fixture.Schema("input.schema.json")).Validate(document.RootElement);
        Assert.True(errors.Count == 0, string.Join("; ", errors));
    }

    [Fact]
    public void Every_states_document_validates_against_the_states_schema()
    {
        var schema = JsonSchema.Load(fixture.Schema("states.schema.json"));
        foreach (var name in new[] { "states.json", "states-part1.json", "states-part2.json", "states-ap-al-record.json" })
        {
            using var document = JsonDocument.Parse(File.ReadAllText(fixture.Document(name)));
            var errors = schema.Validate(document.RootElement);
            Assert.True(errors.Count == 0, $"{name}: {string.Join("; ", errors)}");
        }

        foreach (var line in File.ReadAllLines(fixture.Document("states.jsonl")).Where(l => l.Trim().Length > 0))
        {
            using var record = JsonDocument.Parse(line);
            Assert.Empty(schema.Validate(record.RootElement));
        }
    }

    [Fact]
    public void Every_example_of_the_api_document_is_read_or_validates_and_its_records_solve()
    {
        var api = File.ReadAllText(RepositoryPaths.Resolve("src", "Cli", "API.md"));
        var inputs = CliFixture.JsonFencesOf(api, "## Input document ✅");
        Assert.NotEmpty(inputs);
        var inputSchema = JsonSchema.Load(fixture.Schema("input.schema.json"));
        for (var i = 0; i < inputs.Count; i++)
        {
            InputDocuments.ReadProblem(inputs[i], $"API.md input example {i}");
            using var document = JsonDocument.Parse(inputs[i]);
            Assert.Empty(inputSchema.Validate(document.RootElement));
        }

        var records = CliFixture.JsonFencesOf(api, "## Command line ✅");
        Assert.NotEmpty(records);
        for (var i = 0; i < records.Count; i++)
        {
            Assert.NotEmpty(InputDocuments.ReadStates([($"API.md record example {i}", records[i])]));

            // An example record is a real record: it solves, and in particular it weighs one kilogram (2026-09-13: the earlier example did not).
            var path = fixture.TempFile($"api-record-{i}.json");
            File.WriteAllText(path, records[i]);
            var run = fixture.Invoke(fixture.Solving("states", path));
            Assert.True(run.Code == 0, $"API.md record example {i}: exit code {run.Code}: {run.Error}");
        }

        var outputs = CliFixture.JsonFencesOf(api, "## Output document ✅");
        var output = Assert.Single(outputs);
        using var example = JsonDocument.Parse(output);
        var errors = JsonSchema.Load(fixture.Schema("output.schema.json")).Validate(example.RootElement);
        Assert.True(errors.Count == 0, string.Join("; ", errors));
    }

    [Fact]
    public void State_records_may_come_as_an_array_a_single_object_or_lines()
    {
        var fromArray = InputDocuments.ReadStates([("states.json", File.ReadAllText(fixture.Document("states.json")))]);
        var fromLines = InputDocuments.ReadStates([("states.jsonl", File.ReadAllText(fixture.Document("states.jsonl")))]);
        var fromFiles = InputDocuments.ReadStates([
            ("states-part1.json", File.ReadAllText(fixture.Document("states-part1.json"))),
            ("states-part2.json", File.ReadAllText(fixture.Document("states-part2.json"))),
        ]);
        Assert.Equal(3, fromArray.Count);
        foreach (var other in new[] { fromLines, fromFiles })
        {
            Assert.Equal(fromArray.Select(r => r.Pressure), other.Select(r => r.Pressure));
            Assert.Equal(fromArray.Select(r => r.Enthalpy), other.Select(r => r.Enthalpy));
            Assert.Equal(fromArray.Select(r => r.Index), other.Select(r => r.Index));
        }

        Assert.True(fromArray[2].IsRocket);
        Assert.False(fromArray[0].IsRocket);
    }

    [Fact]
    public void A_range_expands_inclusively_and_lands_on_its_end()
    {
        using var whole = JsonDocument.Parse("""{ "from": 4.0, "to": 7.0, "step": 1.0 }""");
        Assert.Equal([4.0, 5.0, 6.0, 7.0], InputDocuments.ReadValues(whole.RootElement, "$.sweep.oxidizerToFuel"));
        using var fine = JsonDocument.Parse("""{ "from": 4.0, "to": 8.0, "step": 0.25 }""");
        var values = InputDocuments.ReadValues(fine.RootElement, "$.sweep.oxidizerToFuel");
        Assert.Equal(17, values.Count);
        Assert.Equal(8.0, values[^1]);
        using var list = JsonDocument.Parse("[5.0e6, 7.0e6]");
        Assert.Equal([5.0e6, 7.0e6], InputDocuments.ReadValues(list.RootElement, "$.sweep.chamberPressure"));
        using var empty = JsonDocument.Parse("[]");
        Assert.Throws<InputException>(() => InputDocuments.ReadValues(empty.RootElement, "$.sweep.chamberPressure"));
    }
}
