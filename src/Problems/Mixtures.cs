namespace AerospacePropellantThermodynamics.Problems;

/// <summary>A mixture given by its element abundances and enthalpy per kilogram: what every solve starts from, whatever the input was.</summary>
public sealed record ElementalMixture
{
    private ElementalMixture(IReadOnlyDictionary<string, double> elementMoles, double? enthalpy, IReadOnlyList<string> elements,
                             IReadOnlyList<string> omit, IReadOnlyList<string>? only)
    {
        ElementMoles = elementMoles;
        Enthalpy = enthalpy;
        Elements = elements;
        Omit = omit;
        Only = only;
    }

    /// <summary>Element moles per kilogram (mol/kg) by symbol, and the enthalpy in J/kg (null when only assigned-temperature problems will be solved).</summary>
    public static ElementalMixture Create(IReadOnlyDictionary<string, double> elementMoles, double? enthalpy = null,
                                          IReadOnlyList<string>? omit = null, IReadOnlyList<string>? only = null)
    {
        ArgumentNullException.ThrowIfNull(elementMoles);
        if (elementMoles.Count == 0)
        {
            throw new ArgumentException("the mixture has no elements", nameof(elementMoles));
        }

        if (enthalpy is { } h && !double.IsFinite(h))
        {
            throw new ArgumentException("the enthalpy must be finite", nameof(enthalpy));
        }

        var moles = new Dictionary<string, double>(StringComparer.Ordinal);
        var elements = new List<string>();
        foreach (var (symbol, value) in elementMoles)
        {
            if (string.IsNullOrWhiteSpace(symbol))
            {
                throw new ArgumentException("an element symbol is empty", nameof(elementMoles));
            }

            var spelling = SpeciesSelection.Spelling(symbol);
            if (!moles.TryAdd(spelling, value))
            {
                throw new ArgumentException($"element '{spelling}' is given twice", nameof(elementMoles));
            }

            if (!(value >= 0.0) || double.IsInfinity(value))
            {
                throw new ArgumentException($"element '{spelling}': the abundance must be a finite non-negative number, not {value}", nameof(elementMoles));
            }

            elements.Add(spelling);
        }

        return new ElementalMixture(moles, enthalpy, elements, omit?.Distinct(StringComparer.Ordinal).ToList() ?? [], only?.Distinct(StringComparer.Ordinal).ToList());
    }

    /// <summary>mol per kg, by symbol in the database spelling; insertion order is the order of <see cref="Elements"/>.</summary>
    public IReadOnlyDictionary<string, double> ElementMoles { get; }

    /// <summary>J/kg, or null.</summary>
    public double? Enthalpy { get; }

    /// <summary>The order used in results and in the tables.</summary>
    public IReadOnlyList<string> Elements { get; }

    public IReadOnlyList<string> Omit { get; }

    public IReadOnlyList<string>? Only { get; }

    /// <summary>The abundances in the numerical nodes' unit, kmol per kg, in the order of the given element list; absent elements are zero.</summary>
    internal double[] KilomolesPerKilogram(IReadOnlyList<string> elements)
    {
        var result = new double[elements.Count];
        for (var i = 0; i < elements.Count; i++)
        {
            result[i] = ElementMoles.TryGetValue(elements[i], out var value) ? value * 1.0e-3 : 0.0;
        }

        return result;
    }
}

/// <summary>The exchange record of another simulation: one state of a mixture, with exactly one of enthalpy, temperature and entropy given.</summary>
public sealed record StateRecord(
    double Pressure,                                  // Pa
    IReadOnlyDictionary<string, double> Composition,  // element moles, mol per kg
    double? Enthalpy = null,                          // J/kg: an assigned-enthalpy problem
    double? Temperature = null,                       // K: an assigned-temperature problem
    double? Entropy = null);                          // J/(kg·K): an assigned-entropy problem

/// <summary>Options of a state batch: transport at every state, and the species lists applied to the batch's table.</summary>
public sealed record StateBatchOptions(bool Transport = false, IReadOnlyList<string>? Omit = null, IReadOnlyList<string>? Only = null);
