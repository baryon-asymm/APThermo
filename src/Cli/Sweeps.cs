namespace AerospacePropellantThermodynamics.Cli;

/// <summary>The Cartesian product of a sweep, ratio-major, then pressure, then temperature: the rule of this node's input document.</summary>
internal static class Sweeps
{
    public static IReadOnlyList<Combination> Expand(SweepDocument? sweep)
    {
        var ratios = sweep?.OxidizerToFuel?.Select(v => (double?)v).ToList() ?? [null];
        var chamberPressures = sweep?.ChamberPressure?.Select(v => (double?)v).ToList() ?? [null];
        var pressures = sweep?.Pressure?.Select(v => (double?)v).ToList() ?? [null];
        var temperatures = sweep?.Temperature?.Select(v => (double?)v).ToList() ?? [null];
        var combinations = new List<Combination>();
        foreach (var ratio in ratios)
        {
            foreach (var chamberPressure in chamberPressures)
            {
                foreach (var pressure in pressures)
                {
                    foreach (var temperature in temperatures)
                    {
                        combinations.Add(new Combination(ratio, chamberPressure, pressure, temperature));
                    }
                }
            }
        }

        return combinations;
    }
}
