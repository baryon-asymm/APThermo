using APThermo.Data;

namespace APThermo.Cli.Listings;

/// <summary>One species of the database, flattened once (F-CL-08): the fields both document forms of `species` read.</summary>
internal sealed record SpeciesRow
{
    public required string Name { get; init; }

    public required string Section { get; init; }

    public required string Phase { get; init; }

    public required IReadOnlyList<ElementCount> Formula { get; init; }

    public required double MolarMass { get; init; }

    public required double FormationEnthalpy { get; init; }

    /// <summary>Both set together, from the polynomial intervals; null for a record with none (<see cref="AssignedTemperature"/> then applies).</summary>
    public double? TemperatureLow { get; init; }

    public double? TemperatureHigh { get; init; }

    public double? AssignedTemperature { get; init; }

    public required bool TransportData { get; init; }

    public static SpeciesRow From(Species species, SpeciesDatabase database) => new()
    {
        Name = species.Name,
        Section = Names.Camel(species.Section.ToString()),
        Phase = Names.Camel(species.Phase.ToString()),
        Formula = species.Formula,
        MolarMass = species.MolarMass,
        FormationEnthalpy = species.FormationEnthalpy,
        TemperatureLow = species.Intervals.Count > 0 ? species.Intervals.Min(i => i.TLow) : null,
        TemperatureHigh = species.Intervals.Count > 0 ? species.Intervals.Max(i => i.THigh) : null,
        AssignedTemperature = species.Intervals.Count == 0 ? species.AssignedTemperature : null,
        TransportData = database.Transport?.Find(species.Name) is not null,
    };
}
