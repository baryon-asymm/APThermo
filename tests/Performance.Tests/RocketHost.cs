using System.Text.Json;
using AerospacePropellantThermodynamics.Data;
using AerospacePropellantThermodynamics.Equilibrium;
using AerospacePropellantThermodynamics.Fixtures;
using AerospacePropellantThermodynamics.Thermo;
using ILGPU.Runtime;

namespace AerospacePropellantThermodynamics.Performance.Tests;

/// <summary>The inputs of one rocket case as the solver takes them, read from a fixture or given directly.</summary>
internal sealed record RocketInputs(
    string[] Elements, double[] ElementMoles, string[] Products, double ChamberPressure, double ReactantEnthalpy,
    FlowModel Flow, double[] ExitValues, ExitSpecification[] ExitKinds)
{
    public int ExitCount => ExitValues.Length;

    /// <summary>A key identifying the table and the exit layout: cases with equal keys can share a batch.</summary>
    public string BatchKey => string.Join(",", Elements) + "|" + string.Join(",", Products) + "|" + string.Join(",", ExitKinds);

    public static FlowModel FlowOf(string flow) => flow switch
    {
        "shiftingEquilibrium" => FlowModel.ShiftingEquilibrium,
        "frozenAtChamber" => FlowModel.FrozenAtChamber,
        "frozenAtThroat" => FlowModel.FrozenAtThroat,
        _ => throw new ArgumentException($"unknown flow model {flow}", nameof(flow)),
    };

    /// <summary>
    /// The fixture's exits in the reference's station order: pressure ratios, then the supersonic area ratios. Subsonic area
    /// ratios are not solved by version 1 and are left out; <see cref="FixtureStationsOf"/> maps the solver's stations back.
    /// </summary>
    public static RocketInputs Of(CeaCase c)
    {
        var inputs = c.Inputs;
        var elements = inputs.GetProperty("elementMoles").EnumerateObject().Select(p => p.Name).ToArray();
        var elementMoles = inputs.GetProperty("elementMoles").EnumerateObject().Select(p => p.Value.GetDouble()).ToArray();
        var products = inputs.GetProperty("products").EnumerateArray().Select(e => e.GetString()!).ToArray();
        var pressureRatios = inputs.GetProperty("pressureRatios").EnumerateArray().Select(e => e.GetDouble()).ToArray();
        var areaRatios = inputs.GetProperty("areaRatios").EnumerateArray().Select(e => e.GetDouble()).ToArray();
        var values = pressureRatios.Concat(areaRatios).ToArray();
        var kinds = pressureRatios.Select(_ => ExitSpecification.PressureRatio).Concat(areaRatios.Select(_ => ExitSpecification.AreaRatio)).ToArray();
        return new RocketInputs(elements, elementMoles, products, inputs.GetProperty("chamberPressure").GetDouble(),
                                inputs.GetProperty("reactantEnthalpy").GetDouble(), FlowOf(inputs.GetProperty("flow").GetString()!), values, kinds);
    }

    /// <summary>The fixture stations the solver's stations correspond to, in solver order: chamber, throat, then the exits without the subsonic ones.</summary>
    public static IReadOnlyList<JsonElement> FixtureStationsOf(CeaCase c) =>
        c.Outputs.GetProperty("stations").EnumerateArray()
            .Where(s => !s.GetProperty("station").GetString()!.StartsWith("subsonic", StringComparison.Ordinal))
            .ToList();
}

/// <summary>What one host call of the rocket solver produced.</summary>
internal sealed record RocketSolution(
    SpeciesTable Table, RocketInputs Inputs, MixtureState[] Stations, double[] Moles, double[] Multipliers,
    PerformanceFigures[] Figures, CaseStatus[] StationStatus, int[] Iterations, CaseStatus Status)
{
    public int StationCount => Stations.Length;

    public double TotalMoles(int station) => Enumerable.Range(0, Table.SpeciesCount).Sum(j => Moles[station * Table.SpeciesCount + j]);

    /// <summary>The reference reports one fraction per database name: the pieces of a cut species sum under it.</summary>
    public double MoleFraction(int station, string species) =>
        Table.IndicesOf(species).Sum(index => Moles[station * Table.SpeciesCount + index]) / TotalMoles(station);
}

/// <summary>Runs the rocket solver on the host over CPU-accelerator buffers.</summary>
internal static class RocketHost
{
    public static RocketSolution Solve(CpuFixture fixture, CeaCase c) => Solve(fixture, RocketInputs.Of(c));

    public static RocketSolution Solve(CpuFixture fixture, RocketInputs inputs) =>
        Solve(fixture.Accelerator, SpeciesTable.Build(fixture.Database, inputs.Elements, inputs.Products), inputs);

    public static RocketSolution Solve(Accelerator accelerator, SpeciesTable table, RocketInputs inputs)
    {
        using var rocketCase = new RocketCase(accelerator, table, inputs);
        var context = rocketCase.Context;
        RocketSolver.Solve(in context.Table, in context.Problem, in context.Scratch, in context.Result);
        return rocketCase.Read();
    }

    /// <summary>The rocket fixture files as theory data: the file name without extension.</summary>
    public static IEnumerable<object[]> Cases() =>
        FixtureFiles.Enumerate("rocket").Select(path => new object[] { Path.GetFileNameWithoutExtension(path) });

    public static CeaCase Load(string name) => CeaFixtures.Load(Path.Combine(FixtureFiles.Root, "rocket", name + ".json"));
}
