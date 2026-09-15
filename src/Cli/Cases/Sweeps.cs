using APThermo.Cli.Documents;

namespace APThermo.Cli.Cases;

/// <summary>The Cartesian product of a sweep, ratio-major, then chamber pressure, then pressure, then temperature: the order of the cases in the batch and in the document (API.md, `sweep`).</summary>
internal static class Sweeps
{
    public static IReadOnlyList<Combination> Expand(SweepDocument? sweep)
    {
        var ratios = Axis(sweep?.OxidizerToFuel);
        var chamberPressures = Axis(sweep?.ChamberPressure);
        var pressures = Axis(sweep?.Pressure);
        var temperatures = Axis(sweep?.Temperature);

        return (from ratio in ratios
                from chamberPressure in chamberPressures
                from pressure in pressures
                from temperature in temperatures
                select new Combination(ratio, chamberPressure, pressure, temperature)).ToList();
    }

    private static IReadOnlyList<double?> Axis(IReadOnlyList<double>? values) => values?.Select(v => (double?)v).ToList() ?? [null];
}
