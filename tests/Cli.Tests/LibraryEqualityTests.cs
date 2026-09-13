using System.Reflection;
using System.Text.Json;
using AerospacePropellantThermodynamics.Execution;
using AerospacePropellantThermodynamics.Fixtures;
using AerospacePropellantThermodynamics.Problems;
using AerospacePropellantThermodynamics.Thermo;
using AerospacePropellantThermodynamics.Transport;
using PerformanceFigures = AerospacePropellantThermodynamics.Performance.PerformanceFigures;
using ProblemKind = AerospacePropellantThermodynamics.Equilibrium.ProblemKind;

namespace AerospacePropellantThermodynamics.Cli.Tests;

/// <summary>
/// L2: the executable's numbers are the library's numbers. The library call is built from the fixture the example document
/// encodes, not from the document, so that a unit or a value changed in the document is seen.
/// </summary>
[Collection(CliCollection.Name)]
public sealed class LibraryEqualityTests(CliFixture fixture)
{
    /// <summary>g0 of the root's invariant, m/s².</summary>
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

    /// <summary>Every field of every station, the compositions above the threshold, the mixture and its mass: exactly the library's values.</summary>
    private static void AssertCase(ElementalMixture mixture, double mixtureMass, IReadOnlyList<string> species, IReadOnlyList<Station> stations, JsonElement actual, double threshold)
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

        var actualStations = actual.GetProperty("stations").EnumerateArray().ToList();
        Assert.Equal(stations.Count, actualStations.Count);
        for (var s = 0; s < stations.Count; s++)
        {
            var station = stations[s];
            var element = actualStations[s];
            Assert.Equal(station.Name, element.GetProperty("name").GetString());
            Assert.Equal(CamelCase(station.Status.ToString()), element.GetProperty("status").GetString());
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
                Assert.Equal(CamelCase(transportStatus.ToString()), transport.GetProperty("status").GetString());
                if (station.Transport is { } figures2)
                {
                    AssertFields(figures2, transport, $"{station.Name} transport");
                }
            }
            else
            {
                Assert.False(element.TryGetProperty("transport", out _));
            }

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
    }

    /// <summary>Every public field of the struct, by reflection, against the property of the same camel-case name: exact.</summary>
    private static void AssertFields<T>(T value, JsonElement element, string label) where T : struct
    {
        foreach (var field in typeof(T).GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            var name = CamelCase(field.Name);
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

    private static string CamelCase(string name) => char.ToLowerInvariant(name[0]) + name[1..];
}
