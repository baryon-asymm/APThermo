using ProblemKind = APThermo.Equilibrium.ProblemKind;

namespace APThermo.Cli.Documents;

/// <summary>Reads the `sweep` object of a problem document (API.md, Input document): the ranges the batch's Cartesian product runs over.</summary>
internal static class SweepDocumentReader
{
    public static SweepDocument Read(StrictObject sweep, ProblemDocument problem, PropellantDocument propellant)
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
