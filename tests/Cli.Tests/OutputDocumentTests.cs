using System.Globalization;
using System.Reflection;
using System.Text.Json;
using AerospacePropellantThermodynamics.Execution;
using AerospacePropellantThermodynamics.Performance;
using AerospacePropellantThermodynamics.Problems;
using AerospacePropellantThermodynamics.Thermo;
using AerospacePropellantThermodynamics.Transport;

namespace AerospacePropellantThermodynamics.Cli.Tests;

/// <summary>L1: every example document runs end to end and its result validates against the output schema; sweeps, states, thresholds, listings.</summary>
[Collection(CliCollection.Name)]
public sealed class OutputDocumentTests(CliFixture fixture)
{
    /// <summary>Relative slack on a pressure or area ratio read back from a rocket station against the sweep or record document's own value: double's rounding floor is far below this, so any looser value would hide a real mismatch instead of a rounding one.</summary>
    private const double RatioTolerance = 1e-6;

    /// <summary>Absolute slack, kg, on a mixture's mass read back from a document against one kilogram, coarser than <see cref="Problems.ElementalMixture.DefaultMassTolerance"/> so that a mixture accepted at the default never fails this check by rounding alone.</summary>
    private const double MassRoundingTolerance = 1e-4;

    /// <summary>A --threshold coarser than the default (CommandOptions.DefaultThreshold, 5e-6), chosen only to omit more species than the default does, for the omission test below.</summary>
    private const double CoarseThreshold = 1e-3;

    public static IEnumerable<object[]> Problems() => CliFixture.ProblemDocumentNames().Select(n => new object[] { n });

    [Theory]
    [MemberData(nameof(Problems))]
    public void Every_example_document_runs_and_its_result_validates_against_the_output_schema(string name)
    {
        var command = name.StartsWith("rocket", StringComparison.Ordinal) ? "rocket" : "equilibrium";
        var (code, document, error) = fixture.Produce(command, name);
        Assert.Equal(name == "rocket-failing.json" ? 1 : 0, code);
        Assert.Empty(error);
        var errors = JsonSchema.Load(fixture.Schema("output.schema.json")).Validate(document.RootElement);
        Assert.True(errors.Count == 0, string.Join("; ", errors));
        var run = document.RootElement.GetProperty("run");
        Assert.Equal(command, run.GetProperty("command").GetString());
        Assert.Equal("cpu", run.GetProperty("accelerator").GetProperty("kind").GetString());
        Assert.Equal(fixture.Document(name), run.GetProperty("inputs")[0].GetString());
        Assert.True(document.RootElement.GetProperty("cases").GetArrayLength() >= 1);
    }

    [Fact]
    public void The_states_result_validates_and_echoes_every_record_in_order()
    {
        var (code, document, _) = fixture.Produce("states", "states.json");
        Assert.Equal(0, code);
        var errors = JsonSchema.Load(fixture.Schema("output.schema.json")).Validate(document.RootElement);
        Assert.True(errors.Count == 0, string.Join("; ", errors));
        using var records = JsonDocument.Parse(File.ReadAllText(fixture.Document("states.json")));
        var cases = document.RootElement.GetProperty("cases").EnumerateArray().ToList();
        Assert.Equal(records.RootElement.GetArrayLength(), cases.Count);
        for (var i = 0; i < cases.Count; i++)
        {
            Assert.Equal(i, cases[i].GetProperty("index").GetInt32());
            Assert.True(JsonElement.DeepEquals(records.RootElement[i], cases[i].GetProperty("inputs")), $"record {i} is not echoed");
            Assert.Equal("ok", cases[i].GetProperty("status").GetString());
        }

        Assert.Equal(["state"], cases[0].GetProperty("stations").EnumerateArray().Select(s => s.GetProperty("name").GetString()));
        Assert.Equal(["state"], cases[1].GetProperty("stations").EnumerateArray().Select(s => s.GetProperty("name").GetString()));
        var rocket = cases[2].GetProperty("stations").EnumerateArray().ToList();
        Assert.Equal(["chamber", "throat", "exit1", "exit2"], rocket.Select(s => s.GetProperty("name").GetString()));
        Assert.Equal(10.0, rocket[2].GetProperty("performance").GetProperty("pressureRatio").GetDouble(), RatioTolerance);
        Assert.Equal(20.0, rocket[3].GetProperty("performance").GetProperty("areaRatio").GetDouble(), RatioTolerance);
        Assert.False(cases[0].GetProperty("stations")[0].TryGetProperty("performance", out _), "an equilibrium state has no performance figures");
    }

    [Fact]
    public void States_from_an_array_a_lines_file_and_several_files_give_the_same_cases()
    {
        var (_, fromArray, _) = fixture.Produce("states", "states.json");
        var (_, fromLines, _) = fixture.Produce("states", "states.jsonl");
        var fromFiles = fixture.Invoke("states", fixture.Document("states-part1.json"), fixture.Document("states-part2.json"),
                                       "--database", fixture.DatabasePath, "--accelerator", "cpu");
        Assert.Equal(0, fromFiles.Code);
        using var filesDocument = fromFiles.Json();
        var expected = fromArray.RootElement.GetProperty("cases");
        Assert.True(JsonElement.DeepEquals(expected, fromLines.RootElement.GetProperty("cases")), "JSON Lines gave other cases");
        Assert.True(JsonElement.DeepEquals(expected, filesDocument.RootElement.GetProperty("cases")), "several files gave other cases");
    }

    [Fact]
    public void A_rocket_sweep_expands_ratio_major_then_pressure_in_one_document()
    {
        var (code, document, _) = fixture.Produce("rocket", "rocket-sweep.json");
        Assert.Equal(0, code);
        var cases = document.RootElement.GetProperty("cases").EnumerateArray().ToList();
        var expected = new List<(double, double)>();
        foreach (var ratio in new[] { 4.0, 5.0, 6.0, 7.0 })
        {
            foreach (var pressure in new[] { 5.0e6, 7.0e6 })
            {
                expected.Add((ratio, pressure));
            }
        }

        Assert.Equal(expected, cases.Select(c => (c.GetProperty("inputs").GetProperty("oxidizerToFuel").GetDouble(), c.GetProperty("inputs").GetProperty("chamberPressure").GetDouble())));
        foreach (var c in cases)
        {
            var stations = c.GetProperty("stations").EnumerateArray().ToList();
            Assert.Equal(["chamber", "throat", "exit1", "exit2"], stations.Select(s => s.GetProperty("name").GetString()));
            Assert.Equal(10.0, stations[2].GetProperty("performance").GetProperty("pressureRatio").GetDouble(), RatioTolerance);
            Assert.Equal(20.0, stations[3].GetProperty("performance").GetProperty("areaRatio").GetDouble(), RatioTolerance);
            Assert.Equal(c.GetProperty("inputs").GetProperty("chamberPressure").GetDouble(), stations[0].GetProperty("pressure").GetDouble());
        }
    }

    [Fact]
    public void An_equilibrium_sweep_expands_pressure_then_temperature()
    {
        var (code, document, _) = fixture.Produce("equilibrium", "equilibrium-sweep.json");
        Assert.Equal(0, code);
        var cases = document.RootElement.GetProperty("cases").EnumerateArray().ToList();
        var expected = new List<(double, double)>();
        foreach (var pressure in new[] { 1.0e5, 1.0e6, 1.0e7 })
        {
            foreach (var temperature in new[] { 2000.0, 2500.0, 3000.0 })
            {
                expected.Add((pressure, temperature));
            }
        }

        Assert.Equal(expected, cases.Select(c => (c.GetProperty("inputs").GetProperty("pressure").GetDouble(), c.GetProperty("inputs").GetProperty("temperature").GetDouble())));
        Assert.All(cases, c => Assert.Equal("tp", c.GetProperty("inputs").GetProperty("kind").GetString()));
        Assert.All(cases, c => Assert.Equal(c.GetProperty("inputs").GetProperty("temperature").GetDouble(), c.GetProperty("stations")[0].GetProperty("temperature").GetDouble()));
    }

    [Fact]
    public void The_mass_tolerance_is_echoed_and_every_case_reports_the_mass_of_its_mixture()
    {
        var (_, standard, _) = fixture.Produce("states", "states.json");
        Assert.Equal(ElementalMixture.DefaultMassTolerance, standard.RootElement.GetProperty("run").GetProperty("massTolerance").GetDouble());
        var (_, loose, _) = fixture.Produce("states", "states.json", "--mass-tolerance", "0.05");
        Assert.Equal(0.05, loose.RootElement.GetProperty("run").GetProperty("massTolerance").GetDouble());
        foreach (var document in new[] { standard, loose })
        {
            foreach (var c in document.RootElement.GetProperty("cases").EnumerateArray())
            {
                var mass = c.GetProperty("mixture").GetProperty("mass").GetDouble();
                Assert.True(Math.Abs(mass - 1.0) <= ElementalMixture.DefaultMassTolerance, $"case {c.GetProperty("index").GetInt32()}: mass {mass:R} kg");
            }
        }

        // The propellant path reports it too: every case of the LOX/LH2 sweep within the reactant records' rounding of one kilogram.
        var (_, sweep, _) = fixture.Produce("rocket", "rocket-sweep.json");
        Assert.Equal(ElementalMixture.DefaultMassTolerance, sweep.RootElement.GetProperty("run").GetProperty("massTolerance").GetDouble());
        Assert.All(sweep.RootElement.GetProperty("cases").EnumerateArray(),
                   c => Assert.True(Math.Abs(c.GetProperty("mixture").GetProperty("mass").GetDouble() - 1.0) < MassRoundingTolerance, c.GetProperty("mixture").GetProperty("mass").GetRawText()));
    }

    [Fact]
    public void The_threshold_omits_small_mole_fractions_and_the_default_is_the_reference_print_threshold()
    {
        var (_, everything, _) = fixture.Produce("rocket", "rocket-lox-lh2.json", "--threshold", "0");
        var (_, coarse, _) = fixture.Produce("rocket", "rocket-lox-lh2.json", "--threshold", CoarseThreshold.ToString(CultureInfo.InvariantCulture));
        var (_, standard, _) = fixture.Produce("rocket", "rocket-lox-lh2.json");
        var all = everything.RootElement.GetProperty("cases")[0].GetProperty("stations")[0].GetProperty("moleFractions").EnumerateObject().ToList();
        var few = coarse.RootElement.GetProperty("cases")[0].GetProperty("stations")[0].GetProperty("moleFractions").EnumerateObject().ToList();
        var some = standard.RootElement.GetProperty("cases")[0].GetProperty("stations")[0].GetProperty("moleFractions").EnumerateObject().ToList();
        Assert.True(all.Count > some.Count && some.Count > few.Count, $"{all.Count} species at 0, {some.Count} at the default, {few.Count} at the coarse threshold");
        Assert.All(few, p => Assert.True(p.Value.GetDouble() >= CoarseThreshold));
        Assert.All(some, p => Assert.True(p.Value.GetDouble() >= CommandOptions.DefaultThreshold));
        Assert.Equal(CommandOptions.DefaultThreshold, standard.RootElement.GetProperty("run").GetProperty("threshold").GetDouble());
        Assert.Equal(CoarseThreshold, coarse.RootElement.GetProperty("run").GetProperty("threshold").GetDouble());
    }

    [Fact]
    public void Transport_figures_are_present_only_when_requested()
    {
        var (_, with, _) = fixture.Produce("rocket", "rocket-lox-lh2.json");
        foreach (var station in with.RootElement.GetProperty("cases")[0].GetProperty("stations").EnumerateArray())
        {
            var transport = station.GetProperty("transport");
            Assert.Equal("ok", transport.GetProperty("status").GetString());
            Assert.True(transport.GetProperty("viscosity").GetDouble() > 0.0);
            Assert.True(transport.GetProperty("speciesCount").GetInt32() > 0);
        }

        var (_, without, _) = fixture.Produce("equilibrium", "equilibrium-hp.json");
        Assert.False(without.RootElement.GetProperty("cases")[0].GetProperty("stations")[0].TryGetProperty("transport", out _));
    }

    [Fact]
    public void The_species_listing_validates_and_finds_names_case_insensitively()
    {
        var run = fixture.Invoke("species", "--database", fixture.DatabasePath, "--find", "h2o");
        Assert.Equal(0, run.Code);
        using var document = run.Json();
        var errors = JsonSchema.Load(fixture.Schema("species.schema.json")).Validate(document.RootElement);
        Assert.True(errors.Count == 0, string.Join("; ", errors));
        var names = document.RootElement.GetProperty("species").EnumerateArray().Select(s => s.GetProperty("name").GetString()!).ToList();
        Assert.Contains("H2O", names);
        Assert.Contains("H2O(L)", names);
        Assert.All(names, n => Assert.Contains("h2o", n, StringComparison.OrdinalIgnoreCase));
        var water = document.RootElement.GetProperty("species").EnumerateArray().First(s => s.GetProperty("name").GetString() == "H2O");
        Assert.Equal("gas", water.GetProperty("phase").GetString());
        Assert.True(water.GetProperty("transportData").GetBoolean());
        var csv = fixture.Invoke("species", "--database", fixture.DatabasePath, "--find", "h2o", "--format", "csv");
        Assert.Equal(0, csv.Code);
        Assert.StartsWith("name,section,phase,formula,molarMass", csv.Output);
        Assert.Equal(names.Count + 1, csv.Output.TrimEnd('\n').Split('\n').Length);
    }

    [Fact]
    public void An_auto_run_that_fell_back_says_why()
    {
        // A separate process (ProcessTests' own pattern): APTHERMO_NO_CUDA is a process environment variable, and
        // this run must not affect any accelerator choice of a test running elsewhere in this collection.
        var environment = new Dictionary<string, string> { [EngineOptions.NoCudaVariable] = "1" };
        var run = fixture.InvokeProcess(["rocket", fixture.Document("rocket-lox-lh2.json"), "--database", fixture.DatabasePath], environment);
        Assert.True(run.Code == 0, $"exit code {run.Code}: {run.Error}");
        using var document = run.Json();
        var errors = JsonSchema.Load(fixture.Schema("output.schema.json")).Validate(document.RootElement);
        Assert.True(errors.Count == 0, string.Join("; ", errors));
        var accelerator = document.RootElement.GetProperty("run").GetProperty("accelerator");
        Assert.Equal("cpu", accelerator.GetProperty("kind").GetString());
        var reason = accelerator.GetProperty("cudaSkippedBecause");
        Assert.Equal(JsonValueKind.String, reason.ValueKind);
        Assert.Contains(EngineOptions.NoCudaVariable, reason.GetString());

        var devices = fixture.InvokeProcess(["devices"], environment);
        Assert.Equal(0, devices.Code);
        using var devicesDocument = devices.Json();
        var devicesErrors = JsonSchema.Load(fixture.Schema("devices.schema.json")).Validate(devicesDocument.RootElement);
        Assert.True(devicesErrors.Count == 0, string.Join("; ", devicesErrors));
        Assert.Contains(EngineOptions.NoCudaVariable, devicesDocument.RootElement.GetProperty("cuda").GetProperty("message").GetString());
    }

    [Fact]
    public void The_devices_listing_validates()
    {
        var run = fixture.Invoke("devices");
        using var document = run.Json();
        var errors = JsonSchema.Load(fixture.Schema("devices.schema.json")).Validate(document.RootElement);
        Assert.True(errors.Count == 0, string.Join("; ", errors));
    }

    [Fact]
    public void The_schema_lists_every_field_of_the_library_result_structs()
    {
        using var schema = JsonDocument.Parse(File.ReadAllText(fixture.Schema("output.schema.json")));
        var definitions = schema.RootElement.GetProperty("$defs");
        AssertSchemaListsFields<MixtureState>(definitions.GetProperty("station"), required: true);
        AssertSchemaListsFields<PerformanceFigures>(definitions.GetProperty("performance"), required: true);
        AssertSchemaListsFields<TransportFigures>(definitions.GetProperty("transport"), required: false);
    }

    /// <summary>Every public field of the struct is one of the schema's declared properties, by the same camel-case rule the document itself is written with (Names.Camel); required when the field is never omitted.</summary>
    private static void AssertSchemaListsFields<T>(JsonElement definition, bool required) where T : struct
    {
        var properties = definition.GetProperty("properties").EnumerateObject().Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
        var requiredNames = definition.GetProperty("required").EnumerateArray().Select(e => e.GetString()!).ToHashSet(StringComparer.Ordinal);
        foreach (var field in typeof(T).GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            var name = Names.Camel(field.Name);
            Assert.True(properties.Contains(name), $"{typeof(T).Name}.{field.Name} is not in the schema's properties as {name}");
            if (required)
            {
                Assert.True(requiredNames.Contains(name), $"{name} is not required by the schema");
            }
        }
    }
}
