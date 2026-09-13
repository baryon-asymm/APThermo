using System.Globalization;

namespace AerospacePropellantThermodynamics.Problems;

/// <summary>A mixture given by its element abundances and enthalpy per kilogram: what every solve starts from, whatever the input was.</summary>
public sealed record ElementalMixture
{
    /// <summary>
    /// How far the mass of the element moles, Σ n_i A_i with the database's atomic weights, may lie from one kilogram (relative). The
    /// solver refuses a mixture beyond it with a <see cref="MixtureMassException"/>, whichever front door it came through; BOOT.md derives the value.
    /// </summary>
    public const double MassTolerance = 1.0e-2;

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

/// <summary>
/// A mixture whose element moles do not describe one kilogram: their mass with the database's atomic weights differs from 1 kg by more
/// than <see cref="ElementalMixture.MassTolerance"/>. An <see cref="ArgumentException"/>, so that a caller mapping those maps this one too.
/// </summary>
public sealed class MixtureMassException : ArgumentException
{
    public MixtureMassException(string subject, int index, double mass)
        : base(string.Create(CultureInfo.InvariantCulture, $"{subject}: {ReasonFor(mass)}"))
    {
        Index = index;
        Mass = mass;
        Reason = ReasonFor(mass);
    }

    /// <summary>The position of the mixture in its batch: the case index, or the state record's index.</summary>
    public int Index { get; }

    /// <summary>Σ n_i A_i in kg: the mass the element moles describe with the database's atomic weights.</summary>
    public double Mass { get; }

    /// <summary>The message without the subject, for a caller that names the mixture its own way (the command line names the record's file and position).</summary>
    public string Reason { get; }

    private static string ReasonFor(double mass) => string.Create(
        CultureInfo.InvariantCulture,
        $"the composition weighs {mass * 1.0e3:G7} g with the database's atomic weights; element moles are per kilogram of mixture, so it must weigh 1000 g within {ElementalMixture.MassTolerance * 100.0:G} %");
}
