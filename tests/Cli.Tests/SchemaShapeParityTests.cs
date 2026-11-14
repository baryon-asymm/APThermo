using System.Text.Json;
using System.Text.Json.Nodes;
using APThermo.Cli.Documents;
using APThermo.Harness;

namespace APThermo.Cli.Tests;

/// <summary>Independent conditional-shape table, checked in both directions against reader and published schema.</summary>
[Collection(CliFixture.CollectionName)]
public sealed class SchemaShapeParityTests
{
    /// <summary>Expected shape validity comes from the documented TP/HP/SP contract, not from either validator.</summary>
    public static TheoryData<string, bool> Shapes() => new()
    {
        { """{"kind":"tp","temperature":300}""", true },
        { """{"kind":"tp"}""", false },
        { """{"kind":"tp","temperature":300,"enthalpy":0}""", false },
        { """{"kind":"tp","temperature":300,"entropy":0}""", false },
        { """{"kind":"tp","temperature":"300"}""", false },
        { """{"kind":"hp"}""", true },
        { """{"kind":"hp","enthalpy":0}""", true },
        { """{"kind":"hp","temperature":0}""", true },
        { """{"kind":"hp","temperature":300,"enthalpy":0}""", true },
        { """{"kind":"hp","entropy":0}""", false },
        { """{"kind":"hp","enthalpy":0,"entropy":0}""", false },
        { """{"kind":"sp","entropy":0}""", true },
        { """{"kind":"sp","entropy":0,"temperature":0}""", true },
        { """{"kind":"sp","entropy":0,"temperature":300}""", true },
        { """{"kind":"sp"}""", false },
        { """{"kind":"sp","entropy":0,"enthalpy":0}""", false },
        { """{"kind":"sp","entropy":null}""", false },
        { """{"kind":"uv","temperature":300}""", false },
        { """{"kind":"tp","temperature":300,"unexpected":1}""", false },
    };

    /// <summary>Each side must agree with the independent expectation; mere mutual agreement is insufficient.</summary>
    [Theory]
    [MemberData(nameof(Shapes))]
    public void ReaderAndSchemaEachImplementTheConditionalShape(string fields, bool expected)
    {
        var problem = JsonNode.Parse(fields)!.AsObject();
        problem["type"] = "equilibrium";
        problem["pressure"] = 1e5;
        var input = new JsonObject
        {
            ["propellant"] = JsonNode.Parse("""{"elementMoles":{"H":1}}"""),
            ["problem"] = problem,
        }.ToJsonString();
        using var document = JsonDocument.Parse(input);
        var errors = JsonSchema.Parse(CliFixture.SchemaText("input")).Validate(document.RootElement);
        var readerError = Record.Exception(() => ProblemDocumentReader.Read(input, "shape-control"));
        Assert.Equal(expected, readerError is null);
        if (!expected)
        {
            _ = Assert.IsType<InputException>(readerError);
        }

        Assert.True(expected == (errors.Count == 0), $"expected {expected}: {fields}; schema: {string.Join("; ", errors)}");
    }

    /// <summary>A real emitted zero-mass refusal conforms; the schema retains negative-mass and status guards.</summary>
    [Theory]
    [InlineData(0.0, "invalidInput", true)]
    [InlineData(-1.0, "invalidInput", false)]
    [InlineData(0.0, "unknownStatus", false)]
    public void DiagnosticOutputKeepsItsShapeGuards(double mass, string status, bool expected)
    {
        var path = CliFixture.Shared.TempFile("zero-mass-diagnostic.json");
        File.WriteAllText(path, """
            {"propellant":{"elementMoles":{"H":0},"only":["H2"]},
             "problem":{"type":"equilibrium","kind":"tp","pressure":100000,"temperature":300}}
            """);
        var run = CliFixture.Invoke(CliFixture.Shared.Solving("equilibrium", path, "--mass-tolerance", "1"));
        Assert.Equal(1, run.Code);
        var output = JsonNode.Parse(run.Output)!;
        var first = output["cases"]![0]!;
        Assert.Equal("invalidInput", first["status"]!.GetValue<string>());
        Assert.Equal(0.0, first["mixture"]!["mass"]!.GetValue<double>());
        first["status"] = status;
        first["mixture"]!["mass"] = mass;
        using var document = JsonDocument.Parse(output.ToJsonString());
        var errors = JsonSchema.Parse(CliFixture.SchemaText("output")).Validate(document.RootElement);
        Assert.True(expected == (errors.Count == 0), $"expected {expected}: {string.Join("; ", errors)}");
    }
}