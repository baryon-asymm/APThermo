using System.Text.Json;
using APThermo.Cli.Documents;
using APThermo.Harness;

namespace APThermo.Cli.Tests;

/// <summary>Independent expected input shapes, exercised against both the reader and the public schema.</summary>
public sealed class ConditionalInputSchemaTests
{
    /// <summary>Problem-dependent sweep axes, ratio prerequisites and custom reactant fields.</summary>
    [Theory]
    [InlineData("{\"type\":\"rocket\",\"chamberPressure\":100000}", "{\"chamberPressure\":[100000]}", "{\"elementMoles\":{\"H\":1}}", true)]
    [InlineData("{\"type\":\"rocket\",\"chamberPressure\":100000}", "{\"pressure\":[100000]}", "{\"elementMoles\":{\"H\":1}}", false)]
    [InlineData("{\"type\":\"rocket\",\"chamberPressure\":100000}", "{\"temperature\":[300]}", "{\"elementMoles\":{\"H\":1}}", false)]
    [InlineData("{\"type\":\"equilibrium\",\"kind\":\"tp\",\"pressure\":100000,\"temperature\":300}", "{\"temperature\":[300]}", "{\"elementMoles\":{\"H\":1}}", true)]
    [InlineData("{\"type\":\"equilibrium\",\"kind\":\"hp\",\"pressure\":100000}", "{\"temperature\":[300]}", "{\"elementMoles\":{\"H\":1}}", false)]
    [InlineData("{\"type\":\"equilibrium\",\"kind\":\"hp\",\"pressure\":100000}", "{\"pressure\":[100000]}", "{\"elementMoles\":{\"H\":1}}", true)]
    [InlineData("{\"type\":\"equilibrium\",\"kind\":\"sp\",\"pressure\":100000,\"entropy\":1}", "{\"chamberPressure\":[100000]}", "{\"elementMoles\":{\"H\":1}}", false)]
    [InlineData("{\"type\":\"rocket\",\"chamberPressure\":100000}", "{\"oxidizerToFuel\":[1]}", "{\"elementMoles\":{\"H\":1}}", false)]
    [InlineData("{\"type\":\"rocket\",\"chamberPressure\":100000}", "{}", "{\"reactants\":[{\"name\":\"X\",\"role\":\"named\",\"amount\":1,\"formula\":{\"H\":1}}]}", false)]
    [InlineData("{\"type\":\"rocket\",\"chamberPressure\":100000}", "{}", "{\"reactants\":[{\"name\":\"X\",\"role\":\"named\",\"amount\":1,\"formula\":{\"H\":1},\"enthalpy\":0}]}", false)]
    [InlineData("{\"type\":\"rocket\",\"chamberPressure\":100000}", "{}", "{\"reactants\":[{\"name\":\"X\",\"role\":\"named\",\"amount\":1,\"formula\":{\"H\":1},\"enthalpy\":0,\"temperature\":300}]}", true)]
    [InlineData("{\"type\":\"rocket\",\"chamberPressure\":100000}", "{}", "{\"reactants\":[{\"name\":\"X\",\"role\":\"named\",\"amount\":1,\"molarMass\":1}]}", false)]
    public void ReaderAndSchemaRespectTheDocumentedConditions(string problem, string sweep, string propellant, bool expected)
    {
        var text = $"{{\"problem\":{problem},\"sweep\":{sweep},\"propellant\":{propellant}}}";
        using var document = JsonDocument.Parse(text);
        var errors = JsonSchema.Parse(CliFixture.SchemaText("input")).Validate(document.RootElement);
        var readerError = Record.Exception(() => ProblemDocumentReader.Read(text, "conditional-control"));
        Assert.Equal(expected, readerError is null);
        Assert.True(expected == (errors.Count == 0), $"expected {expected}: {text}; schema: {string.Join("; ", errors)}");
    }
}
