namespace APThermo.Data;

/// <summary>The transport fits of <c>trans.inp</c>, addressed by species name or by pair.</summary>
public sealed class TransportDatabase
{
    private readonly Dictionary<string, TransportEntry> _single;
    private readonly Dictionary<(string, string), TransportEntry> _pairs;

    internal TransportDatabase(IReadOnlyList<TransportEntry> entries)
    {
        Entries = entries;
        _single = new Dictionary<string, TransportEntry>(StringComparer.Ordinal);
        _pairs = [];
        foreach (var entry in entries)
        {
            _ = entry.Partner is null
                ? _single.TryAdd(entry.Species, entry)
                : _pairs.TryAdd(PairKey(entry.Species, entry.Partner), entry);
        }
    }

    /// <value>All blocks of <c>trans.inp</c>, in file order.</value>
    public IReadOnlyList<TransportEntry> Entries { get; }

    /// <summary>The single-species block of <paramref name="species"/>, or null.</summary>
    /// <param name="species">The exact species name, as it appears in the file.</param>
    /// <returns>The single-species block of <paramref name="species"/>, or <see langword="null"/> when there
    /// is none.</returns>
    public TransportEntry? Find(string species) => _single.GetValueOrDefault(species);

    /// <summary>The interaction block of the pair, in either order, or null.</summary>
    /// <param name="first">One species name of the pair.</param>
    /// <param name="second">The other species name of the pair.</param>
    /// <returns>The interaction block of <paramref name="first"/> and <paramref name="second"/>, in either
    /// order, or <see langword="null"/> when there is none.</returns>
    public TransportEntry? FindPair(string first, string second) => _pairs.GetValueOrDefault(PairKey(first, second));

    private static (string, string) PairKey(string a, string b) =>
        string.CompareOrdinal(a, b) <= 0 ? (a, b) : (b, a);
}
