using System.Text.Json;
using System.Text.Json.Nodes;
using APThermo.Cli.Cases;
using APThermo.Cli.Output;
using APThermo.Cli.Syntax;
using APThermo.Equilibrium;
using APThermo.Execution;
using APThermo.Problems;
using APThermo.Thermo;

namespace APThermo.Cli.Tests;

/// <summary>Independent controls for the CLI's failure and JSON boundary rules.</summary>
[Collection(CliFixture.CollectionName)]
public sealed class OutputBoundaryGuardTests
{
    private static readonly Lazy<CaseOutput> Sample = new(CreateSample);

    private static CaseOutput CreateSample()
    {
        var propellant = Propellant.From(CliFixture.Shared.Database)
            .Oxidizer("O2(L)", temperature: 90.17).Fuel("H2(L)", temperature: 20.27)
            .OxidizerToFuelRatio(6.0).Build();
        using var solver = Solver.Create(CliFixture.Shared.Database, new EngineOptions { Accelerator = AcceleratorKind.Cpu });
        var result = solver.Solve(propellant, new EquilibriumProblem
        {
            Kind = ProblemKind.AssignedTemperaturePressure,
            Temperature = 3000.0,
            Pressure = 1e6,
        });
        Assert.Equal(CaseStatus.Ok, result.Status);
        return new CaseOutput
        {
            Index = 0,
            Inputs = new JsonObject(),
            Status = CaseStatus.Ok,
            Mixture = result.Mixture,
            MixtureMass = result.MixtureMass,
            Species = result.Species,
            Stations = [result.State],
        };
    }

    /// <summary>An otherwise successful physical station with failed transport returns exit 1.</summary>
    [Fact]
    public void TransportFailureOnAnOkStationIsCaseFailure()
    {
        var station = Sample.Value.Stations[0] with { TransportStatus = CaseStatus.NoTransportData };
        var (code, output) = Write(Sample.Value with { Stations = [station] });
        Assert.Equal(ExitCode.CaseFailed, code);
        using var document = JsonDocument.Parse(output);
        var written = document.RootElement.GetProperty("cases")[0].GetProperty("stations")[0];
        Assert.Equal("ok", written.GetProperty("status").GetString());
        Assert.Equal("noTransportData", written.GetProperty("transport").GetProperty("status").GetString());
        Assert.Equal(ExitCode.Ok, Write(Sample.Value with { Stations = [station with { TransportStatus = CaseStatus.Ok }] }).Code);
    }

    /// <summary>Every nonfinite variant is null while finite positive and negative numbers remain numbers.</summary>
    [Theory]
    [InlineData(double.NaN, JsonValueKind.Null)]
    [InlineData(double.PositiveInfinity, JsonValueKind.Null)]
    [InlineData(double.NegativeInfinity, JsonValueKind.Null)]
    [InlineData(1.5, JsonValueKind.Number)]
    [InlineData(-1.5, JsonValueKind.Number)]
    public void NonfiniteNumbersBecomeNull(double value, JsonValueKind expected)
    {
        var state = Sample.Value.Stations[0].State;
        state.Temperature = value;
        var station = Sample.Value.Stations[0] with { State = state };
        var text = Write(Sample.Value with { Stations = [station] }).Output;
        using var document = JsonDocument.Parse(text);
        Assert.Equal(expected, document.RootElement.GetProperty("cases")[0].GetProperty("stations")[0].GetProperty("temperature").ValueKind);
    }

    /// <summary>The schema's complete status enum comes from every actual CaseStatus, with no typed list.</summary>
    [Fact]
    public void SchemaStatusInventoryEqualsEveryCaseStatus()
    {
        using var schema = JsonDocument.Parse(CliFixture.SchemaText("output"));
        var actual = schema.RootElement.GetProperty("$defs").GetProperty("status").GetProperty("enum")
            .EnumerateArray().Select(item => item.GetString()!).ToArray();
        var expected = Enum.GetNames<CaseStatus>().Select(JsonNamingPolicy.CamelCase.ConvertName).ToArray();
        Assert.Equal(expected.Length, actual.Length);
        Assert.Equal(expected.Order(StringComparer.Ordinal), actual.Order(StringComparer.Ordinal));
        foreach (var value in Enum.GetValues<CaseStatus>())
        {
            var output = Write(Sample.Value with { Status = value }).Output;
            using var document = JsonDocument.Parse(output);
            var written = document.RootElement.GetProperty("cases")[0].GetProperty("status").GetString();
            Assert.Equal(JsonNamingPolicy.CamelCase.ConvertName(value.ToString()), written);
        }
    }

    private static (ExitCode Code, string Output) Write(CaseOutput item)
    {
        var run = new RunInfo("equilibrium", [], null, null, new Timings(0, 0), new RunLimits(0, 0.01));
        using var output = new StringWriter();
        var code = DocumentWriter.Write(run, [item], new CommandOptions(), output);
        return (code, output.ToString());
    }
}
