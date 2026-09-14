using System.Text.Json;
using AerospacePropellantThermodynamics.Data;
using AerospacePropellantThermodynamics.Equilibrium;
using AerospacePropellantThermodynamics.Fixtures;
using AerospacePropellantThermodynamics.Performance;
using AerospacePropellantThermodynamics.Thermo;
using AerospacePropellantThermodynamics.Transport;

namespace AerospacePropellantThermodynamics.Execution.Tests;

/// <summary>The inputs of one rocket fixture as the solver takes them.</summary>
internal sealed record RocketInputs(
    string[] Elements, double[] ElementMoles, string[] Products, double ChamberPressure, double ReactantEnthalpy,
    FlowModel Flow, double[] ExitValues, ExitSpecification[] ExitKinds, bool Transport)
{
    public string BatchKey => string.Join(",", Elements) + "|" + string.Join(",", Products) + "|" + string.Join(",", ExitKinds);

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
        return new RocketInputs(elements, elementMoles, products, inputs.GetProperty("chamberPressure").GetDouble(),
                                inputs.GetProperty("reactantEnthalpy").GetDouble(), flow,
                                pressureRatios.Concat(areaRatios).ToArray(),
                                pressureRatios.Select(_ => ExitSpecification.PressureRatio).Concat(areaRatios.Select(_ => ExitSpecification.AreaRatio)).ToArray(),
                                inputs.GetProperty("transport").GetBoolean());
    }
}

/// <summary>A family of rocket fixtures sharing one table and one exit layout, as one batch.</summary>
internal sealed record RocketFamily(string Name, SpeciesTable Table, TransportTable Transport, IReadOnlyList<string> Members, IReadOnlyList<RocketInputs> Inputs)
{
    public RocketBatch Batch()
    {
        var batch = new RocketBatch(Inputs.Count, Table.ElementCount, Inputs[0].ExitKinds);
        for (var k = 0; k < Inputs.Count; k++)
        {
            var input = Inputs[k];
            batch.ChamberPressure[k] = input.ChamberPressure;
            batch.ReactantEnthalpy[k] = input.ReactantEnthalpy;
            batch.Flow[k] = input.Flow;
            Array.Copy(input.ElementMoles, 0, batch.ElementMoles, k * Table.ElementCount, Table.ElementCount);
            Array.Copy(input.ExitValues, 0, batch.ExitValues, k * batch.Exits, batch.Exits);
        }

        return batch;
    }
}

/// <summary>Fixtures to families and batches: nothing here runs a solver, on the host or the accelerator.</summary>
internal static class FixtureBatches
{
    /// <summary>Every rocket fixture, grouped into families; the largest family first.</summary>
    public static IReadOnlyList<RocketFamily> RocketFamilies(SpeciesDatabase database)
    {
        var groups = new Dictionary<string, (List<string> Names, List<RocketInputs> Inputs)>(StringComparer.Ordinal);
        foreach (var path in FixtureFiles.Enumerate("rocket"))
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

        return groups.Values
            .OrderByDescending(g => g.Names.Count).ThenBy(g => g.Names[0], StringComparer.Ordinal)
            .Select(g =>
            {
                var table = SpeciesTable.Build(database, g.Inputs[0].Elements, g.Inputs[0].Products);
                return new RocketFamily(g.Names[0], table, TransportTable.Build(database.Transport!, table), g.Names, g.Inputs);
            })
            .ToList();
    }

    /// <summary>The family names as theory data.</summary>
    public static IEnumerable<object[]> FamilyNames(SpeciesDatabase database) => RocketFamilies(database).Select(f => new object[] { f.Name });

    public static RocketFamily Family(SpeciesDatabase database, string name) => RocketFamilies(database).Single(f => f.Name == name);

    /// <summary>
    /// A parametric sweep between two fixtures of one family: element moles and reactant enthalpy interpolated linearly (a mixture of the
    /// two propellants), the chamber pressure swept between the bounds, shifting equilibrium, the exits of the family.
    /// </summary>
    public static RocketBatch Sweep(RocketFamily family, string from, string to, int count, double pressureLow, double pressureHigh)
    {
        var a = family.Inputs[family.Members.ToList().IndexOf(from)];
        var b = family.Inputs[family.Members.ToList().IndexOf(to)];
        var elementCount = family.Table.ElementCount;
        var batch = new RocketBatch(count, elementCount, a.ExitKinds);
        const int mixtureSteps = 400;
        var pressureSteps = Math.Max(1, (count + mixtureSteps - 1) / mixtureSteps);
        for (var k = 0; k < count; k++)
        {
            var t = (k % mixtureSteps) / (double)(mixtureSteps - 1);
            var u = pressureSteps == 1 ? 0.0 : (k / mixtureSteps) / (double)(pressureSteps - 1);
            batch.ChamberPressure[k] = pressureLow + u * (pressureHigh - pressureLow);
            batch.ReactantEnthalpy[k] = (1.0 - t) * a.ReactantEnthalpy + t * b.ReactantEnthalpy;
            batch.Flow[k] = FlowModel.ShiftingEquilibrium;
            for (var i = 0; i < elementCount; i++)
            {
                batch.ElementMoles[k * elementCount + i] = (1.0 - t) * a.ElementMoles[i] + t * b.ElementMoles[i];
            }

            Array.Copy(a.ExitValues, 0, batch.ExitValues, k * batch.Exits, batch.Exits);
        }

        return batch;
    }

    /// <summary>The equilibrium fixtures (tp, hp, sp) sharing one table, as one batch with the table, in file order.</summary>
    public static (EquilibriumBatch Batch, SpeciesTable Table, IReadOnlyList<CeaCase> Cases) EquilibriumFamily(SpeciesDatabase database, string namePrefix)
    {
        var cases = new[] { "tp", "hp", "sp" }
            .SelectMany(kind => FixtureFiles.Enumerate(kind))
            .Select(CeaFixtures.Load)
            .Where(c => c.Name.StartsWith(namePrefix, StringComparison.Ordinal))
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
