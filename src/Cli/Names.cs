using AerospacePropellantThermodynamics.Execution;
using AerospacePropellantThermodynamics.Thermo;
using ProblemKind = AerospacePropellantThermodynamics.Equilibrium.ProblemKind;

namespace AerospacePropellantThermodynamics.Cli;

/// <summary>Names in documents: camel case of the library's names, of statuses and of kinds.</summary>
internal static class Names
{
    public static string Camel(string name) => name.Length == 0 ? name : char.ToLowerInvariant(name[0]) + name[1..];

    public static string Status(CaseStatus status) => Camel(status.ToString());

    public static string Accelerator(AcceleratorKind kind) => Camel(kind.ToString());

    public static string Kind(ProblemKind kind) => kind switch
    {
        ProblemKind.AssignedTemperaturePressure => "tp",
        ProblemKind.AssignedEnthalpyPressure => "hp",
        ProblemKind.AssignedEntropyPressure => "sp",
        _ => Camel(kind.ToString()),
    };
}
