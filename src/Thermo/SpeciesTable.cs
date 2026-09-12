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

    private SpeciesTable(IReadOnlyList<string> elements, IReadOnlyList<Species> records, int gasCount, SpeciesTableArrays arrays)
    {
        Elements = elements;
        Records = records;
        Species = records.Select(r => r.Name).ToArray();
        GasCount = gasCount;
        Arrays = arrays;
        _index = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < records.Count; i++)
        {
            _index[records[i].Name] = i;
        }
    }

    /// <summary>The element symbols, in the order given to the builder; their index is the row of the stoichiometry matrix.</summary>
    public IReadOnlyList<string> Elements { get; }

    /// <summary>The species names in table order: gaseous species first, then condensed, each group in the order given.</summary>
    public IReadOnlyList<string> Species { get; }

    /// <summary>The database records in table order.</summary>
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
    /// Builds a table from database records. Unknown names throw <see cref="KeyNotFoundException"/>; a species with an
    /// element outside <paramref name="elements"/>, without polynomial intervals, or beyond <see cref="TableLimits"/>
    /// throws <see cref="ArgumentException"/> naming it.
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

        var gaseous = new List<Species>();
        var condensed = new List<Species>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var name in species)
        {
            if (!seen.Add(name))
            {
                throw new ArgumentException($"species '{name}' is listed twice", nameof(species));
            }

            var record = database[name];
            if (record.Intervals.Count == 0)
            {
                throw new ArgumentException($"species '{name}' has no polynomial intervals (a reactant-only record) and cannot enter a table", nameof(species));
            }

            if (record.Intervals.Count > TableLimits.MaxIntervalsPerSpecies)
            {
                throw new ArgumentException($"species '{name}' has {record.Intervals.Count} intervals, more than the limit of {TableLimits.MaxIntervalsPerSpecies}", nameof(species));
            }

            foreach (var pair in record.Formula)
            {
                if (!elementIndex.ContainsKey(pair.Symbol))
                {
                    throw new ArgumentException($"species '{name}' contains the element '{pair.Symbol}', which is not among the table's elements", nameof(species));
                }
            }

            (record.Phase == SpeciesPhase.Gas ? gaseous : condensed).Add(record);
        }

        var records = gaseous.Concat(condensed).ToArray();
        var count = records.Length;
        var molarMass = new double[count];
        var formationEnthalpy = new double[count];
        var stoichiometry = new double[elements.Count * count];
        var intervalStart = new int[count];
        var intervalCount = new int[count];
        var total = records.Sum(r => r.Intervals.Count);
        var bounds = new double[total * 2];
        var exponents = new double[total * 8];
        var coefficients = new double[total * 9];
        var next = 0;
        for (var j = 0; j < count; j++)
        {
            var record = records[j];
            molarMass[j] = record.MolarMass;
            formationEnthalpy[j] = record.FormationEnthalpy;
            foreach (var pair in record.Formula)
            {
                stoichiometry[elementIndex[pair.Symbol] * count + j] += pair.Count;
            }

            intervalStart[j] = next;
            intervalCount[j] = record.Intervals.Count;
            foreach (var interval in record.Intervals)
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
        return new SpeciesTable(elements.ToArray(), records, gaseous.Count, arrays);
    }
}
