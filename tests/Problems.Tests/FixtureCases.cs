using System.Text.Json;
using APThermo.Data;
using APThermo.Equilibrium;
using APThermo.Execution;
using APThermo.Fixtures;
using APThermo.Performance;
using APThermo.Thermo;
using APThermo.Transport;

namespace APThermo.Problems.Tests;

/// <summary>Turns fixture documents into propellants, problems and state records, as a user of the library would write them.</summary>
internal static class FixtureCases
{
    /// <summary>The kinds whose inputs carry reactants.</summary>
    public static readonly IReadOnlyList<string> KindsWithReactants = ["rocket", "tp", "hp", "sp"];

    /// <summary>Unit of the fixtures' element moles (kmol per kg) in the library's (mol per kg).</summary>
    public const double KilomolesToMoles = 1.0e3;

    /// <summary>Every fixture name of <paramref name="kind"/>, one theory row each.</summary>
    public static TheoryData<string> Names(string kind) =>
        [.. FixtureFiles.Enumerate(kind).Select(path => Path.GetFileNameWithoutExtension(path)!)];

    /// <summary>Every (kind, name) pair of <see cref="KindsWithReactants"/>, one theory row each.</summary>
    public static TheoryData<string, string> NamesWithReactants()
    {
        var data = new TheoryData<string, string>();
        foreach (var kind in KindsWithReactants)
        {
            foreach (var path in FixtureFiles.Enumerate(kind))
            {
                data.Add(kind, Path.GetFileNameWithoutExtension(path)!);
            }
        }

        return data;
    }

    public static CeaCase Load(string kind, string name) => CeaFixtures.Load(Path.Combine(FixtureFiles.Root, kind, name + ".json"));

    public static double? OxidizerToFuelRatioOf(CeaCase c)
    {
        var ratio = c.Inputs.GetProperty("oxidizerToFuelRatio");
        return ratio.ValueKind == JsonValueKind.Null ? null : ratio.GetDouble();
    }

    /// <summary>
    /// The role the fixture recorded for one reactant of a ratio case (Fixtures BOOT.md): written by the generator
    /// from the oxidizer and fuel vectors it passed to the package, never guessed here from the reactant's name (the
    /// gap the sodium case found, `NaNO3(a)` missing from a hand-typed set of oxidizer names).
    /// </summary>
    private static ReactantRole RoleOf(JsonElement reactant) => reactant.GetProperty("role").GetString() switch
    {
        "oxidizer" => ReactantRole.Oxidizer,
        "fuel" => ReactantRole.Fuel,
        var other => throw new ArgumentException($"unknown reactant role '{other}'", nameof(reactant)),
    };

    /// <summary>
    /// The propellant as the fixture describes it: with the reference's ratio and roles when it has one (or the ratio given instead),
    /// else by total mass fractions; <paramref name="byMassFractions"/> takes the recorded mass fractions as they are, without a ratio.
    /// </summary>
    public static Propellant PropellantOf(SpeciesDatabase database, CeaCase c, double? ratioOverride = null, bool byMassFractions = false)
    {
        var inputs = c.Inputs;
        var builder = Propellant.From(database);
        var ratio = byMassFractions ? null : OxidizerToFuelRatioOf(c);
        if (ratioOverride is not null)
        {
            ratio = ratio is null ? throw new ArgumentException("the fixture has no oxidizer-to-fuel ratio to override", nameof(ratioOverride)) : ratioOverride;
        }
        foreach (var r in inputs.GetProperty("reactants").EnumerateArray())
        {
            var name = r.GetProperty("name").GetString()!;
            var massFraction = r.GetProperty("massFraction").GetDouble();
            var t = r.GetProperty("temperature");
            double? temperature = t.ValueKind == JsonValueKind.Null ? null : t.GetDouble();
            var role = ratio is null ? ReactantRole.Named : RoleOf(r);
            if (r.TryGetProperty("custom", out var custom) && custom.GetBoolean())
            {
                var formula = r.GetProperty("formula").EnumerateObject().Select(p => new ElementCount(p.Name, p.Value.GetDouble())).ToList();
                var definition = new CustomReactantDefinition(formula, r.GetProperty("enthalpy").GetDouble(), temperature!.Value);
                _ = builder.Custom(Reactant.Custom(name, definition, role, massFraction));
            }
            else
            {
                _ = builder.Add(Reactant.FromDatabase(name, role, massFraction, temperature));
            }
        }

        if (ratio is { } value)
        {
            _ = builder.OxidizerToFuelRatio(value);
        }

        var omit = OmitOf(c);
        if (omit.Count > 0)
        {
            _ = builder.Omit([.. omit]);
        }

        if (OnlyOf(c) is { } only)
        {
            _ = builder.Only([.. only]);
        }

        return builder.Build();
    }

    public static IReadOnlyList<string> OmitOf(CeaCase c) => [.. c.Inputs.GetProperty("omit").EnumerateArray().Select(e => e.GetString()!)];

    /// <summary>The explicit product list the reference was given, or null when it selected the products from the elements.</summary>
    public static IReadOnlyList<string>? OnlyOf(CeaCase c) =>
        c.Inputs.TryGetProperty("only", out var only) ? only.EnumerateArray().Select(e => e.GetString()!).ToList() : null;

    public static IReadOnlyList<string> ProductsOf(CeaCase c) => [.. c.Inputs.GetProperty("products").EnumerateArray().Select(e => e.GetString()!)];

    /// <summary>The fixture's element moles in kmol per kg, by symbol.</summary>
    public static IReadOnlyDictionary<string, double> ElementMolesOf(CeaCase c) =>
        c.Inputs.GetProperty("elementMoles").EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetDouble(), StringComparer.Ordinal);

    public static IReadOnlyDictionary<string, double> ReactantMassFractionsOf(CeaCase c) =>
        c.Inputs.GetProperty("reactants").EnumerateArray().ToDictionary(r => r.GetProperty("name").GetString()!, r => r.GetProperty("massFraction").GetDouble(), StringComparer.Ordinal);

    public static FlowModel FlowOf(string flow) => flow switch
    {
        "shiftingEquilibrium" => FlowModel.ShiftingEquilibrium,
        "frozenAtChamber" => FlowModel.FrozenAtChamber,
        "frozenAtThroat" => FlowModel.FrozenAtThroat,
        _ => throw new ArgumentException($"unknown flow model {flow}", nameof(flow)),
    };

    /// <summary>The rocket problem of a rocket fixture: pressure-ratio exits, then the supersonic area ratios; subsonic exits are not in version 1.</summary>
    public static RocketProblem RocketProblemOf(CeaCase c)
    {
        var inputs = c.Inputs;
        return new RocketProblem
        {
            ChamberPressure = inputs.GetProperty("chamberPressure").GetDouble(),
            Flow = FlowOf(inputs.GetProperty("flow").GetString()!),
            PressureRatios = [.. inputs.GetProperty("pressureRatios").EnumerateArray().Select(e => e.GetDouble())],
            AreaRatios = [.. inputs.GetProperty("areaRatios").EnumerateArray().Select(e => e.GetDouble())],
            Transport = inputs.GetProperty("transport").GetBoolean(),
        };
    }

    /// <summary>The equilibrium problem of a tp, hp or sp fixture.</summary>
    public static EquilibriumProblem EquilibriumProblemOf(CeaCase c)
    {
        var inputs = c.Inputs;
        var pressure = inputs.GetProperty("pressure").GetDouble();
        var transport = inputs.GetProperty("transport").GetBoolean();
        return c.Kind switch
        {
            "tp" => new EquilibriumProblem { Kind = ProblemKind.AssignedTemperaturePressure, Pressure = pressure, Temperature = inputs.GetProperty("temperature").GetDouble(), Transport = transport },
            "hp" => new EquilibriumProblem { Kind = ProblemKind.AssignedEnthalpyPressure, Pressure = pressure, Enthalpy = inputs.GetProperty("enthalpy").GetDouble(), Transport = transport },
            "sp" => new EquilibriumProblem { Kind = ProblemKind.AssignedEntropyPressure, Pressure = pressure, Entropy = inputs.GetProperty("entropy").GetDouble(), Transport = transport },
            var other => throw new ArgumentException($"unknown kind {other}"),
        };
    }

    /// <summary>The state record of a tp, hp or sp fixture: its element moles in mol per kg, its pressure and its one target.</summary>
    public static StateRecord StateRecordOf(CeaCase c)
    {
        var inputs = c.Inputs;
        var composition = ElementMolesOf(c).ToDictionary(kv => kv.Key, kv => kv.Value * KilomolesToMoles, StringComparer.Ordinal);
        var pressure = inputs.GetProperty("pressure").GetDouble();
        return c.Kind switch
        {
            "tp" => new StateRecord(pressure, composition, Temperature: inputs.GetProperty("temperature").GetDouble()),
            "hp" => new StateRecord(pressure, composition, Enthalpy: inputs.GetProperty("enthalpy").GetDouble()),
            "sp" => new StateRecord(pressure, composition, Entropy: inputs.GetProperty("entropy").GetDouble()),
            var other => throw new ArgumentException($"unknown kind {other}"),
        };
    }

    /// <summary>The fixture stations the result's stations correspond to: chamber, throat, then the exits without the subsonic ones.</summary>
    public static IReadOnlyList<JsonElement> ReferenceStationsOf(CeaCase c) =>
        [.. c.Outputs.GetProperty("stations").EnumerateArray().Where(s => !s.GetProperty("station").GetString()!.StartsWith("subsonic", StringComparison.Ordinal))];

    /// <summary>
    /// The stations at which the reference's reacting conductivity carries its documented defect (Fixtures BOOT.md): those where the
    /// tree's transport solver, evaluated on the reference's own composition, eliminates a trace component from the reaction set. The
    /// same signature the Transport tests use; on the tree's own composition the basis may differ and the signature vanish.
    /// </summary>
    public static IReadOnlySet<int> DefectiveStationsOf(SolverFixture fixture, CeaCase c, IReadOnlyList<string> species)
    {
        var defective = new HashSet<int>();
        if (!c.Inputs.GetProperty("transport").GetBoolean())
        {
            return defective;
        }

        var reference = ReferenceStationsOf(c);
        var table = SpeciesTable.Build(fixture.Database, [.. ElementMolesOf(c).Keys], species);
        var transport = TransportTable.Build(fixture.Database.Transport!, table);
        using var tables = fixture.Engine.Upload(table, transport);
        var batch = new TransportBatch(reference.Count, table.SpeciesCount);
        for (var s = 0; s < reference.Count; s++)
        {
            batch.Temperature[s] = reference[s].GetProperty("temperature").GetDouble();
            var mixtureMolarMass = reference[s].GetProperty("mixtureMolarMass").GetDouble();
            foreach (var entry in reference[s].GetProperty("moleFractions").EnumerateObject())
            {
                var indices = table.IndicesOf(entry.Name);
                if (indices.Count == 0)
                {
                    throw new InvalidOperationException($"{entry.Name} is not in the table");
                }

                // The whole fraction on the first piece of a cut species: the solver reads only the gases, which never split.
                batch.Moles[s * table.SpeciesCount + indices[0]] = entry.Value.GetDouble() / mixtureMolarMass;
            }
        }

        var result = fixture.Engine.Run(tables, batch);
        for (var s = 0; s < reference.Count; s++)
        {
            if (result.Status[s] == CaseStatus.Ok && result.Figures[s].TraceEliminations > 0)
            {
                _ = defective.Add(s);
            }
        }

        return defective;
    }
}
