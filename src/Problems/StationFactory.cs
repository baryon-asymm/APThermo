using AerospacePropellantThermodynamics.Performance;
using AerospacePropellantThermodynamics.Thermo;

namespace AerospacePropellantThermodynamics.Problems;

/// <summary>One station from one slice of the engine's flat result, and the species-name list a result reports (F-PR-11).</summary>
internal static class StationFactory
{
    /// <summary>The names of the stations RocketLayout.FixedStations counts, in order; one entry per fixed station.</summary>
    private static readonly string[] FixedNames = ["chamber", "throat"];

    /// <summary>The rocket station name at a flat station index: the fixed names, then "exit1", "exit2", … after them.</summary>
    public static string NameOf(int stationIndex) =>
        stationIndex < RocketLayout.FixedStations ? FixedNames[stationIndex] : $"exit{stationIndex - RocketLayout.FixedStations + 1}";

    /// <summary>One station: mole fractions and condensed mass fractions over the whole table, a cut record's pieces summed under its database name (BOOT.md, results).</summary>
    public static Station Create(string name, in StationSlice slice)
    {
        var table = slice.Table;
        var moles = slice.Moles;
        var offset = slice.Offset;
        var speciesCount = table.SpeciesCount;
        var total = 0.0;
        for (var j = 0; j < speciesCount; j++)
        {
            total += moles[offset + j];
        }

        var fractions = new Dictionary<string, double>(speciesCount, StringComparer.Ordinal);
        var condensed = new Dictionary<string, double>(table.CondensedCount, StringComparer.Ordinal);
        for (var j = 0; j < speciesCount; j++)
        {
            // A condensed record cut at a fit discontinuity reports the record's name, its pieces summed (BOOT.md, results).
            var species = table.Records[j].Name;
            var n = moles[offset + j];
            var fraction = total > 0.0 ? n / total : 0.0;
            fractions[species] = fractions.TryGetValue(species, out var f) ? f + fraction : fraction;
            if (j >= table.GasCount)
            {
                var massFraction = n * table.Arrays.MolarMass[j];
                condensed[species] = condensed.TryGetValue(species, out var w) ? w + massFraction : massFraction;
            }
        }

        return new Station(name, slice.State, slice.Performance, fractions, condensed, slice.Transport, slice.TransportStatus, slice.Status);
    }

    /// <summary>The species names a result reports: the table's, with the pieces of a cut condensed record collapsed to the record's name (BOOT.md, results).</summary>
    public static IReadOnlyList<string> SpeciesNames(SpeciesTable table)
    {
        var names = new List<string>(table.SpeciesCount);
        for (var i = 0; i < table.SpeciesCount; i++)
        {
            var name = table.Records[i].Name;
            if (names.Count == 0 || !string.Equals(names[^1], name, StringComparison.Ordinal))
            {
                names.Add(name);
            }
        }

        return names;
    }
}
