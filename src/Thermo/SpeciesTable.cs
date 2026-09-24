using APThermo.Data;

namespace APThermo.Thermo;

/// <summary>The flat host arrays of a species table, in the layout the kernels read. Do not modify them after the build.</summary>
internal sealed class SpeciesTableArrays
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

/// <summary>
/// An immutable, ordered species table: a chosen subset of the database flattened for the kernels.
/// <see cref="Build"/> checks the request (<see cref="TableRequest"/>), resolves each name to a gas entry or to
/// condensed pieces (<see cref="SpeciesResolution"/>, BOOT.md's join-and-cut), concatenates gaseous then
/// condensed, checks the species limit, and flattens the result (<see cref="TableLayout"/>).
/// </summary>
internal sealed class SpeciesTable
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
    /// The piece of a database name that covers <paramref name="temperature"/>, by the same rule
    /// <see cref="SpeciesFunctions.IntervalOf"/> uses within one species: the first piece whose last (highest)
    /// bound is not below the temperature, else the last piece. −1 when the table does not hold the name.
    /// </summary>
    public int PieceOf(string species, double temperature)
    {
        var indices = IndicesOf(species);
        if (indices.Count == 0)
        {
            return -1;
        }

        for (var k = 0; k < indices.Count - 1; k++)
        {
            var j = indices[k];
            if (temperature <= Arrays.IntervalBounds[(Arrays.IntervalStart[j] + Arrays.IntervalCount[j] - 1) * TableLayout.BoundsStride + 1])
            {
                return j;
            }
        }

        return indices[^1];
    }

    /// <summary>
    /// Builds a table from database records. Unknown names throw <see cref="KeyNotFoundException"/>; a species with an
    /// element outside <paramref name="elements"/>, without polynomial intervals, or beyond <see cref="TableLimits"/>
    /// throws <see cref="ArgumentException"/> naming it. Condensed records are joined and cut (BOOT.md): product
    /// records sharing one name are concatenated into one species when their formulas, molar masses and formation
    /// enthalpies agree and their ranges touch, and a species whose adjacent fits disagree at a shared internal bound
    /// by |ΔH°/RT| ≥ <see cref="SpeciesFunctions.LatentHeatThreshold"/> is cut there into one entry per piece.
    /// </summary>
    public static SpeciesTable Build(SpeciesDatabase database, IReadOnlyList<string> elements, IReadOnlyList<string> species)
    {
        ArgumentNullException.ThrowIfNull(database);
        var elementIndex = TableRequest.Validate(elements, species);

        var gaseous = new List<TablePiece>();
        var condensed = new List<TablePiece>();
        foreach (var name in species)
        {
            var pieces = SpeciesResolution.Resolve(database, elementIndex, name);
            (pieces[0].Record.Phase == SpeciesPhase.Gas ? gaseous : condensed).AddRange(pieces);
        }

        var entries = gaseous.Concat(condensed).ToArray();
        if (entries.Length > TableLimits.MaxSpecies)
        {
            throw new ArgumentException($"{entries.Length} species after joining and cutting exceed the limit of {TableLimits.MaxSpecies}", nameof(species));
        }

        var arrays = TableLayout.Flatten(elements, elementIndex, entries);
        return new SpeciesTable(
            [.. elements], [.. entries.Select(e => e.Name)], [.. entries.Select(e => e.Record)], gaseous.Count, arrays);
    }
}
