using System.Text.Json;
using APThermo.Fixtures;
using APThermo.Performance;
using APThermo.Problems;

namespace APThermo.Benchmarks;

/// Turns the JSON `case.inputs` of a tp, hp, sp or rocket fixture into a `StateRecord`
/// (BOOT.md, Invariants): a fixture's `elementMoles` and its target already are the
/// element-moles-and-enthalpy shape `StateRecord` takes, so no `Propellant` or
/// `Reactant` construction is needed to read one.
internal sealed record RocketFixtureInputs(double Pressure, double Enthalpy, FlowModel Flow, IReadOnlyList<double> AreaRatios);

internal static class FixtureStateRecords
{
    public static StateRecord EquilibriumRecord(CeaCase fixtureCase)
    {
        var inputs = fixtureCase.Inputs;
        var composition = FixtureJson.ReadComposition(inputs);
        var pressure = inputs.GetProperty("pressure").GetDouble();
        return new StateRecord(
            pressure, composition,
            ReadOptionalDouble(inputs, "enthalpy"),
            ReadOptionalDouble(inputs, "temperature"),
            ReadOptionalDouble(inputs, "entropy"));
    }

    public static StateRecord RocketRecord(CeaCase fixtureCase)
    {
        var inputs = fixtureCase.Inputs;
        var composition = FixtureJson.ReadComposition(inputs);
        var pressure = inputs.GetProperty("chamberPressure").GetDouble();
        var enthalpy = inputs.GetProperty("reactantEnthalpy").GetDouble();
        return new StateRecord(pressure, composition, enthalpy)
        {
            AreaRatios = FixtureJson.ReadDoubles(inputs, "areaRatios"),
            PressureRatios = FixtureJson.ReadDoubles(inputs, "pressureRatios"),
            Flow = ReadFlow(inputs),
        };
    }

    public static FlowModel ReadFlow(JsonElement inputs) => inputs.GetProperty("flow").GetString() switch
    {
        "shiftingEquilibrium" => FlowModel.ShiftingEquilibrium,
        "frozenAtChamber" => FlowModel.FrozenAtChamber,
        "frozenAtThroat" => FlowModel.FrozenAtThroat,
        var other => throw new FormatException($"unknown flow model '{other}'"),
    };

    /// The fields a rocket fixture's `case.inputs` gives a raw `Execution.RocketBatch`
    /// or a `Problems.RocketProblem` (group 1 and group 7 of BOOT.md, Constraints
    /// alike): shared so that the two paths solve the identical case.
    public static RocketFixtureInputs RocketBatchInputs(JsonElement inputs) => new(
        inputs.GetProperty("chamberPressure").GetDouble(),
        inputs.GetProperty("reactantEnthalpy").GetDouble(),
        ReadFlow(inputs),
        FixtureJson.ReadDoubles(inputs, "areaRatios"));

    private static double? ReadOptionalDouble(JsonElement inputs, string property) =>
        inputs.TryGetProperty(property, out var value) && value.ValueKind != JsonValueKind.Null
            ? value.GetDouble()
            : null;
}
