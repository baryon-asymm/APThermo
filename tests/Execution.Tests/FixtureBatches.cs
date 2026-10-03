using APThermo.Data;
using APThermo.Equilibrium;
using APThermo.Fixtures;
using APThermo.Performance;
using APThermo.Thermo;
using APThermo.Transport;

namespace APThermo.Execution.Tests;

/// <summary>The element list and the candidate species of one rocket fixture's chemical system: what a table is built from, and
/// what <see cref="RocketInputs.BatchKey"/> keys a family of fixtures by.</summary>
internal sealed record ChemicalSystem(string[] Elements, string[] Products);

/// <summary>What one rocket fixture starts from: the element moles per kilogram of propellant and the reactant enthalpy per kilogram.</summary>
internal sealed record Mixture(double[] ElementMoles, double ReactantEnthalpy);

/// <summary>The exits of one rocket fixture: a value and a kind (pressure or area ratio) per exit, in the reference's station order.</summary>
internal sealed record ExitPlan(double[] Values, ExitSpecification[] Kinds);

/// <summary>The inputs of one rocket fixture as the solver takes them.</summary>
internal sealed record RocketInputs(ChemicalSystem System, Mixture Mixture, double ChamberPressure, FlowModel Flow, ExitPlan Exits, bool Transport)
{
    public string BatchKey => string.Join(",", System.Elements) + "|" + string.Join(",", System.Products) + "|" + string.Join(",", Exits.Kinds);

    public static RocketInputs Of(CeaCase c)
    {
        var inputs = c.Inputs;
        var elements = inputs.GetProperty("elementMoles").EnumerateObject().Select(p => p.Name).ToArray();
        var elementMoles = inputs.GetProperty("elementMoles").EnumerateObject().Select(p => p.Value.GetDouble()).ToArray();
        var products = inputs.GetProperty("products").EnumerateArray().Select(e => e.GetString()!).ToArray();
        var pressureRatios = inputs.GetProperty("pressureRatios").EnumerateArray().Select(e => e.GetDouble()).ToArray();
        var areaRatios = inputs.GetProperty("areaRatios").EnumerateArray().Select(e => e.GetDouble()).ToArray();
        var flow = inputs.GetProperty("flow").GetString() switch
        {
            "shiftingEquilibrium" => FlowModel.ShiftingEquilibrium,
            "frozenAtChamber" => FlowModel.FrozenAtChamber,
            "frozenAtThroat" => FlowModel.FrozenAtThroat,
            var other => throw new ArgumentException($"unknown flow model {other}"),
        };
        var exitValues = pressureRatios.Concat(areaRatios).ToArray();
        var exitKinds = pressureRatios.Select(_ => ExitSpecification.PressureRatio).Concat(areaRatios.Select(_ => ExitSpecification.AreaRatio)).ToArray();
        return new RocketInputs(new ChemicalSystem(elements, products), new Mixture(elementMoles, inputs.GetProperty("reactantEnthalpy").GetDouble()),
                                inputs.GetProperty("chamberPressure").GetDouble(), flow, new ExitPlan(exitValues, exitKinds),
                                inputs.GetProperty("transport").GetBoolean());
    }
}

/// <summary>A family of rocket fixtures sharing one table and one exit layout, as one batch.</summary>
internal sealed record RocketFamily(string Name, SpeciesTable Table, TransportTable Transport, IReadOnlyList<string> Members, IReadOnlyList<RocketInputs> Inputs)
{
    public RocketBatch Batch()
    {
        var batch = new RocketBatch(Inputs.Count, Table.ElementCount, Inputs[0].Exits.Kinds);
        for (var k = 0; k < Inputs.Count; k++)
        {
            var input = Inputs[k];
            batch.ChamberPressure[k] = input.ChamberPressure;
            batch.ReactantEnthalpy[k] = input.Mixture.ReactantEnthalpy;
            batch.Flow[k] = input.Flow;
            Array.Copy(input.Mixture.ElementMoles, 0, batch.ElementMoles, k * Table.ElementCount, Table.ElementCount);
            Array.Copy(input.Exits.Values, 0, batch.ExitValues, k * batch.Exits, batch.Exits);
        }

        return batch;
    }
}

/// <summary>Fixtures to families and batches: nothing here runs a solver, on the host or the accelerator.</summary>
internal static class FixtureBatches
{
    /// <summary>The fixture kind of the rocket fixtures: what <see cref="RocketFamilies"/> reads by default.</summary>
    public const string RocketKind = "rocket";

    /// <summary>The fixture kind of the throat fixtures: rocket problems without exit stations, whose chambers and throats lie on phase plateaus.</summary>
    public const string ThroatKind = "throat";

    /// <summary>Every fixture of one rocket-shaped kind (<see cref="RocketKind"/> or <see cref="ThroatKind"/>), grouped into families; the largest family first.</summary>
    public static IReadOnlyList<RocketFamily> RocketFamilies(SpeciesDatabase database, string kind = RocketKind)
    {
        var groups = new Dictionary<string, (List<string> Names, List<RocketInputs> Inputs)>(StringComparer.Ordinal);
        foreach (var path in FixtureFiles.Enumerate(kind))
        {
            var c = CeaFixtures.Load(path);
            var inputs = RocketInputs.Of(c);
            if (!groups.TryGetValue(inputs.BatchKey, out var group))
            {
                groups[inputs.BatchKey] = group = ([], []);
            }

            group.Names.Add(c.Name);
            group.Inputs.Add(inputs);
        }

        return [.. groups.Values
            .OrderByDescending(g => g.Names.Count).ThenBy(g => g.Names[0], StringComparer.Ordinal)
            .Select(g =>
            {
                var table = SpeciesTable.Build(database, g.Inputs[0].System.Elements, g.Inputs[0].System.Products);
                return new RocketFamily(g.Names[0], table, TransportTable.Build(database.Transport!, table), g.Names, g.Inputs);
            })];
    }

    /// <summary>The family names of one kind as theory data.</summary>
    public static TheoryData<string> FamilyNames(SpeciesDatabase database, string kind = RocketKind)
    {
        var data = new TheoryData<string>();
        foreach (var family in RocketFamilies(database, kind))
        {
            data.Add(family.Name);
        }

        return data;
    }

    public static RocketFamily Family(SpeciesDatabase database, string name, string kind = RocketKind) => RocketFamilies(database, kind).Single(f => f.Name == name);

    /// <summary>
    /// A parametric sweep between two fixtures of one family: element moles and reactant enthalpy interpolated linearly (a mixture of the
    /// two propellants), the chamber pressure swept between the bounds, shifting equilibrium, the exits of the family.
    /// </summary>
    public static RocketBatch Sweep(RocketFamily family, string from, string to, int count, double pressureLow, double pressureHigh)
    {
        var a = family.Inputs[family.Members.ToList().IndexOf(from)];
        var b = family.Inputs[family.Members.ToList().IndexOf(to)];
        var elementCount = family.Table.ElementCount;
        var batch = new RocketBatch(count, elementCount, a.Exits.Kinds);
        const int mixtureSteps = 400;
        var pressureSteps = Math.Max(1, (count + mixtureSteps - 1) / mixtureSteps);
        for (var k = 0; k < count; k++)
        {
            var t = k % mixtureSteps / (double)(mixtureSteps - 1);
            var u = pressureSteps == 1 ? 0.0 : k / mixtureSteps / (double)(pressureSteps - 1);
            batch.ChamberPressure[k] = pressureLow + u * (pressureHigh - pressureLow);
            batch.ReactantEnthalpy[k] = (1.0 - t) * a.Mixture.ReactantEnthalpy + t * b.Mixture.ReactantEnthalpy;
            batch.Flow[k] = FlowModel.ShiftingEquilibrium;
            for (var i = 0; i < elementCount; i++)
            {
                batch.ElementMoles[k * elementCount + i] = (1.0 - t) * a.Mixture.ElementMoles[i] + t * b.Mixture.ElementMoles[i];
            }

            Array.Copy(a.Exits.Values, 0, batch.ExitValues, k * batch.Exits, batch.Exits);
        }

        return batch;
    }

    /// <summary>The equilibrium problem kinds an equilibrium family's cases are drawn from.</summary>
    private static readonly string[] EquilibriumKinds = ["tp", "hp", "sp"];

    /// <summary>
    /// The equilibrium families of the 0.2.1 fixtures by family name: the fixture-name prefixes of each, which share one element list,
    /// one candidate list and so one table. The three-element fixtures and the threshold-flip and gas-column salt fixtures of the tp kind.
    /// </summary>
    private static readonly Dictionary<string, string[]> NamedEquilibriumFamilies = new()
    {
        ["three-element-example1"] = ["three-element_rp1311-example1_"],
        ["three-element-example12"] = ["three-element_rp1311-example12_"],
        ["threshold-flip-kclo4"] = ["kclo4_T1150", "kclo4_T1200_p10bar"],
        ["threshold-flip-naclo4"] = ["naclo4_T1120"],
        ["gas-column-kclo4-lean"] = ["kclo4-lean_"],
    };

    /// <summary>The names of the 0.2.1 equilibrium families (<see cref="NamedEquilibriumFamily"/>) as theory data.</summary>
    public static TheoryData<string> NamedEquilibriumFamilyNames()
    {
        var data = new TheoryData<string>();
        foreach (var name in NamedEquilibriumFamilies.Keys)
        {
            data.Add(name);
        }

        return data;
    }

    /// <summary>One of the 0.2.1 equilibrium families by its name, as one batch with its table.</summary>
    public static (EquilibriumBatch Batch, SpeciesTable Table, IReadOnlyList<CeaCase> Cases) NamedEquilibriumFamily(SpeciesDatabase database, string name) =>
        EquilibriumFamily(database, NamedEquilibriumFamilies[name]);

    /// <summary>The equilibrium fixtures (tp, hp, sp) sharing one table whose names start with a prefix, as one batch with the table, in file order.</summary>
    public static (EquilibriumBatch Batch, SpeciesTable Table, IReadOnlyList<CeaCase> Cases) EquilibriumFamily(SpeciesDatabase database, string namePrefix) =>
        EquilibriumFamily(database, [namePrefix]);

    /// <summary>The equilibrium fixtures (tp, hp, sp) sharing one table whose names start with any of the prefixes, as one batch with the table, in file order.</summary>
    public static (EquilibriumBatch Batch, SpeciesTable Table, IReadOnlyList<CeaCase> Cases) EquilibriumFamily(SpeciesDatabase database, IReadOnlyList<string> namePrefixes)
    {
        var cases = EquilibriumKinds
            .SelectMany(FixtureFiles.Enumerate)
            .Select(CeaFixtures.Load)
            .Where(c => namePrefixes.Any(prefix => c.Name.StartsWith(prefix, StringComparison.Ordinal)))
            .ToList();
        Assert.NotEmpty(cases);
        var first = cases[0].Inputs;
        var elements = first.GetProperty("elementMoles").EnumerateObject().Select(p => p.Name).ToArray();
        var products = first.GetProperty("products").EnumerateArray().Select(e => e.GetString()!).ToArray();
        var table = SpeciesTable.Build(database, elements, products);
        var batch = new EquilibriumBatch(cases.Count, elements.Length);
        for (var k = 0; k < cases.Count; k++)
        {
            var c = cases[k];
            Assert.Equal(elements, c.Inputs.GetProperty("elementMoles").EnumerateObject().Select(p => p.Name).ToArray());
            Assert.Equal(products, c.Inputs.GetProperty("products").EnumerateArray().Select(e => e.GetString()!).ToArray());
            batch.Kind[k] = c.Kind switch
            {
                "tp" => ProblemKind.AssignedTemperaturePressure,
                "hp" => ProblemKind.AssignedEnthalpyPressure,
                "sp" => ProblemKind.AssignedEntropyPressure,
                var other => throw new ArgumentException($"unknown kind {other}"),
            };
            batch.Pressure[k] = c.Inputs.GetProperty("pressure").GetDouble();
            batch.Temperature[k] = c.Kind == "tp" ? c.Inputs.GetProperty("temperature").GetDouble() : 0.0;
            batch.Target[k] = c.Kind switch
            {
                "hp" => c.Inputs.GetProperty("enthalpy").GetDouble(),
                "sp" => c.Inputs.GetProperty("entropy").GetDouble(),
                _ => 0.0,
            };
            var moles = c.Inputs.GetProperty("elementMoles").EnumerateObject().Select(p => p.Value.GetDouble()).ToArray();
            Array.Copy(moles, 0, batch.ElementMoles, k * elements.Length, elements.Length);
        }

        return (batch, table, cases);
    }
}
