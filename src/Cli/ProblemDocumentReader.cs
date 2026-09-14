using System.Text.Json;
using AerospacePropellantThermodynamics.Data;
using AerospacePropellantThermodynamics.Execution;
using AerospacePropellantThermodynamics.Performance;
using AerospacePropellantThermodynamics.Problems;
using ProblemKind = AerospacePropellantThermodynamics.Equilibrium.ProblemKind;

namespace AerospacePropellantThermodynamics.Cli;

/// <summary>Reads a rocket or equilibrium problem document (API.md, Input document): the propellant, the problem, the sweep.</summary>
internal static class ProblemDocumentReader
{
    public static InputDocument Read(string text, string source)
    {
        using var document = JsonText.Parse(text, source);
        try
        {
            var root = new StrictObject(document.RootElement, "$");
            var propellant = ReadPropellant(root.Object("propellant"));
            var problem = ReadProblemPart(root.Object("problem"));
            var sweep = root.OptionalObject("sweep") is { } s ? ReadSweep(s, problem, propellant) : null;
            AcceleratorKind? accelerator = null;
            if (root.OptionalObject("engine") is { } engine)
            {
                accelerator = DocumentWords.ParseAccelerator(engine.String("accelerator"), engine.Path + ".accelerator");
                engine.Finish();
            }

            root.Finish();
            return new InputDocument(propellant, problem, sweep, accelerator);
        }
        catch (InputException e)
        {
            throw new InputException($"{source}: {e.Message}");
        }
    }

    private static PropellantDocument ReadPropellant(StrictObject propellant)
    {
        var omit = propellant.OptionalStringList("omit") ?? [];
        var only = propellant.OptionalStringList("only");
        if (only is { Count: 0 })
        {
            throw new InputException($"the field 'only' at {propellant.Path} is empty; leave it out to select the species from the elements");
        }

        if (propellant.Has("elementMoles"))
        {
            var moles = propellant.NumberMap("elementMoles");
            var enthalpy = propellant.OptionalNumber("enthalpy");
            propellant.Finish();
            return new ElementalPropellant(moles, enthalpy, omit, only);
        }

        var reactants = propellant.ObjectList("reactants").Select(ReadReactant).ToList();
        if (reactants.Count == 0)
        {
            throw new InputException($"the list at {propellant.Path}.reactants is empty");
        }

        double? ratio = null;
        if (propellant.OptionalObject("mixture") is { } mixture)
        {
            ratio = mixture.Number("oxidizerToFuel");
            mixture.Finish();
        }

        propellant.Finish();
        return new ReactantPropellant(reactants, ratio, omit, only);
    }

    private static ReactantDocument ReadReactant(StrictObject reactant)
    {
        var name = reactant.String("name");
        var role = DocumentWords.ParseRole(reactant.String("role"), reactant.Path + ".role");
        var amount = reactant.Number("amount");
        var amountKind = DocumentWords.ParseAmountKind(reactant.OptionalString("amountKind"), reactant.Path + ".amountKind");
        var temperature = reactant.OptionalNumber("temperature");
        var formula = reactant.OptionalNumberMap("formula");
        var enthalpy = reactant.OptionalNumber("enthalpy");
        var molarMass = reactant.OptionalNumber("molarMass");
        var custom = ReadCustomPart(reactant.Path, temperature, formula, enthalpy, molarMass);
        reactant.Finish();
        return new ReactantDocument(name, role, amount, amountKind) { Temperature = temperature, Custom = custom };
    }

    private static CustomReactantDefinition? ReadCustomPart(string path, double? temperature, IReadOnlyDictionary<string, double>? formula, double? enthalpy, double? molarMass)
    {
        if (formula is null)
        {
            if (enthalpy is not null || molarMass is not null)
            {
                throw new InputException($"'enthalpy' and 'molarMass' at {path} belong to a custom reactant, which needs a 'formula'");
            }

            return null;
        }

        if (enthalpy is null)
        {
            throw new InputException($"missing field 'enthalpy' at {path}: a custom reactant needs its enthalpy (J/mol) at its temperature");
        }

        if (temperature is null)
        {
            throw new InputException($"missing field 'temperature' at {path}: a custom reactant needs the temperature of its enthalpy");
        }

        return new CustomReactantDefinition(formula.Select(pair => new ElementCount(pair.Key, pair.Value)).ToList(), enthalpy.Value, temperature.Value, molarMass);
    }

    private static ProblemDocument ReadProblemPart(StrictObject problem)
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

    private static SweepDocument ReadSweep(StrictObject sweep, ProblemDocument problem, PropellantDocument propellant)
    {
        IReadOnlyList<double>? Values(string name) => sweep.OptionalAny(name) is { } value ? SweepValues.Read(value, $"{sweep.Path}.{name}") : null;

        var ratios = Values("oxidizerToFuel");
        IReadOnlyList<double>? chamberPressures = null, pressures = null, temperatures = null;
        if (problem is RocketDocument)
        {
            chamberPressures = Values("chamberPressure");
        }
        else
        {
            pressures = Values("pressure");
            if (((EquilibriumDocument)problem).Kind == ProblemKind.AssignedTemperaturePressure)
            {
                temperatures = Values("temperature");
            }
        }

        sweep.Finish();
        if (ratios is not null && propellant is not ReactantPropellant { OxidizerToFuel: not null })
        {
            throw new InputException($"a sweep over oxidizerToFuel at {sweep.Path} needs a propellant given with mixture.oxidizerToFuel");
        }

        return new SweepDocument(ratios, chamberPressures, pressures, temperatures);
    }
}
