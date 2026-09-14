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

        // The product built one axis at a time: each step crosses the combinations found so far with one more axis,
        // in the same order the four nested loops used to (ratio-major, then chamber pressure, then pressure, then
        // temperature), so the code reads as the rule it implements without nesting past the crossing itself.
        var combinations = ratios.Select(ratio => new Combination(ratio, null, null, null)).ToList();
        combinations = CrossedWith(combinations, chamberPressures, (c, v) => c with { ChamberPressure = v });
        combinations = CrossedWith(combinations, pressures, (c, v) => c with { Pressure = v });
        combinations = CrossedWith(combinations, temperatures, (c, v) => c with { Temperature = v });
        return combinations;
    }

    /// <summary>Every combination found so far, crossed with every value of one more axis, in that order.</summary>
    private static List<Combination> CrossedWith(List<Combination> combinations, List<double?> axis, Func<Combination, double?, Combination> assign)
    {
        var crossed = new List<Combination>();
        foreach (var combination in combinations)
        {
            foreach (var value in axis)
            {
                crossed.Add(assign(combination, value));
            }
        }

        return crossed;
    }
}
