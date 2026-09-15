using APThermo.Execution;
using APThermo.Performance;
using APThermo.Problems;
using ProblemKind = APThermo.Equilibrium.ProblemKind;

namespace APThermo.Cli;

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

    public const string AcceleratorAuto = "auto";
    public const string AcceleratorCpu = "cpu";
    public const string AcceleratorCuda = "cuda";

    public const string KindAssignedTemperature = "tp";
    public const string KindAssignedEnthalpy = "hp";
    public const string KindAssignedEntropy = "sp";

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
        AcceleratorAuto => AcceleratorKind.Auto,
        AcceleratorCpu => AcceleratorKind.Cpu,
        AcceleratorCuda => AcceleratorKind.Cuda,
        _ => throw new InputException(path is null
            ? $"unknown accelerator '{value}'; auto, cpu or cuda"
            : $"unknown accelerator '{value}' at {path}; auto, cpu or cuda"),
    };

    /// <summary>The reverse of <see cref="ParseAccelerator"/>, for the <c>run</c> section and the devices listing.</summary>
    public static string Accelerator(AcceleratorKind kind) => kind switch
    {
        AcceleratorKind.Auto => AcceleratorAuto,
        AcceleratorKind.Cpu => AcceleratorCpu,
        AcceleratorKind.Cuda => AcceleratorCuda,
        _ => Names.Camel(kind.ToString()),
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
        KindAssignedTemperature => ProblemKind.AssignedTemperaturePressure,
        KindAssignedEnthalpy => ProblemKind.AssignedEnthalpyPressure,
        KindAssignedEntropy => ProblemKind.AssignedEntropyPressure,
        var other => throw new InputException($"unknown kind '{other}' at {path}; tp, hp or sp"),
    };

    /// <summary>The reverse of <see cref="ParseProblemKind"/>, for a case's <c>inputs</c> echo.</summary>
    public static string Kind(ProblemKind kind) => kind switch
    {
        ProblemKind.AssignedTemperaturePressure => KindAssignedTemperature,
        ProblemKind.AssignedEnthalpyPressure => KindAssignedEnthalpy,
        ProblemKind.AssignedEntropyPressure => KindAssignedEntropy,
        _ => Names.Camel(kind.ToString()),
    };
}
