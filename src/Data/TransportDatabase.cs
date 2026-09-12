namespace AerospacePropellantThermodynamics.Data;

/// <summary>The transport fits of <c>trans.inp</c>, addressed by species name or by pair.</summary>
public sealed class TransportDatabase
{
    private readonly Dictionary<string, TransportEntry> _single;
    private readonly Dictionary<(string, string), TransportEntry> _pairs;

    internal TransportDatabase(IReadOnlyList<TransportEntry> entries)
    {
        Entries = entries;
        _single = new Dictionary<string, TransportEntry>(StringComparer.Ordinal);
        _pairs = new Dictionary<(string, string), TransportEntry>();
        foreach (var entry in entries)
        {
            if (entry.Partner is null)
            {
                _single.TryAdd(entry.Species, entry);
            }
            else
            {
                _pairs.TryAdd(PairKey(entry.Species, entry.Partner), entry);
            }
        }
    }

    /// <summary>All blocks in file order.</summary>
    public IReadOnlyList<TransportEntry> Entries { get; }

    /// <summary>The single-species block of <paramref name="species"/>, or null.</summary>
    public TransportEntry? Find(string species) => _single.GetValueOrDefault(species);

    /// <summary>The interaction block of the pair, in either order, or null.</summary>
    public TransportEntry? FindPair(string first, string second) => _pairs.GetValueOrDefault(PairKey(first, second));

    private static (string, string) PairKey(string a, string b) =>
        string.CompareOrdinal(a, b) <= 0 ? (a, b) : (b, a);
}
