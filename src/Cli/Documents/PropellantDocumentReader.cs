using APThermo.Data;
using APThermo.Problems;

namespace APThermo.Cli.Documents;

/// <summary>Reads the propellant part of a problem document (API.md, Input document): reactants or element moles, a custom reactant's formula.</summary>
internal static class PropellantDocumentReader
{
    public static PropellantDocument Read(StrictObject propellant)
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

    private static CustomReactantDefinition? ReadCustomPart(string path, double? temperature, IReadOnlyDictionary<string, double>? formula, double? enthalpy, double? molarMass) =>
        formula is null
            ? enthalpy is not null || molarMass is not null
                ? throw new InputException($"'enthalpy' and 'molarMass' at {path} belong to a custom reactant, which needs a 'formula'")
                : null
            : enthalpy is null
                ? throw new InputException($"missing field 'enthalpy' at {path}: a custom reactant needs its enthalpy (J/mol) at its temperature")
                : temperature is null
                    ? throw new InputException($"missing field 'temperature' at {path}: a custom reactant needs the temperature of its enthalpy")
                    : new CustomReactantDefinition([.. formula.Select(pair => new ElementCount(pair.Key, pair.Value))], enthalpy.Value, temperature.Value, molarMass);
}
