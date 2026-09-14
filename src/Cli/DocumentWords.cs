using AerospacePropellantThermodynamics.Execution;
using AerospacePropellantThermodynamics.Performance;
using AerospacePropellantThermodynamics.Problems;
using ProblemKind = AerospacePropellantThermodynamics.Equilibrium.ProblemKind;

namespace AerospacePropellantThermodynamics.Cli;

/// <summary>
/// The word spellings of the documents and the options, parsed into their enum, with the place (a JSON path, or an
/// option, with no path in the message) folded in by the caller. One statement of each word list, so a document and
/// an option never learn to spell the same word two ways.
/// </summary>
internal static class DocumentWords
{
    public const string FlowShifting = "shifting-equilibrium";
    public const string FlowFrozenAtChamber = "frozen-at-chamber";
    public const string FlowFrozenAtThroat = "frozen-at-throat";

    public static FlowModel ParseFlow(string value, string path) => value switch
    {
        FlowShifting => FlowModel.ShiftingEquilibrium,
        FlowFrozenAtChamber => FlowModel.FrozenAtChamber,
        FlowFrozenAtThroat => FlowModel.FrozenAtThroat,
        _ => throw new InputException($"unknown flow '{value}' at {path}; {FlowShifting}, {FlowFrozenAtChamber} or {FlowFrozenAtThroat}"),
    };

    /// <summary><paramref name="path"/> is the JSON path of the value, or null for a command-line option (no path in the message).</summary>
    public static AcceleratorKind ParseAccelerator(string value, string? path) => value switch
    {
        "auto" => AcceleratorKind.Auto,
        "cpu" => AcceleratorKind.Cpu,
        "cuda" => AcceleratorKind.Cuda,
        _ => throw new InputException(path is null
            ? $"unknown accelerator '{value}'; auto, cpu or cuda"
            : $"unknown accelerator '{value}' at {path}; auto, cpu or cuda"),
    };

    public static ReactantRole ParseRole(string value, string path) => value switch
    {
        "oxidizer" => ReactantRole.Oxidizer,
        "fuel" => ReactantRole.Fuel,
        "named" => ReactantRole.Named,
        var other => throw new InputException($"unknown role '{other}' at {path}; oxidizer, fuel or named"),
    };

    public static AmountKind ParseAmountKind(string? value, string path) => value switch
    {
        null or "mass-fraction" => AmountKind.MassFraction,
        "moles" => AmountKind.Moles,
        var other => throw new InputException($"unknown amountKind '{other}' at {path}; mass-fraction or moles"),
    };

    public static ProblemKind ParseProblemKind(string value, string path) => value switch
    {
        "tp" => ProblemKind.AssignedTemperaturePressure,
        "hp" => ProblemKind.AssignedEnthalpyPressure,
        "sp" => ProblemKind.AssignedEntropyPressure,
        var other => throw new InputException($"unknown kind '{other}' at {path}; tp, hp or sp"),
    };
}
