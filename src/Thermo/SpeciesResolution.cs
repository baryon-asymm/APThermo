using System.Globalization;
using AerospacePropellantThermodynamics.Data;

namespace AerospacePropellantThermodynamics.Thermo;

/// <summary>
/// Resolves one requested species name into its table pieces (BOOT.md, the join-and-cut of condensed records): the
/// name's product records, or the database's own record for a name no product carries; every element of its formula
/// among the table's, or the name is refused. A gaseous name resolves to its first product record, as the database
/// indexer does, and is never joined or split. The records of a condensed name are joined into one contiguous piece
/// when their formulas, molar masses and formation enthalpies agree and their ranges touch (F-TD-09: no same-name
/// group of the committed file disagrees in formation enthalpy, so a pair that does is refused like a differing formula
/// or molar mass), and the joined intervals are cut at a shared bound where two adjacent fits disagree by a real latent
/// heat.
/// </summary>
internal static class SpeciesResolution
{
    /// <summary>The gas piece of a gaseous name, or the condensed pieces of a condensed one.</summary>
    public static IReadOnlyList<TablePiece> Resolve(SpeciesDatabase database, IReadOnlyDictionary<string, int> elementIndex, string name)
    {
        var group = database.Records(name).Where(record => record.Section == SpeciesSection.Products).ToList();
        if (group.Count == 0)
        {
            group = [database[name]];
        }

        var first = group[0];
        foreach (var pair in first.Formula)
        {
            if (!elementIndex.ContainsKey(pair.Symbol))
            {
                throw new ArgumentException($"species '{name}' contains the element '{pair.Symbol}', which is not among the table's elements", "species");
            }
        }

        if (first.Phase == SpeciesPhase.Gas)
        {
            RequireIntervals(first, name);
            RequireIntervalLimit(first.Intervals.Count, name);
            return [new TablePiece(name, first, first.Intervals.Select(interval => (interval, first)).ToList())];
        }

        return Cut(Join(group, name), name);
    }

    private static List<(TemperatureInterval Interval, Species Source)> Join(List<Species> group, string name)
    {
        var intervals = new List<(TemperatureInterval Interval, Species Source)>();
        Species? previous = null;
        foreach (var record in group)
        {
            RequireIntervals(record, name);
            if (previous is not null && !Joins(previous, record))
            {
                throw new ArgumentException(
                    $"species '{name}' has {group.Count} records that cannot be joined into one species: they must share the formula, the molar mass and the formation enthalpy, and their ranges must touch",
                    "species");
            }

            intervals.AddRange(record.Intervals.Select(interval => (interval, record)));
            previous = record;
        }

        RequireIntervalLimit(intervals.Count, name);
        return intervals;
    }

    private static bool Joins(Species previous, Species record) =>
        record.Phase == SpeciesPhase.Condensed
        && SameFormula(previous, record)
        && record.MolarMass == previous.MolarMass
        && record.FormationEnthalpy == previous.FormationEnthalpy
        && record.Intervals[0].TLow == previous.Intervals[^1].THigh;

    private static IReadOnlyList<TablePiece> Cut(List<(TemperatureInterval Interval, Species Source)> intervals, string name)
    {
        var pieces = new List<List<(TemperatureInterval Interval, Species Source)>>();
        var piece = new List<(TemperatureInterval Interval, Species Source)> { intervals[0] };
        for (var k = 1; k < intervals.Count; k++)
        {
            var bound = intervals[k - 1].Interval.THigh;
            if (bound == intervals[k].Interval.TLow
                && Math.Abs(SpeciesFunctions.HOverRT(intervals[k].Interval, bound) - SpeciesFunctions.HOverRT(intervals[k - 1].Interval, bound))
                   >= SpeciesFunctions.LatentHeatThreshold)
            {
                pieces.Add(piece);
                piece = [];
            }

            piece.Add(intervals[k]);
        }

        pieces.Add(piece);
        var result = new List<TablePiece>(pieces.Count);
        foreach (var part in pieces)
        {
            var pieceName = pieces.Count == 1 ? name : $"{name}[{Bound(part[0].Interval.TLow)}-{Bound(part[^1].Interval.THigh)}]";
            result.Add(new TablePiece(pieceName, part[0].Source, part));
        }

        return result;
    }

    private static void RequireIntervals(Species record, string name)
    {
        if (record.Intervals.Count == 0)
        {
            throw new ArgumentException($"species '{name}' has no polynomial intervals (a reactant-only record) and cannot enter a table", "species");
        }
    }

    private static void RequireIntervalLimit(int intervals, string name)
    {
        if (intervals > TableLimits.MaxIntervalsPerSpecies)
        {
            throw new ArgumentException($"species '{name}' has {intervals} intervals, more than the limit of {TableLimits.MaxIntervalsPerSpecies}", "species");
        }
    }

    private static bool SameFormula(Species a, Species b)
    {
        if (a.Formula.Count != b.Formula.Count)
        {
            return false;
        }

        foreach (var pair in a.Formula)
        {
            if (!b.Formula.Any(other => string.Equals(other.Symbol, pair.Symbol, StringComparison.OrdinalIgnoreCase) && other.Count == pair.Count))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>A range bound in a piece name: kelvin, up to four decimals, invariant.</summary>
    private static string Bound(double value) => value.ToString("0.####", CultureInfo.InvariantCulture);
}
