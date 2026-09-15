using System.Text.Json;
using AerospacePropellantThermodynamics.Fixtures;
using AerospacePropellantThermodynamics.Performance;
using AerospacePropellantThermodynamics.Problems;

namespace AerospacePropellantThermodynamics.Benchmarks;

/// Turns the JSON `case.inputs` of a tp, hp, sp or rocket fixture into a `StateRecord`
/// (BOOT.md, Invariants): a fixture's `elementMoles` and its target already are the
/// element-moles-and-enthalpy shape `StateRecord` takes, so no `Propellant` or
/// `Reactant` construction is needed to read one.
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

    private static double? ReadOptionalDouble(JsonElement inputs, string property) =>
        inputs.TryGetProperty(property, out var value) && value.ValueKind != JsonValueKind.Null
            ? value.GetDouble()
            : null;
}
