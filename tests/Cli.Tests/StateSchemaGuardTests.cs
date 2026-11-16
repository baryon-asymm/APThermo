using System.Text.Json;
using System.Text.Json.Nodes;
using APThermo.Harness;

namespace APThermo.Cli.Tests;

/// <summary>State shapes are checked against both the published schema and the actual command.</summary>
[Collection(CliFixture.CollectionName)]
public sealed class StateSchemaGuardTests
{
    /// <summary>The negation branch rejects matches, accepts nonmatches and is audited before use.</summary>
    [Theory]
    [InlineData("""{"not":{"type":"number"}}""", "1", false)]
    [InlineData("""{"not":{"type":"number"}}""", "\"text\"", true)]
    [InlineData("""{"not":false}""", "null", true)]
    [InlineData("""{"not":true}""", "null", false)]
    public void NegationHasIndependentControls(string schemaText, string value, bool expected)
    {
        using var instance = JsonDocument.Parse(value);
        Assert.Equal(expected, JsonSchema.Parse(schemaText).Validate(instance.RootElement).Count == 0);
    }

    /// <summary>Unsupported content under negation is rejected at parse time.</summary>
    [Fact]
    public void NegationBranchIsAudited() =>
        _ = Assert.Throws<InvalidOperationException>(() => JsonSchema.Parse("""{"not":{"type":"numbre"}}"""));

    /// <summary>Inputs are independent contract examples; the committed composition is used by the command.</summary>
    [Theory]
    [InlineData("{\"enthalpy\":0}", true)]
    [InlineData("{\"temperature\":300}", true)]
    [InlineData("{\"entropy\":0}", true)]
    [InlineData("{\"enthalpy\":0,\"areaRatios\":[]}", true)]
    [InlineData("{\"temperature\":300,\"areaRatios\":[],\"pressureRatios\":[]}", true)]
    [InlineData("{\"enthalpy\":0,\"areaRatios\":[],\"pressureRatios\":[2]}", true)]
    [InlineData("{\"enthalpy\":0,\"areaRatios\":[2],\"pressureRatios\":[],\"flow\":\"frozen-at-chamber\"}", true)]
    [InlineData("{}", false)]
    [InlineData("{\"temperature\":300,\"enthalpy\":0}", false)]
    [InlineData("{\"temperature\":300,\"entropy\":0}", false)]
    [InlineData("{\"enthalpy\":0,\"entropy\":0}", false)]
    [InlineData("{\"temperature\":300,\"areaRatios\":[2]}", false)]
    [InlineData("{\"entropy\":0,\"pressureRatios\":[2],\"areaRatios\":[]}", false)]
    [InlineData("{\"enthalpy\":0,\"flow\":\"frozen-at-chamber\"}", false)]
    [InlineData("{\"enthalpy\":0,\"areaRatios\":[],\"pressureRatios\":[],\"flow\":\"frozen-at-chamber\"}", false)]
    public void PublishedSchemaAndCommandMatchTheRecordContract(string fields, bool expected)
    {
        var record = JsonNode.Parse(File.ReadAllText(CliFixture.Document("states.json")))![0]!.DeepClone().AsObject();
        _ = record.Remove("enthalpy");
        foreach (var property in JsonNode.Parse(fields)!.AsObject())
        {
            record[property.Key] = property.Value?.DeepClone();
        }
        using var instance = JsonDocument.Parse(record.ToJsonString());
        using var schemaOutput = new StringWriter();
        using var schemaError = new StringWriter();
        Assert.Equal(0, Cli.Program.Run(["schema", "states"], schemaOutput, schemaError));
        var errors = JsonSchema.Parse(schemaOutput.ToString()).Validate(instance.RootElement);
        var path = CliFixture.Shared.TempFile(Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(path, record.ToJsonString());
        var run = CliFixture.Invoke(CliFixture.Shared.Solving("states", path));
        Assert.True(expected ? run.Code is 0 or 1 : run.Code == 2, $"{fields}: exit {run.Code}: {run.Error}");
        Assert.True(expected == (errors.Count == 0), $"{fields}: {string.Join("; ", errors)}");
    }
}
