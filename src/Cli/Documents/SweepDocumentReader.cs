using ProblemKind = APThermo.Equilibrium.ProblemKind;

namespace APThermo.Cli.Documents;

/// <summary>Reads the `sweep` object of a problem document (API.md, Input document): the ranges the batch's Cartesian product runs over.</summary>
internal static class SweepDocumentReader
{
    /// <summary>
    /// The most cases one document's sweep may expand to (the second hidden-defect audit of 2026-09-28, finding
    /// F4, and `API.md`, Input document): the product of the axes' own value counts, checked here before
    /// <see cref="APThermo.Cli.Cases.Sweeps.Expand"/> would materialize the Cartesian product itself and before any
    /// solve runs.
    /// </summary>
    public const long MaxCases = 10_000_000;

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

        CheckCaseCount(sweep.Path, ratios, chamberPressures, pressures, temperatures);
        return new SweepDocument(ratios, chamberPressures, pressures, temperatures);
    }

    private static void CheckCaseCount(
        string path, IReadOnlyList<double>? ratios, IReadOnlyList<double>? chamberPressures, IReadOnlyList<double>? pressures, IReadOnlyList<double>? temperatures)
    {
        var count = (long)(ratios?.Count ?? 1) * (chamberPressures?.Count ?? 1) * (pressures?.Count ?? 1) * (temperatures?.Count ?? 1);
        if (count > MaxCases)
        {
            throw new InputException($"the sweep at {path} would produce {count} cases, more than the limit of {MaxCases}");
        }
    }
}
