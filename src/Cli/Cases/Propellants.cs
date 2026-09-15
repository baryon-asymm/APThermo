using APThermo.Cli.Documents;
using APThermo.Data;
using APThermo.Problems;

namespace APThermo.Cli.Cases;

/// <summary>Propellant documents into the library's definitions.</summary>
internal static class Propellants
{
    public static Propellant Build(SpeciesDatabase database, ReactantPropellant document)
    {
        var builder = Propellant.From(database);
        foreach (var r in document.Reactants)
        {
            if (r.Custom is { } custom)
            {
                builder.Custom(Reactant.Custom(r.Name, custom, r.Role, r.Amount, r.AmountKind));
            }
            else
            {
                builder.Add(Reactant.FromDatabase(r.Name, r.Role, r.Amount, r.Temperature, r.AmountKind));
            }
        }

        if (document.OxidizerToFuel is { } ratio)
        {
            builder.OxidizerToFuelRatio(ratio);
        }

        if (document.Omit.Count > 0)
        {
            builder.Omit([.. document.Omit]);
        }

        if (document.Only is not null)
        {
            builder.Only([.. document.Only]);
        }

        return builder.Build();
    }

    public static ElementalMixture Build(ElementalPropellant document, double massTolerance) =>
        ElementalMixture.Create(document.ElementMoles, document.Enthalpy, document.Omit, document.Only, massTolerance);
}
