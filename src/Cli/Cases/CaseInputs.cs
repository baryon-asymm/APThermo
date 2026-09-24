using System.Text.Json.Nodes;
using APThermo.Problems;
using ProblemKind = APThermo.Equilibrium.ProblemKind;

namespace APThermo.Cli.Cases;

/// <summary>The `inputs` echo of a case, written once, as API.md (Output document) defines it for a rocket or an equilibrium case.</summary>
internal static class CaseInputs
{
    public static JsonObject Rocket(double? ratio, RocketProblem problem)
    {
        var inputs = Start(ratio);
        inputs["chamberPressure"] = problem.ChamberPressure;
        return inputs;
    }

    public static JsonObject Equilibrium(double? ratio, EquilibriumProblem problem, ElementalMixture mixture)
    {
        var inputs = Start(ratio);
        inputs["kind"] = DocumentWords.Kind(problem.Kind);
        inputs["pressure"] = problem.Pressure;
        AddTarget(inputs, problem, mixture);
        return inputs;
    }

    private static JsonObject Start(double? ratio)
    {
        var inputs = new JsonObject();
        if (ratio is { } value)
        {
            inputs["oxidizerToFuel"] = value;
        }

        return inputs;
    }

    private static void AddTarget(JsonObject inputs, EquilibriumProblem problem, ElementalMixture mixture)
    {
        switch (problem.Kind)
        {
            case ProblemKind.AssignedTemperaturePressure:
                inputs["temperature"] = problem.Temperature;
                break;
            case ProblemKind.AssignedEnthalpyPressure:
                if ((problem.Enthalpy ?? mixture.Enthalpy) is { } enthalpy)
                {
                    inputs["enthalpy"] = enthalpy;
                }

                break;
            case ProblemKind.AssignedEntropyPressure:
            default:
                inputs["entropy"] = problem.Entropy;
                break;
        }
    }
}
