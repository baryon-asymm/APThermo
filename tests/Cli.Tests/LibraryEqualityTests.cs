using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using APThermo.Cli.Syntax;
using APThermo.Execution;
using APThermo.Fixtures;
using APThermo.Problems;
using APThermo.Thermo;
using APThermo.Transport;
using PerformanceFigures = APThermo.Performance.PerformanceFigures;
using ProblemKind = APThermo.Equilibrium.ProblemKind;

namespace APThermo.Cli.Tests;

/// <summary>
/// L2: the executable's numbers are the library's numbers. The library call is built from the fixture the example document
/// encodes, not from the document, so that a unit or a value changed in the document is seen.
/// </summary>
[Collection(CliCollection.Name)]
public sealed class LibraryEqualityTests(CliFixture fixture)
{
    /// <summary>g0 of the root's invariant, m/s², transcribed rather than read from the node (BOOT.md: so a changed g0 in the node is seen).</summary>
    public const double StandardGravity = 9.80665;

    [Fact]
    public void The_rocket_example_equals_the_library_field_by_field()
    {
        var c = CeaFixtures.Load(Path.Combine(FixtureFiles.Root, "rocket", "lox-lh2_of6_pc7MPa_shiftingEquilibrium.json"));
        var inputs = c.Inputs;
        var builder = Propellant.From(fixture.Database);
        foreach (var r in inputs.GetProperty("reactants").EnumerateArray())
        {
            var name = r.GetProperty("name").GetString()!;
            builder.Add(Reactant.FromDatabase(name, name == "O2(L)" ? ReactantRole.Oxidizer : ReactantRole.Fuel, 1.0, r.GetProperty("temperature").GetDouble()));
        }

        var propellant = builder.OxidizerToFuelRatio(inputs.GetProperty("oxidizerToFuelRatio").GetDouble()).Build();
        var problem = new RocketProblem
        {
            ChamberPressure = inputs.GetProperty("chamberPressure").GetDouble(),
            AreaRatios = inputs.GetProperty("areaRatios").EnumerateArray().Select(e => e.GetDouble()).ToList(),
            PressureRatios = inputs.GetProperty("pressureRatios").EnumerateArray().Select(e => e.GetDouble()).ToList(),
            Transport = inputs.GetProperty("transport").GetBoolean(),
        };
        using var solver = Solver.Create(fixture.Database, new EngineOptions { Accelerator = AcceleratorKind.Cpu });
        var expected = solver.Solve(propellant, problem);
        Assert.Equal(CaseStatus.Ok, expected.Status);

        var (code, document, _) = fixture.Produce("rocket", "rocket-lox-lh2.json");
        Assert.Equal(0, code);
        var actual = Assert.Single(document.RootElement.GetProperty("cases").EnumerateArray());
        Assert.Equal(propellant.OxidizerToFuelRatio, actual.GetProperty("inputs").GetProperty("oxidizerToFuel").GetDouble());
        Assert.Equal(problem.ChamberPressure, actual.GetProperty("inputs").GetProperty("chamberPressure").GetDouble());
        AssertCase(expected.Mixture, expected.MixtureMass, expected.Species, expected.Stations, actual, CommandOptions.DefaultThreshold);
    }

    [Fact]
    public void The_equilibrium_examples_equal_the_library_field_by_field()
    {
        using var solver = Solver.Create(fixture.Database, new EngineOptions { Accelerator = AcceleratorKind.Cpu });

        // hp at the chamber pressure of the LOX/RP-1 reference case, the enthalpy the propellant's own
        var rocket = CeaFixtures.Load(Path.Combine(FixtureFiles.Root, "rocket", "lox-rp1_of2.6_pc10MPa_shiftingEquilibrium.json")).Inputs;
        var builder = Propellant.From(fixture.Database);
        foreach (var r in rocket.GetProperty("reactants").EnumerateArray())
        {
            var name = r.GetProperty("name").GetString()!;
            var t = r.GetProperty("temperature");
            builder.Add(Reactant.FromDatabase(name, name == "O2(L)" ? ReactantRole.Oxidizer : ReactantRole.Fuel, 1.0, t.ValueKind == JsonValueKind.Null ? null : t.GetDouble()));
        }

        var propellant = builder.OxidizerToFuelRatio(rocket.GetProperty("oxidizerToFuelRatio").GetDouble()).Build();
        var hp = solver.Solve(propellant, new EquilibriumProblem { Kind = ProblemKind.AssignedEnthalpyPressure, Pressure = rocket.GetProperty("chamberPressure").GetDouble() });
        Assert.Equal(CaseStatus.Ok, hp.Status);
        var (code, document, _) = fixture.Produce("equilibrium", "equilibrium-hp.json");
        Assert.Equal(0, code);
        var actual = Assert.Single(document.RootElement.GetProperty("cases").EnumerateArray());
        Assert.Equal("hp", actual.GetProperty("inputs").GetProperty("kind").GetString());
        Assert.Equal(hp.Mixture.Enthalpy, actual.GetProperty("inputs").GetProperty("enthalpy").GetDouble());
        AssertCase(hp.Mixture, hp.MixtureMass, hp.Species, [hp.State], actual, CommandOptions.DefaultThreshold);

        // tp from element moles, with transport, the N2O4/UDMH chamber
        var tp = CeaFixtures.Load(Path.Combine(FixtureFiles.Root, "tp", "nto-udmh_of2.2_pc2MPa_shiftingEquilibrium_chamber.json")).Inputs;
        var moles = tp.GetProperty("elementMoles").EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetDouble() * 1e3, StringComparer.Ordinal);
        var mixture = ElementalMixture.Create(moles);
        var state = solver.Solve(mixture, new EquilibriumProblem
        {
            Kind = ProblemKind.AssignedTemperaturePressure,
            Pressure = tp.GetProperty("pressure").GetDouble(),
            Temperature = tp.GetProperty("temperature").GetDouble(),
            Transport = true,
        });
        Assert.Equal(CaseStatus.Ok, state.Status);
        (code, document, _) = fixture.Produce("equilibrium", "equilibrium-tp-elemental.json");
        Assert.Equal(0, code);
        actual = Assert.Single(document.RootElement.GetProperty("cases").EnumerateArray());
        Assert.False(actual.GetProperty("inputs").TryGetProperty("oxidizerToFuel", out _));
        AssertCase(state.Mixture, state.MixtureMass, state.Species, [state.State], actual, CommandOptions.DefaultThreshold);
        Assert.Equal(JsonValueKind.Null, actual.GetProperty("mixture").GetProperty("enthalpy").ValueKind);
    }

    [Fact]
    public void The_states_example_equals_the_library_field_by_field()
    {
        // The front door's own state batches (F-AR-02): a record without exits through SolveStates, one with
        // exits through SolveRocketStates, each field checked against the same call the states command now makes.
        using var solver = Solver.Create(fixture.Database, new EngineOptions { Accelerator = AcceleratorKind.Cpu });

        // Without exits: the N2O4/UDMH chamber, tp from element moles (the same fixture as the equilibrium tp-elemental example above).
        var tp = CeaFixtures.Load(Path.Combine(FixtureFiles.Root, "tp", "nto-udmh_of2.2_pc2MPa_shiftingEquilibrium_chamber.json")).Inputs;
        var moles = tp.GetProperty("elementMoles").EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetDouble() * 1e3, StringComparer.Ordinal);
        var noExits = new StateRecord(tp.GetProperty("pressure").GetDouble(), moles, Temperature: tp.GetProperty("temperature").GetDouble());
        var expectedEquilibrium = Assert.Single(solver.SolveStates([noExits]));
        Assert.Equal(CaseStatus.Ok, expectedEquilibrium.Status);

        // With exits: the LOX/LH2 rocket example's own mixture and enthalpy, from the library's own MixtureOf, not typed.
        var rocketInputs = CeaFixtures.Load(Path.Combine(FixtureFiles.Root, "rocket", "lox-lh2_of6_pc7MPa_shiftingEquilibrium.json")).Inputs;
        var builder = Propellant.From(fixture.Database);
        foreach (var r in rocketInputs.GetProperty("reactants").EnumerateArray())
        {
            var name = r.GetProperty("name").GetString()!;
            builder.Add(Reactant.FromDatabase(name, name == "O2(L)" ? ReactantRole.Oxidizer : ReactantRole.Fuel, 1.0, r.GetProperty("temperature").GetDouble()));
        }

        var propellant = builder.OxidizerToFuelRatio(rocketInputs.GetProperty("oxidizerToFuelRatio").GetDouble()).Build();
        var mixture = solver.MixtureOf(propellant);
        var withExits = new StateRecord(rocketInputs.GetProperty("chamberPressure").GetDouble(), mixture.ElementMoles, Enthalpy: mixture.Enthalpy)
        {
            AreaRatios = rocketInputs.GetProperty("areaRatios").EnumerateArray().Select(e => e.GetDouble()).ToList(),
            PressureRatios = rocketInputs.GetProperty("pressureRatios").EnumerateArray().Select(e => e.GetDouble()).ToList(),
        };
        var expectedRocket = Assert.Single(solver.SolveRocketStates([withExits]));
        Assert.Equal(CaseStatus.Ok, expectedRocket.Status);

        // The same two records, read by the CLI's states command, in that order.
        var records = new JsonArray(RecordJson(noExits), RecordJson(withExits));
        var path = fixture.TempFile("states-example.json");
        File.WriteAllText(path, records.ToJsonString());
        var run = fixture.Invoke(fixture.Solving("states", path));
        Assert.True(run.Code is 0 or 1, $"exit code {run.Code}: {run.Error}");
        using var document = run.Json();
        var cases = document.RootElement.GetProperty("cases").EnumerateArray().ToList();
        Assert.Equal(2, cases.Count);
        AssertCase(expectedEquilibrium.Mixture, expectedEquilibrium.MixtureMass, expectedEquilibrium.Species, [expectedEquilibrium.State], cases[0], CommandOptions.DefaultThreshold);
        AssertCase(expectedRocket.Mixture, expectedRocket.MixtureMass, expectedRocket.Species, expectedRocket.Stations, cases[1], CommandOptions.DefaultThreshold);
    }

    /// <summary>A StateRecord as the JSON object the states command reads (API.md, Command line).</summary>
    private static JsonObject RecordJson(StateRecord record)
    {
        var json = new JsonObject { ["pressure"] = record.Pressure, ["composition"] = Composition(record.Composition) };
        if (record.Enthalpy is { } enthalpy)
        {
            json["enthalpy"] = enthalpy;
        }

        if (record.Temperature is { } temperature)
        {
            json["temperature"] = temperature;
        }

        if (record.AreaRatios.Count > 0)
        {
            json["areaRatios"] = new JsonArray(record.AreaRatios.Select(v => (JsonNode?)v).ToArray());
        }

        if (record.PressureRatios.Count > 0)
        {
            json["pressureRatios"] = new JsonArray(record.PressureRatios.Select(v => (JsonNode?)v).ToArray());
        }

        return json;
    }

    private static JsonObject Composition(IReadOnlyDictionary<string, double> elementMoles) =>
        new(elementMoles.Select(p => KeyValuePair.Create<string, JsonNode?>(p.Key, p.Value)));

    /// <summary>Every field of every station, the compositions above the threshold, the mixture and its mass: exactly the library's values.</summary>
    private static void AssertCase(ElementalMixture mixture, double mixtureMass, IReadOnlyList<string> species, IReadOnlyList<Station> stations, JsonElement actual, double threshold)
    {
        AssertMixture(mixture, mixtureMass, actual);
        var actualStations = actual.GetProperty("stations").EnumerateArray().ToList();
        Assert.Equal(stations.Count, actualStations.Count);
        for (var s = 0; s < stations.Count; s++)
        {
            AssertStation(stations[s], actualStations[s]);
            AssertComposition(stations[s], actualStations[s], species, threshold);
        }
    }

    /// <summary>The element moles, the enthalpy when given, and the mass: exactly the library's.</summary>
    private static void AssertMixture(ElementalMixture mixture, double mixtureMass, JsonElement actual)
    {
        var elementMoles = actual.GetProperty("mixture").GetProperty("elementMoles");
        foreach (var element in mixture.Elements)
        {
            Assert.Equal(mixture.ElementMoles[element], elementMoles.GetProperty(element).GetDouble());
        }

        if (mixture.Enthalpy is { } enthalpy)
        {
            Assert.Equal(enthalpy, actual.GetProperty("mixture").GetProperty("enthalpy").GetDouble());
        }

        Assert.Equal(mixtureMass, actual.GetProperty("mixture").GetProperty("mass").GetDouble());
    }

    /// <summary>One station's name, status, state fields, performance figures and transport figures: exactly the library's.</summary>
    private static void AssertStation(Station station, JsonElement element)
    {
        Assert.Equal(station.Name, element.GetProperty("name").GetString());
        Assert.Equal(CliFixture.Camel(station.Status.ToString()), element.GetProperty("status").GetString());
        AssertFields(station.State, element, $"{station.Name} state");
        if (station.Performance is { } figures)
        {
            var performance = element.GetProperty("performance");
            AssertFields(figures, performance, $"{station.Name} performance");
            Assert.Equal(figures.SpecificImpulse / StandardGravity, performance.GetProperty("specificImpulseSeconds").GetDouble());
            Assert.Equal(figures.VacuumSpecificImpulse / StandardGravity, performance.GetProperty("vacuumSpecificImpulseSeconds").GetDouble());
        }
        else
        {
            Assert.False(element.TryGetProperty("performance", out _));
        }

        if (station.TransportStatus is { } transportStatus)
        {
            var transport = element.GetProperty("transport");
            Assert.Equal(CliFixture.Camel(transportStatus.ToString()), transport.GetProperty("status").GetString());
            if (station.Transport is { } figures2)
            {
                AssertFields(figures2, transport, $"{station.Name} transport");
            }
        }
        else
        {
            Assert.False(element.TryGetProperty("transport", out _));
        }
    }

    /// <summary>One station's mole fractions and condensed mass fractions, above the threshold: exactly the library's.</summary>
    private static void AssertComposition(Station station, JsonElement element, IReadOnlyList<string> species, double threshold)
    {
        var moleFractions = element.GetProperty("moleFractions");
        var condensed = element.GetProperty("condensedMassFractions");
        foreach (var name in species)
        {
            var fraction = station.MoleFractions[name];
            if (fraction >= threshold)
            {
                Assert.Equal(fraction, moleFractions.GetProperty(name).GetDouble());
                if (station.CondensedMassFractions.TryGetValue(name, out var mass))
                {
                    Assert.Equal(mass, condensed.GetProperty(name).GetDouble());
                }
            }
            else
            {
                Assert.False(moleFractions.TryGetProperty(name, out _), $"{name} is below the threshold and listed");
                Assert.False(condensed.TryGetProperty(name, out _), $"{name} is below the threshold and listed");
            }
        }

        Assert.Equal(species.Count(name => station.MoleFractions[name] >= threshold), moleFractions.EnumerateObject().Count());
    }

    /// <summary>Every public property of the struct, by reflection, against the property of the same camel-case name (CliFixture.Camel): exact.</summary>
    private static void AssertFields<T>(T value, JsonElement element, string label) where T : struct
    {
        foreach (var field in typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var name = CliFixture.Camel(field.Name);
            Assert.True(element.TryGetProperty(name, out var property), $"{label}: {name} is missing");
            var expected = field.GetValue(value)!;
            if (expected is double number)
            {
                Assert.True(number == property.GetDouble(), $"{label}: {name} is {property.GetDouble():R}, the library has {number:R}");
            }
            else
            {
                Assert.Equal((int)expected, property.GetInt32());
            }
        }
    }
}
