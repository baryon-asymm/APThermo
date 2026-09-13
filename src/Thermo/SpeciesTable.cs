using System.Globalization;
using AerospacePropellantThermodynamics.Data;

namespace AerospacePropellantThermodynamics.Thermo;

/// <summary>The flat host arrays of a species table, in the layout the kernels read. Do not modify them after the build.</summary>
public sealed class SpeciesTableArrays
{
    internal SpeciesTableArrays(
        double[] molarMass, double[] formationEnthalpy, double[] stoichiometry,
        int[] intervalStart, int[] intervalCount, double[] intervalBounds, double[] exponents, double[] coefficients)
    {
        MolarMass = molarMass;
        FormationEnthalpy = formationEnthalpy;
        Stoichiometry = stoichiometry;
        IntervalStart = intervalStart;
        IntervalCount = intervalCount;
        IntervalBounds = intervalBounds;
        Exponents = exponents;
        Coefficients = coefficients;
    }

    /// <summary>[species], kg/kmol.</summary>
    public double[] MolarMass { get; }

    /// <summary>[species], J/mol at 298.15 K, as in the record.</summary>
    public double[] FormationEnthalpy { get; }

    /// <summary>[element * SpeciesCount + species], atoms per formula unit.</summary>
    public double[] Stoichiometry { get; }

    /// <summary>[species], index of the species' first interval in the interval arrays.</summary>
    public int[] IntervalStart { get; }

    /// <summary>[species], number of intervals of the species.</summary>
    public int[] IntervalCount { get; }

    /// <summary>[interval * 2 + (0: TLow, 1: THigh)], K.</summary>
    public double[] IntervalBounds { get; }

    /// <summary>[interval * 8 + k], the exponents of T of the record.</summary>
    public double[] Exponents { get; }

    /// <summary>[interval * 9 + k]: a1 … a7, b1, b2.</summary>
    public double[] Coefficients { get; }

    /// <summary>The total number of intervals in the table.</summary>
    public int IntervalTotal => IntervalCount.Length == 0 ? 0 : IntervalBounds.Length / 2;
}

/// <summary>An immutable, ordered species table: a chosen subset of the database flattened for the kernels.</summary>
public sealed class SpeciesTable
{
    private readonly Dictionary<string, int> _index;

    private readonly Dictionary<string, int[]> _pieces;

    private SpeciesTable(IReadOnlyList<string> elements, IReadOnlyList<string> names, IReadOnlyList<Species> records, int gasCount, SpeciesTableArrays arrays)
    {
        Elements = elements;
        Records = records;
        Species = names;
        GasCount = gasCount;
        Arrays = arrays;
        _index = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < names.Count; i++)
        {
            _index[names[i]] = i;
        }

        var pieces = new Dictionary<string, List<int>>(StringComparer.Ordinal);
        for (var i = 0; i < records.Count; i++)
        {
            if (!pieces.TryGetValue(records[i].Name, out var list))
            {
                pieces[records[i].Name] = list = new List<int>(1);
            }

            list.Add(i);
        }

        _pieces = pieces.ToDictionary(kv => kv.Key, kv => kv.Value.ToArray(), StringComparer.Ordinal);
    }

    /// <summary>The element symbols, in the order given to the builder; their index is the row of the stoichiometry matrix.</summary>
    public IReadOnlyList<string> Elements { get; }

    /// <summary>
    /// The species names in table order: gaseous species first, then condensed, each group in the order given. A
    /// condensed species cut at a fit discontinuity (BOOT.md, join-and-cut) appears as one entry per piece, named
    /// <c>NAME[TLow-THigh]</c> over the piece's range, the pieces adjacent and ascending in the record's place.
    /// </summary>
    public IReadOnlyList<string> Species { get; }

    /// <summary>The database records in table order: for a joined or cut species, the record that provided the entry's first interval.</summary>
    public IReadOnlyList<Species> Records { get; }

    public int SpeciesCount => Records.Count;

    public int GasCount { get; }

    public int CondensedCount => Records.Count - GasCount;

    public int ElementCount => Elements.Count;

    /// <summary>The flat host arrays, ready to upload.</summary>
    public SpeciesTableArrays Arrays { get; }

    /// <summary>The table index of a species name, or −1 when the table does not hold it.</summary>
    public int IndexOf(string species) => _index.TryGetValue(species, out var index) ? index : -1;

    /// <summary>
    /// The table indices of a database name, adjacent and ascending in the record's place: the pieces of a species cut
    /// at a fit discontinuity (BOOT.md, join-and-cut), or the single entry of an uncut name. Empty when the table does
    /// not hold the name.
    /// </summary>
    public IReadOnlyList<int> IndicesOf(string species) => _pieces.TryGetValue(species, out var indices) ? indices : [];

    /// <summary>
    /// Builds a table from database records. Unknown names throw <see cref="KeyNotFoundException"/>; a species with an
    /// element outside <paramref name="elements"/>, without polynomial intervals, or beyond <see cref="TableLimits"/>
    /// throws <see cref="ArgumentException"/> naming it. Condensed records are joined and cut (BOOT.md): product
    /// records sharing one name are concatenated into one species when their formulas and molar masses agree and
    /// their ranges touch, and a species whose adjacent fits disagree at a shared internal bound by
    /// |ΔH°/RT| ≥ <see cref="SpeciesFunctions.LatentHeatThreshold"/> is cut there into one entry per piece.
    /// </summary>
    public static SpeciesTable Build(SpeciesDatabase database, IReadOnlyList<string> elements, IReadOnlyList<string> species)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(elements);
        ArgumentNullException.ThrowIfNull(species);
        if (elements.Count == 0)
        {
            throw new ArgumentException("a table needs at least one element", nameof(elements));
        }

        if (elements.Count > TableLimits.MaxElements)
        {
            throw new ArgumentException($"{elements.Count} elements exceed the limit of {TableLimits.MaxElements}", nameof(elements));
        }

        if (species.Count == 0)
        {
            throw new ArgumentException("a table needs at least one species", nameof(species));
        }

        if (species.Count > TableLimits.MaxSpecies)
        {
            throw new ArgumentException($"{species.Count} species exceed the limit of {TableLimits.MaxSpecies}", nameof(species));
        }

        var elementIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < elements.Count; i++)
        {
            if (!elementIndex.TryAdd(elements[i], i))
            {
                throw new ArgumentException($"element '{elements[i]}' is listed twice", nameof(elements));
            }
        }

        var products = new Dictionary<string, List<Species>>(StringComparer.Ordinal);
        foreach (var record in database.Products)
        {
            if (!products.TryGetValue(record.Name, out var list))
            {
                products[record.Name] = list = new List<Species>(1);
            }

            list.Add(record);
        }

        var gaseous = new List<Entry>();
        var condensed = new List<Entry>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var name in species)
        {
            if (!seen.Add(name))
            {
                throw new ArgumentException($"species '{name}' is listed twice", nameof(species));
            }

            var group = products.TryGetValue(name, out var found) ? found : [database[name]];
            var first = group[0];
            foreach (var pair in first.Formula)
            {
                if (!elementIndex.ContainsKey(pair.Symbol))
                {
                    throw new ArgumentException($"species '{name}' contains the element '{pair.Symbol}', which is not among the table's elements", nameof(species));
                }
            }

            if (first.Phase == SpeciesPhase.Gas)
            {
                // A gaseous name resolves to its first record, as the database index does; gases are never joined or split (BOOT.md).
                RequireIntervals(first, name);
                RequireIntervalLimit(first.Intervals.Count, name);
                gaseous.Add(new Entry(name, first, first.Intervals.Select(interval => (interval, first)).ToList()));
                continue;
            }

            // Join: concatenate the records of the name into one contiguous interval list.
            var intervals = new List<(TemperatureInterval Interval, Species Source)>();
            Species? previous = null;
            foreach (var record in group)
            {
                RequireIntervals(record, name);
                if (previous is not null
                    && (record.Phase != SpeciesPhase.Condensed || !SameFormula(previous, record)
                        || record.MolarMass != previous.MolarMass || record.Intervals[0].TLow != previous.Intervals[^1].THigh))
                {
                    throw new ArgumentException(
                        $"species '{name}' has {group.Count} records that cannot be joined into one species: they must share the formula and the molar mass, and their ranges must touch",
                        nameof(species));
                }

                intervals.AddRange(record.Intervals.Select(interval => (interval, record)));
                previous = record;
            }

            RequireIntervalLimit(intervals.Count, name);

            // Cut: a shared internal bound where the two fits disagree by a real latent heat starts a new piece.
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
            foreach (var part in pieces)
            {
                var entryName = pieces.Count == 1
                    ? name
                    : $"{name}[{Bound(part[0].Interval.TLow)}-{Bound(part[^1].Interval.THigh)}]";
                condensed.Add(new Entry(entryName, part[0].Source, part));
            }
        }

        var entries = gaseous.Concat(condensed).ToArray();
        var count = entries.Length;
        if (count > TableLimits.MaxSpecies)
        {
            throw new ArgumentException($"{count} species after joining and cutting exceed the limit of {TableLimits.MaxSpecies}", nameof(species));
        }

        var molarMass = new double[count];
        var formationEnthalpy = new double[count];
        var stoichiometry = new double[elements.Count * count];
        var intervalStart = new int[count];
        var intervalCount = new int[count];
        var total = entries.Sum(e => e.Intervals.Count);
        var bounds = new double[total * 2];
        var exponents = new double[total * 8];
        var coefficients = new double[total * 9];
        var next = 0;
        for (var j = 0; j < count; j++)
        {
            var record = entries[j].Record;
            molarMass[j] = record.MolarMass;
            formationEnthalpy[j] = record.FormationEnthalpy;
            foreach (var pair in record.Formula)
            {
                stoichiometry[elementIndex[pair.Symbol] * count + j] += pair.Count;
            }

            intervalStart[j] = next;
            intervalCount[j] = entries[j].Intervals.Count;
            foreach (var (interval, _) in entries[j].Intervals)
            {
                bounds[next * 2] = interval.TLow;
                bounds[next * 2 + 1] = interval.THigh;
                for (var k = 0; k < 8; k++)
                {
                    exponents[next * 8 + k] = k < interval.Exponents.Count ? interval.Exponents[k] : 0.0;
                }

                for (var k = 0; k < 7; k++)
                {
                    coefficients[next * 9 + k] = interval.Coefficients[k];
                }

                coefficients[next * 9 + 7] = interval.B1;
                coefficients[next * 9 + 8] = interval.B2;
                next++;
            }
        }

        var arrays = new SpeciesTableArrays(molarMass, formationEnthalpy, stoichiometry, intervalStart, intervalCount, bounds, exponents, coefficients);
        return new SpeciesTable(
            elements.ToArray(), entries.Select(e => e.Name).ToArray(), entries.Select(e => e.Record).ToArray(), gaseous.Count, arrays);
    }

    private readonly record struct Entry(string Name, Species Record, List<(TemperatureInterval Interval, Species Source)> Intervals);

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
