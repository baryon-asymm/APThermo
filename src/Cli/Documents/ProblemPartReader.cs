using AerospacePropellantThermodynamics.Performance;
using ProblemKind = AerospacePropellantThermodynamics.Equilibrium.ProblemKind;

namespace AerospacePropellantThermodynamics.Cli.Documents;

/// <summary>Reads the `problem` object of a problem document (API.md, Input document): one reader per problem kind.</summary>
internal static class ProblemPartReader
{
    public static ProblemDocument Read(StrictObject problem)
    {
        var type = problem.String("type");
        return type switch
        {
            "rocket" => ReadRocket(problem),
            "equilibrium" => ReadEquilibrium(problem),
            _ => throw new InputException($"unknown problem type '{type}' at {problem.Path}.type; rocket or equilibrium"),
        };
    }

    private static RocketDocument ReadRocket(StrictObject problem)
    {
        var chamberPressure = problem.Number("chamberPressure");
        var flow = DocumentWords.ParseFlow(problem.OptionalString("flow") ?? DocumentWords.FlowShifting, problem.Path + ".flow");
        var areaRatios = problem.OptionalNumberList("areaRatios") ?? [];
        var pressureRatios = problem.OptionalNumberList("pressureRatios") ?? [];
        var transport = problem.OptionalBool("transport", false);
        var estimate = problem.OptionalNumber("temperatureEstimate") ?? 0.0;
        problem.Finish();
        return new RocketDocument(chamberPressure, flow, areaRatios, pressureRatios, transport, estimate);
    }

    private static EquilibriumDocument ReadEquilibrium(StrictObject problem)
    {
        var kind = DocumentWords.ParseProblemKind(problem.String("kind"), problem.Path + ".kind");
        var pressure = problem.Number("pressure");
        var temperature = problem.OptionalNumber("temperature");
        var enthalpy = problem.OptionalNumber("enthalpy");
        var entropy = problem.OptionalNumber("entropy");
        var transport = problem.OptionalBool("transport", false);
        problem.Finish();
        switch (kind)
        {
            case ProblemKind.AssignedTemperaturePressure:
                Forbid(enthalpy, "enthalpy", "tp", problem.Path);
                Forbid(entropy, "entropy", "tp", problem.Path);
                if (temperature is null)
                {
                    throw new InputException($"missing field 'temperature' at {problem.Path}: a tp problem assigns the temperature");
                }

                break;
            case ProblemKind.AssignedEnthalpyPressure:
                Forbid(entropy, "entropy", "hp", problem.Path);
                break;
            default:
                Forbid(enthalpy, "enthalpy", "sp", problem.Path);
                if (entropy is null)
                {
                    throw new InputException($"missing field 'entropy' at {problem.Path}: an sp problem assigns the entropy");
                }

                break;
        }

        return new EquilibriumDocument(kind, pressure, temperature, enthalpy, entropy, transport);
    }

    private static void Forbid(double? value, string name, string kind, string path)
    {
        if (value is not null)
        {
            throw new InputException($"the field '{name}' at {path} does not belong to a {kind} problem");
        }
    }
}
