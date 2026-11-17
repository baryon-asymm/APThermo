using System.Text.Json;
using APThermo.Cli.Documents;
using APThermo.Harness;

namespace APThermo.Cli.Tests;

/// <summary>Positive conditional combinations remain available after schema hardening.</summary>
public sealed class ConditionalInputPositiveTests
{
    /// <summary>A ratio sweep accepts a reactant propellant that actually declares its ratio.</summary>
    [Fact]
    public void RatioSweepWithMixturePassesReaderAndSchema()
    {
        const string text = """
            {"propellant":{"reactants":[{"name":"O2(L)","role":"oxidizer","amount":1},
              {"name":"H2(L)","role":"fuel","amount":1}],"mixture":{"oxidizerToFuel":6}},
             "problem":{"type":"rocket","chamberPressure":100000},"sweep":{"oxidizerToFuel":[5,6]}}
            """;
        _ = ProblemDocumentReader.Read(text, "positive-ratio-control");
        using var document = JsonDocument.Parse(text);
        Assert.Empty(JsonSchema.Parse(CliFixture.SchemaText("input")).Validate(document.RootElement));
    }

    /// <summary>A normal database reactant may supply temperature without custom fields.</summary>
    [Fact]
    public void DatabaseReactantTemperatureDoesNotRequireCustomFields()
    {
        const string text = """
            {"propellant":{"reactants":[{"name":"H2(L)","role":"named","amount":1,"temperature":20.27}]},
             "problem":{"type":"equilibrium","kind":"tp","pressure":100000,"temperature":300}}
            """;
        _ = ProblemDocumentReader.Read(text, "positive-reactant-control");
        using var document = JsonDocument.Parse(text);
        Assert.Empty(JsonSchema.Parse(CliFixture.SchemaText("input")).Validate(document.RootElement));
    }
}
