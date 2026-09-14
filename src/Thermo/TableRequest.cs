namespace AerospacePropellantThermodynamics.Thermo;

/// <summary>
/// Validates a table request before any database lookup: the element and species counts against
/// <see cref="TableLimits"/>, no duplicate element or species name, each refused by name; builds the element index
/// (case-insensitive, so "Al" and "AL" name the same row).
/// </summary>
internal static class TableRequest
{
    public static Dictionary<string, int> Validate(IReadOnlyList<string> elements, IReadOnlyList<string> species)
    {
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

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var name in species)
        {
            if (!seen.Add(name))
            {
                throw new ArgumentException($"species '{name}' is listed twice", nameof(species));
            }
        }

        return elementIndex;
    }
}
