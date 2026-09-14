using System.Globalization;

namespace AerospacePropellantThermodynamics.Problems;

/// <summary>A mixture given by its element abundances and enthalpy per kilogram: what every solve starts from, whatever the input was.</summary>
public sealed record ElementalMixture
{
    /// <summary>
    /// The mass tolerance a mixture declares when it names none: how far the mass of its element moles, Σ n_i A_i with the database's
    /// atomic weights, may lie from one kilogram (relative). The solver refuses a mixture beyond its tolerance with a
    /// <see cref="MixtureMassException"/>, whichever front door it came through; BOOT.md derives the value.
    /// </summary>
    public const double DefaultMassTolerance = 1.0e-2;

    private ElementalMixture(IReadOnlyDictionary<string, double> elementMoles, double? enthalpy, IReadOnlyList<string> elements,
                             IReadOnlyList<string> omit, IReadOnlyList<string>? only, double massTolerance)
    {
        ElementMoles = elementMoles;
        Enthalpy = enthalpy;
        Elements = elements;
        Omit = omit;
        Only = only;
        MassTolerance = massTolerance;
    }

    /// <summary>
    /// Element moles per kilogram (mol/kg) by symbol, the enthalpy in J/kg (null when only assigned-temperature problems will be solved),
    /// and the mass tolerance the mixture declares, relative to one kilogram (BOOT.md).
    /// </summary>
    public static ElementalMixture Create(IReadOnlyDictionary<string, double> elementMoles, double? enthalpy = null,
                                          IReadOnlyList<string>? omit = null, IReadOnlyList<string>? only = null,
                                          double massTolerance = DefaultMassTolerance)
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

        if (!double.IsFinite(massTolerance) || massTolerance < 0.0)
        {
            throw new ArgumentException($"the mass tolerance must be a finite non-negative number, not {massTolerance}", nameof(massTolerance));
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

        return new ElementalMixture(moles, enthalpy, elements, omit?.Distinct(StringComparer.Ordinal).ToList() ?? [], only?.Distinct(StringComparer.Ordinal).ToList(), massTolerance);
    }

    /// <summary>mol per kg, by symbol in the database spelling; insertion order is the order of <see cref="Elements"/>.</summary>
    public IReadOnlyDictionary<string, double> ElementMoles { get; }

    /// <summary>J/kg, or null.</summary>
    public double? Enthalpy { get; }

    /// <summary>The order used in results and in the tables.</summary>
    public IReadOnlyList<string> Elements { get; }

    public IReadOnlyList<string> Omit { get; }

    public IReadOnlyList<string>? Only { get; }

    /// <summary>The mass tolerance this mixture declares: how far Σ n_i A_i may lie from one kilogram, relative; the solver checks it.</summary>
    public double MassTolerance { get; }

    /// <summary>The abundances in the numerical nodes' unit, kmol per kg, in the order of the given element list; absent elements are zero.</summary>
    internal double[] KilomolesPerKilogram(IReadOnlyList<string> elements)
    {
        var result = new double[elements.Count];
        for (var i = 0; i < elements.Count; i++)
        {
            // Multiplying by the reciprocal reproduces the pre-decomposition v * 1.0e-3 bit for bit; see MixtureMass.Of.
            result[i] = ElementMoles.TryGetValue(elements[i], out var value) ? value * (1.0 / UnitFactors.MolesPerKilomole) : 0.0;
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

/// <summary>Options of a state batch: transport at every state, the species lists applied to the batch's table, and the mass tolerance every record declares.</summary>
public sealed record StateBatchOptions(bool Transport = false, IReadOnlyList<string>? Omit = null, IReadOnlyList<string>? Only = null,
                                       double MassTolerance = ElementalMixture.DefaultMassTolerance);

/// <summary>
/// A mixture whose element moles do not describe one kilogram: their mass with the database's atomic weights differs from 1 kg by more
/// than the mixture's <see cref="ElementalMixture.MassTolerance"/>. An <see cref="ArgumentException"/>, so that a caller mapping those maps this one too.
/// </summary>
public sealed class MixtureMassException : ArgumentException
{
    public MixtureMassException(string subject, int index, double mass, double tolerance)
        : base(string.Create(CultureInfo.InvariantCulture, $"{subject}: {ReasonFor(mass, tolerance)}"))
    {
        Index = index;
        Mass = mass;
        Tolerance = tolerance;
        Reason = ReasonFor(mass, tolerance);
    }

    /// <summary>The position of the mixture in its batch: the case index, or the state record's index.</summary>
    public int Index { get; }

    /// <summary>Σ n_i A_i in kg: the mass the element moles describe with the database's atomic weights.</summary>
    public double Mass { get; }

    /// <summary>The tolerance in force, relative to one kilogram: the mixture's <see cref="ElementalMixture.MassTolerance"/>.</summary>
    public double Tolerance { get; }

    /// <summary>The message without the subject, for a caller that names the mixture its own way (the command line names the record's file and position).</summary>
    public string Reason { get; }

    private static string ReasonFor(double mass, double tolerance) => string.Create(
        CultureInfo.InvariantCulture,
        $"the composition weighs {mass * UnitFactors.GramsPerKilogram:G7} g with the database's atomic weights; element moles are per kilogram of mixture, so it must weigh 1000 g within {tolerance * 100.0:G3} %");
}
